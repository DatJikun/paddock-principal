using Paddock.Application.Career;
using Paddock.Application.Commands;
using Paddock.Application.Contracts;
using Paddock.Application.Inbox;
using Paddock.Domain.Infrastructure;
using Paddock.Domain.World;

namespace Paddock.Application.Infrastructure;

/// <summary>
/// Team factory, rented tests and the day handler (#231, PP-064). With a catalog it registers the upgrade and book-test
/// commands and the day handler (order 770), and on a new career seeds a factory for every team. Without a catalog it
/// does nothing. It does not touch engine supply.
/// </summary>
public sealed class InfrastructureModule : CareerModule
{
    public const string ModuleName = "infrastructure";

    public override string Name => ModuleName;

    public override IReadOnlyList<string> Sections => [InfrastructureSection.SectionName];

    public override IReadOnlyList<CommandCodecEntry> CommandCodecs => InfrastructureCommandCodecs.Entries;

    public override void Attach(CareerModuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.Inputs.Facilities is not { } catalog || catalog.Kinds.Count == 0)
        {
            return;
        }

        var session = context.Session;
        var book = InfrastructureBook.ForSession(session);
        var environment = new InfrastructureEnvironment(
            context.Require<IOrganizationControl>(),
            catalog,
            context.Inputs.TeamCountries,
            context.Inputs.RulePeriods);
        context.Provide(book);
        context.Provide(environment);
        context.AddCommandHandlers(dispatcher => InfrastructureRegistration.Register(dispatcher, book, environment));
        context.AddDayHandler(new InfrastructureDayHandler(
            book,
            environment,
            inbox: context.Require<InboxBook>(),
            managers: context.Managers));
    }

    public override void Open(CareerModuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.TryGet<InfrastructureEnvironment>() is not { } environment
            || context.Session.World.Section<InfrastructureSection>(InfrastructureSection.SectionName) is not null)
        {
            return;
        }

        var session = context.Session;
        session.StoreWorld(session.World.WithSection(InitialInfrastructureFactory.Install(
            session.World,
            environment.Catalog,
            session.Date)));
    }
}
