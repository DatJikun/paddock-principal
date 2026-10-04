namespace Paddock.Domain.Random;

public static class RngStreamName
{
    public const string Weather = "Weather";
    public const string LapNoise = "LapNoise";
    public const string Incidents = "Incidents";
    public const string Failures = "Failures";
    public const string PitStops = "PitStops";
    public const string Market = "Market";
    public const string AiDecisions = "AiDecisions";
    public const string People = "People";
    public const string History = "History";
    public const string LifeEvents = "LifeEvents";
    public const string Regulations = "Regulations";

    /// <summary>Observation noise of scouting (T40). Kept apart from <see cref="People"/> so extra scouting never moves development draws.</summary>
    public const string Scouting = "Scouting";

    /// <summary>
    /// Concept ceilings (T41). Kept apart from <see cref="People"/> so approving a car never moves a person,
    /// and apart from <see cref="Scouting"/> so a ceiling draw never moves an observation.
    /// </summary>
    public const string Development = "Development";

    public static readonly IReadOnlyList<string> All =
    [
        Weather,
        LapNoise,
        Incidents,
        Failures,
        PitStops,
        Market,
        AiDecisions,
        People,
        History,
        LifeEvents,
        Regulations,
        Scouting,
        Development,
    ];
}
