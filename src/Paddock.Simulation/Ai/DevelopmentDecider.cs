using System.Globalization;

namespace Paddock.Simulation.Ai;

/// <summary>A concept project of the team that is running or waiting for its deployment moment (T42). Gains are the team's own band, a rating-point range.</summary>
/// <param name="ProjectId">The project.</param>
/// <param name="Ready">True when the concept is finished and waits; false while it is still running.</param>
/// <param name="CurrentTiming">The timing now set: <c>WhenReady</c>, <c>AfterRaces</c> or <c>NextSeason</c>.</param>
/// <param name="ExpectedGainMid">The middle of the band of rating points the concept is expected to bring.</param>
/// <param name="ProductionDays">Days committing a ready concept to production would take, as the engineers show them (0 when not shown).</param>
public sealed record ConceptCase(string ProjectId, bool Ready, string CurrentTiming, double ExpectedGainMid, int ProductionDays = 0, double? StartDelta = null);

/// <summary>One character the next concept could have, as the engineers' ranges give it: the middle and half-width of its ceiling and the share of it the car starts at.</summary>
public sealed record CharacterCase(int PhilosophyMilli, double CeilingMid, double CeilingHalf, double StartShare);

/// <summary>The choice of the next concept's character (PP-066): what is chosen now, the ceiling now (middle of the band) and the options.</summary>
public sealed record NextCharacterView(int PhilosophyMilli, int AeroMilli, double CeilingNowMid, IReadOnlyList<CharacterCase> Options);

/// <summary>The character of the next concept the principal picks; null keeps the one in force.</summary>
public sealed record CharacterDecision(int PhilosophyMilli, int AeroMilli);

/// <summary>What the development decider reads: the team's own plan and car as its engineers show them, its results, and the rules it has heard are coming.</summary>
/// <param name="Today">The day.</param>
/// <param name="Plan">The split and priorities now in force.</param>
/// <param name="AeroLevel">The middle of the band of the car's downforce, 0 to 100.</param>
/// <param name="ChassisLevel">Mechanical grip.</param>
/// <param name="ReliabilityLevel">Reliability.</param>
/// <param name="TyresLevel">Tyre handling (braking).</param>
/// <param name="LastPosition">Constructors' position last season, or null.</param>
/// <param name="PreviousPosition">The season before it, or null.</param>
/// <param name="CurrentPosition">The position in this season's standings so far, or null before a race.</param>
/// <param name="Teams">How many teams the standings have.</param>
/// <param name="NextRuleChange">The share (0 to 1) of the technical rules announced to change next season, or 0 when none is known.</param>
/// <param name="SacrificedSeason">The season this principal already wrote off (0 when none).</param>
/// <param name="CashTight">True when the team's headroom is gone.</param>
/// <param name="Concepts">The concept projects.</param>
public sealed record DevelopmentInput(
    DateOnly Today,
    DevelopmentPlanView Plan,
    double AeroLevel,
    double ChassisLevel,
    double ReliabilityLevel,
    double TyresLevel,
    int? LastPosition,
    int? PreviousPosition,
    int? CurrentPosition,
    int Teams,
    double NextRuleChange,
    int SacrificedSeason,
    bool CashTight,
    IReadOnlyList<ConceptCase> Concepts,
    NextCharacterView? Next = null);

/// <summary>The split of resources and the priority of each area (T42): the only things the principal sets.</summary>
public sealed record DevelopmentPlanView(int Current, int Account, int NextYear, int Aero, int Chassis, int Reliability, int Tyres);

/// <summary>When one concept is deployed.</summary>
public sealed record TimingDecision(string ProjectId, string Timing, int Races, bool Commit = false);

/// <summary>The outcome of a development review. <see cref="Plan"/> is null when the plan in force stays.</summary>
public sealed record DevelopmentDecision(DevelopmentPlanView? Plan, bool Sacrifice, IReadOnlyList<TimingDecision> Timings, CharacterDecision? Character = null);

/// <summary>
/// The development split and the deployment of concepts (DESIGN section 5.3 and 8, T42). The principal chooses among a fixed menu of
/// splits by utility: results of the present car against the next car and the development account, weighed by what the standings say
/// about the present season, by the rules announced for the next, and by the archetype. It writes a season off (the
/// "sacrifice" splits) when its position is settled or a big rule change is coming and it is a planner (<see cref="ArchetypeProfile.SacrificeBias"/>
/// and <see cref="AiEstimates.PlanningLevel"/>). It never sets a project: the engineers do (PP-043).
/// </summary>
public static class DevelopmentDecider
{
    public const string SplitKind = "development.split";

    public const string TimingKind = "development.timing";

    /// <summary>ESTIMATE: the menu of splits (current car, account, next year's car); each sums to 100.</summary>
    public static readonly IReadOnlyList<(string Name, int Current, int Account, int NextYear)> Menu =
    [
        ("balanced", 80, 0, 20),
        ("push_now", 95, 0, 5),
        ("invest", 60, 0, 40),
        ("build_next", 50, 0, 50),
        ("sacrifice", 10, 0, 90),
        ("sacrifice_hard", 0, 0, 100),
    ];

    public static DevelopmentDecision Review(DecisionContext context, DevelopmentInput input)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(input);
        var (plan, sacrifice) = Split(context, input);
        return new DevelopmentDecision(plan, sacrifice, Timings(context, input, sacrifice), Character(context, input));
    }

    private static (DevelopmentPlanView? Plan, bool Sacrifice) Split(DecisionContext context, DevelopmentInput input)
    {
        var profile = context.Profile;
        var seasonLeft = SeasonLeft(input.Today);
        var leverage = Leverage(input);
        var stable = Stable(input);
        var planner = context.Level >= AiEstimates.PlanningLevel;
        var nextChange = context.Skills.Politics >= AiEstimates.PlanningLevel ? Math.Clamp(input.NextRuleChange, 0.0, 1.0) : 0.0;
        var pressing = stable || nextChange >= 0.2;
        var options = new List<OptionDraft>();
        foreach (var (name, current, account, next) in Menu)
        {
            var sacrificing = current <= 10;
            if (sacrificing && (!planner || profile.SacrificeBias <= 0.0))
            {
                continue;
            }

            var factors = new List<FactorDraft>
            {
                new(AiTextKeys.FactorResultsNow, profile.Now * leverage * seasonLeft * current / 100.0),
                new(
                    AiTextKeys.FactorNextYear,
                    profile.Future * AiEstimates.NextYearValue * next / 100.0 * (0.5 + (1.5 * nextChange)) * (planner ? 1.0 : 0.5) * (stable ? 1.25 : 1.0)),
                new(
                    AiTextKeys.FactorAccount,
                    profile.Future * 0.5 * account / 100.0 * Math.Max(0.0, 1.0 - (AiEstimates.AccountRuleLoss * nextChange))),
            };
            if (sacrificing && pressing)
            {
                factors.Add(new FactorDraft(AiTextKeys.FactorSacrifice, profile.SacrificeBias * 0.3));
            }

            if (input.SacrificedSeason == context.Season && sacrificing)
            {
                factors.Add(new FactorDraft(AiTextKeys.FactorInertia, AiEstimates.PlanInertia * profile.Stability));
            }
            else if (current == input.Plan.Current && account == input.Plan.Account && next == input.Plan.NextYear)
            {
                factors.Add(new FactorDraft(AiTextKeys.FactorInertia, AiEstimates.PlanInertia * profile.Stability));
            }

            options.Add(new OptionDraft("split/" + name, factors));
        }

        var note = new TraceNote(
            SplitKind,
            context.Season.ToString(CultureInfo.InvariantCulture),
            "season-" + context.Season.ToString(CultureInfo.InvariantCulture),
            "Weighs the present car against the next one with " + (input.CurrentPosition?.ToString(CultureInfo.InvariantCulture) ?? "no") + " standing so far.",
            null,
            IsKeyDecision: true);
        var chosen = UtilityChooser.Choose(context, DecisionFacet.Development, note, options, id => id.StartsWith("split/sacrifice", StringComparison.Ordinal) ? AiTextKeys.ReasonSacrifice : AiTextKeys.ReasonSplit);
        var picked = Menu.First(item => "split/" + item.Name == chosen.Id);
        var sacrifice = picked.Current <= 10;
        var (aero, chassis, reliability, tyres) = Priorities(context, input);
        var plan = new DevelopmentPlanView(picked.Current, picked.Account, picked.NextYear, aero, chassis, reliability, tyres);
        return (plan == input.Plan ? null : plan, sacrifice);
    }

    public const string CharacterKind = "development.character";

    /// <summary>
    /// Evolution or revolution for the next concept, from the same ranges the player reads: the gain of the ceiling over today's, the
    /// width of that range as risk, and how far below today's car the start would be. Null keeps the character in force.
    /// </summary>
    private static string CharacterName(int philosophyMilli) => philosophyMilli < 0 ? "evolution" : "revolution";

    private static CharacterDecision? Character(DecisionContext context, DevelopmentInput input)
    {
        if (input.Next is not { } next || next.Options.Count == 0)
        {
            return null;
        }

        var profile = context.Profile;
        var options = new List<OptionDraft>();
        foreach (var option in next.Options)
        {
            var gain = (option.CeilingMid - next.CeilingNowMid) / 100.0;
            var factors = new List<FactorDraft>
            {
                new(AiTextKeys.FactorNextYear, profile.Future * gain * 4.0),
                new(AiTextKeys.FactorDisruption, (-profile.RiskAversion * (option.CeilingHalf / 100.0) * 3.0) - (profile.Now * (1.0 - option.StartShare) * 0.5)),
            };
            if (option.PhilosophyMilli == next.PhilosophyMilli)
            {
                factors.Add(new FactorDraft(AiTextKeys.FactorInertia, AiEstimates.PlanInertia * profile.Stability));
            }

            options.Add(new OptionDraft("character/" + CharacterName(option.PhilosophyMilli), factors));
        }

        var note = new TraceNote(
            CharacterKind,
            context.Season.ToString(CultureInfo.InvariantCulture),
            "character-" + context.Season.ToString(CultureInfo.InvariantCulture),
            "Chooses evolution or revolution for the next concept.",
            null,
            IsKeyDecision: false);
        var chosen = UtilityChooser.Choose(context, DecisionFacet.Development, note, options, _ => AiTextKeys.ReasonSplit);
        var picked = next.Options.First(item => "character/" + CharacterName(item.PhilosophyMilli) == chosen.Id);
        return picked.PhilosophyMilli == next.PhilosophyMilli ? null : new CharacterDecision(picked.PhilosophyMilli, next.AeroMilli);
    }

    /// <summary>Priorities by the weakest areas first. A level-1 principal does not read the car and keeps the middle.</summary>
    private static (int Aero, int Chassis, int Reliability, int Tyres) Priorities(DecisionContext context, DevelopmentInput input)
    {
        const int middle = 5;
        if (context.Skills.Business < AiEstimates.PlanningLevel)
        {
            return (middle, middle, middle, middle);
        }

        var levels = new[] { input.AeroLevel, input.ChassisLevel, input.ReliabilityLevel, input.TyresLevel };
        var ranks = new[] { 8, 6, 5, 3 };
        var order = Enumerable.Range(0, 4).OrderBy(i => levels[i]).ThenBy(i => i).ToArray();
        var result = new int[4];
        for (var rank = 0; rank < 4; rank++)
        {
            result[order[rank]] = ranks[rank];
        }

        return (result[0], result[1], result[2], result[3]);
    }

    private static double Leverage(DevelopmentInput input)
    {
        var position = input.CurrentPosition ?? input.LastPosition;
        if (position is not int known)
        {
            return 0.7;
        }

        return known <= AiEstimates.ContentionPosition ? 1.0 : known <= AiEstimates.ContentionPosition + 3 ? 0.7 : 0.5;
    }

    /// <summary>A settled position: the same place two seasons running, or last season's place held so far, and not in the fight.</summary>
    private static bool Stable(DevelopmentInput input)
    {
        if (input.LastPosition is not int last)
        {
            return false;
        }

        var steady = input.PreviousPosition is int previous && Math.Abs(previous - last) <= 1;
        var holding = input.CurrentPosition is int now && Math.Abs(now - last) <= 1;
        return (steady || holding) && last > AiEstimates.ContentionPosition;
    }

    private static double SeasonLeft(DateOnly today)
    {
        var days = DateTime.IsLeapYear(today.Year) ? 366 : 365;
        return (days - today.DayOfYear + 1) / (double)days;
    }

    private static List<TimingDecision> Timings(DecisionContext context, DevelopmentInput input, bool sacrifice)
    {
        var result = new List<TimingDecision>();
        var seasonLeft = SeasonLeft(input.Today);
        var profile = context.Profile;
        foreach (var concept in input.Concepts.OrderBy(item => item.ProjectId, StringComparer.Ordinal))
        {
            var gain = Math.Max(0.0, concept.ExpectedGainMid) / 100.0;
            var nowGain = concept.StartDelta is { } delta ? delta / 100.0 : gain;
            var writtenOff = sacrifice || input.SacrificedSeason == context.Season;
            var options = new List<OptionDraft>();
            if (concept.Ready)
            {
                // Committing builds the car now (T42c): it goes live only after the production days, so less of the season is left to gain.
                var left = Math.Max(0.0, seasonLeft - (concept.ProductionDays / 365.0));
                options.Add(new OptionDraft("timing/commit_now", Timing(profile, nowGain, gain, left, nowShare: 1.0, carryShare: 0.5, disruption: 1.0, writtenOff)));
            }
            else
            {
                options.Add(new OptionDraft("timing/when_ready", Timing(profile, nowGain, gain, seasonLeft, nowShare: 1.0, carryShare: 0.5, disruption: 1.0, writtenOff)));
            }

            options.Add(new OptionDraft("timing/after_races", Timing(profile, nowGain, gain, seasonLeft, nowShare: 0.7, carryShare: 0.6, disruption: 0.5, writtenOff)));
            options.Add(new OptionDraft("timing/next_season", Timing(profile, nowGain, gain, seasonLeft, nowShare: 0.0, carryShare: 1.0, disruption: 0.0, writtenOff)));
            var note = new TraceNote(
                TimingKind,
                concept.ProjectId,
                concept.ProjectId,
                "Chooses when to deploy " + concept.ProjectId + " (now " + concept.CurrentTiming + ").",
                null,
                IsKeyDecision: false);
            var chosen = UtilityChooser.Choose(context, DecisionFacet.Development, note, options, _ => AiTextKeys.ReasonTiming);
            if (chosen.Id == "timing/commit_now")
            {
                result.Add(new TimingDecision(concept.ProjectId, "WhenReady", 0, Commit: true));
                continue;
            }

            var (timing, races) = chosen.Id switch
            {
                "timing/next_season" => ("NextSeason", 0),
                "timing/after_races" => ("AfterRaces", 2),
                _ => ("WhenReady", 0),
            };
            if (timing != concept.CurrentTiming)
            {
                result.Add(new TimingDecision(concept.ProjectId, timing, races));
            }
        }

        return result;
    }

    private static List<FactorDraft> Timing(ArchetypeProfile profile, double nowGain, double gain, double seasonLeft, double nowShare, double carryShare, double disruption, bool writtenOff) =>
    [
        new(AiTextKeys.FactorResultsNow, writtenOff ? 0.0 : profile.Now * nowGain * seasonLeft * nowShare),
        new(AiTextKeys.FactorNextYear, profile.Future * gain * carryShare),
        new(AiTextKeys.FactorDisruption, -profile.RiskAversion * 0.3 * seasonLeft * disruption),
    ];
}
