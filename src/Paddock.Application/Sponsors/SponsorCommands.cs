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

    /// <summary>How many years the deal runs, 1 to 3 (#268). One by default.</summary>
    public int Years { get; init; } = SponsorEstimates.MinYears;

    /// <summary>How hard the condition is (#268). The standard one by default.</summary>
    public SponsorAmbition Ambition { get; init; } = SponsorAmbition.Standard;

    public SponsorTerms Terms => new(Years, Ambition);

    public ICommand WithSubmissionNumber(long submissionNumber) => this with { SubmissionNumber = submissionNumber };
}

/// <summary>Changes the terms of open talks: how many years and how hard the condition. The price follows at once (#268).</summary>
public sealed record ProposeSponsorTermsCommand : ICommand
{
    public required ManagerId ManagerId { get; init; }

    public long SubmissionNumber { get; init; }

    public required DateOnly IssuedOn { get; init; }

    public required string OrganizationId { get; init; }

    public required string TalkId { get; init; }

    public required int Years { get; init; }

    public required SponsorAmbition Ambition { get; init; }

    public ICommand WithSubmissionNumber(long submissionNumber) => this with { SubmissionNumber = submissionNumber };
}

/// <summary>
/// Answers a renewal offer with other terms (#268), the way a contract negotiation answers an offer with a counter: the sponsor quotes the
/// new terms at once and the old inbox decision is replaced by a new one. The sponsor gives <see cref="SponsorEstimates.MaxCounterRounds"/> rounds.
/// </summary>
public sealed record CounterSponsorOfferCommand : ICommand
{
    public required ManagerId ManagerId { get; init; }

    public long SubmissionNumber { get; init; }

    public required DateOnly IssuedOn { get; init; }

    public required string OrganizationId { get; init; }

    public required string OfferId { get; init; }

    public required int Years { get; init; }

    public required SponsorAmbition Ambition { get; init; }

    /// <summary>Thousandths above the sponsor's quote the player asks for (#268). The sponsor pays it, or holds at the most it will pay.</summary>
    public int AskMilli { get; init; }

    public ICommand WithSubmissionNumber(long submissionNumber) => this with { SubmissionNumber = submissionNumber };
}

/// <summary>Signs open talks at the price they have reached, optionally asking for a little more than the quote (#268).</summary>
public sealed record SignAtCurrentTermsCommand : ICommand
{
    public required ManagerId ManagerId { get; init; }

    public long SubmissionNumber { get; init; }

    public required DateOnly IssuedOn { get; init; }

    public required string OrganizationId { get; init; }

    public required string TalkId { get; init; }

    /// <summary>Thousandths above the quote the player asks for (#268). The sponsor pays it, or holds at the most it will pay.</summary>
    public int AskMilli { get; init; }

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
        gate ??= SponsorRules.CanBegin(_book, _environment, organization, command.SponsorId, command.Slot, FinanceBook.ToGameDate(command.IssuedOn));
        return gate ?? SponsorRules.CheckTerms(_environment.Catalog.Find(command.SponsorId)!, command.Terms);
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
            SponsorRules.FullAnnualCents(_book, _environment, sponsor, kind, today.Year),
            command.Years,
            command.Ambition);
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

        return SponsorPartnership.IsAskable(command.AskMilli) ? null : TranslationMessage.Of(SponsorKeys.BadAsk);
    }

    protected override IReadOnlyList<IDomainEvent> ExecuteTyped(SignAtCurrentTermsCommand command, CommandContext context)
    {
        var today = FinanceBook.ToGameDate(command.IssuedOn);
        var section = _book.Section;
        var talk = section.FindTalk(command.TalkId)!;
        var sponsor = _environment.Catalog.Find(talk.SponsorId)!;
        var answer = SponsorPartnership.Answer(command.AskMilli, SponsorRules.PartnershipOf(section, sponsor, talk.Organization));
        var (sponsors, objectives, deal) = SponsorRules.Sign(
            section.Replace(talk with { Status = TalkStatus.Signed, ClosedOn = today }),
            _book.Objectives,
            _environment,
            sponsor,
            talk.Organization,
            talk.Slot,
            talk.Kind,
            SponsorPartnership.Raised(talk.AnnualCentsOn(today), answer.AppliedMilli),
            today,
            talk.Terms,
            SponsorWishes.Offered(_book.World, sponsor, today));
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

public sealed class ProposeSponsorTermsHandler : CommandHandler<ProposeSponsorTermsCommand>
{
    private readonly SponsorBook _book;
    private readonly SponsorEnvironment _environment;

    public ProposeSponsorTermsHandler(SponsorBook book, SponsorEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(book);
        ArgumentNullException.ThrowIfNull(environment);
        _book = book;
        _environment = environment;
    }

    protected override TranslationMessage? ValidateTyped(ProposeSponsorTermsCommand command, CommandContext context)
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

        return SponsorRules.CheckTerms(_environment.Catalog.Find(talk.SponsorId)!, new SponsorTerms(command.Years, command.Ambition));
    }

    protected override IReadOnlyList<IDomainEvent> ExecuteTyped(ProposeSponsorTermsCommand command, CommandContext context)
    {
        var talk = _book.Section.FindTalk(command.TalkId)!;
        _book.Write(_book.Section.Replace(talk with { Years = command.Years, Ambition = command.Ambition }));
        return [new SponsorTermsProposed(command.ManagerId, command.IssuedOn, talk.Id, command.Years, command.Ambition)];
    }
}

public sealed class CounterSponsorOfferHandler : CommandHandler<CounterSponsorOfferCommand>
{
    private readonly SponsorBook _book;
    private readonly SponsorEnvironment _environment;

    public CounterSponsorOfferHandler(SponsorBook book, SponsorEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(book);
        ArgumentNullException.ThrowIfNull(environment);
        _book = book;
        _environment = environment;
    }

    protected override TranslationMessage? ValidateTyped(CounterSponsorOfferCommand command, CommandContext context)
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

        if (!offer.CanCounter)
        {
            return TranslationMessage.Of(SponsorKeys.NoRoundsLeft);
        }

        var terms = new SponsorTerms(command.Years, command.Ambition);
        if (!SponsorPartnership.IsAskable(command.AskMilli))
        {
            return TranslationMessage.Of(SponsorKeys.BadAsk);
        }

        if (terms == offer.Terms && command.AskMilli == 0)
        {
            return TranslationMessage.Of(SponsorKeys.SameTerms);
        }

        return SponsorRules.CheckTerms(_environment.Catalog.Find(offer.SponsorId)!, terms);
    }

    protected override IReadOnlyList<IDomainEvent> ExecuteTyped(CounterSponsorOfferCommand command, CommandContext context)
    {
        var today = FinanceBook.ToGameDate(command.IssuedOn);
        var offer = _book.Section.FindOffer(command.OfferId)!;
        var sponsor = _environment.Catalog.Find(offer.SponsorId)!;
        var deal = _book.Section.FindDeal(SponsorsSection.DealIdOf(offer.DealNumber))!;
        var terms = new SponsorTerms(command.Years, command.Ambition);
        var answer = SponsorPartnership.Answer(command.AskMilli, SponsorRules.PartnershipOf(_book.Section, sponsor, offer.Organization));
        var amount = SponsorPartnership.Raised(SponsorRules.RenewalCents(_book, _environment, deal, sponsor, terms), answer.AppliedMilli);
        var countered = offer with { Years = terms.Years, Ambition = terms.Ambition, AnnualCents = amount, Rounds = offer.Rounds + 1 };
        _book.Write(_book.Section.Replace(countered));

        // The decision in the inbox quoted the old terms, so it is withdrawn and the sponsor's answer is posted in its place: the player
        // always decides on what is on the table, and the clock stays held until they do.
        foreach (var item in context.Inbox.Section.Items)
        {
            if (item.IsOpen && item.Kind == SponsorOfferCodes.OfferKind
                && item.Arguments.TryGetValue(SponsorOfferCodes.OfferArgument, out var id) && id == offer.Id)
            {
                context.Inbox.Withdraw(context.Managers, item.Id, today);
            }
        }

        new SponsorNotices(_environment, context.Inbox, context.Managers).PostOffer(offer.Organization, today, offer.ValidUntil, countered, sponsor.Name);
        return [new SponsorOfferCountered(command.ManagerId, command.IssuedOn, offer.Id, terms.Years, terms.Ambition, amount)];
    }
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
        var start = book.Section.FindDeal(SponsorsSection.DealIdOf(offer.DealNumber))!.End.AddDays(1);
        var (sponsors, objectives, deal) = SponsorRules.Sign(
            section.Replace(offer with { Status = OfferStatus.Accepted, ClosedOn = today }),
            book.Objectives,
            environment,
            sponsor,
            offer.Organization,
            offer.Slot,
            offer.Kind,
            offer.AnnualCents,
            start,
            offer.Terms,
            SponsorWishes.Offered(book.World, sponsor, start));
        book.Write(sponsors, objectives);
        return [new SponsorOfferAnswered(manager, issuedOn, offer.Id, true, deal.Id)];
    }
}

public static class SponsorCommandCodecs
{
    private static SponsorAmbition Ambition(string key) =>
        SponsorAmbitions.TryParse(key, out var ambition) ? ambition : throw new FormatException("The sponsor ambition '" + key + "' is unknown.");

    public static IReadOnlyList<CommandCodecEntry> Entries { get; } =
    [
        CommandCodecEntry.For<BeginSponsorTalksCommand>(
            "sponsor.beginTalks/1",

            // Terms (#268) are two optional fields: a command with the default terms is written exactly as it always was, so older saves
            // read as before, and a body with the two fields is read as well. One codec entry exists per command type, so the tag stays.
            command => command.Terms.IsDefault
                ? FlatJson.Write(("organization", command.OrganizationId), ("sponsor", command.SponsorId), ("slot", command.Slot))
                : FlatJson.Write(
                    ("organization", command.OrganizationId),
                    ("sponsor", command.SponsorId),
                    ("slot", command.Slot),
                    ("years", command.Years),
                    ("ambition", SponsorAmbitions.KeyOf(command.Ambition))),
            (body, manager, issued) =>
            {
                if (body.Contains("\"years\"", StringComparison.Ordinal))
                {
                    var wide = FlatJson.Read(body, "organization", "sponsor", "slot", "years", "ambition");
                    return new BeginSponsorTalksCommand
                    {
                        ManagerId = manager,
                        IssuedOn = issued,
                        OrganizationId = wide.String("organization"),
                        SponsorId = wide.String("sponsor"),
                        Slot = wide.Int32("slot"),
                        Years = wide.Int32("years"),
                        Ambition = Ambition(wide.String("ambition")),
                    };
                }

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
        CommandCodecEntry.For<ProposeSponsorTermsCommand>(
            "sponsor.terms/1",
            command => FlatJson.Write(
                ("organization", command.OrganizationId),
                ("talk", command.TalkId),
                ("years", command.Years),
                ("ambition", SponsorAmbitions.KeyOf(command.Ambition))),
            (body, manager, issued) =>
            {
                var fields = FlatJson.Read(body, "organization", "talk", "years", "ambition");
                return new ProposeSponsorTermsCommand
                {
                    ManagerId = manager,
                    IssuedOn = issued,
                    OrganizationId = fields.String("organization"),
                    TalkId = fields.String("talk"),
                    Years = fields.Int32("years"),
                    Ambition = Ambition(fields.String("ambition")),
                };
            }),
        CommandCodecEntry.For<CounterSponsorOfferCommand>(
            "sponsor.counter/1",

            // The ask (#268) is written only when there is one, so a counter without an ask is encoded as it was.
            command => command.AskMilli == 0
                ? FlatJson.Write(
                    ("organization", command.OrganizationId),
                    ("offer", command.OfferId),
                    ("years", command.Years),
                    ("ambition", SponsorAmbitions.KeyOf(command.Ambition)))
                : FlatJson.Write(
                    ("organization", command.OrganizationId),
                    ("offer", command.OfferId),
                    ("years", command.Years),
                    ("ambition", SponsorAmbitions.KeyOf(command.Ambition)),
                    ("ask", command.AskMilli)),
            (body, manager, issued) =>
            {
                var asked = body.Contains("\"ask\"", StringComparison.Ordinal);
                var fields = asked
                    ? FlatJson.Read(body, "organization", "offer", "years", "ambition", "ask")
                    : FlatJson.Read(body, "organization", "offer", "years", "ambition");
                return new CounterSponsorOfferCommand
                {
                    ManagerId = manager,
                    IssuedOn = issued,
                    OrganizationId = fields.String("organization"),
                    OfferId = fields.String("offer"),
                    Years = fields.Int32("years"),
                    Ambition = Ambition(fields.String("ambition")),
                    AskMilli = asked ? fields.Int32("ask") : 0,
                };
            }),
        CommandCodecEntry.For<SignAtCurrentTermsCommand>(
            "sponsor.sign/1",
            command => command.AskMilli == 0
                ? FlatJson.Write(("organization", command.OrganizationId), ("talk", command.TalkId))
                : FlatJson.Write(("organization", command.OrganizationId), ("talk", command.TalkId), ("ask", command.AskMilli)),
            (body, manager, issued) =>
            {
                var asked = body.Contains("\"ask\"", StringComparison.Ordinal);
                var fields = asked ? FlatJson.Read(body, "organization", "talk", "ask") : FlatJson.Read(body, "organization", "talk");
                return new SignAtCurrentTermsCommand
                {
                    ManagerId = manager,
                    IssuedOn = issued,
                    OrganizationId = fields.String("organization"),
                    TalkId = fields.String("talk"),
                    AskMilli = asked ? fields.Int32("ask") : 0,
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
