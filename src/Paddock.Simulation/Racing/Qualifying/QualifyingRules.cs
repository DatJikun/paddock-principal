using System.Collections.Immutable;

namespace Paddock.Simulation.Racing.Qualifying;

/// <summary>How the grid is worked out from the sessions.</summary>
public enum QualifyingFormat
{
    /// <summary>One timed session. The best lap of the session counts (1950s, 1996-2002 hour).</summary>
    SingleSession,

    /// <summary>Two sessions (Friday and Saturday). The best lap of both counts.</summary>
    TwoDay,

    /// <summary>
    /// One-lap runs, one car at a time. Sessions flagged as not counting only set the running order of the next one
    /// (2003-2004). The best lap of the counting sessions decides the grid.
    /// </summary>
    OneLapShootout,

    /// <summary>The times of all counting sessions are added up (the opening 2005 format).</summary>
    Aggregate,

    /// <summary>
    /// Q1/Q2/Q3: after each segment but the last the slowest cars are out and take the places behind the
    /// survivors (2006+).
    /// </summary>
    Knockout,
}

/// <summary>The 107 percent cutoff variants of the regulation catalog (<c>qualifying_time_cutoff</c>).</summary>
public enum QualifyingCutoff
{
    None,

    /// <summary>Within 107 percent of pole (1996-2002).</summary>
    WithinOfPole,

    /// <summary>Within 107 percent of the fastest Q1 time (2011-2017).</summary>
    WithinOfFastestQ1,

    /// <summary>Like <see cref="WithinOfFastestQ1"/>, but a session declared wet is exempt (2018+).</summary>
    WithinOfFastestQ1UnlessWet,
}

/// <summary>In which order the cars go out in a session.</summary>
public enum QualifyingRunningOrder
{
    /// <summary>The order the entrants were handed over in (the caller decides, for example championship order).</summary>
    EntryOrder,

    /// <summary>The previous session's ranking, slowest first (the fastest car runs last).</summary>
    SlowestFirst,
}

/// <summary>One timed session (or knockout segment).</summary>
/// <param name="Id">Key of the session in the result (for example <c>Q1</c>).</param>
/// <param name="LapsPerDriver">Hard cap on the laps each car may run. The simulated total is bounded by it.</param>
/// <param name="CountsForGrid">False for a session that only sets the running order of the next one.</param>
/// <param name="RunningOrder">Order of the cars within one lap of the session.</param>
public sealed record QualifyingSessionSpec(
    string Id,
    int LapsPerDriver,
    bool CountsForGrid = true,
    QualifyingRunningOrder RunningOrder = QualifyingRunningOrder.EntryOrder);

/// <summary>Pre-qualifying: a separate early session, run only by the cars flagged for it.</summary>
/// <param name="LapsPerDriver">Hard cap on laps per car.</param>
/// <param name="AdvancingCount">How many of the fastest pre-qualifiers join the main session. The rest do not qualify.</param>
public sealed record PreQualifyingOptions(int LapsPerDriver, int AdvancingCount);

/// <summary>
/// The qualifying rules of one era: format, sessions and the rules that decide who may start. Build one with the
/// factories (<see cref="SingleSession"/>, <see cref="Knockout"/>, ...), with <see cref="FromCatalog"/> from
/// regulation-catalog values, or by hand. The simulator validates it.
/// </summary>
public sealed record QualifyingRules
{
    public required QualifyingFormat Format { get; init; }

    public required IReadOnlyList<QualifyingSessionSpec> Sessions { get; init; }

    /// <summary>Knockout only: how many cars are eliminated after segment 1, 2, ... (one fewer than segments).</summary>
    public IReadOnlyList<int> KnockoutEliminations { get; init; } = [];

    public QualifyingCutoff Cutoff { get; init; } = QualifyingCutoff.None;

    /// <summary>
    /// How many cars may start. Null means no cap. This is a constructor-style parameter on purpose: the
    /// regulation catalog's <c>maximum_grid</c> is an entry cap that does not say how many cars start.
    /// </summary>
    public int? MaxGridSize { get; init; }

    public PreQualifyingOptions? PreQualifying { get; init; }

    /// <summary>The most laps any one car can run in the whole qualifying (pre-qualifying included).</summary>
    public int MaxLapsPerDriver => Sessions.Sum(s => s.LapsPerDriver) + (PreQualifying?.LapsPerDriver ?? 0);

    // ---- Factories ------------------------------------------------------------------------------------------

    /// <summary>One timed session, best lap counts. The hour of 1996-2002 is <c>SingleSession(12)</c>.</summary>
    public static QualifyingRules SingleSession(int lapsPerDriver = QualifyingConstants.TwelveLapSession) => new()
    {
        Format = QualifyingFormat.SingleSession,
        Sessions = [new QualifyingSessionSpec("Session", lapsPerDriver)],
    };

    /// <summary>Two-day format: the best lap of both sessions counts (<c>two_session_best_time</c>).</summary>
    public static QualifyingRules TwoDay(int lapsPerSession = QualifyingConstants.TwelveLapSession) => new()
    {
        Format = QualifyingFormat.TwoDay,
        Sessions = [new QualifyingSessionSpec("Day1", lapsPerSession), new QualifyingSessionSpec("Day2", lapsPerSession)],
    };

    /// <summary>
    /// One flying lap per car per session, one car at a time. The first run only sets the running order
    /// (fastest car goes last) of the counting second run (2003 and 2004).
    /// </summary>
    public static QualifyingRules OneLapShootout() => new()
    {
        Format = QualifyingFormat.OneLapShootout,
        Sessions =
        [
            new QualifyingSessionSpec("Run1", 1, CountsForGrid: false),
            new QualifyingSessionSpec("Run2", 1, CountsForGrid: true, QualifyingRunningOrder.SlowestFirst),
        ],
    };

    /// <summary>Two one-lap runs whose times are added up (the opening 2005 format).</summary>
    public static QualifyingRules Aggregate() => new()
    {
        Format = QualifyingFormat.Aggregate,
        Sessions =
        [
            new QualifyingSessionSpec("Run1", 1),
            new QualifyingSessionSpec("Run2", 1, CountsForGrid: true, QualifyingRunningOrder.SlowestFirst),
        ],
    };

    /// <summary>Q1/Q2/Q3 knockout with the cuts worked out from the field size (see <see cref="KnockoutEliminationsFor"/>).</summary>
    public static QualifyingRules Knockout(int fieldSize) => KnockoutWith(["Q1", "Q2", "Q3"], QualifyingConstants.KnockoutLapsPerSegment, fieldSize);

    /// <summary>
    /// A sprint-weekend shootout (<c>friday_sprint_qualifying</c>, <c>standalone_sprint_shootout</c>): the same
    /// knockout in shorter segments. Its result is the sprint grid.
    /// </summary>
    public static QualifyingRules SprintShootout(int fieldSize) =>
        KnockoutWith(["SQ1", "SQ2", "SQ3"], QualifyingConstants.SprintShootoutLapsPerSegment, fieldSize);

    private static QualifyingRules KnockoutWith(string[] ids, int[] laps, int fieldSize) => new()
    {
        Format = QualifyingFormat.Knockout,
        Sessions = ids.Select((id, i) => new QualifyingSessionSpec(id, laps[i])).ToImmutableArray(),
        KnockoutEliminations = KnockoutEliminationsFor(fieldSize),
    };

    /// <summary>
    /// Cars eliminated after Q1 and after Q2 for a field. DATA: 20 cars gives 5 and 5, 22 cars gives 6 and 6
    /// (Q3 keeps 10). ESTIMATE for other sizes: Q3 keeps <see cref="QualifyingConstants.KnockoutFinalSegmentSize"/>
    /// (or the whole field when smaller) and the rest is cut in two halves, Q1 taking the odd car.
    /// </summary>
    public static IReadOnlyList<int> KnockoutEliminationsFor(int fieldSize)
    {
        int cuts = Math.Max(0, fieldSize - QualifyingConstants.KnockoutFinalSegmentSize);
        int second = cuts / 2;
        return [cuts - second, second];
    }

    // ---- Catalog --------------------------------------------------------------------------------------------

    /// <summary>
    /// Builds the rules from the value ids of the regulation catalog (data/authored/regulations): the
    /// <c>qualifying_format</c>, <c>qualifying_time_cutoff</c>, <c>pre_qualifying_session</c> and
    /// <c>maximum_grid</c> dimensions. An unknown id throws, so a new catalog value cannot be silently ignored.
    /// </summary>
    /// <param name="qualifyingFormat">Value of <c>qualifying_format</c>.</param>
    /// <param name="cutoff">Value of <c>qualifying_time_cutoff</c>.</param>
    /// <param name="preQualifying">Value of <c>pre_qualifying_session</c>.</param>
    /// <param name="maximumGrid">Value of <c>maximum_grid</c>.</param>
    /// <param name="fieldSize">Number of cars of the era's field; sets the knockout cuts.</param>
    /// <param name="lateInSeason">Only for <c>twenty_five_then_twenty_six</c>: true once the cap has been raised.</param>
    public static QualifyingRules FromCatalog(
        string qualifyingFormat,
        string cutoff,
        string preQualifying,
        string maximumGrid,
        int fieldSize,
        bool lateInSeason = false)
    {
        QualifyingRules baseRules = qualifyingFormat switch
        {
            // DATA: "two one-hour sessions, twelve laps each, with the best lap counting".
            "two_session_best_time" => TwoDay(),
            "saturday_twelve_lap" => SingleSession(QualifyingConstants.TwelveLapSession),
            "one_lap_friday_and_saturday" or "one_lap_saturday_pair" => OneLapShootout(),
            // The catalog has one value for 2005, which opened with the aggregate and later reverted to one lap.
            "one_lap_2005_mixed" => Aggregate(),
            "knockout_q1_q2_q3" => Knockout(fieldSize),
            _ => throw new ArgumentException($"Unknown qualifying_format '{qualifyingFormat}'.", nameof(qualifyingFormat)),
        };

        return baseRules with
        {
            Cutoff = cutoff switch
            {
                "none" => QualifyingCutoff.None,
                "within_107_of_pole" => QualifyingCutoff.WithinOfPole,
                "within_107_of_fastest_q1" => QualifyingCutoff.WithinOfFastestQ1,
                "within_107_of_fastest_q1_unless_wet" => QualifyingCutoff.WithinOfFastestQ1UnlessWet,
                _ => throw new ArgumentException($"Unknown qualifying_time_cutoff '{cutoff}'.", nameof(cutoff)),
            },
            PreQualifying = preQualifying switch
            {
                "no_regular_session" or "not_held" => null,
                "four_fastest_advance" => new PreQualifyingOptions(
                    QualifyingConstants.PreQualifyingLaps, QualifyingConstants.PreQualifyingAdvancing),
                _ => throw new ArgumentException($"Unknown pre_qualifying_session '{preQualifying}'.", nameof(preQualifying)),
            },
            MaxGridSize = maximumGrid switch
            {
                "no_single_cap_confirmed" => null,
                "twenty_six" => QualifyingConstants.GridCapTwentySix,
                "twenty_five_then_twenty_six" => lateInSeason
                    ? QualifyingConstants.GridCapTwentySix
                    : QualifyingConstants.GridCapTwentyFive,
                _ => throw new ArgumentException($"Unknown maximum_grid '{maximumGrid}'.", nameof(maximumGrid)),
            },
        };
    }

    /// <summary>
    /// The sprint shootout the catalog's <c>sprint_format</c> value asks for, or null when that value has none.
    /// <c>sprint_qualifying_points_3_2_1</c> and <c>sprint_points_top_8_sets_grid</c> (2021, 2022) used the
    /// ordinary Friday qualifying to set the sprint grid, so they need no extra format.
    /// </summary>
    public static QualifyingRules? SprintShootoutFromCatalog(string sprintFormat, int fieldSize) => sprintFormat switch
    {
        "friday_sprint_qualifying" or "standalone_sprint_shootout" => SprintShootout(fieldSize),
        "none" or "sprint_qualifying_points_3_2_1" or "sprint_points_top_8_sets_grid" => null,
        _ => throw new ArgumentException($"Unknown sprint_format '{sprintFormat}'.", nameof(sprintFormat)),
    };

    /// <summary>Throws <see cref="ArgumentException"/> when the rules do not fit their format.</summary>
    public void Validate()
    {
        if (Sessions.Count == 0)
        {
            throw new ArgumentException("Qualifying needs at least one session.");
        }

        if (Sessions.Any(s => s.LapsPerDriver < 1))
        {
            throw new ArgumentException("Every session needs at least one lap per driver.");
        }

        if (Sessions.Select(s => s.Id).Distinct().Count() != Sessions.Count)
        {
            throw new ArgumentException("Session ids must be unique.");
        }

        if (MaxGridSize is < 1)
        {
            throw new ArgumentException("MaxGridSize must be at least 1 when set.");
        }

        if (PreQualifying is { LapsPerDriver: < 1 } or { AdvancingCount: < 1 })
        {
            throw new ArgumentException("Pre-qualifying needs at least one lap and one advancing car.");
        }

        switch (Format)
        {
            case QualifyingFormat.SingleSession when Sessions.Count != 1:
                throw new ArgumentException("A single-session format has exactly one session.");
            case QualifyingFormat.TwoDay when Sessions.Count != 2 || Sessions.Any(s => !s.CountsForGrid):
                throw new ArgumentException("A two-day format has exactly two counting sessions.");
            case QualifyingFormat.Aggregate when Sessions.Count < 2 || Sessions.Any(s => !s.CountsForGrid):
                throw new ArgumentException("An aggregate format needs at least two counting sessions.");
            case QualifyingFormat.OneLapShootout when !Sessions.Any(s => s.CountsForGrid):
                throw new ArgumentException("A shootout needs a counting session.");
            case QualifyingFormat.Knockout:
                if (Sessions.Count < 2 || KnockoutEliminations.Count != Sessions.Count - 1 || KnockoutEliminations.Any(e => e < 0))
                {
                    throw new ArgumentException("A knockout needs two or more segments and one non-negative cut per segment but the last.");
                }

                if (Sessions.Any(s => !s.CountsForGrid))
                {
                    throw new ArgumentException("Every knockout segment counts for the grid.");
                }

                break;
        }

        if (Format != QualifyingFormat.Knockout && KnockoutEliminations.Count != 0)
        {
            throw new ArgumentException("Knockout eliminations only apply to the knockout format.");
        }
    }
}
