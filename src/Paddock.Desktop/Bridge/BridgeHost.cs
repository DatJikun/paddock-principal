using System.Text.Json;
using Paddock.Application.Commands;
using Paddock.Application.Inbox;
using HostManagerId = Paddock.Application.Managers.ManagerId;

namespace Paddock.Desktop.Bridge;

/// <summary>What one page message produced: the reply, then zero or more pushes.</summary>
public sealed record BridgeExchange(string Response, IReadOnlyList<string> Events);

/// <summary>
/// The JSON bridge (TECH §1.2). The page sends <c>{ kind, name, args, id }</c> and receives
/// <c>{ id, ok, data | error }</c>. Pushes are separate messages with a <c>type</c>.
/// </summary>
public sealed class BridgeHost
{
    private readonly CareerBridge _career;
    private readonly object _gate = new();

    public BridgeHost(CareerBridge career)
    {
        ArgumentNullException.ThrowIfNull(career);
        _career = career;
    }

    public static BridgeHost Open(string? dataRoot = null, int year = CareerBridge.DefaultYear, ulong seed = CareerBridge.DefaultSeed) =>
        new(CareerBridge.Open(dataRoot ?? RepositoryRoot(), year, seed));

    public string StateHash
    {
        get
        {
            lock (_gate)
            {
                return _career.StateHash;
            }
        }
    }

    public BridgeExchange Handle(string json)
    {
        lock (_gate)
        {
            return HandleLocked(json);
        }
    }

    public static string RepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "strings", "en.json"))
                && Directory.Exists(Path.Combine(dir.FullName, "data", "authored")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not find the repository root from " + AppContext.BaseDirectory + ".");
    }

    private BridgeExchange HandleLocked(string json)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            return Fail("", BridgeKeys.BadMessage, null);
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return Fail("", BridgeKeys.BadMessage, null);
            }

            var id = Text(root, "id") ?? "";
            var kind = Text(root, "kind");
            var name = Text(root, "name");
            if (kind is null || name is null || !root.TryGetProperty("args", out var args) || args.ValueKind != JsonValueKind.Object)
            {
                return Fail(id, BridgeKeys.BadMessage, null);
            }

            if (!BridgeRegistry.TryFind(kind, name, out var endpoint))
            {
                return Fail(id, BridgeKeys.UnknownName, new Dictionary<string, string> { ["name"] = name });
            }

            var managerId = Text(args, "managerId");
            if (string.IsNullOrWhiteSpace(managerId))
            {
                return Fail(id, BridgeKeys.ManagerRequired, null);
            }

            if (!_career.IsHuman(managerId))
            {
                return Fail(
                    id,
                    TranslationKeys.ManagerUnknown,
                    new Dictionary<string, string> { ["managerId"] = managerId });
            }

            if (endpoint.Kind == BridgeRegistry.Query)
            {
                try
                {
                    return new BridgeExchange(BridgeValues.Response(id, _career.Query(name)), []);
                }
                catch (BridgeQueryException exception)
                {
                    return Fail(id, exception.Key, null);
                }
            }

            return Command(id, name, args);
        }
    }

    private BridgeExchange Command(string id, string name, JsonElement args)
    {
        switch (name)
        {
            case "advanceDay":
                var advance = _career.Advance();
                if (!advance.Advanced)
                {
                    return Fail(id, advance.Reason!.Key, Parameters(advance.Reason));
                }

                var events = new List<string>
                {
                    BridgeValues.Event("dayAdvanced", BridgeValues.ToNode(new AdvanceDayView(advance.Date!))),
                    BridgeValues.Event("inboxChanged", null),
                };
                if (_career.TakeFinishedRace(out var race))
                {
                    events.Add(BridgeValues.Event("raceFinished", BridgeValues.ToNode(race)));
                }

                return new BridgeExchange(
                    BridgeValues.Response(id, BridgeValues.ToNode(new AdvanceDayView(advance.Date!))),
                    events);
            case "resolveInbox":
                var itemId = Text(args, "itemId");
                var optionId = Text(args, "optionId");
                if (itemId is null || optionId is null)
                {
                    return Fail(id, BridgeKeys.BadMessage, null);
                }

                return Finish(
                    id,
                    _career.Submit(new ResolveInboxItemCommand
                    {
                        ManagerId = _career.Human,
                        IssuedOn = _career.IssuedOn,
                        ItemId = itemId,
                        OptionId = optionId,
                    }));
            case "dismissInbox":
                var dismissId = Text(args, "itemId");
                if (dismissId is null)
                {
                    return Fail(id, BridgeKeys.BadMessage, null);
                }

                return Finish(
                    id,
                    _career.Submit(new DismissInboxItemCommand
                    {
                        ManagerId = _career.Human,
                        IssuedOn = _career.IssuedOn,
                        ItemId = dismissId,
                    }));
            default:
                throw new InvalidOperationException("Command '" + name + "' is registered but not implemented.");
        }
    }

    private static BridgeExchange Finish(string id, CommandResult result)
    {
        if (result is CommandResult.Rejected rejected)
        {
            return Fail(id, rejected.Reason.Key, Parameters(rejected.Reason));
        }

        return new BridgeExchange(
            BridgeValues.Response(id, BridgeValues.ToNode(new CommandAck(true))),
            [BridgeValues.Event("inboxChanged", null)]);
    }

    private static BridgeExchange Fail(string id, string key, IReadOnlyDictionary<string, string>? parameters) =>
        new(BridgeValues.Failure(id, key, parameters), []);

    private static Dictionary<string, string> Parameters(TranslationMessage message)
    {
        var copy = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in message.Parameters)
        {
            copy[pair.Key] = pair.Value;
        }

        return copy;
    }

    private static string? Text(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var text = value.GetString();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }
}
