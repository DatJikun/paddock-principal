using Paddock.Domain.Contracts;
using Paddock.Domain.Time;
using Paddock.Domain.World;

namespace Paddock.Domain.Supply;

/// <summary>What is on the table: the yearly price in cents (T37 money), the length in seasons, and exclusivity.</summary>
public sealed record SupplyTerms
{
    public SupplyTerms(long annualPriceCents, int seasons, bool exclusive)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(annualPriceCents);
        ArgumentOutOfRangeException.ThrowIfLessThan(seasons, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(seasons, NegotiationEstimates.MaxYears);
        AnnualPriceCents = annualPriceCents;
        Seasons = seasons;
        Exclusive = exclusive;
    }

    public long AnnualPriceCents { get; init; }

    public int Seasons { get; init; }

    public bool Exclusive { get; init; }

    /// <summary>
    /// A real change from <paramref name="previous"/>: another length or exclusivity, or a price rise of at least
    /// <see cref="NegotiationEstimates.MinMeaningfulImprovementPercent"/> percent (the T39 nudge rule). Anything smaller is nudging.
    /// </summary>
    public bool IsMeaningfulChangeFrom(SupplyTerms previous)
    {
        ArgumentNullException.ThrowIfNull(previous);
        if (Seasons != previous.Seasons || Exclusive != previous.Exclusive)
        {
            return true;
        }

        var rise = AnnualPriceCents - previous.AnnualPriceCents;
        return rise > 0 && rise * 100 >= previous.AnnualPriceCents * NegotiationEstimates.MinMeaningfulImprovementPercent;
    }
}

/// <summary>
/// One supply deal: a supplier delivers one item to one customer for whole seasons. The authored start data and a signed negotiation
/// both produce this type. <see cref="PaidSeason"/> is the last season whose fee was posted to the ledger (0 for none), so a fee is
/// never posted twice. <see cref="EngineName"/> is the authored model name when there is one.
/// </summary>
public sealed record SupplyDeal
{
    public SupplyDeal(
        long number,
        SupplyItem item,
        SupplyKind kind,
        OrganizationId supplier,
        OrganizationId customer,
        int firstSeason,
        SupplyTerms terms,
        GameDate signed,
        SupplyDealStatus status,
        GameDate? endedOn,
        int paidSeason,
        string? engineName)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(number, 1);
        if (!Enum.IsDefined(item) || !Enum.IsDefined(kind) || !Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(nameof(item), "Unknown supply item, kind or status.");
        }

        if (!supplier.IsAssigned || !customer.IsAssigned || supplier == customer && kind != SupplyKind.Works)
        {
            throw new ArgumentException("A deal names two different organizations (a works deal may name one).", nameof(supplier));
        }

        ArgumentNullException.ThrowIfNull(terms);
        ArgumentOutOfRangeException.ThrowIfLessThan(firstSeason, 1950);
        if ((status == SupplyDealStatus.Active) == endedOn.HasValue)
        {
            throw new ArgumentException("An ended deal has an end date and an active one has none.", nameof(endedOn));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(paidSeason);
        Number = number;
        Item = item;
        Kind = kind;
        Supplier = supplier;
        Customer = customer;
        FirstSeason = firstSeason;
        Terms = terms;
        Signed = signed;
        Status = status;
        EndedOn = endedOn;
        PaidSeason = paidSeason;
        EngineName = string.IsNullOrWhiteSpace(engineName) ? null : engineName;
    }

    public long Number { get; init; }

    public string Id => SupplyIds.Deal(Number);

    public SupplyItem Item { get; init; }

    public SupplyKind Kind { get; init; }

    public OrganizationId Supplier { get; init; }

    public OrganizationId Customer { get; init; }

    public int FirstSeason { get; init; }

    public SupplyTerms Terms { get; init; }

    public GameDate Signed { get; init; }

    public SupplyDealStatus Status { get; init; }

    public GameDate? EndedOn { get; init; }

    public int PaidSeason { get; init; }

    public string? EngineName { get; init; }

    public int LastSeason => FirstSeason + Terms.Seasons - 1;

    public long AnnualPriceCents => Terms.AnnualPriceCents;

    public bool IsActive => Status == SupplyDealStatus.Active;

    /// <summary>True when the deal is in force on <paramref name="date"/>: active, and the date falls in its seasons.</summary>
    public bool IsInForceOn(GameDate date) => IsActive && date.Year >= FirstSeason && date.Year <= LastSeason;

    /// <summary>True when the seasons of this deal overlap the whole seasons from <paramref name="first"/> to <paramref name="last"/>.</summary>
    public bool Overlaps(int first, int last) => IsActive && first <= LastSeason && FirstSeason <= last;

    public SupplyDeal WithPaidSeason(int season) => this with { PaidSeason = season };

    public SupplyDeal Ended(GameDate on) => this with { Status = SupplyDealStatus.Ended, EndedOn = on };

    public SupplyDeal Voided(GameDate on) => this with { Status = SupplyDealStatus.Voided, EndedOn = on };
}

/// <summary>
/// One negotiation of a deal between the proposing customer (run by a manager) and a supplier. It keeps the T39 vocabulary:
/// <see cref="NegotiationStatus"/> (only <c>AwaitingResponse</c>, <c>Countered</c> and the closed states occur), the
/// <see cref="NegotiationEstimates"/> for patience, interest, the nudge rule and the deadline, and reasons as translation keys.
/// <see cref="NegotiationCore"/> itself answers for a person (personality, utility of a contract) and so is not reused for the answer:
/// the supplier answers in <see cref="SupplierResponder"/>. Truth: a manager reads a query (INV-003).
/// </summary>
public sealed record SupplyNegotiation
{
    public SupplyNegotiation(
        long number,
        string managerId,
        OrganizationId customer,
        OrganizationId supplier,
        SupplyItem item,
        SupplyKind kind,
        int firstSeason,
        GameDate opened,
        GameDate deadline,
        int maxRounds,
        int roundsUsed,
        int interest,
        NegotiationStatus status,
        SupplyTerms offer,
        SupplyTerms? counter,
        GameDate? respondOn,
        IReadOnlyList<string> reasons,
        GameDate? closedOn,
        long? signedDeal)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(number, 1);
        ArgumentException.ThrowIfNullOrWhiteSpace(managerId);
        if (!customer.IsAssigned || !supplier.IsAssigned || customer == supplier)
        {
            throw new ArgumentException("A negotiation names two different organizations.", nameof(supplier));
        }

        if (!Enum.IsDefined(item) || !Enum.IsDefined(kind) || !Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(nameof(item), "Unknown supply item, kind or status.");
        }

        ArgumentNullException.ThrowIfNull(offer);
        ArgumentNullException.ThrowIfNull(reasons);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxRounds, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(roundsUsed);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(roundsUsed, maxRounds);
        ArgumentOutOfRangeException.ThrowIfNegative(interest);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(interest, NegotiationEstimates.InterestStart);
        if (deadline < opened)
        {
            throw new ArgumentException("The deadline is before the day the negotiation opened.", nameof(deadline));
        }

        var alive = status is NegotiationStatus.AwaitingResponse or NegotiationStatus.Countered;
        if (!alive && status is not (NegotiationStatus.Agreed or NegotiationStatus.Refused or NegotiationStatus.WalkedAway or NegotiationStatus.Lapsed))
        {
            throw new ArgumentOutOfRangeException(nameof(status), status, "A supply negotiation does not use this status.");
        }

        if (alive == closedOn.HasValue)
        {
            throw new ArgumentException("A closed negotiation has a closing date and a live one has none.", nameof(closedOn));
        }

        if ((status == NegotiationStatus.Agreed) != signedDeal.HasValue)
        {
            throw new ArgumentException("Only an agreed negotiation names the deal it signed.", nameof(signedDeal));
        }

        if (status == NegotiationStatus.Countered && counter is null)
        {
            throw new ArgumentException("A countered negotiation has the terms the supplier holds to.", nameof(counter));
        }

        Number = number;
        ManagerId = managerId;
        Customer = customer;
        Supplier = supplier;
        Item = item;
        Kind = kind;
        FirstSeason = firstSeason;
        Opened = opened;
        Deadline = deadline;
        MaxRounds = maxRounds;
        RoundsUsed = roundsUsed;
        Interest = interest;
        Status = status;
        Offer = offer;
        Counter = counter;
        RespondOn = respondOn;
        Reasons = Array.AsReadOnly(reasons.ToArray());
        ClosedOn = closedOn;
        SignedDeal = signedDeal;
    }

    public long Number { get; init; }

    public string Id => SupplyIds.Negotiation(Number);

    public string ManagerId { get; init; }

    public OrganizationId Customer { get; init; }

    public OrganizationId Supplier { get; init; }

    public SupplyItem Item { get; init; }

    public SupplyKind Kind { get; init; }

    public int FirstSeason { get; init; }

    public GameDate Opened { get; init; }

    public GameDate Deadline { get; init; }

    public int MaxRounds { get; init; }

    public int RoundsUsed { get; init; }

    /// <summary>The supplier's interest in thousandths. A nudge lowers it, and it raises the price the supplier accepts.</summary>
    public int Interest { get; init; }

    public NegotiationStatus Status { get; init; }

    /// <summary>The customer's latest offer.</summary>
    public SupplyTerms Offer { get; init; }

    /// <summary>The terms the supplier holds to after a counter.</summary>
    public SupplyTerms? Counter { get; init; }

    public GameDate? RespondOn { get; init; }

    /// <summary>Translation keys of the supplier's latest answer.</summary>
    public IReadOnlyList<string> Reasons { get; init; }

    public GameDate? ClosedOn { get; init; }

    public long? SignedDeal { get; init; }

    public bool IsActive => Status is NegotiationStatus.AwaitingResponse or NegotiationStatus.Countered;

    public int RoundsLeft => MaxRounds - RoundsUsed;

    public static SupplyNegotiation Start(
        long number,
        string managerId,
        OrganizationId customer,
        OrganizationId supplier,
        SupplyItem item,
        SupplyKind kind,
        int firstSeason,
        GameDate opened,
        SupplyTerms offer) =>
        new(
            number,
            managerId,
            customer,
            supplier,
            item,
            kind,
            firstSeason,
            opened,
            opened.AddDays(SupplyEstimates.DeadlineDays),
            NegotiationEstimates.BaseRounds,
            1,
            NegotiationEstimates.InterestStart,
            NegotiationStatus.AwaitingResponse,
            offer,
            null,
            null,
            [],
            null,
            null);

    /// <summary>True when <paramref name="offer"/> would be a nudge: no meaningful change from the counter or the offer before it.</summary>
    public bool WouldBeNudge(SupplyTerms offer) => !offer.IsMeaningfulChangeFrom(Counter ?? Offer);

    /// <summary>The customer answers a counter with a new offer. Uses a round; a nudge also costs interest.</summary>
    public SupplyNegotiation WithOffer(SupplyTerms offer, GameDate today)
    {
        ArgumentNullException.ThrowIfNull(offer);
        if (Status != NegotiationStatus.Countered || RoundsUsed >= MaxRounds)
        {
            throw new InvalidOperationException($"Negotiation '{Id}' takes no offer now.");
        }

        var interest = WouldBeNudge(offer) ? Math.Max(0, Interest - NegotiationEstimates.NudgePenalty) : Interest;
        var held = today.AddDays(NegotiationEstimates.ResponseHoldDays);
        return this with
        {
            Status = NegotiationStatus.AwaitingResponse,
            RoundsUsed = RoundsUsed + 1,
            Interest = interest,
            Offer = offer,
            Counter = null,
            RespondOn = null,
            Reasons = [],
            Deadline = held > Deadline ? held : Deadline,
        };
    }

    public SupplyNegotiation WithResponseScheduled(GameDate respondOn)
    {
        RequireStatus(NegotiationStatus.AwaitingResponse);
        return this with { RespondOn = respondOn };
    }

    public SupplyNegotiation WithCounter(SupplyTerms counter, IReadOnlyList<string> reasons, GameDate today)
    {
        ArgumentNullException.ThrowIfNull(counter);
        RequireReasons(reasons);
        RequireStatus(NegotiationStatus.AwaitingResponse);
        var held = today.AddDays(NegotiationEstimates.ResponseHoldDays);
        return this with
        {
            Status = NegotiationStatus.Countered,
            Counter = counter,
            RespondOn = null,
            Reasons = reasons.ToArray(),
            Deadline = held > Deadline ? held : Deadline,
        };
    }

    public SupplyNegotiation WithRefusal(IReadOnlyList<string> reasons, GameDate today)
    {
        RequireReasons(reasons);
        RequireStatus(NegotiationStatus.AwaitingResponse);
        return this with { Status = NegotiationStatus.Refused, RespondOn = null, Reasons = reasons.ToArray(), ClosedOn = today };
    }

    public SupplyNegotiation WithAgreement(long deal, GameDate today)
    {
        if (!IsActive)
        {
            throw new InvalidOperationException($"Negotiation '{Id}' is already {Status}.");
        }

        return this with { Status = NegotiationStatus.Agreed, RespondOn = null, SignedDeal = deal, ClosedOn = today };
    }

    public SupplyNegotiation WithWalkAway(GameDate today) => Close(NegotiationStatus.WalkedAway, [], today);

    public SupplyNegotiation WithLapse(IReadOnlyList<string> reasons, GameDate today)
    {
        RequireReasons(reasons);
        return Close(NegotiationStatus.Lapsed, reasons, today);
    }

    private SupplyNegotiation Close(NegotiationStatus status, IReadOnlyList<string> reasons, GameDate today)
    {
        if (!IsActive)
        {
            throw new InvalidOperationException($"Negotiation '{Id}' is already {Status}.");
        }

        return this with { Status = status, RespondOn = null, Reasons = reasons.ToArray(), ClosedOn = today };
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
            throw new ArgumentException("A refusal, counter or lapse always gives at least one reason.", nameof(reasons));
        }
    }
}
