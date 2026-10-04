using System.Globalization;
using Paddock.Domain.Time;
using Paddock.Domain.World;

namespace Paddock.Domain.Contracts;

/// <summary>
/// Where a negotiation stands. The first group is alive, the second is over.
/// <see cref="Open"/>: no offer yet. <see cref="AwaitingResponse"/>: an offer is with the person. <see cref="Countered"/>: the
/// person asks for other terms and holds to them. <see cref="PersonAgreed"/>: the person would sign the offer as it is.
/// <see cref="Considering"/>: the offer is acceptable but the person is waiting for the earliest deadline among rival offers.
/// </summary>
public enum NegotiationStatus
{
    Open = 0,
    AwaitingResponse = 1,
    Countered = 2,
    PersonAgreed = 3,
    Considering = 4,
    Agreed = 5,
    Refused = 6,
    WalkedAway = 7,
    Lost = 8,
    Lapsed = 9,
}

/// <summary>What one line of a negotiation's history is.</summary>
public enum RoundKind
{
    /// <summary>The proposer's offer.</summary>
    Offer = 0,

    /// <summary>The person asked for other terms.</summary>
    Counter = 1,

    /// <summary>The person would sign the offer as it is.</summary>
    Acceptance = 2,

    /// <summary>The person turned the offer down for good.</summary>
    Refusal = 3,
}

/// <summary>
/// One entry of a negotiation's history. <see cref="Number"/> is the round of the offer it belongs to. <see cref="Reasons"/>
/// are translation keys the person is willing to state (never hidden traits). Terms are present for offers, counters and acceptances.
/// </summary>
public sealed record NegotiationRound
{
    public NegotiationRound(int number, RoundKind kind, GameDate on, OfferTerms? terms, IReadOnlyList<string> reasons)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(number, 1);
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown round kind.");
        }

        if (kind != RoundKind.Refusal && terms is null)
        {
            throw new ArgumentException("An offer, counter or acceptance carries terms.", nameof(terms));
        }

        ArgumentNullException.ThrowIfNull(reasons);
        Number = number;
        Kind = kind;
        On = on;
        Terms = terms;
        Reasons = Array.AsReadOnly(reasons.ToArray());
    }

    public int Number { get; }

    public RoundKind Kind { get; }

    public GameDate On { get; }

    public OfferTerms? Terms { get; }

    public IReadOnlyList<string> Reasons { get; }
}

/// <summary>
/// One negotiation between a proposing organization (run by a manager) and a person about a subject (a driver seat or
/// a key staff role). Immutable: every change returns a new value, and an illegal change throws. The rules about rounds,
/// patience and status live here, so they hold whatever subject a later system brings. This is truth: what a manager may
/// see goes through a query (INV-003).
/// <para>
/// Interest runs from <see cref="NegotiationEstimates.InterestStart"/> down to zero, in thousandths. A nudge costs
/// <see cref="NegotiationEstimates.NudgePenalty"/>. At zero the person walks away.
/// </para>
/// </summary>
public sealed class Negotiation
{
    public const string IdPrefix = "neg:";

    public Negotiation(
        long number,
        string managerId,
        OrganizationId proposer,
        PersonId counterparty,
        NegotiationSubject subject,
        ContractId? renewalOf,
        GameDate opened,
        GameDate deadline,
        int maxRounds,
        int roundsUsed,
        int interest,
        NegotiationStatus status,
        OfferTerms? currentOffer,
        OfferTerms? counter,
        GameDate? respondOn,
        long consideredUtilityMicro,
        IReadOnlyList<string> reasons,
        IReadOnlyList<NegotiationRound> history,
        GameDate? closedOn,
        ContractId? signedContract)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(number, 1);
        ArgumentException.ThrowIfNullOrWhiteSpace(managerId);
        if (!proposer.IsAssigned)
        {
            throw new ArgumentException("Proposer is unassigned.", nameof(proposer));
        }

        if (!counterparty.IsAssigned)
        {
            throw new ArgumentException("Counterparty is unassigned.", nameof(counterparty));
        }

        if (deadline < opened)
        {
            throw new ArgumentException("The deadline is before the day the negotiation opened.", nameof(deadline));
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(maxRounds, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(roundsUsed);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(roundsUsed, maxRounds);
        ArgumentOutOfRangeException.ThrowIfNegative(interest);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(interest, NegotiationEstimates.InterestStart);
        if (!Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown status.");
        }

        ArgumentNullException.ThrowIfNull(reasons);
        ArgumentNullException.ThrowIfNull(history);
        var alive = status <= NegotiationStatus.Considering;
        if (alive == closedOn.HasValue)
        {
            throw new ArgumentException("A closed negotiation has a closing date and a live one has none.", nameof(closedOn));
        }

        if (closedOn is GameDate closed && closed < opened)
        {
            throw new ArgumentException("The closing date is before the day the negotiation opened.", nameof(closedOn));
        }

        if ((status == NegotiationStatus.Agreed) != signedContract.HasValue)
        {
            throw new ArgumentException("Only an agreed negotiation names the contract it signed.", nameof(signedContract));
        }

        if (status is NegotiationStatus.AwaitingResponse or NegotiationStatus.Countered or NegotiationStatus.PersonAgreed
            or NegotiationStatus.Considering && currentOffer is null)
        {
            throw new ArgumentException("A negotiation with an offer on the table has a current offer.", nameof(currentOffer));
        }

        if (status is NegotiationStatus.Countered or NegotiationStatus.PersonAgreed && counter is null)
        {
            throw new ArgumentException("A countered or agreed negotiation has the terms the person holds to.", nameof(counter));
        }

        if (respondOn is not null && status != NegotiationStatus.AwaitingResponse)
        {
            throw new ArgumentException("Only a negotiation awaiting a response has a response day.", nameof(respondOn));
        }

        Number = number;
        ManagerId = managerId;
        Proposer = proposer;
        Counterparty = counterparty;
        Subject = subject;
        RenewalOf = renewalOf;
        Opened = opened;
        Deadline = deadline;
        MaxRounds = maxRounds;
        RoundsUsed = roundsUsed;
        Interest = interest;
        Status = status;
        CurrentOffer = currentOffer;
        Counter = counter;
        RespondOn = respondOn;
        ConsideredUtilityMicro = consideredUtilityMicro;
        Reasons = Array.AsReadOnly(reasons.ToArray());
        History = Array.AsReadOnly(history.ToArray());
        ClosedOn = closedOn;
        SignedContract = signedContract;
    }

    /// <summary>A new negotiation with no offer yet.</summary>
    public static Negotiation Start(
        long number,
        string managerId,
        OrganizationId proposer,
        PersonId counterparty,
        NegotiationSubject subject,
        ContractId? renewalOf,
        GameDate opened,
        GameDate deadline,
        int maxRounds) =>
        new(
            number,
            managerId,
            proposer,
            counterparty,
            subject,
            renewalOf,
            opened,
            deadline,
            maxRounds,
            0,
            NegotiationEstimates.InterestStart,
            NegotiationStatus.Open,
            null,
            null,
            null,
            0,
            [],
            [],
            null,
            null);

    public long Number { get; }

    /// <summary>Stable id, <c>neg:{number}</c>.</summary>
    public string Id => IdOf(Number);

    /// <summary>The manager who runs the proposing side.</summary>
    public string ManagerId { get; }

    public OrganizationId Proposer { get; }

    public PersonId Counterparty { get; }

    public NegotiationSubject Subject { get; }

    /// <summary>The contract this negotiation would renew, or null for a new hire.</summary>
    public ContractId? RenewalOf { get; }

    public GameDate Opened { get; }

    /// <summary>
    /// The last day the offer stands. The person decides by the earliest deadline among acceptable offers. An answer (a counter
    /// or an agreement) pushes it out to at least <see cref="NegotiationEstimates.ResponseHoldDays"/> after the answer.
    /// </summary>
    public GameDate Deadline { get; }

    public int MaxRounds { get; }

    public int RoundsUsed { get; }

    /// <summary>The person's interest in thousandths. A nudge lowers it.</summary>
    public int Interest { get; }

    public NegotiationStatus Status { get; }

    /// <summary>The proposer's latest offer.</summary>
    public OfferTerms? CurrentOffer { get; }

    /// <summary>The terms the person holds to (a counter, or the offer itself when the person agreed).</summary>
    public OfferTerms? Counter { get; }

    /// <summary>The day the person answers, once scheduled.</summary>
    public GameDate? RespondOn { get; }

    /// <summary>The utility, times a million and rounded, the person found in the offer while considering it. Developer-side only.</summary>
    public long ConsideredUtilityMicro { get; }

    /// <summary>The reasons of the person's latest answer, as translation keys.</summary>
    public IReadOnlyList<string> Reasons { get; }

    public IReadOnlyList<NegotiationRound> History { get; }

    public GameDate? ClosedOn { get; }

    public ContractId? SignedContract { get; }

    /// <summary>True while the negotiation can still end in a contract.</summary>
    public bool IsActive => Status <= NegotiationStatus.Considering;

    /// <summary>True when an offer of the proposer is with, or held by, the person.</summary>
    public bool HasOfferOnTable =>
        Status is NegotiationStatus.AwaitingResponse or NegotiationStatus.Countered or NegotiationStatus.PersonAgreed
            or NegotiationStatus.Considering;

    public int RoundsLeft => MaxRounds - RoundsUsed;

    public static string IdOf(long number) => IdPrefix + number.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// The proposer makes an offer. It uses a round; when it is not a meaningful change from the previous offer or counter
    /// (<see cref="OfferTerms.IsMeaningfulChangeFrom"/>) it also costs interest. Allowed while open, countered or considered.
    /// </summary>
    public Negotiation WithOffer(OfferTerms offer, GameDate today)
    {
        ArgumentNullException.ThrowIfNull(offer);
        if (Status is not (NegotiationStatus.Open or NegotiationStatus.Countered or NegotiationStatus.Considering))
        {
            throw new InvalidOperationException($"Negotiation '{Id}' is {Status} and takes no offer.");
        }

        if (RoundsUsed >= MaxRounds)
        {
            throw new InvalidOperationException($"Negotiation '{Id}' has no rounds left.");
        }

        var reference = Counter ?? CurrentOffer;
        var interest = Interest;
        if (reference is not null && !offer.IsMeaningfulChangeFrom(reference))
        {
            interest = Math.Max(0, interest - NegotiationEstimates.NudgePenalty);
        }

        var round = RoundsUsed + 1;
        return Rebuild(
            status: NegotiationStatus.AwaitingResponse,
            roundsUsed: round,
            interest: interest,
            currentOffer: offer,
            counter: null,
            respondOn: null,
            consideredUtilityMicro: 0,
            reasons: [],
            history: Append(new NegotiationRound(round, RoundKind.Offer, today, offer, [])));
    }

    /// <summary>True when the offer would be a nudge: no meaningful change from the previous offer or counter.</summary>
    public bool WouldBeNudge(OfferTerms offer)
    {
        ArgumentNullException.ThrowIfNull(offer);
        var reference = Counter ?? CurrentOffer;
        return reference is not null && !offer.IsMeaningfulChangeFrom(reference);
    }

    /// <summary>The day the person will answer has been fixed.</summary>
    public Negotiation WithResponseScheduled(GameDate respondOn)
    {
        RequireStatus(NegotiationStatus.AwaitingResponse);
        if (RespondOn is not null)
        {
            throw new InvalidOperationException($"Negotiation '{Id}' already has a response day.");
        }

        return Rebuild(respondOn: respondOn);
    }

    /// <summary>The person asks for other terms and holds to them.</summary>
    public Negotiation WithCounter(OfferTerms counter, IReadOnlyList<string> reasons, GameDate today)
    {
        ArgumentNullException.ThrowIfNull(counter);
        RequireReasons(reasons);
        RequireStatus(NegotiationStatus.AwaitingResponse);
        return Rebuild(
            status: NegotiationStatus.Countered,
            counter: counter,
            respondOn: null,
            deadline: HeldUntil(today),
            reasons: reasons,
            history: Append(new NegotiationRound(RoundsUsed, RoundKind.Counter, today, counter, reasons)));
    }

    /// <summary>The person would sign the offer as it stands.</summary>
    public Negotiation WithAgreement(IReadOnlyList<string> reasons, GameDate today)
    {
        ArgumentNullException.ThrowIfNull(reasons);
        if (Status is not (NegotiationStatus.AwaitingResponse or NegotiationStatus.Considering))
        {
            throw new InvalidOperationException($"Negotiation '{Id}' is {Status} and cannot be agreed by the person.");
        }

        var offer = CurrentOffer!;
        return Rebuild(
            status: NegotiationStatus.PersonAgreed,
            counter: offer,
            respondOn: null,
            deadline: HeldUntil(today),
            consideredUtilityMicro: 0,
            reasons: reasons,
            history: Append(new NegotiationRound(RoundsUsed, RoundKind.Acceptance, today, offer, reasons)));
    }

    /// <summary>The offer is acceptable but the person waits for the earliest deadline among rival offers.</summary>
    public Negotiation WithConsideration(long utilityMicro)
    {
        RequireStatus(NegotiationStatus.AwaitingResponse);
        return Rebuild(status: NegotiationStatus.Considering, respondOn: null, consideredUtilityMicro: utilityMicro);
    }

    /// <summary>The person turns the offer down for good.</summary>
    public Negotiation WithRefusal(IReadOnlyList<string> reasons, GameDate today)
    {
        RequireReasons(reasons);
        RequireStatus(NegotiationStatus.AwaitingResponse);
        return Rebuild(
            status: NegotiationStatus.Refused,
            respondOn: null,
            closedOn: today,
            reasons: reasons,
            history: Append(new NegotiationRound(RoundsUsed, RoundKind.Refusal, today, null, reasons)));
    }

    /// <summary>The proposer walks away.</summary>
    public Negotiation WithWalkAway(GameDate today) =>
        Close(NegotiationStatus.WalkedAway, [], today);

    /// <summary>The person went elsewhere or is no longer available. At least one reason is required.</summary>
    public Negotiation WithLoss(IReadOnlyList<string> reasons, GameDate today)
    {
        RequireReasons(reasons);
        return Close(NegotiationStatus.Lost, reasons, today);
    }

    /// <summary>The deadline passed with no outcome.</summary>
    public Negotiation WithLapse(IReadOnlyList<string> reasons, GameDate today)
    {
        RequireReasons(reasons);
        return Close(NegotiationStatus.Lapsed, reasons, today);
    }

    /// <summary>The contract was signed.</summary>
    public Negotiation WithSigning(ContractId contract, GameDate today)
    {
        if (Status is not (NegotiationStatus.Countered or NegotiationStatus.PersonAgreed))
        {
            throw new InvalidOperationException($"Negotiation '{Id}' is {Status} and cannot be signed.");
        }

        return Rebuild(status: NegotiationStatus.Agreed, signedContract: contract, closedOn: today, respondOn: null);
    }

    /// <summary>The deadline once the person has answered: at least <see cref="NegotiationEstimates.ResponseHoldDays"/> after today.</summary>
    private GameDate HeldUntil(GameDate today)
    {
        var held = today.AddDays(NegotiationEstimates.ResponseHoldDays);
        return held > Deadline ? held : Deadline;
    }

    private Negotiation Close(NegotiationStatus status, IReadOnlyList<string> reasons, GameDate today)
    {
        if (!IsActive)
        {
            throw new InvalidOperationException($"Negotiation '{Id}' is already {Status}.");
        }

        return Rebuild(status: status, respondOn: null, closedOn: today, reasons: reasons);
    }

    private void RequireStatus(NegotiationStatus expected)
    {
        if (Status != expected)
        {
            throw new InvalidOperationException($"Negotiation '{Id}' is {Status}, not {expected}.");
        }
    }

    private static void RequireReasons(IReadOnlyList<string> reasons)
    {
        ArgumentNullException.ThrowIfNull(reasons);
        if (reasons.Count == 0)
        {
            throw new ArgumentException("A refusal, counter or loss always gives at least one reason.", nameof(reasons));
        }
    }

    private List<NegotiationRound> Append(NegotiationRound round)
    {
        var copy = new List<NegotiationRound>(History.Count + 1);
        copy.AddRange(History);
        copy.Add(round);
        return copy;
    }

    private Negotiation Rebuild(
        NegotiationStatus? status = null,
        int? roundsUsed = null,
        int? interest = null,
        OfferTerms? currentOffer = null,
        OfferTerms? counter = null,
        GameDate? respondOn = null,
        GameDate? deadline = null,
        long? consideredUtilityMicro = null,
        IReadOnlyList<string>? reasons = null,
        IReadOnlyList<NegotiationRound>? history = null,
        GameDate? closedOn = null,
        ContractId? signedContract = null)
    {
        var next = status ?? Status;
        var keepTerms = next is NegotiationStatus.AwaitingResponse or NegotiationStatus.Countered or NegotiationStatus.PersonAgreed
            or NegotiationStatus.Considering;
        return new Negotiation(
            Number,
            ManagerId,
            Proposer,
            Counterparty,
            Subject,
            RenewalOf,
            Opened,
            deadline ?? Deadline,
            MaxRounds,
            roundsUsed ?? RoundsUsed,
            interest ?? Interest,
            next,
            currentOffer ?? CurrentOffer,
            counter ?? (next is NegotiationStatus.Countered or NegotiationStatus.PersonAgreed ? Counter : keepTerms ? null : Counter),
            respondOn ?? (next == NegotiationStatus.AwaitingResponse ? RespondOn : null),
            consideredUtilityMicro ?? ConsideredUtilityMicro,
            reasons ?? Reasons,
            history ?? History,
            closedOn ?? ClosedOn,
            signedContract ?? SignedContract);
    }
}
