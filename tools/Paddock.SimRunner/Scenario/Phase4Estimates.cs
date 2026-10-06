using Paddock.Domain.Time;

namespace Paddock.SimRunner.Scenario;

/// <summary>
/// Thresholds of the phase-4 gate (#113). Every number is an ESTIMATE: uncalibrated, not a measured fact.
/// The robustness calendar is the owner's scope extension (1 March 1957). Counterfactual pairs stay in 1955.
/// </summary>
public static class Phase4Estimates
{
    public const string ScenarioName = "phase4-1955";

    public const string DefaultTeam = "ferrari";

    public const ulong StorySeed = 7;

    /// <summary>ESTIMATE: monkey sample size in the issue (50 seeds, any team).</summary>
    public const int MonkeySeeds = 50;

    /// <summary>ESTIMATE: how many monkey seeds a CI test runs so the suite stays short.</summary>
    public const int TestMonkeySeeds = 2;

    /// <summary>ESTIMATE: wall-clock seconds per season before the checklist warns.</summary>
    public const double SeasonWallWarnSeconds = 120;

    /// <summary>Design target from TECH §8: save after a long career below ~50 MB.</summary>
    public const long SaveSizeWarnBytes = 50L * 1024 * 1024;

    public static GameDate Start { get; } = GameDate.SeasonStart(1955);

    public static GameDate StoryUntil { get; } = new(1955, 1, 2);

    public static GameDate RobustUntil { get; } = new(1957, 3, 1);

    public static GameDate CounterfactualUntil { get; } = GameDate.SeasonEnd(1955);

    /// <summary>Documented command that rewrites <c>phase4-1955.story.hash</c>.</summary>
    public const string UpdateHashCommand =
        "PADDOCK_UPDATE_FIXTURES=1 dotnet test --filter Phase4ScenarioTests.TheScriptedStoryHashIsStable";
}
