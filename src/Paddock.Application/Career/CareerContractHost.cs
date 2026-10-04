using Paddock.Application.Commands;
using Paddock.Application.Contracts;
using Paddock.Application.Inbox;
using Paddock.Application.Managers;
using Paddock.Application.Objectives;
using Paddock.Application.Pool;
using Paddock.Domain.Contracts;
using Paddock.Domain.Spy;
using Paddock.Domain.World;
using Paddock.Simulation.Career;
using Paddock.Simulation.Time;

namespace Paddock.Application.Career;

/// <summary>
/// Holds the contract book and the inbox for one career run, registers the T39 day handlers on the session, and files the
/// placeholder renewal commands. The book and the session share one world: the book is copied from the session before a
/// command or a contract handler, and written back after, so a pool change and a contract change in the same morning both survive.
/// </summary>
public sealed class CareerContractHost
{
    private readonly CareerSession _session;
    private readonly ContractBook _book;
    private readonly InboxBook _inbox;
    private readonly ContractEngine _engine;
    private readonly ManagerId _ai;

    private CareerContractHost(CareerSession session, ContractBook book, InboxBook inbox, ContractEngine engine, ManagerId ai)
    {
        _session = session;
        _book = book;
        _inbox = inbox;
        _engine = engine;
        _ai = ai;
    }

    public ContractEngine Engine => _engine;

    public InboxBook Inbox => _inbox;

    public ITraceSink Trace => _book.Environment.Trace;

    public static CareerContractHost Attach(CareerSession session, ManagerRegistry managers, CommandDispatcher dispatcher, ManagerId ai)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(managers);
        ArgumentNullException.ThrowIfNull(dispatcher);
        var control = new ControlTable();
        foreach (var organization in session.World.Organizations)
        {
            control.Assign(ai, organization.Id);
        }

        var trace = new MemorySink();
        var environment = new ContractEnvironment(
            new DerivedPersonalitySource(session.Clock.MasterSeed),
            new FlatCareerPay(),
            control,
            trace: trace);
        var resolvers = new InboxResolvers();
        var inbox = InboxBook.From(session.World, resolvers);
        var book = new ContractBook(session.World, environment);
        var engine = new ContractEngine(book, inbox, managers);
        ContractRegistration.Register(dispatcher, resolvers);
        dispatcher.Register(new SignPoolDriverHandler(
            PoolBook.ForSession(session),
            new SingleOrganization(session.World),
            new ContractPoolNegotiations(engine)));
        session.AttachHandlers(
        [
            new SyncingHandler(session, book, inbox, new NegotiationDayHandler(engine)),
            new SyncingHandler(session, book, inbox, new ContractLifecycleHandler(engine)),
        ]);
        return new CareerContractHost(session, book, inbox, engine, ai);
    }

    public void BeginMorning()
    {
        AssignNewOrganizations();
        _book.UseWorld(_session.World);
    }

    public void FileRenewals(CommandQueue queue) =>
        AiContractPlaceholder.File(_session, _engine, queue, _ai, _book.Environment.Trace);

    public void EndMorning()
    {
        _book.UseWorld(_session.World);
        _session.StoreWorld(_inbox.Into(_book.Into()));
    }

    private void AssignNewOrganizations()
    {
        if (_book.Environment.Control is not ControlTable control)
        {
            return;
        }

        foreach (var organization in _session.World.Organizations)
        {
            control.Assign(_ai, organization.Id);
        }
    }

    /// <summary>ESTIMATE: one reference salary for every subject until era pay is wired into the career host.</summary>
    private sealed class FlatCareerPay : IPayBenchmark
    {
        public const long Amount = 100_000;

        public long Reference(int season, NegotiationSubject subject, double stars) => Amount;
    }

    /// <summary>The AI runs whichever organization was created first. Enough for a pool signing; T44 picks per team.</summary>
    private sealed class SingleOrganization : IManagerOrganizations
    {
        private readonly OrganizationId? _organization;

        public SingleOrganization(WorldState world)
        {
            _organization = world.Organizations.Count == 0 ? null : world.Organizations[0].Id;
        }

        public OrganizationId? OrganizationOf(string managerId) => _organization;
    }

    private sealed class SyncingHandler : IDayHandler
    {
        private readonly CareerSession _session;
        private readonly ContractBook _book;
        private readonly InboxBook _inbox;
        private readonly IDayHandler _inner;

        public SyncingHandler(CareerSession session, ContractBook book, InboxBook inbox, IDayHandler inner)
        {
            _session = session;
            _book = book;
            _inbox = inbox;
            _inner = inner;
            Order = inner.Order;
        }

        public int Order { get; }

        public void OnDay(DayContext context)
        {
            _book.UseWorld(_session.World);
            _inner.OnDay(context);
            _session.StoreWorld(_inbox.Into(_book.Into()));
        }
    }
}
