using Paddock.Application.Career;
using Paddock.Application.Commands;
using Paddock.Application.Contracts;
using Paddock.Application.Inbox;
using Paddock.Domain.Supply;

namespace Paddock.Application.Supply;

/// <summary>
/// T43 supply deals in the career loop (#160). With the era periods it registers the supply commands and the supply day
/// handler (order 760, after the sponsors and before the finance review), and, once, builds the opening engine deals from the
/// world initializer's links after the cars exist (<see cref="InitialSupplyFactory"/>). Without the periods it does nothing.
/// The race module reads <c>SupplyPerformance</c> when it builds a grid.
/// </summary>
public sealed class SupplyModule : CareerModule
{
    public const string ModuleName = "supply";

    public override string Name => ModuleName;

    public override IReadOnlyList<string> Sections => [SupplySection.SectionName];

    public override IReadOnlyList<CommandCodecEntry> CommandCodecs => SupplyCommandCodecs.Entries;

    public override void Attach(CareerModuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.Inputs.EraPeriods is not { } periods)
        {
            return;
        }

        var session = context.Session;
        var book = SupplyBook.ForSession(session);
        var environment = new SupplyEnvironment(
            context.Require<IOrganizationControl>(),
            new PeriodSupplyEras(periods),
            new EstimateSupplierProfiles(session.OpenedYear, context.Inputs.CarStrength, session.Clock.MasterSeed));
        context.Provide(book);
        context.Provide(environment);
        context.AddCommandHandlers(dispatcher => SupplyRegistration.Register(dispatcher, book, environment));
        context.AddDayHandler(new SupplyDayHandler(book, environment, context.Require<InboxBook>(), context.Managers));
    }

    public override void Open(CareerModuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.TryGet<SupplyEnvironment>() is not { } environment
            || context.Inputs.SupplyLinks is not { } links
            || context.Session.World.Section<SupplySection>(SupplySection.SectionName) is not null)
        {
            return;
        }

        var session = context.Session;
        session.StoreWorld(InitialSupplyFactory.Install(
            session.World,
            links,
            session.Date.Year,
            environment.Eras.ReferenceBudgetCents(session.Date.Year)));
    }
}
