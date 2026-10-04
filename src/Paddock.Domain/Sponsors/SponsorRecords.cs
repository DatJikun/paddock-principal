using System.Globalization;
using Paddock.Domain.Time;
using Paddock.Domain.World;

namespace Paddock.Domain.Sponsors;

public enum TalkStatus
{
    Open = 0,
    Signed = 1,
    WalkedAway = 2,
    LostToRival = 3,
}

/// <summary>Whether a rival team is also talking to the sponsor. Hidden truth, decided by the day handler.</summary>
public enum RivalState
{
    Undecided = 0,
    Absent = 1,
    Present = 2,
}

public enum DealStatus
{
    Active = 0,

    /// <summary>The deal ran to its end date.</summary>
    Completed = 1,

    /// <summary>The sponsor left before the end because its objective failed.</summary>
    Ended = 2,
}

public enum DealObjectiveOutcome
{
    /// <summary>No objective, or it is not settled yet.</summary>
    None = 0,
    Met = 1,
    Failed = 2,
}

public enum OfferStatus
{
    Open = 0,
    Accepted = 1,
    Declined = 2,
    Lapsed = 3,
}

/// <summary>
/// A negotiation with one sponsor for one slot (the waiting game). Immutable. <see cref="Rival"/> is truth the
/// organization does not see unless its negotiator is skilled enough (INV-003); it only decides whether the day roll can take
/// the sponsor away. It starts <see cref="RivalState.Undecided"/> because a command cannot draw a random number: the day
/// handler decides it on the first day, from the <c>Market</c> stream. <see cref="FullAnnualCents"/> is the full price fixed
/// when the talks opened.
/// </summary>
public sealed record SponsorTalk(
    long Number,
    OrganizationId Organization,
    string SponsorId,
    int Slot,
    SlotKind Kind,
    string ManagerId,
    GameDate Opened,
    TalkStatus Status,
    GameDate? ClosedOn,
    RivalState Rival,
    int CapMilli,
    long FullAnnualCents)
{
    public string Id => SponsorsSection.TalkIdOf(Number);

    public bool IsOpen => Status == TalkStatus.Open;

    /// <summary>Terms today in thousandths of the full price: they improve with waiting, up to the cap.</summary>
    public int TermsMilliOn(GameDate today) => SponsorPricing.TermsMilli(Opened.DaysUntil(today), CapMilli);

    /// <summary>The annual amount the sponsor would sign for today.</summary>
    public long AnnualCentsOn(GameDate today) => FullAnnualCents * TermsMilliOn(today) / 1000;

    /// <summary>The annual amount at the cap, which is the best the waiting can reach.</summary>
    public long CappedAnnualCents => FullAnnualCents * CapMilli / 1000;
}

/// <summary>A signed sponsorship. Immutable; the section returns a new one when it changes.</summary>
public sealed record SponsorDeal(
    long Number,
    OrganizationId Organization,
    string SponsorId,
    int Slot,
    SlotKind Kind,
    GameDate Start,
    GameDate End,
    long AnnualCents,
    int InstalmentsPaid,
    string? ObjectiveId,
    DealObjectiveOutcome Outcome,
    long BonusCents,
    DealStatus Status,
    GameDate? EndedOn)
{
    public string Id => SponsorsSection.DealIdOf(Number);

    public bool IsActive => Status == DealStatus.Active;

    public bool IsActiveOn(GameDate day) => IsActive && Start <= day && day <= End;
}

/// <summary>A renewal the sponsor offers shortly before a deal ends. Answered with <c>RespondToSponsorOffer</c>.</summary>
public sealed record SponsorOffer(
    long Number,
    long DealNumber,
    OrganizationId Organization,
    string SponsorId,
    int Slot,
    SlotKind Kind,
    long AnnualCents,
    GameDate Opened,
    GameDate ValidUntil,
    OfferStatus Status,
    GameDate? ClosedOn)
{
    public string Id => SponsorsSection.OfferIdOf(Number);

    public bool IsOpen => Status == OfferStatus.Open;
}

/// <summary>Ledger reason keys of the sponsor system. Copy lives in the string files under the same keys.</summary>
public static class SponsorReason
{
    public const string Instalment = "sponsor.reason.instalment";

    public const string Bonus = "sponsor.reason.bonus";
}

internal static class SponsorText
{
    public static string Invariant(long value) => value.ToString(CultureInfo.InvariantCulture);
}
