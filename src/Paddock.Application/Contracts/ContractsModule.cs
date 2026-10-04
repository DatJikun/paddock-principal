using Paddock.Application.Career;
using Paddock.Application.Commands;
using Paddock.Application.Inbox;
using Paddock.Domain.Board;
using Paddock.Domain.Contracts;
using Paddock.Domain.Spy;

namespace Paddock.Application.Contracts;

/// <summary>
/// T39 contracts and negotiations in the career loop (#129). It builds the contract book, the engine and the environment of
/// ports (payroll and reputation from the modules that offer them), registers the six contract commands with their inbox
/// resolvers and the two contract day handlers (orders 700 and 710), and files the placeholder AI renewals (until T44).
/// The book is bound to the session's world (<see cref="ContractBook.Bind"/>), so a contract a command signs and a pool change in
/// the same morning both survive; the contracts section is put into the world by the module's flush.
/// </summary>
public sealed class ContractsModule : CareerModule
{
    public const string ModuleName = "contracts";

    public override string Name => ModuleName;

    public override IReadOnlyList<string> Sections => [ContractsSection.SectionName];

    public override IReadOnlyList<CommandCodecEntry> CommandCodecs => ContractCommandCodecs.Entries;

    public override void Attach(CareerModuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var session = context.Session;
        var trace = new MemorySink();
        var environment = new ContractEnvironment(
            new DerivedPersonalitySource(session.Clock.MasterSeed),
            new FlatCareerPay(),
            context.Require<IOrganizationControl>(),
            payroll: context.TryGet<IPayrollLedger>(),
            trace: trace,
            reputation: context.TryGet<IReputationSource>());
        var resolvers = context.Require<InboxResolvers>();
        var inbox = context.Require<InboxBook>();
        var book = new ContractBook(session.World, environment);
        var engine = new ContractEngine(book, inbox, context.Managers);
        context.Provide(book);
        context.Provide(engine);
        context.AddCommandHandlers(dispatcher => ContractRegistration.Register(dispatcher, resolvers));
        book.Bind(() => session.World, session.StoreWorld);
        context.AddFlush(book.Into);
        context.AddDayHandler(new NegotiationDayHandler(engine));
        context.AddDayHandler(new ContractLifecycleHandler(engine));
        context.AddMorning(queue => AiContractPlaceholder.File(session, engine, queue, context.Ai, trace));
    }

    /// <summary>ESTIMATE: one reference salary for every subject until era pay is wired into the career host.</summary>
    private sealed class FlatCareerPay : IPayBenchmark
    {
        public const long Amount = 100_000;

        public long Reference(int season, NegotiationSubject subject, double stars) => Amount;
    }
}
