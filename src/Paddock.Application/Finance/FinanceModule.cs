using Paddock.Application.Board;
using Paddock.Application.Career;
using Paddock.Application.Commands;
using Paddock.Application.Contracts;
using Paddock.Application.Pool;
using Paddock.Domain.Finance;
using Paddock.Domain.Objectives;
using Paddock.Domain.Time;
using Paddock.Domain.World;
using Paddock.Simulation.Career;

namespace Paddock.Application.Finance;

/// <summary>
/// T37 finance in the career loop (#160). With era data it opens the books of every team (the opening capital comes from the era's
/// budget for the team's tier), replaces the stand-ins of the other systems with the ledger (the payroll port of the contracts, the
/// junior funding of the pool, the severance of the board), registers its three commands and the finance day handler (order 800),
/// and tells the objectives the cash of a team (<c>finance.cash</c>). Without era data it does nothing and the stand-ins stay.
/// <para>
/// Race weekends (T47) post <see cref="ApplyRaceResultsCommand"/>'s ledger effect and the season's popularity directly from the
/// race day, through the same section methods the commands use. A team founded after the run starts gets no books.
/// </para>
/// </summary>
public sealed class FinanceModule : CareerModule
{
    public const string ModuleName = "finance";

    public override string Name => ModuleName;

    public override IReadOnlyList<string> Sections => [FinanceSection.SectionName];

    public override IReadOnlyList<CommandCodecEntry> CommandCodecs => FinanceCommandCodecs.Entries;

    public override void Configure(CareerModuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.Inputs.Eras is null)
        {
            return;
        }

        var book = FinanceBook.ForSession(context.Session);
        context.Provide(book);
        context.Provide<IPayrollLedger>(new FinancePayroll(book));
        context.Provide<IJuniorFunding>(new FinanceJuniorFunding(book));
        context.Provide<IBoardSeverance>(new FinanceSeverance(book));
    }

    public override void Attach(CareerModuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.Inputs.Eras is not { } eras || context.TryGet<FinanceBook>() is not { } book)
        {
            return;
        }

        var session = context.Session;
        var control = context.Require<IOrganizationControl>();
        var tiers = context.Inputs.Tiers ?? new TypicalTier();
        context.AddCommandHandler(new OpenBooksHandler(book, control, eras, tiers));
        context.AddCommandHandler(new ApplyRaceResultsHandler(book, eras));
        context.AddCommandHandler(new ApplySeasonEndedHandler(book));
        context.AddDayHandler(new FinanceDayHandler(() => session.World, session.StoreWorld, [new AllLedgerSources(context)]));
        context.TryGet<ObjectiveFactRegistry>()?.RegisterNumber(
            ObjectiveFactKeys.Cash,
            owner => book.Section.HasBook(owner) ? new Money(book.Section.BalanceOf(owner)).WholeDollars : null);
    }

    public override void Open(CareerModuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.Inputs.Eras is not { } eras || context.TryGet<FinanceBook>() is not { } book)
        {
            return;
        }

        var session = context.Session;
        var today = session.Date;
        var tiers = context.Inputs.Tiers ?? new TypicalTier();
        var facts = eras.Facts(today.Year);
        var section = book.Section;
        var changed = false;
        foreach (var team in CareerTeams.Active(session.World, today))
        {
            if (section.HasBook(team.Id))
            {
                continue;
            }

            section = section.Open(team.Id, today, facts.Dollars(tiers.TierOf(team.Id, today.Year)), facts);
            changed = true;
        }

        if (changed)
        {
            book.Replace(section);
        }
    }

    /// <summary>Every team starts as a typical one. The fallback when the host has no standings facts at all.</summary>
    private sealed class TypicalTier : ITeamTierSource
    {
        public TeamTier TierOf(OrganizationId organization, int startSeason) => TeamTier.Typical;
    }

    /// <summary>The ledger sources every module added (car build, development, supply), read each day.</summary>
    private sealed class AllLedgerSources : ILedgerSource
    {
        private readonly CareerModuleContext _context;

        public AllLedgerSources(CareerModuleContext context) => _context = context;

        public IReadOnlyList<LedgerDraft> Due(GameDate today)
        {
            var drafts = new List<LedgerDraft>();
            foreach (var source in _context.All<ILedgerSource>())
            {
                drafts.AddRange(source.Due(today));
            }

            return drafts;
        }
    }
}

/// <summary>The severance of a dismissed manager, posted to the organization's ledger as a termination cost (no ledger book, no posting).</summary>
public sealed class FinanceSeverance : IBoardSeverance
{
    private readonly FinanceBook _book;

    public FinanceSeverance(FinanceBook book)
    {
        ArgumentNullException.ThrowIfNull(book);
        _book = book;
    }

    public void Pay(OrganizationId payer, string managerId, long amount, GameDate on)
    {
        if (amount <= 0 || !_book.Section.HasBook(payer))
        {
            return;
        }

        _book.Replace(_book.Section.Post(
            payer,
            on,
            LedgerCategories.Other,
            managerId,
            -Money.FromDollars(amount).Cents,
            FinanceReason.Termination));
    }
}
