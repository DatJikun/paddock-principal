using Paddock.Application.Board;
using Paddock.Application.Commands;
using Paddock.Application.Contracts;
using Paddock.Application.Inbox;
using Paddock.Application.Managers;
using Paddock.Application.Objectives;
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

    private CareerModuleHost(CareerModuleContext context, ControlTable control)
    {
        _context = context;
        _control = control;
    }

    /// <summary>
    /// Builds the host for one run. <paramref name="modules"/> must have distinct names. Each module sees the shared
    /// infrastructure: <see cref="IOrganizationControl"/> (the AI manager runs every team until the board says otherwise),
    /// <see cref="IManagerOrganizations"/>, <see cref="InboxResolvers"/> and <see cref="InboxBook"/>.
    /// </summary>
    public static CareerModuleHost Attach(
        CareerSession session,
        ManagerRegistry managers,
        CommandDispatcher dispatcher,
        ManagerId ai,
        IReadOnlyList<ICareerModule> modules,
        CareerInputs? inputs = null)
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
        AssignAll(control, session, ai);
        var resolvers = new InboxResolvers();
        context.Provide<IOrganizationControl>(control);
        context.Provide<IManagerOrganizations>(new FirstOrganizationOnly(session));
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

        context.Freeze();
        session.AttachHandlers(context.DayHandlers);
        session.AttachAfterDay(context.AfterDay);
        foreach (var module in modules)
        {
            module.Open(context);
        }

        return new CareerModuleHost(context, control);
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

    /// <summary>Hands new organizations to the AI manager and lets the modules file today's commands.</summary>
    public void BeginMorning(CommandQueue queue)
    {
        ArgumentNullException.ThrowIfNull(queue);
        AssignAll(_control, _context.Session, _context.Ai);
        _context.RunMorning(queue);
    }

    /// <summary>Puts what the morning's commands changed in the books into the session's world.</summary>
    public void EndMorning() => _context.Flush();

    private static void AssignAll(ControlTable control, CareerSession session, ManagerId ai)
    {
        foreach (var organization in session.World.Organizations)
        {
            control.Assign(ai, organization.Id);
        }
    }

    /// <summary>The AI runs whichever organization was created first. Enough for a pool signing; T44 picks per team.</summary>
    private sealed class FirstOrganizationOnly : IManagerOrganizations
    {
        private readonly OrganizationId? _organization;

        public FirstOrganizationOnly(CareerSession session)
        {
            var world = session.World;
            _organization = world.Organizations.Count == 0 ? null : world.Organizations[0].Id;
        }

        public OrganizationId? OrganizationOf(string managerId) => _organization;
    }
}
