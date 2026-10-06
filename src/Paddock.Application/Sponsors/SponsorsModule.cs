using Paddock.Application.Career;
using Paddock.Application.Commands;
using Paddock.Application.Contracts;
using Paddock.Application.Inbox;
using Paddock.Domain.Objectives;
using Paddock.Domain.Sponsors;

namespace Paddock.Application.Sponsors;

/// <summary>
/// T38 sponsors in the career loop (#160). With the sponsor catalog and the era data it registers the four sponsor commands, the
/// sponsor day handler (order 750, after the contracts and before finance), and, after each lived day, applies the outcomes of the
/// sponsors' objectives with the same events the objectives got (<see cref="SponsorOutcomes.Apply"/>). Without that data it does
/// nothing. The AI teams sign nobody yet: that is the AI principals' job (T44), so in an AI-only run the day handler finds no talks
/// and no deals and returns at once.
/// </summary>
public sealed class SponsorsModule : CareerModule
{
    public const string ModuleName = "sponsors";

    /// <summary>The same place in the day as the sponsor day handler: outcomes are applied before the board's (910) and the plain settling (990).</summary>
    public const int OutcomeOrder = SponsorDayHandler.DefaultOrder;

    public override string Name => ModuleName;

    public override IReadOnlyList<string> Sections => [SponsorsSection.SectionName];

    public override IReadOnlyList<CommandCodecEntry> CommandCodecs => SponsorCommandCodecs.Entries;

    public override void Attach(CareerModuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var inputs = context.Inputs;
        if (inputs.Sponsors is not { } catalog || inputs.SponsorEras is not { } eras || inputs.Eras is not { } finance)
        {
            return;
        }

        var session = context.Session;
        var book = SponsorBook.ForSession(session);
        var environment = new SponsorEnvironment(
            catalog,
            eras,
            finance,
            context.Require<IOrganizationControl>(),
            context.Require<IObjectiveFacts>(),
            outlook: new PublicTeamOutlook(() => session.World, ranking: inputs.RankKeys()));
        var inbox = context.Require<InboxBook>();
        context.Provide(book);
        context.Provide(environment);
        context.AddCommandHandler(new BeginSponsorTalksHandler(book, environment));
        context.AddCommandHandler(new SignAtCurrentTermsHandler(book, environment));
        context.AddCommandHandler(new WalkAwayFromTalksHandler(book, environment));
        context.AddCommandHandler(new RespondToSponsorOfferHandler(book, environment));
        context.Require<InboxResolvers>().Register(new SponsorOfferResolver(book, environment));
        context.AddDayHandler(new SponsorDayHandler(book, environment, inbox, context.Managers));
        context.AddAfterDay(OutcomeOrder, events => SponsorOutcomes.Apply(book, environment, events, inbox, context.Managers));
    }
}
