using Paddock.Application.Career;
using Paddock.Application.Commands;
using Paddock.Application.Contracts;
using Paddock.Application.Inbox;
using Paddock.Domain.Career;
using Paddock.Domain.Racing;

namespace Paddock.Application.Regulation;

/// <summary>
/// The political life of the racing series (#275): proposals that cost a fee, one ballot item per dimension, the FIA's own votes,
/// one vote each or a bank of votes, and the result stored for the next season. A historical career, or a host with no regulation
/// catalog, gets nothing from it: no handler, no section, no change to a single hash.
/// </summary>
public sealed class RegulationsModule : CareerModule
{
    public const string ModuleName = "regulations";

    public override string Name => ModuleName;

    public override IReadOnlyList<string> Sections => [RegulationsSection.SectionName];

    public override IReadOnlyList<CommandCodecEntry> CommandCodecs => RegulationCommandCodecs.Entries;

    public override void Attach(CareerModuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var inputs = context.Inputs;
        if (inputs.Rules != RulesSource.VotedEachSeason
            || inputs.RegulationCatalog is not { } catalog
            || inputs.RegulationDimensionIds is not { } dimensions
            || inputs.RulePeriods is not { } periods)
        {
            return;
        }

        var environment = new RegulationEnvironment(
            inputs.Rules,
            inputs.VoteMode,
            context.Session.Clock.MasterSeed,
            catalog,
            dimensions,
            periods,
            context.Require<IOrganizationControl>(),
            context.Managers,
            new SingleSeriesDirectory(),
            inputs.Layouts,
            inputs.RaceAssignments,
            inputs.TeamCountries,
            context.SeatedHumans);
        var politics = new RegulationPolitics(RegulationBook.ForSession(context.Session), environment, context.TryGet<InboxBook>());
        context.Provide(environment);
        context.Provide(politics);
        context.AddCommandHandlers(dispatcher => RegulationRegistration.Register(dispatcher, politics));
        context.AddDayHandler(new RegulationDayHandler(politics));
    }

    public override void Open(CareerModuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.TryGet<RegulationPolitics>() is not { } politics
            || context.Session.World.Section(RegulationsSection.SectionName) is not null)
        {
            return;
        }

        var session = context.Session;
        session.StoreWorld(session.World.WithSection(politics.Open(session.World, session.Date)));
    }
}
