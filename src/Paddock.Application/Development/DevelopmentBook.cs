using Paddock.Application.Commands;
using Paddock.Application.Contracts;
using Paddock.Application.Managers;
using Paddock.Domain.Cars;
using Paddock.Domain.Development;
using Paddock.Domain.Finance;
using Paddock.Domain.Spy;
using Paddock.Domain.Time;
using Paddock.Domain.World;
using Paddock.Simulation.Career;

namespace Paddock.Application.Development;

/// <summary>
/// The live world as development sees it: the host owns the world, this reads it and puts sections back. One
/// <see cref="Write"/> replaces the cars, development and finance sections together, so a project that finishes, the car it
/// moves and the money it cost are never half written.
/// </summary>
public sealed class DevelopmentBook
{
    private readonly Func<WorldState> _read;
    private readonly Action<WorldState> _write;

    public DevelopmentBook(Func<WorldState> read, Action<WorldState> write, ulong masterSeed)
    {
        ArgumentNullException.ThrowIfNull(read);
        ArgumentNullException.ThrowIfNull(write);
        _read = read;
        _write = write;
        MasterSeed = masterSeed;
    }

    public ulong MasterSeed { get; }

    public WorldState World => _read();

    public CarsSection Cars => World.Section<CarsSection>(CarsSection.SectionName) ?? CarsSection.Empty;

    public DevelopmentSection Section => World.Section<DevelopmentSection>(DevelopmentSection.SectionName) ?? DevelopmentSection.Empty;

    public FinanceSection Finance => World.Section<FinanceSection>(FinanceSection.SectionName) ?? FinanceSection.Empty;

    public static DevelopmentBook ForSession(CareerSession session, ulong masterSeed)
    {
        ArgumentNullException.ThrowIfNull(session);
        return new DevelopmentBook(() => session.World, session.StoreWorld, masterSeed);
    }

    public DevelopmentInputs Inputs(GameDate today, DevelopmentEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        return new DevelopmentInputs(World, Cars, Section, Finance, today, MasterSeed, environment.Rules, environment.Trace);
    }

    public void Write(DevelopmentOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        var world = World.WithSection(outcome.Cars).WithSection(outcome.Development);
        if (outcome.Finance.HasBooks)
        {
            world = world.WithSection(outcome.Finance);
        }

        _write(world);
    }
}

/// <summary>The ports of the development system. A host replaces the ones it has a real system for.</summary>
public sealed class DevelopmentEnvironment
{
    public DevelopmentEnvironment(IDevelopmentRules rules, IOrganizationControl control, ITraceSink? trace = null)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(control);
        Rules = rules;
        Control = control;
        Trace = trace ?? NullSink.Instance;
    }

    public IDevelopmentRules Rules { get; }

    public IOrganizationControl Control { get; }

    public ITraceSink Trace { get; }
}

public static class DevelopmentEventTypes
{
    public const string SplitSet = "development.split_set";

    public const string TimingSet = "development.timing_set";

    public const string ProjectCut = "development.project_cut";
}

public sealed record DevelopmentSplitSet(ManagerId ManagerId, DateOnly OccurredOn, string OrganizationId) : IDomainEvent
{
    public string TypeId => DevelopmentEventTypes.SplitSet;
}

public sealed record ConceptTimingSet(ManagerId ManagerId, DateOnly OccurredOn, string ProjectId, string Timing) : IDomainEvent
{
    public string TypeId => DevelopmentEventTypes.TimingSet;
}

public sealed record DevelopmentProjectCut(ManagerId ManagerId, DateOnly OccurredOn, string ProjectId) : IDomainEvent
{
    public string TypeId => DevelopmentEventTypes.ProjectCut;
}

/// <summary>
/// The hook the host calls after a race has been run and written: cars that ran gain understanding, concepts waiting for
/// "after N races" count one more. A post-race step, not a command, so it never names a manager.
/// </summary>
public static class DevelopmentRaceHook
{
    public static void OnRaceFinished(
        DevelopmentBook book,
        DevelopmentEnvironment environment,
        GameDate today,
        IReadOnlyList<(string CarId, int Kilometres)> runs)
    {
        ArgumentNullException.ThrowIfNull(book);
        var outcome = DevelopmentEngine.RaceFinished(book.Inputs(today, environment), runs);
        if (outcome.Changed)
        {
            book.Write(outcome);
        }
    }
}
