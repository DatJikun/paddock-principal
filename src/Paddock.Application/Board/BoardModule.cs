using Paddock.Application.Career;
using Paddock.Application.Commands;
using Paddock.Application.Contracts;
using Paddock.Application.Inbox;
using Paddock.Application.Objectives;
using Paddock.Domain.Board;
using Paddock.Domain.Objectives;

namespace Paddock.Application.Board;

/// <summary>
/// T45 reputation, board objectives and dismissal in the career loop (#160). It registers the three board commands and the job offer
/// resolver, the board day handler (order 910, after the objective handler at 900), and applies the outcomes of the board's
/// objectives after each lived day (<see cref="BoardOutcomes.Apply"/>, before the plain settling). It replaces who-runs-what with
/// who-holds-the-post (<see cref="TenureControl"/>, <see cref="TenureOrganizations"/>: a dismissed human no longer orders a team
/// around), feeds the principal's reputation into negotiations (<see cref="BoardReputationSource"/>), and pays a dismissed manager's
/// severance through the ledger when finance is there. It needs the objectives module (its facts) and the contracts module.
/// <para>
/// Not here yet: <see cref="IBoardHistory"/> has no supplier (the standings system), so expectations come from the budget rank
/// alone, and <c>board.peopleDevelopment</c> has none (T42). There are no human managers in an AI-only run, so nobody is appointed
/// to the player's team.
/// </para>
/// </summary>
public sealed class BoardModule : CareerModule
{
    public const string ModuleName = "board";

    /// <summary>The board applies the effect of an objective's outcome after the sponsors (750) and before the plain settling (990).</summary>
    public const int OutcomeOrder = BoardDayHandler.DefaultOrder;

    public override string Name => ModuleName;

    public override IReadOnlyList<string> Sections => [BoardSection.SectionName];

    public override IReadOnlyList<CommandCodecEntry> CommandCodecs => BoardCommandCodecs.Entries;

    public override void Configure(CareerModuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // The board does not exist yet while modules configure; these read it when asked.
        BoardSection Section() => context.Require<BoardBook>().Section;
        context.Provide<IReputationSource>(new BoardReputationSource(Section));
        context.Decorate<IOrganizationControl>(inner => new TenureControl(Section, inner));
        context.Decorate<IManagerOrganizations>(inner => new TenureOrganizations(Section, inner));
    }

    public override void Attach(CareerModuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var contracts = context.Require<ContractBook>();
        var book = new BoardBook(
            contracts,
            context.Require<IObjectiveFacts>(),
            context.Session.Clock.MasterSeed,
            severance: context.TryGet<IBoardSeverance>());
        var engine = new BoardEngine(book, context.Require<InboxBook>(), context.Managers);
        context.Provide(book);
        context.Provide(engine);
        var resolvers = context.Require<InboxResolvers>();
        context.AddCommandHandlers(dispatcher => BoardRegistration.Register(dispatcher, resolvers));
        context.AddDayHandler(new BoardDayHandler(engine));
        context.AddAfterDay(OutcomeOrder, events => BoardOutcomes.Apply(engine, events));
    }

    public override void Open(CareerModuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Every active team has a board and its principal on the record from the first day, so a save taken before any day has them.
        context.Require<BoardEngine>().EnsureBoards(context.Session.Date);
    }
}
