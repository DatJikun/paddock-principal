namespace Paddock.Simulation.Racing.Qualifying;

/// <summary>
/// Every tunable number of the qualifying simulation, in one place. Values marked ESTIMATE are guesses that have
/// not been calibrated against real data; changing one changes grids, so it is a balance change and not a refactor.
/// Values marked DATA come from the notes in data/authored/regulations/f1_timeline.json.
/// </summary>
public static class QualifyingConstants
{
    /// <summary>
    /// ESTIMATE: total track evolution over a whole qualifying weekend, in seconds. The track improves linearly
    /// with weekend progress (session by session, lap by lap), so later laps are a little quicker. This single
    /// term is also how "a driver improves over a session" is modelled: there is no separate learning curve.
    /// </summary>
    public const double TrackEvolutionSeconds = 0.5;

    /// <summary>
    /// ESTIMATE: scale (seconds) of the lap-to-lap time loss for a driver with consistency 0. The loss on one lap
    /// is scale * |standard normal|, so it is never negative: a lap can be spoiled but not magically beat the
    /// ideal lap.
    /// </summary>
    public const double NoiseScaleInconsistent = 0.50;

    /// <summary>ESTIMATE: the same loss scale for a driver with consistency 1.</summary>
    public const double NoiseScaleConsistent = 0.05;

    /// <summary>DATA: the cutoff is 107 percent of the reference time (pole, or the fastest Q1 time).</summary>
    public const double CutoffFactor = 1.07;

    /// <summary>Lap times are rounded to whole milliseconds, like the timing in the sources. Ties are therefore possible.</summary>
    public const int TimeDecimals = 3;

    /// <summary>DATA: twelve laps per driver per session in the pre-1996 and 1996-2002 formats.</summary>
    public const int TwelveLapSession = 12;

    /// <summary>ESTIMATE: flying laps a driver can fit into Q1, Q2 and Q3 (the real segments are time-limited).</summary>
    public static readonly int[] KnockoutLapsPerSegment = [4, 4, 3];

    /// <summary>ESTIMATE: flying laps per driver in the three sprint-shootout segments (shorter than the grand prix ones).</summary>
    public static readonly int[] SprintShootoutLapsPerSegment = [3, 3, 2];

    /// <summary>
    /// Cars in the last knockout segment. DATA for fields of 20 (cuts of 5, so 10 remain) and 22 (cuts of 6, so
    /// 10 remain). ESTIMATE for every other field size: the same Q3 size is assumed.
    /// </summary>
    public const int KnockoutFinalSegmentSize = 10;

    /// <summary>ESTIMATE: laps per driver in a pre-qualifying session.</summary>
    public const int PreQualifyingLaps = 8;

    /// <summary>DATA: the four fastest cars of pre-qualifying advanced (1988-1992).</summary>
    public const int PreQualifyingAdvancing = 4;

    /// <summary>DATA: the entry cap of 26 cars (1988-1992 and from 1994, confidence medium for the later span).</summary>
    public const int GridCapTwentySix = 26;

    /// <summary>DATA: the 1993 cap before it was raised to 26 mid-season.</summary>
    public const int GridCapTwentyFive = 25;
}
