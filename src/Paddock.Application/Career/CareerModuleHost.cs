using Paddock.Application.Board;
using Paddock.Application.Commands;
using Paddock.Application.Contracts;
using Paddock.Application.Finance;
using Paddock.Application.Inbox;
using Paddock.Application.Managers;
using Paddock.Application.Objectives;
using Paddock.Application.Principals;
using Paddock.Application.World;
using Paddock.Domain.World;
using Paddock.Simulation.Career;

namespace Paddock.Application.Career;

/// <summary>
/// Puts a list of <see cref="ICareerModule"/>s onto one career run. It is the only code that knows the order of the steps:
/// the shared infrastructure first (who runs what, the inbox), then every module's Configure, every module's Attach, the day
/// handlers handed to the session, and every module's Open. The day loop in <see cref="CareerHost"/> then calls
/// <see cref="BeginMorning"/> and <see cref="EndMorning"/> and knows nothing about any one system.
/// <para>
/// A module is listed after the modules whose services it reads in Attach. Configure has no such rule, because it only offers.
/// </para>
/// </summary>
public sealed class CareerModuleHost
{
    private readonly CareerModuleContext _context;
    private readonly ControlTable _control;
    private readonly List<(ManagerId Manager, OrganizationId Organization)> _humans;

    private CareerModuleHost(
        CareerModuleContext context,
        ControlTable control,
        IReadOnlyList<(ManagerId Manager, OrganizationId Organization)> humans)
    {
        _context = context;
        _control = control;
        _humans = [.. humans];
    }

    /// <summary>
    /// Builds the host for one run. <paramref name="modules"/> must have distinct names. Each module sees the shared
    /// infrastructure: one <see cref="ControlTable"/> (who runs which organization; the AI principal of a team is seated
    /// by the principals module, a human by <paramref name="humans"/>), <see cref="IManagerOrganizations"/>,
    /// <see cref="InboxResolvers"/> and <see cref="InboxBook"/>.
    /// </summary>
    public static CareerModuleHost Attach(
        CareerSession session,
        ManagerRegistry managers,
        CommandDispatcher dispatcher,
        ManagerId ai,
        IReadOnlyList<ICareerModule> modules,
        CareerInputs? inputs = null,
        IReadOnlyList<CareerHuman>? humans = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(managers);
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(modules);
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var module in modules)
        {
            ArgumentNullException.ThrowIfNull(module);
            if (!names.Add(module.Name))
            {
                throw new ArgumentException("Two career modules are named '" + module.Name + "'.", nameof(modules));
            }
        }

        var context = new CareerModuleContext(session, managers, dispatcher, ai, inputs ?? new CareerInputs());
        var control = new ControlTable();
        var seats = HumanSeats(humans);
        context.SeatedHumans = [.. seats.Select(seat => seat.Organization)];
        var resolvers = new InboxResolvers();
        context.Provide<IOrganizationControl>(control);
        context.Provide(control);
        context.Provide<IManagerOrganizations>(new PrincipalOrganizations(new AssignedHumans(seats)));
        context.Provide(resolvers);
        var inbox = InboxBook.From(session.World, resolvers);
        context.Provide(inbox);
        context.AddFlush(inbox.Into);

        dispatcher.Register(new ResolveInboxItemHandler());
        dispatcher.Register(new DismissInboxItemHandler());
        dispatcher.Register(new ExpireInboxItemHandler());

        // An offer that has passed its date lapses by its default option, as a logged command, before anything else is filed today.
        context.AddMorning(queue => InboxExpiry.EnqueueDue(queue, inbox, session.Date));

        foreach (var module in modules)
        {
            module.Configure(context);
        }

        foreach (var module in modules)
        {
            module.Attach(context);
        }

        context.AddDayHandler(new SeasonChangeHandler(context));
        context.Freeze();
        session.AttachHandlers(context.DayHandlers);
        session.AttachAfterDay(context.AfterDay);
        foreach (var module in modules)
        {
            module.Open(context);
        }

        return new CareerModuleHost(context, control, seats);
    }

    /// <summary>The services modules provided; a test reads the books from here.</summary>
    public CareerModuleContext Context => _context;

    /// <summary>The context commands run in: the inbox and, when a module provided them, the contract and board books.</summary>
    public CommandContext CommandContext(IWorldState world, ManagerRegistry managers)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(managers);
        return new CommandContext(
            world,
            managers,
            _context.TryGet<InboxBook>(),
            _context.TryGet<ContractBook>(),
            _context.TryGet<BoardBook>());
    }

    /// <summary>
    /// Seats a human who took a team after the host was attached (the play shell's wizard, or a resumed save whose
    /// manager id is not <c>human:{organizationId}</c>). From now on that team is human-run: the AI principal director skips it.
    /// </summary>
    public void SeatHuman(ManagerId manager, OrganizationId organization)
    {
        if (!_humans.Contains((manager, organization)))
        {
            _humans.Add((manager, organization));
        }

        _control.Assign(manager, organization);
    }

    /// <summary>
    /// Seats the saved managers of a resumed career before its first day is lived. Seats are otherwise set up by the morning, but a
    /// resumed save lives the day it was saved on first, and that day's notices go to whoever sits at the control table (#252).
    /// </summary>
    public void RestoreSeats() => Reseat();

    /// <summary>Seats humans and already-registered team AIs, then lets the modules file today's commands.</summary>
    public void BeginMorning(CommandQueue queue)
    {
        ArgumentNullException.ThrowIfNull(queue);
        Reseat();
        _context.RunMorning(queue);
    }

    /// <summary>
    /// Drains the queue. Modules that registered a before/after hook see each command, so a contract command refreshes the
    /// contract book and writes it back before the next command runs.
    /// </summary>
    public IReadOnlyList<CommandResult> Dispatch(CommandDispatcher dispatcher, CommandQueue queue, CommandContext context)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(queue);
        ArgumentNullException.ThrowIfNull(context);
        var batch = queue.DequeueAll();
        var results = new List<CommandResult>(batch.Count);
        foreach (var command in batch)
        {
            _context.BeforeCommand(command);
            results.Add(dispatcher.Dispatch(command, context));
            _context.AfterCommand(command);
        }

        return results;
    }

    /// <summary>Puts what the morning's commands changed in the books into the session's world.</summary>
    public void EndMorning() => _context.Flush();

    /// <summary>
    /// Puts saved seats back onto the one control table. A human named in the run options, or a saved manager id
    /// <c>human:{organizationId}</c>, runs that organization. An AI manager <c>ai:{organizationId}</c> runs it only when no
    /// human does. The gate manager <see cref="CareerHost.AiManagerId"/> is not a team principal unless a team has that id.
    /// </summary>
    private void Reseat()
    {
        var humanOrgs = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (manager, organization) in _humans)
        {
            if (Exists(organization))
            {
                _control.Assign(manager, organization);
                humanOrgs.Add(organization.Value);
            }
        }

        foreach (var snapshot in _context.Managers.All)
        {
            if (snapshot.Kind != ManagerKind.Human || !TrySeat(snapshot.Id.Value, "human:", out var organization) || !Exists(organization))
            {
                continue;
            }

            _control.Assign(snapshot.Id, organization);
            humanOrgs.Add(organization.Value);
        }

        foreach (var snapshot in _context.Managers.All)
        {
            if (snapshot.Kind != ManagerKind.Ai || !TrySeat(snapshot.Id.Value, PrincipalKeys.ManagerPrefix, out var organization))
            {
                continue;
            }

            if (humanOrgs.Contains(organization.Value) || !Exists(organization))
            {
                continue;
            }

            _control.Assign(snapshot.Id, organization);
        }
    }

    private bool Exists(OrganizationId organization)
    {
        foreach (var candidate in _context.Session.World.Organizations)
        {
            if (candidate.Id == organization)
            {
                return true;
            }
        }

        return false;
    }

    private static List<(ManagerId Manager, OrganizationId Organization)> HumanSeats(IReadOnlyList<CareerHuman>? humans)
    {
        var seats = new List<(ManagerId, OrganizationId)>();
        if (humans is null)
        {
            return seats;
        }

        foreach (var human in humans)
        {
            ArgumentNullException.ThrowIfNull(human);
            seats.Add((new ManagerId(human.ManagerId), FinanceIds.Parse(human.OrganizationId)));
        }

        return seats;
    }

    private static bool TrySeat(string managerId, string prefix, out OrganizationId organization)
    {
        organization = default;
        return managerId.StartsWith(prefix, StringComparison.Ordinal)
            && managerId.Length > prefix.Length
            && FinanceIds.TryParse(managerId[prefix.Length..], out organization);
    }

    /// <summary>The humans named for this run. Any other manager is unknown here; an AI principal id is read by <see cref="PrincipalOrganizations"/>.</summary>
    private sealed class AssignedHumans : IManagerOrganizations
    {
        private readonly Dictionary<string, OrganizationId> _byManager;

        public AssignedHumans(IReadOnlyList<(ManagerId Manager, OrganizationId Organization)> humans)
        {
            _byManager = new Dictionary<string, OrganizationId>(humans.Count, StringComparer.Ordinal);
            foreach (var (manager, organization) in humans)
            {
                _byManager[manager.Value] = organization;
            }
        }

        public OrganizationId? OrganizationOf(string managerId) =>
            _byManager.TryGetValue(managerId, out var organization) ? organization : null;
    }
}
