using Paddock.Application.Career;
using Paddock.Domain.People;
using Paddock.Domain.Random;
using Paddock.Simulation.Career;
using Paddock.Simulation.Time;

namespace Paddock.Application.Staff;

/// <summary>
/// Refills an empty team chair on the morning the season changes, after retirement and contract expiry have run.
/// The race weekend does not use the pairing yet (#112 part B). There is no shell command here: that waits on #112 part A.
/// </summary>
public sealed class StaffModule : CareerModule
{
    public const string ModuleName = "staff";

    public override string Name => ModuleName;

    public override IReadOnlyList<string> Sections => [StaffSection.SectionName];

    public override void Attach(CareerModuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.AddDayHandler(new StaffDayHandler(context.Session));
    }
}

/// <summary>After contract expiry (30) and before the season rollover (40), so a chair emptied today is filled for the new season.</summary>
public sealed class StaffDayHandler : IDayHandler
{
    public const int DefaultOrder = 35;

    private readonly CareerSession _session;

    public StaffDayHandler(CareerSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        _session = session;
    }

    public int Order => DefaultOrder;

    public void OnDay(DayContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!context.HasEmitted(CareerEventType.SeasonChanged))
        {
            return;
        }

        var world = _session.World;
        if (StaffRoster.HasVacancy(world, context.Today))
        {
            var people = new RngStream(RngStreamName.People, context.Stream(RngStreamName.People).State);
            var filled = StaffRoster.Fill(world, people, new FixtureNameSource(), EmptyNameBlocklist.Instance, context.Today);
            world = filled.World;
            foreach (var id in filled.Hired)
            {
                _session.RegisterPerson(world.GetPerson(id), context.Today);
            }
        }

        var current = world.Section<StaffSection>(StaffSection.SectionName) ?? StaffSection.Empty;
        var next = StaffRoster.Advance(world, current, context.Today);
        world = next.IsEmpty ? world.WithoutSection(StaffSection.SectionName) : world.WithSection(next);
        if (world.StateHash() != _session.World.StateHash())
        {
            _session.StoreWorld(world);
        }
    }
}
