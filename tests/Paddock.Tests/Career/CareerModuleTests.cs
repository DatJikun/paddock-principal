using Paddock.Application.Career;
using Paddock.Application.Commands;
using Paddock.Domain.People;
using Paddock.Domain.Pool;
using Paddock.Domain.Time;
using Paddock.Domain.World;
using Paddock.Persistence;
using Paddock.Simulation.Career;
using Paddock.Simulation.Pool;
using Paddock.Simulation.Time;

namespace Paddock.Tests.Career;

/// <summary>
/// #160: a system joins the career loop through one list. These tests are the proof that adding a module is one line and no edit
/// of the host, and that the list is consistent with what a save can hold.
/// </summary>
public sealed class CareerModuleTests
{
    [Fact]
    public void ADummyModuleJoinsTheRunWithOneListEntryAndNoHostEdit()
    {
        var days = new List<GameDate>();
        var dummy = new DummyModule(days);

        var session = Session();
        CareerHost.Run(session, 1950, null, [.. CareerModules.Default, dummy]);

        Assert.Equal(365, days.Count);
        Assert.Equal(new GameDate(1950, 1, 1), days[0]);
        Assert.Equal(1, dummy.Opened);
        Assert.Equal(1, dummy.Configured);
        Assert.Equal(1, dummy.Attached);
        Assert.Equal(DummyModule.DayOrder, session.DayHandlers[^1].Order);
    }

    [Fact]
    public void TheDayHandlersRunInTheOrderOfTheirNumbersWhateverTheListOrder()
    {
        var days = new List<GameDate>();
        var early = new DummyModule(days, order: 5);
        var late = new DummyModule(days, order: 5000, name: "late");

        var session = Session();
        CareerHost.Run(session, 1950, null, [late, .. CareerModules.Default, early]);

        var orders = session.DayHandlers.Select(handler => handler.Order).ToArray();
        Assert.Equal(orders.Order().ToArray(), orders);
        Assert.Equal(5, orders[0]);
        Assert.Equal(5000, orders[^1]);
    }

    [Fact]
    public void TwoModulesWithOneNameAreRefused()
    {
        var session = Session();

        Assert.Throws<ArgumentException>(() =>
            CareerHost.Run(session, 1950, null, [new DummyModule([]), new DummyModule([])]));
    }

    [Fact]
    public void AModuleCannotRegisterAfterTheHostHasAttachedIt()
    {
        var late = new DummyModule([]);
        CareerModuleContext? kept = null;
        var capture = new CapturingModule(context => kept = context);

        CareerHost.Run(Session(), 1950, null, [capture]);

        Assert.NotNull(kept);
        Assert.Throws<InvalidOperationException>(() => kept!.AddDayHandler(new DummyHandler(late, 1)));
    }

    [Fact]
    public void EveryDeclaredSectionHasASaveStoreAndEveryModuleCodecIsInTheProductionCodec()
    {
        var stores = SectionStores.Production.Select(store => store.SectionName).ToHashSet(StringComparer.Ordinal);
        var saved = CommandCodec.Production.SavedTypes.ToHashSet();

        foreach (var module in CareerModules.Default)
        {
            Assert.All(module.Sections, section => Assert.True(stores.Contains(section), module.Name + " declares section '" + section + "' with no save store."));
            Assert.All(module.CommandCodecs, entry => Assert.Contains(entry.CommandType, saved));
        }

        Assert.Equal(CareerModules.Default.Count, CareerModules.Default.Select(module => module.Name).Distinct(StringComparer.Ordinal).Count());
    }

    private static CareerSession Session()
    {
        var world = WorldState.At(new GameDate(1950, 1, 1));
        (world, _) = world.AddOrganization(new OrganizationSpec(
            OrganizationKind.Team,
            true,
            "alpha",
            new GameDate(1950, 1, 1),
            null,
            0,
            [new OrganizationNameSpan("Alpha", new GameDate(1950, 1, 1), null)]));
        return new CareerSession(world, 7, [], [], new CareerSessionOptions { Pool = new TalentPoolOptions { TargetSize = 0 } });
    }

    private sealed class DummyHandler : IDayHandler
    {
        private readonly DummyModule _module;

        public DummyHandler(DummyModule module, int order)
        {
            _module = module;
            Order = order;
        }

        public int Order { get; }

        public void OnDay(DayContext context) => _module.Days.Add(context.Today);
    }

    private sealed class DummyModule : CareerModule
    {
        public const int DayOrder = 9000;

        private readonly int _order;
        private readonly string _name;

        public DummyModule(List<GameDate> days, int order = DayOrder, string name = "dummy")
        {
            Days = days;
            _order = order;
            _name = name;
        }

        public List<GameDate> Days { get; }

        public int Configured { get; private set; }

        public int Attached { get; private set; }

        public int Opened { get; private set; }

        public override string Name => _name;

        public override void Configure(CareerModuleContext context) => Configured++;

        public override void Attach(CareerModuleContext context)
        {
            Attached++;
            context.AddDayHandler(new DummyHandler(this, _order));
        }

        public override void Open(CareerModuleContext context) => Opened++;
    }

    private sealed class CapturingModule : CareerModule
    {
        private readonly Action<CareerModuleContext> _capture;

        public CapturingModule(Action<CareerModuleContext> capture) => _capture = capture;

        public override string Name => "capture";

        public override void Attach(CareerModuleContext context) => _capture(context);
    }
}
