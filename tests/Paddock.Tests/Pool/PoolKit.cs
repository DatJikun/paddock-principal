using Paddock.Domain.Career;
using Paddock.Domain.People;
using Paddock.Domain.Pool;
using Paddock.Domain.Time;
using Paddock.Domain.World;
using Paddock.Simulation.Pool;
using Paddock.Simulation.Time;

namespace Paddock.Tests.Pool;

/// <summary>
/// Fixtures for the talent pool tests. Every person, team, rating and date here is synthetic test data:
/// the ids look like real drivers' ids only because the pool must handle both kinds, and no number is a calibrated value.
/// </summary>
internal static class PoolKit
{
    public static readonly GameDate Start = new(1950, 1, 1);

    public const string TeamAlpha = "alpha";

    public const string TeamBravo = "bravo";

    public static OrganizationId Alpha => OrganizationId.Real(TeamAlpha);

    public static OrganizationId Bravo => OrganizationId.Real(TeamBravo);

    public static PersonTruth Truth(int current, int extra)
    {
        var low = new DriverAttributes(current, current, current, current, current, current, current, current, current, current, current);
        var high = new DriverAttributes(
            Math.Min(20, current + extra),
            Math.Min(20, current + extra),
            Math.Min(20, current + extra),
            Math.Min(20, current + extra),
            Math.Min(20, current + extra),
            Math.Min(20, current + extra),
            Math.Min(20, current + extra),
            Math.Min(20, current + extra),
            Math.Min(20, current + extra),
            Math.Min(20, current + extra),
            Math.Min(20, current + extra));
        return PersonTruth.FromDriver(low, high);
    }

    public static PersonSpec RealDriver(string id, int birthYear, int current = 10, int extra = 4) =>
        new("Given" + id.Replace("_", string.Empty), "Family" + id.Replace("_", string.Empty), new GameDate(birthYear, 6, 15), "GBR", true, id, [PersonRole.Driver], Truth(current, extra));

    public static PersonSpec Scout(string id, int judgement, int network)
    {
        var attributes = new[] { new NamedAttribute("talent_judgement", judgement), new NamedAttribute("contact_network", network) };
        return new PersonSpec("Scout" + id, "Person" + id, new GameDate(1910, 3, 3), "GBR", true, id, [PersonRole.Staff(StaffRole.Scout)], new PersonTruth(attributes, attributes));
    }

    /// <summary>A world with two teams and no people.</summary>
    public static WorldState EmptyWorld(GameDate? date = null)
    {
        var world = WorldState.At(date ?? Start);
        foreach (var id in new[] { TeamAlpha, TeamBravo })
        {
            (world, _) = world.AddOrganization(new OrganizationSpec(
                OrganizationKind.Team,
                true,
                id,
                new GameDate(1949, 1, 1),
                null,
                0,
                [new OrganizationNameSpan(id, new GameDate(1949, 1, 1), null)]));
        }

        return world;
    }

    public static (WorldState World, PersonId Id) Add(WorldState world, PersonSpec spec) => world.AddPerson(spec);

    public static WorldState Sign(WorldState world, PersonId person, OrganizationId team, GameDate from, GameDate to)
    {
        (world, _) = world.AddContract(new ContractSpec(
            person,
            team,
            ContractRole.Driver(SeatStatus.Reserve),
            from,
            to,
            0,
            true,
            null,
            null));
        return world;
    }

    public static WorldState SignScout(WorldState world, PersonId scout, OrganizationId team)
    {
        (world, _) = world.AddContract(new ContractSpec(
            scout,
            team,
            ContractRole.Staff(StaffRole.Scout),
            new GameDate(1949, 1, 1),
            new GameDate(1999, 12, 31),
            0,
            true,
            null,
            null));
        return world;
    }

    /// <summary>
    /// The pool handler on its own, driven day by day through the world clock so the emitted events can be read.
    /// <see cref="World"/> is the live world.
    /// </summary>
    public sealed class Run
    {
        private readonly TalentPoolDayHandler _handler;
        private readonly DayHandlerRegistry _registry;

        public Run(
            WorldState world,
            ulong seed,
            IEnumerable<PersonId> initialPool,
            IEnumerable<ScheduledArrival>? arrivals = null,
            TalentPoolOptions? options = null)
        {
            var pool = (world.Section<TalentPoolSection>(TalentPoolSection.SectionName) ?? TalentPoolSection.Empty)
                .EnterAll(initialPool, world.CurrentDate);
            World = world.WithSection(pool);
            Clock = new WorldClockState(world.CurrentDate, seed);
            var byDay = new Dictionary<GameDate, ScheduledArrival[]>();
            foreach (var group in (arrivals ?? []).GroupBy(arrival => arrival.On))
            {
                byDay.Add(group.Key, group.OrderBy(arrival => arrival.Spec.RealId, StringComparer.Ordinal).ToArray());
            }

            _handler = new TalentPoolDayHandler(
                () => World,
                next => World = next,
                byDay,
                _ => { },
                options ?? new TalentPoolOptions(),
                world.CurrentDate.Year);
            _registry = new DayHandlerRegistry([_handler]);
        }

        public WorldState World { get; set; }

        public WorldClockState Clock { get; private set; }

        public List<DomainEvent> Events { get; } = [];

        public GameDate Date => Clock.Date;

        public TalentPoolSection Pool => World.Section<TalentPoolSection>(TalentPoolSection.SectionName)!;

        public void LiveDay()
        {
            var step = WorldClock.AdvanceDay(Clock, _registry);
            Clock = step.State;
            World = World.WithDate(Clock.Date);
            Events.AddRange(step.Events);
        }

        /// <summary>Lives days until the morning of <paramref name="date"/>.</summary>
        public void LiveUntil(GameDate date)
        {
            while (Clock.Date < date)
            {
                LiveDay();
            }
        }

        public void SetFocus(OrganizationId organization, ScoutFocusKind kind, PersonId? person = null) =>
            World = World.WithSection(Pool.SetFocus(new ScoutFocus(organization, kind, person)));
    }
}
