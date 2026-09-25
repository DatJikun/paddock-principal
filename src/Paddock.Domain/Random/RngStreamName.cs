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
    ];
}
