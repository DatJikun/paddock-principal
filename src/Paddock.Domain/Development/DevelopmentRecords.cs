using System.Globalization;
using Paddock.Domain.Time;
using Paddock.Domain.World;

namespace Paddock.Domain.Development;

/// <summary>
/// The four areas the plan can prioritise. Each feeds one component of the performance vector (T41):
/// aero to downforce, chassis to mechanical grip, reliability to reliability, tyres and handling to braking.
/// Power is the engine (T43) and has no area. The mapping is a PLACEHOLDER for the owner to confirm.
/// </summary>
public enum DevArea
{
    Aero,
    Chassis,
    Reliability,
    TyresHandling,
}

public enum DevKind
{
    /// <summary>A part for the current car (the "current car" share of the split).</summary>
    Upgrade,

    /// <summary>Knowledge for the development account (the "account" share).</summary>
    Research,

    /// <summary>A new concept for the whole car (the "next year's car" share), deployed by <see cref="ConceptTiming"/>.</summary>
    Concept,
}

public enum ProjectStatus
{
    Active,

    /// <summary>A concept that is finished and waiting for its deployment moment.</summary>
    Ready,

    Completed,

    Failed,

    Cut,

    Deployed,
}

public enum ConceptTiming
{
    WhenReady,
    AfterRaces,
    NextSeason,

    /// <summary>Keeps a finished concept undeployed across season rollovers until a new timing is chosen. The old car runs on; the account keeps decaying and rivals keep moving (PP-043).</summary>
    Hold,
}

/// <summary>Stable id <c>dev:{n}</c> from the section counter. Numbers are never reused (INV-009).</summary>
public static class DevProjectIds
{
    public const string Prefix = "dev:";

    public static string Format(long number)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(number, 1);
        return Prefix + number.ToString(CultureInfo.InvariantCulture);
    }

    public static bool TryParse(string? id, out long number)
    {
        number = 0;
        if (id is null || !id.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return false;
        }

        var tail = id.AsSpan(Prefix.Length);
        if (tail.Length == 0 || (tail.Length > 1 && tail[0] == '0'))
        {
            return false;
        }

        return long.TryParse(tail, NumberStyles.None, CultureInfo.InvariantCulture, out number) && number >= 1;
    }
}

/// <summary>
/// What the principal sets, and the only thing a principal sets (PP-043): the split of resources and the area priorities.
/// The same record is what an AI principal sets in T44. <c>Spent*</c> is this season's spending by kind, which the engineers
/// read to see which share of the split is still owed.
/// </summary>
public sealed record DevelopmentPlan(
    OrganizationId Organization,
    int CurrentPercent,
    int AccountPercent,
    int NextYearPercent,
    int AeroPriority,
    int ChassisPriority,
    int ReliabilityPriority,
    int TyresPriority,
    long SpentCurrentCents,
    long SpentAccountCents,
    long SpentNextYearCents,
    GameDate? ChangedOn)
{
    public static DevelopmentPlan Default(OrganizationId organization) =>
        new(
            organization,
            DevelopmentEstimates.DefaultCurrentPercent,
            DevelopmentEstimates.DefaultAccountPercent,
            DevelopmentEstimates.DefaultNextYearPercent,
            DevelopmentEstimates.DefaultPriority,
            DevelopmentEstimates.DefaultPriority,
            DevelopmentEstimates.DefaultPriority,
            DevelopmentEstimates.DefaultPriority,
            0,
            0,
            0,
            null);

    public static bool IsValidSplit(int current, int account, int nextYear) =>
        current >= 0 && account >= 0 && nextYear >= 0 && current + account + nextYear == 100;

    public static bool IsValidPriority(int value) => value is >= 0 and <= DevelopmentEstimates.MaxPriority;

    public int PercentOf(DevKind kind) => kind switch
    {
        DevKind.Upgrade => CurrentPercent,
        DevKind.Research => AccountPercent,
        _ => NextYearPercent,
    };

    public long SpentOf(DevKind kind) => kind switch
    {
        DevKind.Upgrade => SpentCurrentCents,
        DevKind.Research => SpentAccountCents,
        _ => SpentNextYearCents,
    };

    public long TotalSpentCents => SpentCurrentCents + SpentAccountCents + SpentNextYearCents;

    public int PriorityOf(DevArea area) => area switch
    {
        DevArea.Aero => AeroPriority,
        DevArea.Chassis => ChassisPriority,
        DevArea.Reliability => ReliabilityPriority,
        _ => TyresPriority,
    };

    public DevelopmentPlan WithSpent(DevKind kind, long cents) => kind switch
    {
        DevKind.Upgrade => this with { SpentCurrentCents = Math.Max(0, SpentCurrentCents + cents) },
        DevKind.Research => this with { SpentAccountCents = Math.Max(0, SpentAccountCents + cents) },
        _ => this with { SpentNextYearCents = Math.Max(0, SpentNextYearCents + cents) },
    };

    public DevelopmentPlan ResetSpent() => this with { SpentCurrentCents = 0, SpentAccountCents = 0, SpentNextYearCents = 0 };
}

/// <summary>
/// The development account of one organization: knowledge put aside (<see cref="StockMilli"/>, 0 to 100 points in milli), the
/// share of next year's car already worked out (<see cref="NextYearShareMilli"/>, 0 to 1000), and the regulation year the stock
/// was last valued against. A rule change devalues the stock (DESIGN §5.3).
/// </summary>
public sealed record DevelopmentAccount(OrganizationId Organization, int StockMilli, int NextYearShareMilli, int RulesYear)
{
    public static DevelopmentAccount Empty(OrganizationId organization) => new(organization, 0, 0, 0);
}

/// <summary>
/// One development project. <see cref="ShareMilli"/> is the hidden expected share of the remaining headroom it closes, set when
/// it starts from the funding and the team's quality. <see cref="OutcomeMilli"/> is the realised share after the execution
/// noise, drawn from the <c>Development</c> stream when the project finishes. Neither is shown to a manager (INV-003).
/// </summary>
public sealed record DevProject(
    long Number,
    OrganizationId Organization,
    DevKind Kind,
    DevArea? Area,
    string Engineer,
    GameDate Started,
    int DurationDays,
    int ProgressDays,
    long CostCents,
    long PostedCents,
    int ShareMilli,
    int RiskMilli,
    int? OutcomeMilli,
    ProjectStatus Status,
    ConceptTiming Timing,
    int TimingRaces,
    int RacesWaited,
    GameDate? ClosedOn)
{
    public string Id => DevProjectIds.Format(Number);

    public bool IsActive => Status == ProjectStatus.Active;

    public double Progress => DurationDays <= 0 ? 1d : Math.Clamp((double)ProgressDays / DurationDays, 0d, 1d);
}
