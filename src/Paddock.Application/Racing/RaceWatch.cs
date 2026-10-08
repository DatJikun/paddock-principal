using System.Collections.Immutable;
using Paddock.Domain.Racing;
using Paddock.Simulation.Racing.Pits;
using Paddock.Simulation.Racing.Tyres;
using Paddock.Simulation.Racing.Weekend;

namespace Paddock.Application.Racing;

/// <summary>One classified or unclassified row the shell may print. Public race facts only: no lap table and no true weather (INV-003).</summary>
public sealed record RaceResultLine(int Position, bool Classified, string DriverId, string TeamId, string Points);

/// <summary>
/// The tyres a pit wall may call for in a race and what a stop can do (#286): the season's race compounds softest first, then the
/// wet tyre; the compound every car starts on; whether the rules allow a tyre change and refuelling.
/// </summary>
public sealed record TyreOffer(IReadOnlyList<string> Compounds, string StartCompound, string WetCompound, bool TyreChange, bool Refuelling)
{
    /// <summary>The offer of the weekend <paramref name="input"/> describes. No state, no RNG.</summary>
    public static TyreOffer For(RaceWeekendInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var catalog = input.Compounds ?? TyreCompoundCatalog.Default;
        var dry = catalog.RaceCompounds(input.Season);
        var wet = catalog.WetCompound(input.Season).Id;
        var rules = PitRules.For(input.Rules);
        // The weekend starts every car on the middle race compound (WeekendRun.BuildCars).
        return new TyreOffer([.. dry.Select(c => c.Id), wet], dry[dry.Length / 2].Id, wet, rules.TyreChangeAllowed, rules.RefuellingAllowed);
    }
}

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

    /// <summary>Every strategist call of the race, by the driver of the car (all teams; a read for a manager keeps its own).</summary>
    public IReadOnlyList<StrategyCall> Calls { get; private set; } = [];

    /// <summary>What every pit wall knew about its own cars at the start of each lap (#286); a read for a manager keeps its own.</summary>
    public ImmutableArray<PitWallLap> PitWall { get; private set; } = [];

    /// <summary>The tyres a stop may fit and what a stop can do; null before the first race.</summary>
    public TyreOffer? Tyres { get; private set; }

    /// <summary>The pit walls' hold on the race while it is watched; null when its result is already booked (the career's race day).</summary>
    public RaceSteering? Steering { get; private set; }

    public void Publish(
        int season,
        int round,
        string layoutId,
        RaceTape tape,
        IReadOnlyList<RaceResultLine> lines,
        IReadOnlyList<StrategyCall>? calls = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(layoutId);
        ArgumentNullException.ThrowIfNull(tape);
        ArgumentNullException.ThrowIfNull(lines);
        Season = season;
        Round = round;
        LayoutId = layoutId;
        Tape = tape;
        Lines = lines;
        Calls = calls ?? [];
        PitWall = [];
        Tyres = null;
        Steering = null;
        Pending = true;
    }

    /// <summary>The pit wall side of the race just published (#286). Call right after <see cref="Publish"/>.</summary>
    public void PublishPitWall(ImmutableArray<PitWallLap> pitWall, TyreOffer tyres, RaceSteering? steering)
    {
        ArgumentNullException.ThrowIfNull(tyres);
        PitWall = pitWall.IsDefault ? [] : pitWall;
        Tyres = tyres;
        Steering = steering;
    }

    /// <summary>Hands the pending race to the shell once. A second call on the same race returns false.</summary>
    public bool TryTake(
        out int season,
        out int round,
        out string layoutId,
        out RaceTape? tape,
        out IReadOnlyList<RaceResultLine> lines)
    {
        season = Season;
        round = Round;
        layoutId = LayoutId;
        tape = Tape;
        lines = Lines;
        if (!Pending || tape is null)
        {
            return false;
        }

        Pending = false;
        return true;
    }
}
