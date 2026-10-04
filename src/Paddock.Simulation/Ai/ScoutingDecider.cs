using System.Globalization;

namespace Paddock.Simulation.Ai;

/// <summary>What the scouting decider reads: when the scouts were last pointed at the talent pool and how big the pool is.</summary>
public sealed record ScoutingInput(DateOnly Today, int ScoutSeason, int PoolSize);

/// <summary>
/// Whether to point the scouts at the whole talent pool this season (T40). Knowledge of young drivers has to be built before it can be
/// used in the market, so a planner does it every season and a principal who counts every dollar does not. Worth is the archetype's
/// weight on the future and on potential; cost is its thrift.
/// </summary>
public static class ScoutingDecider
{
    public const string Kind = "scouting.focus";

    /// <summary>ESTIMATE: what pointing the scouts costs in utility, before the archetype's thrift.</summary>
    public const double FocusCost = 0.10;

    /// <summary>ESTIMATE: what knowing the pool is worth in utility, before the archetype's weights.</summary>
    public const double FocusValue = 0.5;

    public static bool Review(DecisionContext context, ScoutingInput input)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(input);
        if (input.PoolSize <= 0 || input.ScoutSeason >= context.Season)
        {
            return false;
        }

        var profile = context.Profile;
        var options = new List<OptionDraft>
        {
            new(
                AiTextKeys.OptionScoutPool,
                [
                    new FactorDraft(AiTextKeys.FactorScoutingValue, profile.Future * profile.Upside * FocusValue),
                    new FactorDraft(AiTextKeys.FactorScoutingCost, -profile.Thrift * FocusCost),
                ]),
            new(AiTextKeys.OptionNoScouting, [new FactorDraft(AiTextKeys.FactorScoutingCost, 0.0)]),
        };
        var note = new TraceNote(
            Kind,
            context.Season.ToString(CultureInfo.InvariantCulture),
            "season-" + context.Season.ToString(CultureInfo.InvariantCulture),
            "Weighs pointing the scouts at the pool of " + input.PoolSize.ToString(CultureInfo.InvariantCulture) + " young drivers.",
            null,
            IsKeyDecision: false);
        var chosen = UtilityChooser.Choose(context, DecisionFacet.Market, note, options, _ => AiTextKeys.ReasonScouting);
        return chosen.Id == AiTextKeys.OptionScoutPool;
    }
}
