using Paddock.Domain.Racing;

namespace Paddock.Application.Racing;

/// <summary>One classified or unclassified row the shell may print. Public race facts only: no lap table and no true weather (INV-003).</summary>
public sealed record RaceResultLine(int Position, bool Classified, string DriverId, string TeamId, string Points);

/// <summary>
/// The last race the career ran, kept in memory for the shell. It is not part of the world and not saved: a race is one
/// day, and the result that matters is already in the championship, the ledger and the cars (INV-007).
/// </summary>
public sealed class RaceWatch
{
    public bool Pending { get; private set; }

    public int Season { get; private set; }

    public int Round { get; private set; }

    public string LayoutId { get; private set; } = "";

    public RaceTape? Tape { get; private set; }

    public IReadOnlyList<RaceResultLine> Lines { get; private set; } = [];

    /// <summary>Teams that had cars and did not start because the race running cost was above the cash on hand.</summary>
    public IReadOnlyList<string> SkippedTeamIds { get; private set; } = [];

    /// <summary>Every strategist call of the race, by the driver of the car (all teams; a read for a manager keeps its own).</summary>
    public IReadOnlyList<StrategyCall> Calls { get; private set; } = [];

    public void Publish(
        int season,
        int round,
        string layoutId,
        RaceTape tape,
        IReadOnlyList<RaceResultLine> lines,
        IReadOnlyList<string> skippedTeamIds,
        IReadOnlyList<StrategyCall>? calls = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(layoutId);
        ArgumentNullException.ThrowIfNull(tape);
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(skippedTeamIds);
        Season = season;
        Round = round;
        LayoutId = layoutId;
        Tape = tape;
        Lines = lines;
        SkippedTeamIds = skippedTeamIds;
        Calls = calls ?? [];
        Pending = true;
    }

    /// <summary>Hands the pending race to the shell once. A second call on the same race returns false.</summary>
    public bool TryTake(
        out int season,
        out int round,
        out string layoutId,
        out RaceTape? tape,
        out IReadOnlyList<RaceResultLine> lines,
        out IReadOnlyList<string> skippedTeamIds)
    {
        season = Season;
        round = Round;
        layoutId = LayoutId;
        tape = Tape;
        lines = Lines;
        skippedTeamIds = SkippedTeamIds;
        if (!Pending || tape is null)
        {
            return false;
        }

        Pending = false;
        return true;
    }
}
