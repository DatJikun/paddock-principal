using Paddock.Application.Commands;
using Paddock.Application.Finance;
using Paddock.Application.Managers;
using Paddock.Domain.Sponsors;
using Paddock.Domain.World;
using Paddock.Simulation.Codec;

namespace Paddock.Application.Sponsors;

public sealed record BeginSponsorTalksCommand : ICommand
{
    public required ManagerId ManagerId { get; init; }

    public long SubmissionNumber { get; init; }

    public required DateOnly IssuedOn { get; init; }

    public required string OrganizationId { get; init; }

    public required string SponsorId { get; init; }

    /// <summary>The slot number, 1 to 3. Which kind of slot it is depends on the era.</summary>
    public required int Slot { get; init; }

    public ICommand WithSubmissionNumber(long submissionNumber) => this with { SubmissionNumber = submissionNumber };
}

public sealed record SignAtCurrentTermsCommand : ICommand
{
    public required ManagerId ManagerId { get; init; }

    public long SubmissionNumber { get; init; }

    public required DateOnly IssuedOn { get; init; }

    public required string OrganizationId { get; init; }

    public required string TalkId { get; init; }

    public ICommand WithSubmissionNumber(long submissionNumber) => this with { SubmissionNumber = submissionNumber };
}

public sealed record WalkAwayFromTalksCommand : ICommand
{
    public required ManagerId ManagerId { get; init; }

    public long SubmissionNumber { get; init; }

    public required DateOnly IssuedOn { get; init; }

    public required string OrganizationId { get; init; }

    public required string TalkId { get; init; }

    public ICommand WithSubmissionNumber(long submissionNumber) => this with { SubmissionNumber = submissionNumber };
}

public sealed record RespondToSponsorOfferCommand : ICommand
{
    public required ManagerId ManagerId { get; init; }

    public long SubmissionNumber { get; init; }

    public required DateOnly IssuedOn { get; init; }

    public required string OrganizationId { get; init; }

    public required string OfferId { get; init; }

    public required bool Accept { get; init; }

    public ICommand WithSubmissionNumber(long submissionNumber) => this with { SubmissionNumber = submissionNumber };
}

/// <summary>Checks every sponsor command starts with: a known manager who runs an organization that exists and has books.</summary>
internal static class SponsorGate
{
    public static TranslationMessage? Check(
        SponsorBook book,
        SponsorEnvironment environment,
        CommandContext context,
        ManagerId manager,
        string organizationText,
        out OrganizationId organization)
    {
        organization = default;
        if (!context.Managers.Contains(manager))
        {
            return TranslationMessage.Of(TranslationKeys.ManagerUnknown, ("managerId", manager.Value));
        }

        if (!FinanceIds.TryParse(organizationText, out organization))
        {
            return TranslationMessage.Of(SponsorKeys.UnknownOrganization);
        }

        if (!environment.Control.Controls(manager, organization))
        {
            return TranslationMessage.Of(SponsorKeys.NotYourOrganization);
        }

        var known = organization;
        if (!book.World.Organizations.Any(candidate => candidate.Id == known))
        {
            return TranslationMessage.Of(SponsorKeys.UnknownOrganization);
        }

        return book.Finance.HasBook(organization) ? null : TranslationMessage.Of(SponsorKeys.NoBooks);
    }
}

public sealed class BeginSponsorTalksHandler : CommandHandler<BeginSponsorTalksCommand>
{
    private readonly SponsorBook _book;
    private readonly SponsorEnvironment _environment;

    public BeginSponsorTalksHandler(SponsorBook book, SponsorEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(book);
        ArgumentNullException.ThrowIfNull(environment);
        _book = book;
        _environment = environment;
    }

    protected override TranslationMessage? ValidateTyped(BeginSponsorTalksCommand command, CommandContext context)
    {
        var gate = SponsorGate.Check(_book, _environment, context, command.ManagerId, command.OrganizationId, out var organization);
        return gate ?? SponsorRules.CanBegin(_book, _environment, organization, command.SponsorId, command.Slot, FinanceBook.ToGameDate(command.IssuedOn));
    }

    protected override IReadOnlyList<IDomainEvent> ExecuteTyped(BeginSponsorTalksCommand command, CommandContext context)
    {
        var organization = FinanceIds.Parse(command.OrganizationId);
        var today = FinanceBook.ToGameDate(command.IssuedOn);
        var sponsor = _environment.Catalog.Find(command.SponsorId)!;
        var kind = _environment.Eras.Era(today.Year).Slots[command.Slot - 1];
        var skill = _environment.Negotiators.Skill(_book.World, organization, today);
        var talk = new SponsorTalk(
            0,
            organization,
            sponsor.Id,
            command.Slot,
            kind,
            command.ManagerId.Value,
            today,
            TalkStatus.Open,
            null,
            RivalState.Undecided,
            SponsorPricing.CapMilli(skill),
            SponsorRules.FullAnnualCents(_book, _environment, sponsor, kind, today.Year));
        var (section, added) = _book.Section.AddTalk(talk);
        _book.Write(section);
        return [new SponsorTalksOpened(command.ManagerId, command.IssuedOn, added.Id, sponsor.Id, command.Slot)];
    }
}

public sealed class SignAtCurrentTermsHandler : CommandHandler<SignAtCurrentTermsCommand>
{
    private readonly SponsorBook _book;
    private readonly SponsorEnvironment _environment;

    public SignAtCurrentTermsHandler(SponsorBook book, SponsorEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(book);
        ArgumentNullException.ThrowIfNull(environment);
        _book = book;
        _environment = environment;
    }

    protected override TranslationMessage? ValidateTyped(SignAtCurrentTermsCommand command, CommandContext context)
    {
        var gate = SponsorGate.Check(_book, _environment, context, command.ManagerId, command.OrganizationId, out var organization);
        if (gate is not null)
        {
            return gate;
        }

        var talk = _book.Section.FindTalk(command.TalkId);
        if (talk is null || talk.Organization != organization)
        {
            return TranslationMessage.Of(SponsorKeys.UnknownTalk);
        }

        if (!talk.IsOpen)
        {
            return TranslationMessage.Of(SponsorKeys.TalkClosed);
        }

        return _book.Section.SponsorInDeal(talk.SponsorId) ? TranslationMessage.Of(SponsorKeys.SponsorUnavailable) : null;
    }

    protected override IReadOnlyList<IDomainEvent> ExecuteTyped(SignAtCurrentTermsCommand command, CommandContext context)
    {
        var today = FinanceBook.ToGameDate(command.IssuedOn);
        var section = _book.Section;
        var talk = section.FindTalk(command.TalkId)!;
        var sponsor = _environment.Catalog.Find(talk.SponsorId)!;
        var (sponsors, objectives, deal) = SponsorRules.Sign(
            section.Replace(talk with { Status = TalkStatus.Signed, ClosedOn = today }),
            _book.Objectives,
            _environment,
            sponsor,
            talk.Organization,
            talk.Slot,
            talk.Kind,
            talk.AnnualCentsOn(today),
            today);
        _book.Write(sponsors, objectives);
        return [new SponsorDealSigned(command.ManagerId, command.IssuedOn, deal.Id, sponsor.Id, deal.AnnualCents, deal.ObjectiveId)];
    }
}

public sealed class WalkAwayFromTalksHandler : CommandHandler<WalkAwayFromTalksCommand>
{
    private readonly SponsorBook _book;
    private readonly SponsorEnvironment _environment;

    public WalkAwayFromTalksHandler(SponsorBook book, SponsorEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(book);
        ArgumentNullException.ThrowIfNull(environment);
        _book = book;
        _environment = environment;
    }

    protected override TranslationMessage? ValidateTyped(WalkAwayFromTalksCommand command, CommandContext context)
    {
        var gate = SponsorGate.Check(_book, _environment, context, command.ManagerId, command.OrganizationId, out var organization);
        if (gate is not null)
        {
            return gate;
        }

        var talk = _book.Section.FindTalk(command.TalkId);
        if (talk is null || talk.Organization != organization)
        {
            return TranslationMessage.Of(SponsorKeys.UnknownTalk);
        }

        return talk.IsOpen ? null : TranslationMessage.Of(SponsorKeys.TalkClosed);
    }

    protected override IReadOnlyList<IDomainEvent> ExecuteTyped(WalkAwayFromTalksCommand command, CommandContext context)
    {
        var talk = _book.Section.FindTalk(command.TalkId)!;
        _book.Write(_book.Section.Replace(talk with { Status = TalkStatus.WalkedAway, ClosedOn = FinanceBook.ToGameDate(command.IssuedOn) }));
        return [new SponsorTalksEnded(command.ManagerId, command.IssuedOn, talk.Id)];
    }
}

public sealed class RespondToSponsorOfferHandler : CommandHandler<RespondToSponsorOfferCommand>
{
    private readonly SponsorBook _book;
    private readonly SponsorEnvironment _environment;

    public RespondToSponsorOfferHandler(SponsorBook book, SponsorEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(book);
        ArgumentNullException.ThrowIfNull(environment);
        _book = book;
        _environment = environment;
    }

    protected override TranslationMessage? ValidateTyped(RespondToSponsorOfferCommand command, CommandContext context)
    {
        var gate = SponsorGate.Check(_book, _environment, context, command.ManagerId, command.OrganizationId, out var organization);
        if (gate is not null)
        {
            return gate;
        }

        var offer = _book.Section.FindOffer(command.OfferId);
        if (offer is null || offer.Organization != organization)
        {
            return TranslationMessage.Of(SponsorKeys.UnknownOffer);
        }

        if (!offer.IsOpen || FinanceBook.ToGameDate(command.IssuedOn) > offer.ValidUntil)
        {
            return TranslationMessage.Of(SponsorKeys.OfferClosed);
        }

        return null;
    }

    protected override IReadOnlyList<IDomainEvent> ExecuteTyped(RespondToSponsorOfferCommand command, CommandContext context) =>
        SponsorOfferAnswer.Apply(_book, _environment, command.ManagerId, command.IssuedOn, command.OfferId, command.Accept);
}

/// <summary>
/// Answers a renewal offer. The command and the inbox decision both end here, so a click in the inbox and a command from the
/// sponsors screen do the same thing, and an AI team's answer takes the same path as a player's.
/// </summary>
public static class SponsorOfferAnswer
{
    public static IReadOnlyList<IDomainEvent> Apply(
        SponsorBook book,
        SponsorEnvironment environment,
        ManagerId manager,
        DateOnly issuedOn,
        string offerId,
        bool accept)
    {
        var today = FinanceBook.ToGameDate(issuedOn);
        var section = book.Section;
        var offer = section.FindOffer(offerId)!;
        if (!accept)
        {
            book.Write(section.Replace(offer with { Status = OfferStatus.Declined, ClosedOn = today }));
            return [new SponsorOfferAnswered(manager, issuedOn, offer.Id, false, null)];
        }

        var sponsor = environment.Catalog.Find(offer.SponsorId)!;
        var (sponsors, objectives, deal) = SponsorRules.Sign(
            section.Replace(offer with { Status = OfferStatus.Accepted, ClosedOn = today }),
            book.Objectives,
            environment,
            sponsor,
            offer.Organization,
            offer.Slot,
            offer.Kind,
            offer.AnnualCents,
            book.Section.FindDeal(SponsorsSection.DealIdOf(offer.DealNumber))!.End.AddDays(1));
        book.Write(sponsors, objectives);
        return [new SponsorOfferAnswered(manager, issuedOn, offer.Id, true, deal.Id)];
    }
}

public static class SponsorCommandCodecs
{
    public static IReadOnlyList<CommandCodecEntry> Entries { get; } =
    [
        CommandCodecEntry.For<BeginSponsorTalksCommand>(
            "sponsor.beginTalks/1",
            command => FlatJson.Write(("organization", command.OrganizationId), ("sponsor", command.SponsorId), ("slot", command.Slot)),
            (body, manager, issued) =>
            {
                var fields = FlatJson.Read(body, "organization", "sponsor", "slot");
                return new BeginSponsorTalksCommand
                {
                    ManagerId = manager,
                    IssuedOn = issued,
                    OrganizationId = fields.String("organization"),
                    SponsorId = fields.String("sponsor"),
                    Slot = fields.Int32("slot"),
                };
            }),
        CommandCodecEntry.For<SignAtCurrentTermsCommand>(
            "sponsor.sign/1",
            command => FlatJson.Write(("organization", command.OrganizationId), ("talk", command.TalkId)),
            (body, manager, issued) =>
            {
                var fields = FlatJson.Read(body, "organization", "talk");
                return new SignAtCurrentTermsCommand
                {
                    ManagerId = manager,
                    IssuedOn = issued,
                    OrganizationId = fields.String("organization"),
                    TalkId = fields.String("talk"),
                };
            }),
        CommandCodecEntry.For<WalkAwayFromTalksCommand>(
            "sponsor.walkAway/1",
            command => FlatJson.Write(("organization", command.OrganizationId), ("talk", command.TalkId)),
            (body, manager, issued) =>
            {
                var fields = FlatJson.Read(body, "organization", "talk");
                return new WalkAwayFromTalksCommand
                {
                    ManagerId = manager,
                    IssuedOn = issued,
                    OrganizationId = fields.String("organization"),
                    TalkId = fields.String("talk"),
                };
            }),
        CommandCodecEntry.For<RespondToSponsorOfferCommand>(
            "sponsor.respond/1",
            command => FlatJson.Write(("organization", command.OrganizationId), ("offer", command.OfferId), ("answer", command.Accept ? "accept" : "decline")),
            (body, manager, issued) =>
            {
                var fields = FlatJson.Read(body, "organization", "offer", "answer");
                var answer = fields.String("answer");
                if (answer is not ("accept" or "decline"))
                {
                    throw new FormatException("The sponsor answer must be 'accept' or 'decline'.");
                }

                return new RespondToSponsorOfferCommand
                {
                    ManagerId = manager,
                    IssuedOn = issued,
                    OrganizationId = fields.String("organization"),
                    OfferId = fields.String("offer"),
                    Accept = answer == "accept",
                };
            }),
    ];
}
