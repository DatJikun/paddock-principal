namespace Paddock.Simulation.Cars;

/// <summary>
/// How widely car performance spreads compared with driver skill, per era (PP-050).
/// The source is the ratings model's car-effect variance over its driver-skill variance
/// (<c>tools/Paddock.DataPipeline/Ratings/RatingsModel</c>, parameters <c>c</c> and <c>s</c>).
/// The fitted variances are not in the repo, so these ratios are ESTIMATES until the race engine is calibrated (#122).
/// A value above 1 means cars spread more than drivers.
/// </summary>
public static class DriverCarBalance
{
    public const string Source =
        "RatingsModel car-effect variance / driver-skill variance; ESTIMATE until a ratings run and #122.";

    private static readonly (int FromSeason, double Ratio)[] Ratios =
    [
        (1950, 1.15),
        (1968, 1.05),
        (1983, 0.95),
        (1994, 0.85),
        (2014, 0.90),
        (2022, 1.00),
    ];

    public static double CarToDriverSpread(int season)
    {
        var ratio = Ratios[0].Ratio;
        foreach (var row in Ratios)
        {
            if (row.FromSeason <= season)
            {
                ratio = row.Ratio;
            }
        }

        return ratio;
    }
}
