namespace Paddock.Simulation.Ai;

/// <summary>
/// An inclusive range of a 1 to 20 attribute that an organization believes (a band, never the true number, INV-003). The AI
/// decides on beliefs only, so this type, and every other input type of this namespace, is made of plain numbers and text: it
/// has no way to carry simulation truth.
/// </summary>
public readonly record struct BandOfBelief
{
    public BandOfBelief(int low, int high)
    {
        if (low < 1 || high > 20 || high < low)
        {
            throw new ArgumentOutOfRangeException(nameof(low), "A band runs inside 1 to 20 with its low end at most its high end.");
        }

        Low = low;
        High = high;
    }

    public int Low { get; }

    public int High { get; }

    public double Mid => (Low + High) / 2.0;
}

/// <summary>
/// What a team believes about a person's quality on the stars scale (0 to 5): a range. <see cref="IsKnown"/> is false for a
/// person the team has no belief about, which reads as a wide middle range (<see cref="AiEstimates.UnknownStars"/>).
/// </summary>
public readonly record struct Believed
{
    public Believed(double low, double high, bool isKnown = true)
    {
        if (double.IsNaN(low) || double.IsNaN(high) || low < 0.0 || high > 5.0 || high < low)
        {
            throw new ArgumentOutOfRangeException(nameof(low), "Stars run from 0 to 5 and the low end is at most the high end.");
        }

        Low = low;
        High = high;
        IsKnown = isKnown;
    }

    public double Low { get; }

    public double High { get; }

    public bool IsKnown { get; }

    public double Mid => (Low + High) / 2.0;

    public double Width => High - Low;

    /// <summary>The belief about a person the team knows nothing about.</summary>
    public static Believed Unknown { get; } = new(AiEstimates.UnknownStarsLow, AiEstimates.UnknownStarsHigh, isKnown: false);

    /// <summary>The middle of the range minus the share of its half-width a fearful principal discounts for uncertainty.</summary>
    public double Perceived(double riskAversion) => Mid - (riskAversion * Width / 2.0);
}

/// <summary>The four attributes of a team principal (PP-044) as his own organization believes them. Null means no belief.</summary>
public sealed record PrincipalAttributes(
    BandOfBelief? Negotiation,
    BandOfBelief? PeopleManagement,
    BandOfBelief? Politics,
    BandOfBelief? Business)
{
    public static PrincipalAttributes Unknown { get; } = new(null, null, null, null);
}

/// <summary>The competence tiers (1 to <see cref="AiEstimates.LevelCount"/>) a principal's believed attributes give.</summary>
public sealed record PrincipalSkillset(int Negotiation, int PeopleManagement, int Politics, int Business)
{
    /// <summary>The overall decision level: the tier of the mean of the four believed attributes.</summary>
    public int Level => PrincipalLevels.TierOfMean(
        (PrincipalLevels.Mid(Negotiation) + PrincipalLevels.Mid(PeopleManagement) + PrincipalLevels.Mid(Politics) + PrincipalLevels.Mid(Business)) / 4.0);

    /// <summary>A middling principal. Used when a team has no belief about its own principal.</summary>
    public static PrincipalSkillset Average { get; } = new(2, 2, 2, 2);

    /// <summary>Tier used for a decision of the given kind: people and talks, rules and money, or the mean of two facets.</summary>
    public int For(DecisionFacet facet) => facet switch
    {
        DecisionFacet.Market => Mean(Negotiation, PeopleManagement),
        DecisionFacet.Development => Mean(Politics, Business),
        DecisionFacet.Supply => Mean(Business, Negotiation),
        DecisionFacet.Sponsors => Mean(Business, Negotiation),
        _ => Level,
    };

    private static int Mean(int left, int right) => Math.Clamp((int)Math.Round((left + right) / 2.0, MidpointRounding.AwayFromZero), 1, AiEstimates.LevelCount);
}

/// <summary>Which principal attributes a decision leans on. It picks the noise tier of the decision.</summary>
public enum DecisionFacet
{
    Market = 0,
    Development = 1,
    Supply = 2,
    Sponsors = 3,
    Overall = 4,
}

/// <summary>Maps believed attributes to competence tiers.</summary>
public static class PrincipalLevels
{
    public static PrincipalSkillset From(PrincipalAttributes? attributes)
    {
        if (attributes is null)
        {
            return PrincipalSkillset.Average;
        }

        return new PrincipalSkillset(
            TierOf(attributes.Negotiation),
            TierOf(attributes.PeopleManagement),
            TierOf(attributes.Politics),
            TierOf(attributes.Business));
    }

    public static int TierOf(BandOfBelief? band) =>
        TierOfMean(band is { } known ? known.Mid : AiEstimates.UnknownAttribute);

    public static int TierOfMean(double mean)
    {
        var tier = 1;
        foreach (var floor in AiEstimates.LevelFloors)
        {
            if (mean >= floor)
            {
                tier++;
            }
        }

        return tier;
    }

    /// <summary>A representative attribute value of a tier: the middle of its range, used to average tiers back into a level.</summary>
    public static double Mid(int tier)
    {
        var floors = AiEstimates.LevelFloors;
        var low = tier <= 1 ? 1.0 : floors[tier - 2];
        var high = tier > floors.Count ? 20.0 : floors[tier - 1];
        return (low + high) / 2.0;
    }
}

/// <summary>
/// What a team can spend, as the team itself knows it (its own books: it is the team's own money). Amounts are whole nominal
/// dollars, the unit contracts use.
/// </summary>
/// <param name="CashDollars">Cash today. May be negative: a team may run an overdraft (PP-050).</param>
/// <param name="AllowanceDollars">The overdraft the principal lets the team run (<see cref="AiEstimates.OverdraftAllowanceShare"/> of the budget).</param>
/// <param name="ObligationsDollars">What the rest of the season already owes, salaries and fees, net of certain income.</param>
/// <param name="AnnualBudgetDollars">The era's typical annual budget, the yardstick shares are taken of.</param>
/// <param name="PayrollDollars">The yearly salaries the team already pays or has signed.</param>
public sealed record AiFunds(
    long CashDollars,
    long AllowanceDollars,
    long ObligationsDollars,
    long AnnualBudgetDollars,
    long PayrollDollars)
{
    /// <summary>Money available for new commitments in the rest of the season: cash and allowance, less what is already owed.</summary>
    public long Headroom => CashDollars + AllowanceDollars - (long)(ObligationsDollars * AiEstimates.CommitmentSafetyShare);

    /// <summary>The payroll the archetype lets the team carry.</summary>
    public long PayrollLimit(ArchetypeProfile profile) => (long)(AnnualBudgetDollars * profile.PayrollShare);

    /// <summary>
    /// True when a new yearly salary of <paramref name="annualSalary"/> signed on <paramref name="today"/> fits: the part of it that falls
    /// in the rest of this season has to fit the headroom. A multi-season payroll cap is a separate, softer question (<see cref="PayrollLimit"/>).
    /// </summary>
    public bool CanCommit(long annualSalary, DateOnly today) =>
        annualSalary <= 0 || Headroom >= PartOfSeasonLeft(annualSalary, today);

    public static long PartOfSeasonLeft(long annualAmount, DateOnly today)
    {
        var daysInYear = DateTime.IsLeapYear(today.Year) ? 366 : 365;
        var left = daysInYear - today.DayOfYear + 1;
        return (long)Math.Ceiling(annualAmount * (double)left / daysInYear);
    }
}
