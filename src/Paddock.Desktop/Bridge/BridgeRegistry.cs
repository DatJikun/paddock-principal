using Paddock.Application.Board;
using Paddock.Application.Cars;
using Paddock.Application.Contracts;
using Paddock.Application.Development;
using Paddock.Application.Finance;
using Paddock.Application.Inbox;
using Paddock.Application.Pool;
using Paddock.Application.Sponsors;
using Paddock.Application.Supply;

namespace Paddock.Desktop.Bridge;

/// <summary>
/// Names the page may send. The result type is the JSON shape <see cref="BridgeValues"/> writes.
/// Standings and the calendar are not here: Application has no query for them yet.
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
        Endpoint(Query, "sponsors", typeof(ManagerCall), typeof(SponsorView)),
        Endpoint(Query, "finance", typeof(ManagerCall), typeof(FinanceView)),
        Endpoint(Query, "board", typeof(ManagerCall), typeof(BoardView)),
        Endpoint(Query, "pool", typeof(ManagerCall), typeof(PoolView)),
        Endpoint(Query, "supply", typeof(ManagerCall), typeof(ManagerSupplyView)),
        Endpoint(Query, "negotiations", typeof(ManagerCall), typeof(NegotiationsView)),
        Endpoint(Command, "advanceDay", typeof(ManagerCall), typeof(AdvanceDayView)),
        Endpoint(Command, "resolveInbox", typeof(ResolveInboxCall), typeof(CommandAck)),
        Endpoint(Command, "dismissInbox", typeof(DismissInboxCall), typeof(CommandAck)),
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
