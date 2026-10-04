using Paddock.Application.Commands;
using Paddock.Application.Finance;
using Paddock.Application.Managers;
using Paddock.Domain.Supply;
using Paddock.Domain.World;
using Paddock.Simulation.Codec;

namespace Paddock.Application.Supply;

/// <summary>
/// The manager offers a supplier a deal: an item, a kind, the first season, a yearly price in cents, a length and exclusivity.
/// With <see cref="NegotiationId"/> empty it opens a negotiation; with the id of a countered one it answers the counter with a new
/// offer (it uses a round, and a nudge costs interest). The supplier answers on a later day (the day handler).
/// </summary>
public sealed record ProposeSupplyDealCommand : ICommand
{
    public required ManagerId ManagerId { get; init; }

    public long SubmissionNumber { get; init; }

    public required DateOnly IssuedOn { get; init; }

    public required string OrganizationId { get; init; }

    public required string SupplierId { get; init; }

    public required SupplyItem Item { get; init; }

    public required SupplyKind Kind { get; init; }

    public required int FirstSeason { get; init; }

    public required long AnnualPriceCents { get; init; }

    public required int Seasons { get; init; }

    public bool Exclusive { get; init; }

    /// <summary>Empty to open new talks; the id of a countered negotiation to answer it with a new offer.</summary>
    public string NegotiationId { get; init; } = string.Empty;

    public ICommand WithSubmissionNumber(long submissionNumber) => this with { SubmissionNumber = submissionNumber };
}

/// <summary>The manager answers a supplier's counter: accept the counter terms (the deal is signed) or walk away.</summary>
public sealed record RespondToSupplyOfferCommand : ICommand
{
    public required ManagerId ManagerId { get; init; }

    public long SubmissionNumber { get; init; }

    public required DateOnly IssuedOn { get; init; }

    public required string OrganizationId { get; init; }

    public required string NegotiationId { get; init; }

    public required bool Accept { get; init; }

    public ICommand WithSubmissionNumber(long submissionNumber) => this with { SubmissionNumber = submissionNumber };
}

public sealed class ProposeSupplyDealHandler : CommandHandler<ProposeSupplyDealCommand>
{
    private readonly SupplyBook _book;
    private readonly SupplyEnvironment _environment;

    public ProposeSupplyDealHandler(SupplyBook book, SupplyEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(book);
        ArgumentNullException.ThrowIfNull(environment);
        _book = book;
        _environment = environment;
    }

    protected override TranslationMessage? ValidateTyped(ProposeSupplyDealCommand command, CommandContext context)
    {
        var gate = SupplyRules.Gate(_book, _environment, context, command.ManagerId, command.OrganizationId, out var customer);
        if (gate is not null)
        {
            return gate;
        }

        if (!FinanceIds.TryParse(command.SupplierId, out var supplier))
        {
            return TranslationMessage.Of(SupplyKeys.UnknownSupplier);
        }

        var today = FinanceBook.ToGameDate(command.IssuedOn);
        var revising = string.IsNullOrEmpty(command.NegotiationId) ? null : command.NegotiationId;
        if (revising is not null)
        {
            var existing = _book.Section.FindNegotiation(revising);
            if (existing is null || existing.Customer != customer)
            {
                return TranslationMessage.Of(SupplyKeys.UnknownNegotiation);
            }

            if (!existing.IsActive)
            {
                return TranslationMessage.Of(SupplyKeys.NegotiationClosed);
            }

            if (existing.Status != Paddock.Domain.Contracts.NegotiationStatus.Countered)
            {
                return TranslationMessage.Of(SupplyKeys.NotCountered);
            }

            if (existing.RoundsLeft <= 0)
            {
                return TranslationMessage.Of(SupplyKeys.NoRounds);
            }

            if (existing.Supplier != supplier || existing.Item != command.Item || existing.Kind != command.Kind || existing.FirstSeason != command.FirstSeason)
            {
                return TranslationMessage.Of(SupplyKeys.BadTerms);
            }
        }

        return SupplyRules.CheckProposal(
            _book,
            _environment,
            customer,
            supplier,
            command.Item,
            command.Kind,
            command.FirstSeason,
            command.AnnualPriceCents,
            command.Seasons,
            today,
            revising);
    }

    protected override IReadOnlyList<IDomainEvent> ExecuteTyped(ProposeSupplyDealCommand command, CommandContext context)
    {
        var customer = FinanceIds.Parse(command.OrganizationId);
        var supplier = FinanceIds.Parse(command.SupplierId);
        var today = FinanceBook.ToGameDate(command.IssuedOn);
        var terms = new SupplyTerms(command.AnnualPriceCents, command.Seasons, command.Exclusive);
        var section = _book.Section;
        if (!string.IsNullOrEmpty(command.NegotiationId))
        {
            var revised = section.FindNegotiation(command.NegotiationId)!.WithOffer(terms, today);
            _book.Write(section.ReplaceNegotiation(revised));
            return [new SupplyDealProposed(command.ManagerId, command.IssuedOn, revised.Id, command.SupplierId, command.Item)];
        }

        var (updated, added) = section.AddNegotiation(SupplyNegotiation.Start(
            section.NextNegotiation,
            command.ManagerId.Value,
            customer,
            supplier,
            command.Item,
            command.Kind,
            command.FirstSeason,
            today,
            terms));
        _book.Write(updated);
        return [new SupplyDealProposed(command.ManagerId, command.IssuedOn, added.Id, command.SupplierId, command.Item)];
    }
}

public sealed class RespondToSupplyOfferHandler : CommandHandler<RespondToSupplyOfferCommand>
{
    private readonly SupplyBook _book;
    private readonly SupplyEnvironment _environment;

    public RespondToSupplyOfferHandler(SupplyBook book, SupplyEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(book);
        ArgumentNullException.ThrowIfNull(environment);
        _book = book;
        _environment = environment;
    }

    protected override TranslationMessage? ValidateTyped(RespondToSupplyOfferCommand command, CommandContext context)
    {
        var gate = SupplyRules.Gate(_book, _environment, context, command.ManagerId, command.OrganizationId, out var customer);
        if (gate is not null)
        {
            return gate;
        }

        var negotiation = _book.Section.FindNegotiation(command.NegotiationId);
        if (negotiation is null || negotiation.Customer != customer)
        {
            return TranslationMessage.Of(SupplyKeys.UnknownNegotiation);
        }

        if (!negotiation.IsActive)
        {
            return TranslationMessage.Of(SupplyKeys.NegotiationClosed);
        }

        if (command.Accept && negotiation.Status != Paddock.Domain.Contracts.NegotiationStatus.Countered)
        {
            return TranslationMessage.Of(SupplyKeys.NotCountered);
        }

        if (command.Accept
            && _book.Section.DealsOf(customer).Any(deal => deal.Item == negotiation.Item
                && deal.Overlaps(negotiation.FirstSeason, negotiation.FirstSeason + negotiation.Counter!.Seasons - 1)))
        {
            return TranslationMessage.Of(SupplyKeys.AlreadySupplied);
        }

        return null;
    }

    protected override IReadOnlyList<IDomainEvent> ExecuteTyped(RespondToSupplyOfferCommand command, CommandContext context)
    {
        var today = FinanceBook.ToGameDate(command.IssuedOn);
        var negotiation = _book.Section.FindNegotiation(command.NegotiationId)!;
        if (!command.Accept)
        {
            _book.Write(_book.Section.ReplaceNegotiation(negotiation.WithWalkAway(today)));
            return [new SupplyTalksClosed(command.ManagerId, command.IssuedOn, negotiation.Id)];
        }

        var signed = SupplyRules.Sign(_book.World, negotiation, negotiation.Counter!, today)!.Value;
        _book.Update(signed.World);
        return [new SupplyDealSigned(command.ManagerId, command.IssuedOn, signed.Deal.Id, negotiation.Id)];
    }
}

/// <summary>
/// Registers the supply handlers. The host calls this where it registers <c>ContractRegistration</c>, and adds
/// <see cref="SupplyDayHandler"/> to the day loop. Own-engine commands (<c>StartEngineProgramme</c>, <c>OfferEngineToCustomers</c>)
/// are not here: they wait for T42 (see <see cref="IEngineProgrammes"/>).
/// </summary>
public static class SupplyRegistration
{
    public static void Register(CommandDispatcher dispatcher, SupplyBook book, SupplyEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(book);
        ArgumentNullException.ThrowIfNull(environment);
        dispatcher.Register(new ProposeSupplyDealHandler(book, environment));
        dispatcher.Register(new RespondToSupplyOfferHandler(book, environment));
    }
}

public static class SupplyCommandCodecs
{
    public static IReadOnlyList<CommandCodecEntry> Entries { get; } =
    [
        CommandCodecEntry.For<ProposeSupplyDealCommand>(
            "supply.propose/1",
            command => FlatJson.Write(
                ("organization", command.OrganizationId),
                ("supplier", command.SupplierId),
                ("item", command.Item.ToString()),
                ("kind", command.Kind.ToString()),
                ("firstSeason", command.FirstSeason),
                ("price", command.AnnualPriceCents),
                ("seasons", command.Seasons),
                ("exclusive", command.Exclusive ? "1" : "0"),
                ("negotiation", command.NegotiationId)),
            (body, manager, issued) =>
            {
                var fields = FlatJson.Read(body, "organization", "supplier", "item", "kind", "firstSeason", "price", "seasons", "exclusive", "negotiation");
                return new ProposeSupplyDealCommand
                {
                    ManagerId = manager,
                    IssuedOn = issued,
                    OrganizationId = fields.String("organization"),
                    SupplierId = fields.String("supplier"),
                    Item = ParseEnum<SupplyItem>(fields.String("item")),
                    Kind = ParseEnum<SupplyKind>(fields.String("kind")),
                    FirstSeason = fields.Int32("firstSeason"),
                    AnnualPriceCents = fields.Int64("price"),
                    Seasons = fields.Int32("seasons"),
                    Exclusive = fields.String("exclusive") == "1",
                    NegotiationId = fields.String("negotiation"),
                };
            }),
        CommandCodecEntry.For<RespondToSupplyOfferCommand>(
            "supply.respond/1",
            command => FlatJson.Write(("organization", command.OrganizationId), ("negotiation", command.NegotiationId), ("answer", command.Accept ? "accept" : "decline")),
            (body, manager, issued) =>
            {
                var fields = FlatJson.Read(body, "organization", "negotiation", "answer");
                var answer = fields.String("answer");
                if (answer is not ("accept" or "decline"))
                {
                    throw new FormatException("The supply answer must be 'accept' or 'decline'.");
                }

                return new RespondToSupplyOfferCommand
                {
                    ManagerId = manager,
                    IssuedOn = issued,
                    OrganizationId = fields.String("organization"),
                    NegotiationId = fields.String("negotiation"),
                    Accept = answer == "accept",
                };
            }),
    ];

    private static T ParseEnum<T>(string text)
        where T : struct, Enum =>
        Enum.TryParse<T>(text, out var value) && Enum.IsDefined(value) && value.ToString() == text
            ? value
            : throw new FormatException("'" + text + "' is not a " + typeof(T).Name + ".");
}
