using Paddock.Application.Board;
using Paddock.Application.Cars;
using Paddock.Application.Contracts;
using Paddock.Application.Development;
using Paddock.Application.Finance;
using Paddock.Application.Inbox;
using Paddock.Application.Infrastructure;
using Paddock.Application.Pool;
using Paddock.Application.Racing;
using Paddock.Application.Sponsors;
using Paddock.Application.Supply;

namespace Paddock.Desktop.Bridge;

/// <summary>
/// Names the page may send. The result type is the JSON shape <see cref="BridgeValues"/> writes.
/// </summary>
public static class BridgeRegistry
{
    public const string Query = "query";

    public const string Command = "command";

    public static IReadOnlyList<BridgeEndpoint> Endpoints { get; } =
    [
        Endpoint(Query, "shell", typeof(ManagerCall), typeof(ShellView)),
        Endpoint(Query, "inbox", typeof(ManagerCall), typeof(InboxView)),
        Endpoint(Query, "team", typeof(ManagerCall), typeof(OwnTeamView)),
        Endpoint(Query, "drivers", typeof(ManagerCall), typeof(DriversView)),
        Endpoint(Query, "cars", typeof(ManagerCall), typeof(ManagerCarRoster)),
        Endpoint(Query, "development", typeof(ManagerCall), typeof(DevelopmentOverview)),
        Endpoint(Query, "infrastructure", typeof(ManagerCall), typeof(InfrastructureOverview)),
        Endpoint(Query, "sponsors", typeof(ManagerCall), typeof(SponsorView)),
        Endpoint(Query, "finance", typeof(ManagerCall), typeof(FinanceView)),
        Endpoint(Query, "board", typeof(ManagerCall), typeof(BoardView)),
        Endpoint(Query, "pool", typeof(ManagerCall), typeof(PoolView)),
        Endpoint(Query, "supply", typeof(ManagerCall), typeof(ManagerSupplyView)),
        Endpoint(Query, "negotiations", typeof(ManagerCall), typeof(NegotiationsView)),
        Endpoint(Query, "session", typeof(ManagerCall), typeof(SessionView)),
        Endpoint(Query, "teams", typeof(TeamsCall), typeof(TeamListView)),
        Endpoint(Query, "saves", typeof(ManagerCall), typeof(SaveListView)),
        Endpoint(Query, "calendar", typeof(ManagerCall), typeof(CalendarView)),
        Endpoint(Query, "standings", typeof(ManagerCall), typeof(StandingsView)),
        Endpoint(Query, "raceResult", typeof(RaceResultCall), typeof(RaceResultView)),
        Endpoint(Query, "nextRace", typeof(ManagerCall), typeof(NextRaceView)),
        Endpoint(Query, "track", typeof(TrackCall), typeof(TrackView)),
        Endpoint(Query, "staff", typeof(ManagerCall), typeof(StaffListView)),
        Endpoint(Query, "market", typeof(ManagerCall), typeof(MarketView)),
        Endpoint(Query, "driver", typeof(DriverCall), typeof(DriverProfileView)),
        Endpoint(Query, "manager", typeof(ManagerCall), typeof(ManagerProfileView)),
        Endpoint(Query, "liveRace", typeof(ManagerCall), typeof(LiveRaceView)),
        Endpoint(Query, "liveFrames", typeof(LiveFramesCall), typeof(LiveFramesView)),
        Endpoint(Query, "liveClock", typeof(ManagerCall), typeof(LiveClockView)),
        Endpoint(Query, "quickRounds", typeof(QuickRoundsCall), typeof(QuickRoundsView)),
        Endpoint(Command, "advanceDay", typeof(ManagerCall), typeof(AdvanceDayView)),
        Endpoint(Command, "resolveInbox", typeof(ResolveInboxCall), typeof(CommandAck)),
        Endpoint(Command, "dismissInbox", typeof(DismissInboxCall), typeof(CommandAck)),
        Endpoint(Command, "newCareer", typeof(NewCareerCall), typeof(CareerStartedView)),
        Endpoint(Command, "loadCareer", typeof(LoadCareerCall), typeof(CareerStartedView)),
        Endpoint(Command, "saveCareer", typeof(SaveCareerCall), typeof(CareerSavedView)),
        Endpoint(Command, "openNegotiation", typeof(OpenNegotiationCall), typeof(CommandAck)),
        Endpoint(Command, "submitOffer", typeof(SubmitOfferCall), typeof(CommandAck)),
        Endpoint(Command, "acceptCounter", typeof(NegotiationIdCall), typeof(CommandAck)),
        Endpoint(Command, "walkAway", typeof(NegotiationIdCall), typeof(CommandAck)),
        Endpoint(Command, "renewContract", typeof(RenewContractCall), typeof(CommandAck)),
        Endpoint(Command, "beginSponsorTalks", typeof(BeginSponsorCall), typeof(CommandAck)),
        Endpoint(Command, "signSponsor", typeof(SponsorTalkCall), typeof(CommandAck)),
        Endpoint(Command, "walkAwayFromTalks", typeof(SponsorTalkCall), typeof(CommandAck)),
        Endpoint(Command, "respondToSponsorOffer", typeof(SponsorOfferCall), typeof(CommandAck)),
        Endpoint(Command, "setDevelopmentSplit", typeof(DevelopmentSplitCall), typeof(CommandAck)),
        Endpoint(Command, "commitConcept", typeof(CommitConceptCall), typeof(CommandAck)),
        Endpoint(Command, "upgradeFacility", typeof(UpgradeFacilityCall), typeof(CommandAck)),
        Endpoint(Command, "bookTest", typeof(BookTestCall), typeof(CommandAck)),
        Endpoint(Command, "assignScoutFocus", typeof(ScoutFocusCall), typeof(CommandAck)),
        Endpoint(Command, "fundJunior", typeof(FundJuniorCall), typeof(CommandAck)),
        Endpoint(Command, "signPoolDriver", typeof(SignPoolCall), typeof(CommandAck)),
        Endpoint(Command, "proposeSupply", typeof(SupplyProposalCall), typeof(CommandAck)),
        Endpoint(Command, "respondToSupply", typeof(SupplyResponseCall), typeof(CommandAck)),
        Endpoint(Command, "liveRaceControl", typeof(LiveRaceControlCall), typeof(LiveClockView)),
        Endpoint(Command, "liveRaceOrder", typeof(LiveRaceOrderCall), typeof(LiveClockView)),
        Endpoint(Command, "startQuickRace", typeof(QuickRaceCall), typeof(QuickRaceStartedView)),
        Endpoint(Command, "closeQuickRace", typeof(ManagerCall), typeof(CommandAck)),
    ];

    public static bool TryFind(string kind, string name, out BridgeEndpoint endpoint)
    {
        foreach (var candidate in Endpoints)
        {
            if (string.Equals(candidate.Kind, kind, StringComparison.Ordinal)
                && string.Equals(candidate.Name, name, StringComparison.Ordinal))
            {
                endpoint = candidate;
                return true;
            }
        }

        endpoint = null!;
        return false;
    }

    private static BridgeEndpoint Endpoint(string kind, string name, Type args, Type result) => new(kind, name, args, result);
}

/// <summary>One name on the bridge, with the C# types that fix its JSON shape.</summary>
public sealed class BridgeEndpoint
{
    public BridgeEndpoint(string kind, string name, Type argumentType, Type resultType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(argumentType);
        ArgumentNullException.ThrowIfNull(resultType);
        Kind = kind;
        Name = name;
        ArgumentType = argumentType;
        ResultType = resultType;
    }

    public string Kind { get; }

    public string Name { get; }

    public Type ArgumentType { get; }

    public Type ResultType { get; }
}
