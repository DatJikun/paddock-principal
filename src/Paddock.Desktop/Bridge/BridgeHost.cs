using System.Globalization;
using System.Text.Json;
using Paddock.Application.Career;
using Paddock.Application.Commands;
using Paddock.Application.Inbox;
using Paddock.Persistence;
using Paddock.SimRunner;

namespace Paddock.Desktop.Bridge;

/// <summary>What one page message produced: the reply, then zero or more pushes.</summary>
public sealed record BridgeExchange(string Response, IReadOnlyList<string> Events);

/// <summary>
/// The JSON bridge (TECH §1.2). The page sends <c>{ kind, name, args, id }</c> and receives
/// <c>{ id, ok, data | error }</c>. Pushes are separate messages with a <c>type</c>.
/// The window starts with no career. <c>startCareer</c> and <c>loadCareer</c> open one.
/// </summary>
public sealed class BridgeHost
{
    private readonly string _dataRoot;
    private readonly string _saves;
    private readonly int _suggestedYear;
    private readonly ulong _suggestedSeed;
    private readonly object _gate = new();
    private CareerBridge? _career;

    public BridgeHost(string dataRoot, int suggestedYear, ulong suggestedSeed, string savesDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(savesDirectory);
        _dataRoot = dataRoot;
        _suggestedYear = suggestedYear;
        _suggestedSeed = suggestedSeed;
        _saves = savesDirectory;
    }

    public static BridgeHost Open(
        string? dataRoot = null,
        int suggestedYear = CareerBridge.DefaultYear,
        ulong suggestedSeed = CareerBridge.DefaultSeed,
        string? savesDirectory = null)
    {
        var root = dataRoot ?? Path.Combine(RepositoryRoot(), "data");
        var saves = savesDirectory ?? Path.Combine(Directory.GetParent(Path.GetFullPath(root))?.FullName ?? root, "saves");
        return new BridgeHost(root, suggestedYear, suggestedSeed, saves);
    }

    public string StateHash
    {
        get
        {
            lock (_gate)
            {
                return _career?.StateHash ?? string.Empty;
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

            if (!string.Equals(managerId, CareerBridge.HumanManagerId, StringComparison.Ordinal)
                || (_career is not null && !_career.IsHuman(managerId)))
            {
                return Fail(
                    id,
                    TranslationKeys.ManagerUnknown,
                    new Dictionary<string, string> { ["managerId"] = managerId });
            }

            if (endpoint.Kind == BridgeRegistry.Query)
            {
                return Query(id, name, args);
            }

            return Command(id, name, args);
        }
    }

    private BridgeExchange Query(string id, string name, JsonElement args)
    {
        switch (name)
        {
            case "session":
                return new BridgeExchange(BridgeValues.Response(id, BridgeValues.ToNode(Session())), []);
            case "teams":
                var year = BridgeText.Int(args, "year") ?? _suggestedYear;
                try
                {
                    return new BridgeExchange(
                        BridgeValues.Response(id, BridgeValues.ToNode(new TeamListView(year, CareerLaunch.Teams(_dataRoot, year)))),
                        []);
                }
                catch (ArgumentOutOfRangeException)
                {
                    return Fail(id, PlayKeys.BadYear, null);
                }
            case "saves":
                return new BridgeExchange(BridgeValues.Response(id, BridgeValues.ToNode(new SaveListView(ListSaves()))), []);
            default:
                if (_career is null)
                {
                    return Fail(id, PlayKeys.NoCareer, null);
                }

                return new BridgeExchange(BridgeValues.Response(id, _career.Query(name)), []);
        }
    }

    private BridgeExchange Command(string id, string name, JsonElement args)
    {
        switch (name)
        {
            case "startCareer":
                return OpenCareer(id, args);
            case "loadCareer":
                return LoadCareer(id, args);
            case "saveCareer":
                return SaveCareer(id, args);
        }

        if (_career is null)
        {
            return Fail(id, PlayKeys.NoCareer, null);
        }

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
                if (advance.SeasonChanged)
                {
                    events.Add(BridgeValues.Event("seasonChanged", BridgeValues.ToNode(new AdvanceDayView(advance.Date!))));
                }

                return new BridgeExchange(BridgeValues.Response(id, BridgeValues.ToNode(new AdvanceDayView(advance.Date!))), events);
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
                if (!BridgeActions.TryBuild(name, args, _career, out var command, out var key) || command is null)
                {
                    return Fail(id, key ?? BridgeKeys.BadMessage, null);
                }

                return Finish(id, _career.Submit(command));
        }
    }

    private BridgeExchange OpenCareer(string id, JsonElement args)
    {
        if (_career is not null)
        {
            return Fail(id, BridgeKeys.AlreadyOpen, null);
        }

        var preset = Text(args, "preset");
        var fatality = Text(args, "fatality");
        var team = Text(args, "teamId");
        var given = Text(args, "givenName");
        var family = Text(args, "familyName");
        var nationality = Text(args, "nationality");
        var tilt = Text(args, "tilt");
        if (preset is null || fatality is null || team is null || given is null || family is null || nationality is null || tilt is null)
        {
            return Fail(id, BridgeKeys.BadMessage, null);
        }

        var year = BridgeText.Int(args, "year") ?? _suggestedYear;
        var seedText = Text(args, "seed");
        var seed = _suggestedSeed;
        if (seedText is not null && !ulong.TryParse(seedText, NumberStyles.None, CultureInfo.InvariantCulture, out seed))
        {
            return Fail(id, BridgeKeys.BadMessage, null);
        }

        var opened = CareerLaunch.Start(
            _dataRoot,
            new CareerStartRequest(preset, year, fatality, team, given, family, nationality, tilt, seed, Text(args, "careerName") ?? "career"));
        return AcceptOpened(id, opened);
    }

    private BridgeExchange LoadCareer(string id, JsonElement args)
    {
        if (_career is not null)
        {
            return Fail(id, BridgeKeys.AlreadyOpen, null);
        }

        if (!TrySavePath(Text(args, "name"), out var path))
        {
            return Fail(id, BridgeKeys.BadSaveName, null);
        }

        if (!File.Exists(path))
        {
            return Fail(id, BridgeKeys.SaveMissing, null);
        }

        return AcceptOpened(id, CareerLaunch.Load(_dataRoot, path));
    }

    private BridgeExchange SaveCareer(string id, JsonElement args)
    {
        if (_career is null)
        {
            return Fail(id, PlayKeys.NoCareer, null);
        }

        if (!TrySavePath(Text(args, "name"), out var path))
        {
            return Fail(id, BridgeKeys.BadSaveName, null);
        }

        if (!_career.Shell.QueueIsEmpty || _career.TeamId is not { } team)
        {
            return Fail(id, PlayKeys.SaveFailed, new Dictionary<string, string> { ["reason"] = "queue" });
        }

        try
        {
            Directory.CreateDirectory(_saves);
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            CareerSaveWriter.Write(path, _career.Shell.Session, _career.Config, team, _career.WorldDataHash, _career.CareerName, _career.Shell.HostState);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or UnstableSaveException)
        {
            return Fail(id, PlayKeys.SaveFailed, new Dictionary<string, string> { ["reason"] = ex.GetType().Name });
        }

        var slot = new SaveSlotView(Text(args, "name")!, _career.DateText, team, _career.CareerName);
        return new BridgeExchange(BridgeValues.Response(id, BridgeValues.ToNode(slot)), []);
    }

    private BridgeExchange AcceptOpened(string id, CareerOpenResult opened)
    {
        if (opened.Career is null)
        {
            return Fail(id, opened.Key ?? BridgeKeys.Internal, opened.Parameters);
        }

        _career = opened.Career;
        var view = new CareerOpenedView(
            _career.Human.Value,
            _career.DateText,
            _career.TeamId ?? string.Empty,
            _career.StateHash,
            _career.NoticeKey);
        return new BridgeExchange(
            BridgeValues.Response(id, BridgeValues.ToNode(view)),
            [BridgeValues.Event("inboxChanged", null)]);
    }

    private SessionView Session() => new(
        _career is not null,
        _suggestedYear,
        _suggestedSeed.ToString(CultureInfo.InvariantCulture),
        _career?.Human.Value,
        _career?.DateText,
        _career?.NoticeKey);

    private IReadOnlyList<SaveSlotView> ListSaves()
    {
        if (!Directory.Exists(_saves))
        {
            return [];
        }

        var slots = new List<SaveSlotView>();
        foreach (var path in Directory.EnumerateFiles(_saves, "*.paddock").OrderBy(path => path, StringComparer.Ordinal))
        {
            try
            {
                using var save = SaveFile.Open(path);
                var meta = save.ReadMeta();
                slots.Add(new SaveSlotView(
                    Path.GetFileNameWithoutExtension(path),
                    meta.CurrentGameDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    meta.PlayerTeamId,
                    meta.CareerName));
            }
            catch (InvalidOperationException)
            {
                // A file that is not a career save stays off the list.
            }
        }

        return slots;
    }

    private bool TrySavePath(string? name, out string path)
    {
        path = string.Empty;
        if (name is null || name.Length > 40 || !name.All(static letter => char.IsAsciiLetterOrDigit(letter) || letter is '-' or '_'))
        {
            return false;
        }

        path = Path.Combine(_saves, name + ".paddock");
        return true;
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
