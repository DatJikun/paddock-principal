using Paddock.Application.Career;
using Paddock.Application.Commands;
using Paddock.Application.Contracts;
using Paddock.Application.Inbox;
using Paddock.Domain.Development;

namespace Paddock.Application.Development;

/// <summary>
/// T42 car development in the career loop (#160). With the regulation periods it registers the development commands and the
/// inbox resolvers of the engineers' reply, the development day handler (order 780, with the inbox and the managers so a ready
/// concept asks its principal), and the host's change of season (<see cref="DevelopmentEngine.ChangeSeason"/>, run by the host on
/// 1 January before any day handler). Without the periods it does nothing.
/// <para>
/// The race module, when the career has a calendar, calls <see cref="DevelopmentRaceHook"/> after each race and offers
/// <see cref="INextRaceSource"/>. Without that module the hook is not called and the next race stays unknown.
/// </para>
/// </summary>
public sealed class DevelopmentModule : CareerModule
{
    public const string ModuleName = "development";

    public override string Name => ModuleName;

    public override IReadOnlyList<string> Sections => [DevelopmentSection.SectionName];

    public override IReadOnlyList<CommandCodecEntry> CommandCodecs => DevelopmentCommandCodecs.Entries;

    public override void Attach(CareerModuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.Inputs.RulePeriods is not { } periods)
        {
            return;
        }

        var session = context.Session;
        var book = DevelopmentBook.ForSession(session, session.Clock.MasterSeed);
        var environment = new DevelopmentEnvironment(
            new PeriodDevelopmentRules(periods),
            context.Require<IOrganizationControl>(),
            races: context.TryGet<INextRaceSource>());
        context.Provide(book);
        context.Provide(environment);
        var resolvers = context.Require<InboxResolvers>();
        context.AddCommandHandlers(dispatcher => DevelopmentRegistration.Register(dispatcher, resolvers, book, environment));
        context.AddDayHandler(new DevelopmentDayHandler(book, environment, inbox: context.Require<InboxBook>(), managers: context.Managers));
        context.AddSeasonChange(today =>
        {
            var outcome = DevelopmentEngine.ChangeSeason(book.Inputs(today, environment));
            if (outcome.Changed)
            {
                book.Write(outcome);
            }
        });
    }
}
