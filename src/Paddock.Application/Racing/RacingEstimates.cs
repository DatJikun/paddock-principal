namespace Paddock.Application.Racing;

/// <summary>
/// Guessed numbers used when a career race is assembled from the world. None of these is calibrated.
/// The race streams themselves stay the weekend's existing ones (INV-004).
/// </summary>
public static class RacingEstimates
{
    /// <summary>ESTIMATE: there is no aggression attribute yet, so every driver races at the middle of 1..20.</summary>
    public const int DefaultAggression = 10;

    /// <summary>ESTIMATE: there are no pit-crew attributes yet, so every team gets the middle of the 0..100 crew scale.</summary>
    public const double NeutralPitCrewQuality = 50;

    /// <summary>ESTIMATE: there is no race-engineer attribute yet, so every strategist is the middle of 0..100.</summary>
    public const int NeutralStrategistSkill = 50;

    /// <summary>ESTIMATE: there is no weather department yet, so every forecast is slightly short of perfect (0..1).</summary>
    public const double NeutralForecastQuality = 0.9;
}
