using Paddock.Domain.Cars;
using Paddock.Domain.People;
using Paddock.Domain.Time;
using Paddock.Domain.World;

namespace Paddock.Domain.Development;

/// <summary>One key engineer in post: who, in which chair, with which attributes, and how settled they are.</summary>
public sealed record Engineer(string Id, StaffRole? Role, IReadOnlyDictionary<string, int> Attributes, double Experience, double Adaptation)
{
    public int Attribute(string key) =>
        Attributes.TryGetValue(key, out var value) ? value : CarEstimates.DefaultStaffAttribute;

    public int Innovation => Attribute(StaffCatalogue.InnovationKey);

    /// <summary>The areas this engineer proposes work in, and the attribute that says how good they are at each.</summary>
    public IReadOnlyList<(DevArea Area, string Key)> Areas => Role switch
    {
        StaffRole.TechnicalDirector =>
            [(DevArea.Aero, "vision"), (DevArea.Chassis, "vision"), (DevArea.Reliability, "vision"), (DevArea.TyresHandling, "vision")],
        StaffRole.ChiefDesigner => [(DevArea.Chassis, "chassis"), (DevArea.Reliability, "precision")],
        StaffRole.HeadOfAerodynamics => [(DevArea.Aero, "aerodynamics")],
        StaffRole.HeadOfVehicleDynamics => [(DevArea.TyresHandling, "tyres"), (DevArea.Chassis, "suspension")],
        _ =>
            [(DevArea.Aero, "-"), (DevArea.Chassis, "-"), (DevArea.Reliability, "-"), (DevArea.TyresHandling, "-")],
    };

    /// <summary>The attribute that says how good this engineer is at leading a whole new concept.</summary>
    public int ConceptSkill => Role switch
    {
        StaffRole.TechnicalDirector => Attribute("vision"),
        StaffRole.ChiefDesigner => Attribute("integration"),
        StaffRole.HeadOfAerodynamics => Attribute("tunnel_correlation"),
        StaffRole.HeadOfVehicleDynamics => Attribute("suspension"),
        _ => CarEstimates.DefaultStaffAttribute,
    };
}

/// <summary>
/// PLACEHOLDER for the department model of DESIGN §6.2 (not accepted yet): one headcount and one average quality per team.
/// Headcount sets how long a project takes. Key people and the budget set quality. More engineers never raise quality.
/// </summary>
public sealed record EngineeringCapacity(int Headcount, double Quality, int ReferenceHeadcount)
{
    public int Slots => Math.Clamp(1 + (Headcount / DevelopmentEstimates.HeadcountPerSlot), 1, DevelopmentEstimates.MaxSlots);

    /// <summary>The same team with more people. Quality does not move (DESIGN §5.3).</summary>
    public EngineeringCapacity WithExtraHeadcount(int extra) =>
        this with { Headcount = Math.Max(1, Headcount + extra) };

    /// <summary>Days a project of <paramref name="baseDays"/> takes. Diminishing: it scales with the square root of the headcount ratio.</summary>
    public int DurationDays(int baseDays)
    {
        var ratio = Math.Sqrt((double)ReferenceHeadcount / Math.Max(1, Headcount));
        var scale = Math.Clamp(ratio, DevelopmentEstimates.DurationFloor, DevelopmentEstimates.DurationCeiling);
        return Math.Max(1, (int)Math.Round(baseDays * scale, MidpointRounding.AwayFromZero));
    }

    public static int EraHeadcount(int year)
    {
        var anchors = DevelopmentEstimates.HeadcountAnchors;
        if (year <= anchors[0].Year)
        {
            return anchors[0].Headcount;
        }

        for (var i = 1; i < anchors.Count; i++)
        {
            if (year <= anchors[i].Year)
            {
                var (y0, h0) = anchors[i - 1];
                var (y1, h1) = anchors[i];
                return h0 + (int)Math.Round((double)(h1 - h0) * (year - y0) / (y1 - y0), MidpointRounding.AwayFromZero);
            }
        }

        return anchors[^1].Headcount;
    }

    /// <summary>
    /// Derives the capacity of a team from the engineers it has on the day, the era and its cash.
    /// <paramref name="balanceCents"/> and <paramref name="annualBudgetCents"/> only lower quality when the cash is gone.
    /// </summary>
    public static EngineeringCapacity Derive(
        IReadOnlyList<Engineer> engineers,
        int year,
        int keyChairsInEra,
        long balanceCents,
        long annualBudgetCents)
    {
        ArgumentNullException.ThrowIfNull(engineers);
        var reference = EraHeadcount(year);
        var distinctRoles = engineers.Where(engineer => engineer.Role is not null).Select(engineer => engineer.Role).Distinct().Count();
        var filled = Math.Min(distinctRoles, Math.Max(1, keyChairsInEra));
        var staffFactor = DevelopmentEstimates.StaffFactorFloor
            + ((1d - DevelopmentEstimates.StaffFactorFloor) * filled / Math.Max(1, keyChairsInEra));
        var headcount = Math.Max(1, (int)Math.Round(reference * staffFactor, MidpointRounding.AwayFromZero));

        double sum = 0;
        var count = 0;
        foreach (var engineer in engineers)
        {
            foreach (var (_, key) in engineer.Areas)
            {
                sum += (key == "-" ? CarEstimates.DefaultStaffAttribute : engineer.Attribute(key)) / 20d;
                count++;
            }
        }

        var staff = count == 0 ? CarEstimates.DefaultStaffAttribute / 20d : sum / count;
        var raw = DevelopmentEstimates.QualityFloor + ((1d - DevelopmentEstimates.QualityFloor) * Math.Clamp(staff, 0d, 1d));
        var ratio = annualBudgetCents <= 0 ? 0d : (double)balanceCents / annualBudgetCents;
        var budget = Math.Clamp(0.9d + (0.1d * ratio), DevelopmentEstimates.BudgetFactorFloor, 1d);
        return new EngineeringCapacity(headcount, DevelopmentEstimates.Quantize(raw * budget), reference);
    }
}

/// <summary>Pure formulas of development: gain, funding, the account, understanding. No clock and no RNG.</summary>
public static class DevelopmentMath
{
    /// <summary>The performance component an area feeds, and the matching full-ceiling value of a concept.</summary>
    public static double LevelOf(PerformanceLevels levels, DevArea area) => area switch
    {
        DevArea.Aero => levels.Downforce,
        DevArea.Chassis => levels.MechanicalGrip,
        DevArea.Reliability => levels.Reliability,
        _ => levels.Braking,
    };

    public static PerformanceLevels WithLevel(PerformanceLevels levels, DevArea area, double value) => area switch
    {
        DevArea.Aero => levels with { Downforce = CarEstimates.ClampRating(value) },
        DevArea.Chassis => levels with { MechanicalGrip = CarEstimates.ClampRating(value) },
        DevArea.Reliability => levels with { Reliability = CarEstimates.ClampRating(value) },
        _ => levels with { Braking = CarEstimates.ClampRating(value) },
    };

    /// <summary>Ceiling minus level for one area, never negative. The ceiling is the concept at its full potential.</summary>
    public static double Headroom(TeamCar car, DevArea area)
    {
        ArgumentNullException.ThrowIfNull(car);
        var full = ConceptMapping.Effects(car.Concept, car.ConceptCeiling).Full;
        return Math.Max(0d, LevelOf(full, area) - LevelOf(car.Levels, area));
    }

    public static double AnnualBudgetCents(long typicalCents) => typicalCents * DevelopmentEstimates.AnnualBudgetShare;

    public static int BaseDays(DevKind kind) => kind switch
    {
        DevKind.Upgrade => DevelopmentEstimates.UpgradeBaseDays,
        DevKind.Research => DevelopmentEstimates.ResearchBaseDays,
        _ => DevelopmentEstimates.ConceptBaseDays,
    };

    /// <summary>
    /// Days a committed concept takes before headcount: the base days times the square root of how much bigger the era's team is than
    /// in 1950, as a stand-in for how complex the car is. ESTIMATE; roughly 40 days in 1955 and 190 in 2025.
    /// </summary>
    public static int ConceptProductionDays(int year) =>
        (int)Math.Round(
            DevelopmentEstimates.ConceptProductionBaseDays
            * Math.Sqrt((double)EngineeringCapacity.EraHeadcount(year) / EngineeringCapacity.EraHeadcount(1950)),
            MidpointRounding.AwayFromZero);

    public static long ProductionCostCents(long developmentCostCents) =>
        (long)Math.Round(developmentCostCents * DevelopmentEstimates.ConceptProductionCostShare, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Cost of a project: the annual development budget times the plan's share for the kind times the base duration over a year.
    /// It does not depend on headcount, so more engineers finish sooner for the same money.
    /// </summary>
    public static long CostCents(double annualBudgetCents, int percent, DevKind kind) =>
        (long)Math.Round(annualBudgetCents * percent / 100d * BaseDays(kind) / 365d, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Hidden expected share of the remaining headroom a project closes. Linear in the funding, so half the resources
    /// is half the share; the cap keeps it from reaching the ceiling in one step.
    /// </summary>
    public static double ExpectedShare(DevKind kind, long costCents, double annualBudgetCents, double quality)
    {
        if (annualBudgetCents <= 0d)
        {
            return 0d;
        }

        var funding = costCents / annualBudgetCents;
        var multiple = kind == DevKind.Concept ? DevelopmentEstimates.ConceptGainMultiple : 1d;
        return Math.Clamp(DevelopmentEstimates.GainPerFunding * multiple * funding * quality, 0d, DevelopmentEstimates.MaxShare);
    }

    /// <summary>Chance a project fails: lower for a skilled lead, higher for a concept. <paramref name="skill"/> is 1 to 20.</summary>
    public static double Risk(DevKind kind, int skill)
    {
        var unit = Math.Clamp((skill - 1) / 19d, 0d, 1d);
        var multiple = kind == DevKind.Concept ? DevelopmentEstimates.ConceptRiskMultiple : 1d;
        return Math.Max(DevelopmentEstimates.MinRisk, DevelopmentEstimates.BaseRisk * multiple * (1.5d - unit));
    }

    /// <summary>Execution noise from one uniform draw: 0.75 to 1.25, mean 1.</summary>
    public static double Noise(double uniform) => DevelopmentEstimates.NoiseLow + (DevelopmentEstimates.NoiseSpan * uniform);

    /// <summary>Points gained: the realised share of the headroom, never more than the headroom.</summary>
    public static double Gain(double headroom, double share) =>
        CarEstimates.Quantize(Math.Max(0d, headroom) * Math.Clamp(share, 0d, 1d));

    /// <summary>Research adds to the account in proportion to the room left in it (0 to 100 points).</summary>
    public static int StockAfterResearch(int stockMilli, double share)
    {
        var stock = stockMilli / 1000d;
        return Math.Clamp(DevelopmentEstimates.Milli(stock + ((100d - stock) * Math.Clamp(share, 0d, 1d))), 0, 100_000);
    }

    /// <summary>Extra share an Upgrade gets from the account, and what is left of the account afterwards.</summary>
    public static (double Bonus, int StockMilli) DrawOnAccount(int stockMilli)
    {
        var fraction = stockMilli / 100_000d;
        var bonus = DevelopmentEstimates.AccountBoost * fraction;
        var left = (int)Math.Round(stockMilli * (1d - DevelopmentEstimates.AccountUse), MidpointRounding.AwayFromZero);
        return (bonus, left);
    }

    public static int StockAfterDay(int stockMilli) =>
        (int)Math.Round(stockMilli * (1d - DevelopmentEstimates.AccountDailyDecay), MidpointRounding.AwayFromZero);

    /// <summary>
    /// A rule change devalues the account in proportion to the dimensions that changed: stock times
    /// (1 - changed / total * scale), never below zero. No change leaves it as it is.
    /// </summary>
    public static int StockAfterRuleChange(int stockMilli, int changedDimensions, int totalDimensions)
    {
        if (changedDimensions <= 0 || totalDimensions <= 0)
        {
            return stockMilli;
        }

        var loss = Math.Min(1d, (double)changedDimensions / totalDimensions * DevelopmentEstimates.RuleChangeLossScale);
        return (int)Math.Round(stockMilli * (1d - loss), MidpointRounding.AwayFromZero);
    }

    /// <summary>Share of next year's car already worked out, after one more concept closes <paramref name="share"/> of what is left.</summary>
    public static int NextYearShareAfter(int currentMilli, double share) =>
        Math.Clamp(DevelopmentEstimates.Milli((currentMilli / 1000d) + ((1d - (currentMilli / 1000d)) * Math.Clamp(share, 0d, 1d))), 0, 1000);

    /// <summary>
    /// The most the era's testing rules let understanding reach, 0 to 100. ESTIMATE values per rule value of
    /// <c>in_season_testing</c> and <c>aero_testing_restrictions</c>; the product of both.
    /// </summary>
    public static double UnderstandingCap(string? inSeasonTesting, string? aeroTesting)
    {
        var testing = inSeasonTesting switch
        {
            "annual_mileage_cap" => 90d,
            "limited_days" => 80d,
            "one_nominated_test" => 70d,
            "in_season_banned" => 60d,
            _ => 100d,
        };
        var aero = aeroTesting switch
        {
            "fixed_limit" => 0.9d,
            "sliding_scale_small_step" => 0.9d,
            "sliding_scale_larger_step" => 0.85d,
            _ => 1d,
        };
        return CarEstimates.Quantize(testing * aero);
    }

    /// <summary>Understanding after growth, never above the cap and never below what it already was.</summary>
    public static double GrowUnderstanding(double current, double points, double cap) =>
        CarEstimates.Quantize(current >= cap ? current : Math.Min(cap, current + Math.Max(0d, points)));

    public static double ExperienceOf(GameDate birth, GameDate today)
    {
        var age = today.Year - birth.Year;
        return Math.Clamp((age - 25d) / DevelopmentEstimates.ExperienceAgeSpan, 0d, 1d);
    }

    public static double AdaptationOf(GameDate start, GameDate today)
    {
        var days = Math.Max(0, start.DaysUntil(today));
        return Math.Clamp(days / (365d * DevelopmentEstimates.AdaptationYears), 0d, 1d);
    }
}
