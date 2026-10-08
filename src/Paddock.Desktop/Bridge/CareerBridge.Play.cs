using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Paddock.Application.Board;
using Paddock.Application.Career;
using Paddock.Application.Managers;
using Paddock.Application.Commands;
using Paddock.Application.Contracts;
using Paddock.Application.Development;
using Paddock.Application.Infrastructure;
using Paddock.Application.Pool;
using Paddock.Application.Racing;
using Paddock.Application.Sponsors;
using Paddock.Application.Staff;
using Paddock.Application.Supply;
using Paddock.Data.Authored;
using Paddock.Data.Historical;
using Paddock.Data.World;
using Paddock.Domain.Career;
using Paddock.Domain.Contracts;
using Paddock.Domain.Development;
using Paddock.Domain.People;
using Paddock.Domain.Pool;
using Paddock.Domain.Supply;
using Paddock.Domain.Time;
using Paddock.Domain.World;
using Paddock.Persistence;
using Paddock.Simulation.Career;
using Paddock.Career;
using HostManagerId = Paddock.Application.Managers.ManagerId;

namespace Paddock.Desktop.Bridge;

public sealed partial class CareerBridge
{
    private CareerConfig? _config;
    private string? _worldDataHash;
    private string? _peopleNotice;
    private string _careerName = "career";
    private (string Schedule, string Drivers)? _peopleFiles;
    private Dictionary<string, CircuitLabel> _circuits = new(StringComparer.Ordinal);
    private int _notedSeason;
    private int _notedRound;
    private int _suggestedYear = DefaultYear;
    private ulong _suggestedSeed = DefaultSeed;

    private Dictionary<string, TrackFacts> _tracks = new(StringComparer.Ordinal);
    private string? _cardsKey;
    private IReadOnlyList<TeamCardView>? _cards;

    private IReadOnlyDictionary<string, CircuitLabel> Circuits => _circuits;

    private IReadOnlyDictionary<string, TrackFacts> Tracks => _tracks;

    private (string Root, int Before)? _pastKey;
    private IReadOnlyList<HistoricalRaceFact>? _past;

    /// <summary>
    /// Real races from before this career began, read once from the local cache (PP-041) and reduced to circuit and podium.
    /// Null without a cache, a data root or a career config: the circuit page then has no real history and hides that part.
    /// </summary>
    private IReadOnlyList<HistoricalRaceFact>? PastRaces()
    {
        if (_dataRoot is null || _config is null)
        {
            return null;
        }

        var key = (_dataRoot, _config.StartYear);
        if (_pastKey != key)
        {
            _past = PastPodiumLoader.Load(_dataRoot, _config.StartYear)
                .Select(race => new HistoricalRaceFact(
                    race.Season,
                    race.CircuitId,
                    race.Places.Select(place => new PastPodiumView(place.Position, place.DriverName, place.Nationality, place.ConstructorId, place.ConstructorName)).ToArray()))
                .ToArray();
            _pastKey = key;
        }

        return _past is { Count: > 0 } ? _past : null;
    }

    /// <summary>
    /// A player command, or a career command (new, load, save). Null when this host does not own the name.
    /// A refusal is an error key and changes nothing.
    /// </summary>
    public PlayStep? Play(string name, JsonElement args)
    {
        switch (name)
        {
            case "newCareer":
                return Start(args);
            case "loadCareer":
                return Load(args);
            case "saveCareer":
                return Save(args);
            case "startQuickRace":
                return StartQuickRace(args);
            case "closeQuickRace":
                CloseQuickRace();
                return PlayStep.Ok(BridgeValues.ToNode(new CommandAck(true)), inbox: false);
            default:
                var command = Build(name, args, out var error);
                if (error is not null)
                {
                    return PlayStep.Fail(error);
                }

                if (command is null)
                {
                    return null;
                }

                if (!HasCareer)
                {
                    return PlayStep.Fail(TranslationMessage.Of(BridgeKeys.NoCareer));
                }

                var result = Submit(command);
                return result is CommandResult.Rejected rejected
                    ? PlayStep.Fail(rejected.Reason)
                    : PlayStep.Ok(BridgeValues.ToNode(new CommandAck(true)), inbox: true);
        }
    }

    private AdvanceOutcome AdvancePlay()
    {
        var play = _play ?? throw new InvalidOperationException("No career is open.");
        var before = play.Date.Year;
        play.Ready(play.Player);
        var step = play.Advance();
        if (step is AdvanceResult.Refused refused)
        {
            return new AdvanceOutcome(false, refused.Refusal.Reason, null);
        }

        int? raceSeason = null;
        int? raceRound = null;
        string? layout = null;
        if (Box.TryGet<RaceWatch>() is { Pending: true } watch && (watch.Season != _notedSeason || watch.Round != _notedRound))
        {
            _notedSeason = watch.Season;
            _notedRound = watch.Round;
            raceSeason = watch.Season;
            raceRound = watch.Round;
            layout = watch.LayoutId;
            OpenLiveRace(watch.Season, watch.Round);
        }

        return new AdvanceOutcome(true, null, DateText, play.Date.Year != before, raceSeason, raceRound, layout);
    }

    private SessionView ReadSession()
    {
        if (!HasCareer)
        {
            return new SessionView(false, HumanManagerId, null, null, null, _peopleNotice, _suggestedYear, SeedText(), PresetViews());
        }

        var team = ReadTeam();
        return new SessionView(true, Human.Value, DateText, team.OrganizationId, team.Name, _peopleNotice, _suggestedYear, SeedText(), PresetViews());
    }

    private static IReadOnlyList<PresetView> PresetViews() =>
        new[] { CareerPreset.MostHistorical, CareerPreset.Balanced, CareerPreset.Chaos }
            .Select(preset =>
            {
                var config = CareerConfig.FromPreset(preset);
                return new PresetView(
                    preset.ToString(),
                    config.PeopleSource.ToString(),
                    config.RulesSource.ToString(),
                    config.AiBehavior.ToString(),
                    config.HistoryStrength,
                    config.RandomnessLevel,
                    config.FatalityLevel.ToString(),
                    config.NoNumbers);
            })
            .ToArray();

    private TeamListView ReadTeams(JsonElement args)
    {
        var year = IntOf(args, "year") ?? DefaultYear;
        var root = RequireData();
        var data = AuthoredDataLoader.Load(root);
        var roster = WorldInitializer.PublicTeams(data, year);
        var cards = new Dictionary<string, TeamCardView>(StringComparer.Ordinal);
        TranslationMessage? problem = null;
        if (roster.Count > 0)
        {
            problem = Configure(args, roster[0].Id, year, out var config, out var dataRoot, out var files, out _);
            if (problem is null)
            {
                var seed = ULongOf(args, "seed") ?? _suggestedSeed;
                foreach (var card in PreviewCards(data, dataRoot, config, files, seed))
                {
                    cards[card.Id] = card;
                }
            }
        }

        // Last season's finish first, the way the paddock lists teams; teams with no known place follow, richest first, then by name.
        var teams = roster
            .Select(team => cards.TryGetValue(team.Id, out var card)
                ? new TeamOptionView(team.Id, team.Name, card.Drivers, card.Engine, card.Budget, card.LastSeason, card.Expected, card.FieldSize, card.BudgetCents, card.Levels)
                : new TeamOptionView(team.Id, team.Name, [], null, null, null, null, null, null, null))
            .OrderBy(team => team.LastSeason ?? int.MaxValue)
            .ThenByDescending(team => team.BudgetCents ?? 0L)
            .ThenBy(team => team.Name, StringComparer.Ordinal)
            .ToArray();
        return new TeamListView(year, teams, problem);
    }

    /// <summary>
    /// The public side of the world this setup would start in. The world is built the way <c>newCareer</c> builds it and thrown
    /// away: the page's session is untouched and no stream of it is drawn. One setup is remembered, because the page asks again
    /// whenever it redraws.
    /// </summary>
    private IReadOnlyList<TeamCardView> PreviewCards(
        AuthoredData data,
        string root,
        CareerConfig config,
        (string Schedule, string Drivers)? files,
        ulong seed)
    {
        // The people files are part of the key: the cards must never outlive the cache they were read from, or they would show
        // other names than the career that starts from the same setup.
        var people = files is { } named
            ? string.Join('+', named.Schedule, File.GetLastWriteTimeUtc(named.Schedule).Ticks, named.Drivers, File.GetLastWriteTimeUtc(named.Drivers).Ticks)
            : "none";
        var key = string.Join('|', root, config.StartYear.ToString(CultureInfo.InvariantCulture), config.PeopleSource, people, seed.ToString(CultureInfo.InvariantCulture));
        if (_cardsKey == key && _cards is not null)
        {
            return _cards;
        }

        try
        {
            // The same starting sources as newCareer (#271), so a card shows the car and the budget the career will start with.
            var starting = CareerData.LoadStartingSources(root, data, config.StartYear);
            var created = WorldInitializer.Create(config, data, CareerData.LoadProvider(files), seed, new WorldInitOptions(CarStrength: starting.CarStrength, Tiers: starting.Tiers));
            var tiers = starting.Tiers ?? TeamTiersLoader.ToSource(TeamTiersLoader.Load(root));
            var last = created.World.Organizations
                .Where(organization => organization.IsReal && tiers.PreviousPlaceOf(organization.Id, config.StartYear) is not null)
                .ToDictionary(organization => organization.Id.Value, organization => tiers.PreviousPlaceOf(organization.Id, config.StartYear)!.Value, StringComparer.Ordinal);
            var supplies = created.EngineSupplies
                .Select(link => new SupplyLink(link.Constructor, link.Supplier, link.EngineName, link.SupplyType))
                .ToArray();
            var cards = TeamCardsRead.Of(created.World, created.World.CurrentDate, supplies, tiers, last, starting.CarStrength, data.Facilities);
            _cardsKey = key;
            _cards = cards;
            return cards;
        }
        catch (WorldInitException)
        {
            return [];
        }
    }

    private SaveListView ReadSaves()
    {
        var directory = SavesDirectory();
        if (!Directory.Exists(directory))
        {
            return new SaveListView([]);
        }

        var items = new List<SaveListItem>();
        foreach (var path in Directory.EnumerateFiles(directory, "*.paddock").OrderBy(path => path, StringComparer.Ordinal))
        {
            try
            {
                using var save = SaveFile.Open(path);
                var meta = save.ReadMeta();
                items.Add(new SaveListItem(
                    Path.GetFileName(path),
                    meta.CurrentGameDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    meta.PlayerTeamId,
                    meta.CareerName,
                    File.GetLastWriteTimeUtc(path).ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture)));
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or InvalidOperationException)
            {
                continue;
            }
        }

        return new SaveListView(items);
    }

    private RaceResultView ReadRace(JsonElement args) =>
        ChampionshipRead.Result(Session, IntOf(args, "season"), IntOf(args, "round"), Access());

    private StaffListView ReadStaff()
    {
        var organization = Box.Require<BoardBook>().Section.OrganizationOf(Human.Value);
        if (organization is not OrganizationId id)
        {
            return new StaffListView([]);
        }

        return new StaffListView(StaffQuery.Of(Session.World, id, Session.Date));
    }

    private DriverProfileView ReadDriver(JsonElement args)
    {
        var organization = Box.Require<BoardBook>().Section.OrganizationOf(Human.Value);
        var person = TextOf(args, "personId") ?? "";
        if (organization is not OrganizationId id)
        {
            return DriverProfileRead.None(person);
        }

        return DriverProfileRead.Of(Session.World, id, Session.Date, person);
    }

    private ManagerProfileView ReadManager()
    {
        var organization = Box.Require<BoardBook>().Section.OrganizationOf(Human.Value);
        return organization is OrganizationId id ? ManagerProfileRead.Of(Session.World, id, Session.Date) : ManagerProfileRead.None;
    }

    private MarketView ReadMarket(Paddock.Application.Access.AccessContext access)
    {
        var organization = Box.Require<BoardBook>().Section.OrganizationOf(Human.Value);
        if (organization is not OrganizationId id)
        {
            return new MarketView([], []);
        }

        return MarketRead.Of(access, Contracts(), id, Session.Date);
    }

    /// <summary>
    /// The career's axes from a message: the preset, then the single axes over it. Shared by <c>newCareer</c> and the team cards,
    /// so the cards show the world the career will start in. A refusal is the error and nothing else changes.
    /// </summary>
    private TranslationMessage? Configure(
        JsonElement args,
        string team,
        int year,
        out CareerConfig config,
        out string root,
        out (string Schedule, string Drivers)? files,
        out string? notice,
        CareerPreset fallback = CareerPreset.Chaos)
    {
        config = null!;
        root = null!;
        files = null;
        notice = null;
        var presetText = TextOf(args, "preset") ?? fallback.ToString();
        if (!Enum.TryParse<CareerPreset>(presetText, ignoreCase: false, out var preset) || preset == CareerPreset.Custom || !Enum.IsDefined(preset))
        {
            return TranslationMessage.Of(PlayKeys.BadPreset, ("preset", presetText));
        }

        config = CareerConfig.FromPreset(preset).WithStartYear(year).WithPlayerTeam(team);
        if (TextOf(args, "people") is { } people)
        {
            if (!Enum.TryParse<PeopleSource>(people, ignoreCase: false, out var source) || !Enum.IsDefined(source))
            {
                return TranslationMessage.Of(PlayKeys.BadAxisValue, ("axis", "people"), ("value", people));
            }

            config = config.WithPeopleSource(source);
        }

        if (TextOf(args, "rules") is { } rules)
        {
            if (!Enum.TryParse<RulesSource>(rules, ignoreCase: false, out var source) || !Enum.IsDefined(source))
            {
                return TranslationMessage.Of(PlayKeys.BadAxisValue, ("axis", "rules"), ("value", rules));
            }

            config = config.WithRulesSource(source);
        }

        if (TextOf(args, "ai") is { } ai)
        {
            if (!Enum.TryParse<AiBehavior>(ai, ignoreCase: false, out var behavior) || !Enum.IsDefined(behavior))
            {
                return TranslationMessage.Of(PlayKeys.BadAxisValue, ("axis", "ai"), ("value", ai));
            }

            config = config.WithAiBehavior(behavior);
        }

        if (IntOf(args, "history") is int history)
        {
            config = config.WithHistoryStrength(history);
        }

        if (IntOf(args, "randomness") is int randomness)
        {
            config = config.WithRandomnessLevel(randomness);
        }

        if (TextOf(args, "fatality") is { } fatalityText)
        {
            if (!Enum.TryParse<FatalityLevel>(fatalityText, ignoreCase: false, out var fatality) || !Enum.IsDefined(fatality))
            {
                return TranslationMessage.Of(PlayKeys.BadFatality);
            }

            config = config.WithFatalityLevel(fatality);
        }

        if (args.TryGetProperty("noNumbers", out var numbers) && numbers.ValueKind == JsonValueKind.True)
        {
            config = config.WithNoNumbers(true);
        }
        else if (args.TryGetProperty("noNumbers", out numbers) && numbers.ValueKind == JsonValueKind.False)
        {
            config = config.WithNoNumbers(false);
        }

        var validation = config.Validate();
        if (!validation.IsValid)
        {
            return TranslationMessage.Of(validation.Errors[0].Code);
        }

        root = RequireData();
        files = CareerData.ResolveProviderFiles(root, null, null);
        notice = null;
        if (files is null && config.PeopleSource != PeopleSource.FullyGenerated)
        {
            config = config.WithPeopleSource(PeopleSource.FullyGenerated);
            var again = config.Validate();
            if (!again.IsValid)
            {
                return TranslationMessage.Of(again.Errors[0].Code);
            }

            notice = BridgeKeys.GeneratedPeople;
        }

        return null;
    }

    private PlayStep Start(JsonElement args)
    {
        var team = TextOf(args, "teamId");
        var given = TextOf(args, "givenName");
        var family = TextOf(args, "familyName");
        var nationality = TextOf(args, "nationality");
        var tilt = TextOf(args, "tilt");
        if (team is null || given is null || family is null || nationality is null || tilt is null)
        {
            return PlayStep.Fail(TranslationMessage.Of(BridgeKeys.BadMessage));
        }

        if (!PlayerPrincipal.TryTilt(tilt, out _))
        {
            return PlayStep.Fail(TranslationMessage.Of(PlayKeys.BadTilt, ("tilt", tilt)));
        }

        var year = IntOf(args, "year") ?? DefaultYear;
        var seed = ULongOf(args, "seed") ?? DefaultSeed;
        if (Configure(args, team, year, out var config, out var root, out var files, out var notice) is { } refused)
        {
            return PlayStep.Fail(refused);
        }

        try
        {
            var display = given + " " + family;
            var (shell, data, _) = OpenShell(config, root, files, seed, display);
            var today = new DateOnly(shell.Date.Year, shell.Date.Month, shell.Date.Day);
            var taken = shell.Submit(new TakeOverTeamCommand
            {
                ManagerId = shell.Player,
                IssuedOn = today,
                OrganizationId = team,
                GivenName = given,
                FamilyName = family,
                Nationality = nationality,
                Tilt = tilt,
            });
            if (taken is CommandResult.Rejected rejected)
            {
                return PlayStep.Fail(rejected.Reason);
            }

            shell.BeginDay();
            Install(shell, config, files, CareerData.HashWorldData(root, files), TextOf(args, "name") ?? display, notice, data);
            var own = shell.TeamOf(shell.Player);
            return PlayStep.Ok(
                BridgeValues.ToNode(new CareerStartedView(
                    shell.Player.Value,
                    shell.Date.ToString()!,
                    own?.Value ?? team,
                    shell.WorldHash,
                    notice)),
                inbox: true);
        }
        catch (WorldInitException ex)
        {
            return PlayStep.Fail(TranslationMessage.Of(ex.Code));
        }
    }

    /// <summary>
    /// A new career's world and shell, before anyone takes a team or lives a morning. <c>newCareer</c> and the quick race (#280)
    /// both start here, so a quick race is raced in exactly the world a career would open in.
    /// </summary>
    private static (CareerShell Shell, AuthoredData Data, WorldInitResult Created) OpenShell(
        CareerConfig config,
        string root,
        (string Schedule, string Drivers)? files,
        ulong seed,
        string display)
    {
        var data = AuthoredDataLoader.Load(root);
        var provider = CareerData.LoadProvider(files);
        var starting = CareerData.LoadStartingSources(root, data, config.StartYear);
        var created = WorldInitializer.Create(config, data, provider, seed, new WorldInitOptions(CarStrength: starting.CarStrength, Tiers: starting.Tiers));
        var arrivals = TalentIntakeSchedule.AfterStart(config, provider, created.World, seed);
        var session = new CareerSession(
            created.World,
            seed,
            created.TalentPool,
            arrivals,
            new CareerSessionOptions { LastSeasons = LastSeasons.From(provider) });
        var shell = CareerShell.Open(
            session,
            new CareerRunOptions { Inputs = CareerInputsLoader.Load(root, data, created.EngineSupplies, config, CareerData.LoadRaceDates(root), starting) },
            display,
            HumanManagerId);
        return (shell, data, created);
    }

    private PlayStep Load(JsonElement args)
    {
        var text = TextOf(args, "path") ?? TextOf(args, "name");
        if (text is null)
        {
            return PlayStep.Fail(TranslationMessage.Of(BridgeKeys.BadMessage));
        }

        var path = ResolveSave(text);
        if (!File.Exists(path))
        {
            return PlayStep.Fail(TranslationMessage.Of(PlayKeys.SaveFailed, ("reason", "missing")));
        }

        try
        {
            var root = RequireData();
            var files = CareerData.ResolveProviderFiles(root, null, null);
            var loaded = CareerSaveReader.Read(path);
            var now = CareerData.HashWorldData(root, files);
            if (!string.Equals(now, loaded.Meta.WorldDataHash, StringComparison.Ordinal))
            {
                return PlayStep.Fail(TranslationMessage.Of(PlayKeys.DataChanged, ("saved", loaded.Meta.WorldDataHash), ("now", now)));
            }

            var provider = CareerData.LoadProvider(files);
            var date = loaded.Session.World.CurrentDate;
            var standIn = loaded.Session.World.WithDate(date.IsSeasonStart ? GameDate.SeasonStart(date.Year - 1) : date);
            var arrivals = TalentIntakeSchedule.AfterStart(loaded.Meta.CareerConfig, provider, standIn, loaded.Meta.MasterSeed);
            var session = CareerSession.Resume(loaded.Session, arrivals, new CareerSessionOptions { LastSeasons = LastSeasons.From(provider) });
            HostManagerId? player = null;
            foreach (var manager in loaded.Host.Managers.All)
            {
                if (manager.Id.Value == HumanManagerId)
                {
                    player = manager.Id;
                    break;
                }
            }

            if (player is not HostManagerId human)
            {
                return PlayStep.Fail(TranslationMessage.Of(PlayKeys.NoCareer));
            }

            var data = AuthoredDataLoader.Load(root);
            var shell = CareerShell.Resume(
                session,
                loaded.Host,
                new CareerRunOptions
                {
                    Inputs = CareerInputsLoader.Load(
                        root,
                        data,
                        career: loaded.Meta.CareerConfig,
                        raceDates: CareerData.LoadRaceDates(root),
                        starting: CareerData.LoadStartingSources(root, data, session.OpenedYear)),
                },
                human);
            Install(shell, loaded.Meta.CareerConfig, files, now, loaded.Meta.CareerName, null, data);
            return PlayStep.Ok(
                BridgeValues.ToNode(new CareerStartedView(human.Value, shell.Date.ToString()!, loaded.Meta.PlayerTeamId, shell.WorldHash, null)),
                inbox: true);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or SaveNotResumableException or Paddock.Domain.Codec.UnknownTagException)
        {
            return PlayStep.Fail(TranslationMessage.Of(PlayKeys.SaveFailed, ("reason", ex.GetType().Name)));
        }
    }

    private PlayStep Save(JsonElement args)
    {
        if (_play is null || _config is null || _worldDataHash is null)
        {
            return PlayStep.Fail(TranslationMessage.Of(BridgeKeys.NoCareer));
        }

        if (!_play.QueueIsEmpty)
        {
            return PlayStep.Fail(TranslationMessage.Of(BridgeKeys.SaveNotMorning));
        }

        var name = TextOf(args, "name");
        if (name is null)
        {
            return PlayStep.Fail(TranslationMessage.Of(BridgeKeys.BadMessage));
        }

        var team = _play.TeamOf(_play.Player);
        if (team is not OrganizationId organization)
        {
            return PlayStep.Fail(TranslationMessage.Of(PlayKeys.SaveFailed, ("reason", "team")));
        }

        try
        {
            var path = SavePath(name);
            WriteSave(path, organization.Value);
            return PlayStep.Ok(BridgeValues.ToNode(new CareerSavedView(Path.GetFileName(path), _play.WorldHash)), inbox: false);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or UnstableSaveException or ArgumentException)
        {
            return PlayStep.Fail(TranslationMessage.Of(PlayKeys.SaveFailed, ("reason", ex.GetType().Name)));
        }
    }

    /// <summary>
    /// Writes the career next to its final name and moves it over: the writer refuses to touch an existing file, and a save
    /// that fails half way must not destroy the one it was replacing.
    /// </summary>
    private void WriteSave(string path, string team)
    {
        var temp = path + ".tmp";
        try
        {
            if (File.Exists(temp))
            {
                File.Delete(temp);
            }

            CareerSaveWriter.Write(temp, _play!.Session, _config!, team, _worldDataHash!, _careerName, _play.HostState);
            File.Move(temp, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temp))
            {
                File.Delete(temp);
            }
        }
    }

    private void Install(
        CareerShell shell,
        CareerConfig config,
        (string Schedule, string Drivers)? files,
        string worldDataHash,
        string careerName,
        string? notice,
        AuthoredData data)
    {
        _play = shell;
        CloseQuickRace();
        _fixedSession = null;
        _modules = null;
        _config = config;
        _peopleFiles = files;
        _worldDataHash = worldDataHash;
        _careerName = careerName;
        _peopleNotice = notice;
        _notedSeason = 0;
        _notedRound = 0;
        RememberCircuits(data);
    }

    private void RememberCircuits(AuthoredData data) => (_circuits, _tracks) = CircuitsOf(data);

    private static (Dictionary<string, CircuitLabel> Circuits, Dictionary<string, TrackFacts> Tracks) CircuitsOf(AuthoredData data)
    {
        var circuits = new Dictionary<string, CircuitLabel>(StringComparer.Ordinal);
        var tracks = new Dictionary<string, TrackFacts>(StringComparer.Ordinal);
        var shapes = new Dictionary<string, TrackGeometryFile>(StringComparer.Ordinal);
        foreach (var file in data.TrackGeometries)
        {
            shapes[file.LayoutId] = file;
        }

        foreach (var circuit in data.Circuits.Circuits)
        {
            foreach (var layout in circuit.Layouts)
            {
                circuits[layout.LayoutId] = new CircuitLabel(circuit.CircuitId, circuit.Name, circuit.Country);
                var points = shapes.TryGetValue(layout.LayoutId, out var shape)
                    ? shape.ControlPoints.Where(point => point.Length >= 2).Select(point => new TrackPointView(point[0], point[1])).ToArray()
                    : [];
                tracks[layout.LayoutId] = new TrackFacts(
                    layout.LayoutId,
                    circuit.CircuitId,
                    circuit.Name,
                    circuit.Country,
                    layout.LengthKm,
                    layout.Character.ToArray(),
                    points);
            }
        }

        return (circuits, tracks);
    }

    private ICommand? Build(string name, JsonElement args, out TranslationMessage? error)
    {
        error = null;
        var issued = HasCareer ? IssuedOn : default;
        switch (name)
        {
            case "openNegotiation":
                return Negotiation(args, issued, out error);
            case "submitOffer":
                return Offer(args, issued, out error);
            case "acceptCounter":
                return IdCommand(args, issued, static (manager, day, id) => new AcceptCounterOfferCommand { ManagerId = manager, IssuedOn = day, NegotiationId = id }, out error);
            case "walkAway":
                return IdCommand(args, issued, static (manager, day, id) => new WalkAwayCommand { ManagerId = manager, IssuedOn = day, NegotiationId = id }, out error);
            case "renewContract":
                return Renew(args, issued, out error);
            case "beginSponsorTalks":
                return SponsorBegin(args, issued, out error);
            case "signSponsor":
                return SponsorTalk(args, issued, static (manager, day, org, talk) => new SignAtCurrentTermsCommand { ManagerId = manager, IssuedOn = day, OrganizationId = org, TalkId = talk }, out error);
            case "walkAwayFromTalks":
                return SponsorTalk(args, issued, static (manager, day, org, talk) => new WalkAwayFromTalksCommand { ManagerId = manager, IssuedOn = day, OrganizationId = org, TalkId = talk }, out error);
            case "respondToSponsorOffer":
                return SponsorResponse(args, issued, out error);
            case "setDevelopmentSplit":
                return Split(args, issued, out error);
            case "commitConcept":
                return Concept(args, issued, out error);
            case "upgradeFacility":
                return Facility(args, issued, out error);
            case "cancelTest":
                return CancelTest(args, issued, out error);
            case "bookTest":
                return RentTest(args, issued, out error);
            case "assignScoutFocus":
                error = null;
                return new AssignScoutFocusCommand { ManagerId = Human, IssuedOn = issued, PersonHandle = TextOf(args, "personHandle") };
            case "fundJunior":
                return Junior(args, issued, out error);
            case "signPoolDriver":
                return PoolSign(args, issued, out error);
            case "proposeSupply":
                return SupplyProposal(args, issued, out error);
            case "respondToSupply":
                return SupplyResponse(args, issued, out error);
            default:
                return null;
        }
    }

    private ICommand? Negotiation(JsonElement args, DateOnly issued, out TranslationMessage? error)
    {
        error = null;
        var organization = TextOf(args, "organizationId");
        var person = TextOf(args, "personId");
        var subject = TextOf(args, "subject");
        if (organization is null || person is null || subject is null || !TrySubject(subject, out var parsed))
        {
            error = TranslationMessage.Of(BridgeKeys.BadMessage);
            return null;
        }

        DateOnly? deadline = null;
        if (TextOf(args, "deadline") is { } text && DateOnly.TryParse(text, CultureInfo.InvariantCulture, out var date))
        {
            deadline = date;
        }

        return new OpenNegotiationCommand
        {
            ManagerId = Human,
            IssuedOn = issued,
            Organization = Organization(organization),
            Person = Person(person),
            Subject = parsed,
            Deadline = deadline,
        };
    }

    private ICommand? Offer(JsonElement args, DateOnly issued, out TranslationMessage? error)
    {
        var id = TextOf(args, "negotiationId");
        var terms = TermsOf(args, out error);
        if (id is null || terms is null)
        {
            error ??= TranslationMessage.Of(BridgeKeys.BadMessage);
            return null;
        }

        return new SubmitOfferCommand { ManagerId = Human, IssuedOn = issued, NegotiationId = id, Terms = terms };
    }

    private ICommand? Renew(JsonElement args, DateOnly issued, out TranslationMessage? error)
    {
        error = null;
        var id = TextOf(args, "contractId");
        if (id is null)
        {
            error = TranslationMessage.Of(BridgeKeys.BadMessage);
            return null;
        }

        var exercise = args.TryGetProperty("exerciseOption", out var flag) && flag.ValueKind == JsonValueKind.True;
        OfferTerms? terms = null;
        if (!exercise)
        {
            terms = TermsOf(args, out error);
            if (terms is null)
            {
                error ??= TranslationMessage.Of(BridgeKeys.BadMessage);
                return null;
            }
        }

        return new RenewContractCommand
        {
            ManagerId = Human,
            IssuedOn = issued,
            Contract = Contract(id),
            ExerciseOption = exercise,
            Offer = terms,
        };
    }

    private ICommand? SponsorBegin(JsonElement args, DateOnly issued, out TranslationMessage? error)
    {
        error = null;
        var organization = TextOf(args, "organizationId");
        var sponsor = TextOf(args, "sponsorId");
        var slot = IntOf(args, "slot");
        if (organization is null || sponsor is null || slot is null)
        {
            error = TranslationMessage.Of(BridgeKeys.BadMessage);
            return null;
        }

        return new BeginSponsorTalksCommand
        {
            ManagerId = Human,
            IssuedOn = issued,
            OrganizationId = organization,
            SponsorId = sponsor,
            Slot = slot.Value,
        };
    }

    private ICommand? SponsorTalk(JsonElement args, DateOnly issued, Func<HostManagerId, DateOnly, string, string, ICommand> build, out TranslationMessage? error)
    {
        error = null;
        var organization = TextOf(args, "organizationId");
        var talk = TextOf(args, "talkId");
        if (organization is null || talk is null)
        {
            error = TranslationMessage.Of(BridgeKeys.BadMessage);
            return null;
        }

        return build(Human, issued, organization, talk);
    }

    private ICommand? SponsorResponse(JsonElement args, DateOnly issued, out TranslationMessage? error)
    {
        error = null;
        var organization = TextOf(args, "organizationId");
        var offer = TextOf(args, "offerId");
        if (organization is null || offer is null || !args.TryGetProperty("accept", out var accept) || accept.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            error = TranslationMessage.Of(BridgeKeys.BadMessage);
            return null;
        }

        return new RespondToSponsorOfferCommand
        {
            ManagerId = Human,
            IssuedOn = issued,
            OrganizationId = organization,
            OfferId = offer,
            Accept = accept.ValueKind == JsonValueKind.True,
        };
    }

    private ICommand? Split(JsonElement args, DateOnly issued, out TranslationMessage? error)
    {
        error = null;
        var organization = TextOf(args, "organizationId");
        if (organization is null
            || IntOf(args, "currentPercent") is not int current
            || IntOf(args, "accountPercent") is not int account
            || IntOf(args, "nextYearPercent") is not int next)
        {
            error = TranslationMessage.Of(BridgeKeys.BadMessage);
            return null;
        }

        return new SetDevelopmentSplitCommand
        {
            ManagerId = Human,
            IssuedOn = issued,
            OrganizationId = organization,
            CurrentPercent = current,
            AccountPercent = account,
            NextYearPercent = next,
            AeroPriority = IntOf(args, "aeroPriority") ?? DevelopmentEstimates.DefaultPriority,
            ChassisPriority = IntOf(args, "chassisPriority") ?? DevelopmentEstimates.DefaultPriority,
            ReliabilityPriority = IntOf(args, "reliabilityPriority") ?? DevelopmentEstimates.DefaultPriority,
            TyresPriority = IntOf(args, "tyresPriority") ?? DevelopmentEstimates.DefaultPriority,
        };
    }

    private ICommand? Facility(JsonElement args, DateOnly issued, out TranslationMessage? error)
    {
        error = null;
        var organization = TextOf(args, "organizationId");
        var kind = TextOf(args, "kind");
        if (organization is null || kind is null)
        {
            error = TranslationMessage.Of(BridgeKeys.BadMessage);
            return null;
        }

        return new UpgradeFacilityCommand { ManagerId = Human, IssuedOn = issued, OrganizationId = organization, Kind = kind };
    }

    private ICommand? RentTest(JsonElement args, DateOnly issued, out TranslationMessage? error)
    {
        error = null;
        var organization = TextOf(args, "organizationId");
        if (organization is null)
        {
            error = TranslationMessage.Of(BridgeKeys.BadMessage);
            return null;
        }

        return new BookTestCommand { ManagerId = Human, IssuedOn = issued, OrganizationId = organization };
    }

    private ICommand? CancelTest(JsonElement args, DateOnly issued, out TranslationMessage? error)
    {
        error = null;
        var organization = TextOf(args, "organizationId");
        if (organization is null
            || TextOf(args, "testOn") is not { } text
            || !DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var on))
        {
            error = TranslationMessage.Of(BridgeKeys.BadMessage);
            return null;
        }

        return new CancelTestCommand { ManagerId = Human, IssuedOn = issued, OrganizationId = organization, TestOn = on };
    }

    private ICommand? Concept(JsonElement args, DateOnly issued, out TranslationMessage? error)
    {
        error = null;
        var organization = TextOf(args, "organizationId");
        var project = TextOf(args, "projectId");
        if (organization is null || project is null)
        {
            error = TranslationMessage.Of(BridgeKeys.BadMessage);
            return null;
        }

        return new CommitConceptCommand { ManagerId = Human, IssuedOn = issued, OrganizationId = organization, ProjectId = project };
    }

    private ICommand? Junior(JsonElement args, DateOnly issued, out TranslationMessage? error)
    {
        error = null;
        var person = TextOf(args, "personHandle");
        var programme = TextOf(args, "programme");
        if (person is null || programme is null || !Enum.TryParse<JuniorProgramme>(programme, ignoreCase: false, out var parsed) || !Enum.IsDefined(parsed))
        {
            error = TranslationMessage.Of(BridgeKeys.BadMessage);
            return null;
        }

        return new FundJuniorCommand { ManagerId = Human, IssuedOn = issued, PersonHandle = person, Programme = parsed };
    }

    private ICommand? PoolSign(JsonElement args, DateOnly issued, out TranslationMessage? error)
    {
        error = null;
        var person = TextOf(args, "personHandle");
        var role = TextOf(args, "role");
        if (person is null || role is null || !Enum.TryParse<PoolSigningRole>(role, ignoreCase: false, out var parsed) || !Enum.IsDefined(parsed))
        {
            error = TranslationMessage.Of(BridgeKeys.BadMessage);
            return null;
        }

        return new SignPoolDriverCommand { ManagerId = Human, IssuedOn = issued, PersonHandle = person, Role = parsed };
    }

    private ICommand? SupplyProposal(JsonElement args, DateOnly issued, out TranslationMessage? error)
    {
        error = null;
        var organization = TextOf(args, "organizationId");
        var supplier = TextOf(args, "supplierId");
        var item = TextOf(args, "item");
        var kind = TextOf(args, "kind");
        if (organization is null || supplier is null || item is null || kind is null
            || !Enum.TryParse<SupplyItem>(item, ignoreCase: false, out var parsedItem) || !Enum.IsDefined(parsedItem)
            || !Enum.TryParse<SupplyKind>(kind, ignoreCase: false, out var parsedKind) || !Enum.IsDefined(parsedKind)
            || IntOf(args, "firstSeason") is not int season
            || LongOf(args, "annualPriceCents") is not long price
            || IntOf(args, "seasons") is not int seasons)
        {
            error = TranslationMessage.Of(BridgeKeys.BadMessage);
            return null;
        }

        var exclusive = args.TryGetProperty("exclusive", out var flag) && flag.ValueKind == JsonValueKind.True;
        return new ProposeSupplyDealCommand
        {
            ManagerId = Human,
            IssuedOn = issued,
            OrganizationId = organization,
            SupplierId = supplier,
            Item = parsedItem,
            Kind = parsedKind,
            FirstSeason = season,
            AnnualPriceCents = price,
            Seasons = seasons,
            Exclusive = exclusive,
            NegotiationId = TextOf(args, "negotiationId") ?? "",
        };
    }

    private ICommand? SupplyResponse(JsonElement args, DateOnly issued, out TranslationMessage? error)
    {
        error = null;
        var organization = TextOf(args, "organizationId");
        var negotiation = TextOf(args, "negotiationId");
        if (organization is null || negotiation is null || !args.TryGetProperty("accept", out var accept) || accept.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            error = TranslationMessage.Of(BridgeKeys.BadMessage);
            return null;
        }

        return new RespondToSupplyOfferCommand
        {
            ManagerId = Human,
            IssuedOn = issued,
            OrganizationId = organization,
            NegotiationId = negotiation,
            Accept = accept.ValueKind == JsonValueKind.True,
        };
    }

    private ICommand? IdCommand(JsonElement args, DateOnly issued, Func<HostManagerId, DateOnly, string, ICommand> build, out TranslationMessage? error)
    {
        error = null;
        var id = TextOf(args, "negotiationId");
        if (id is null)
        {
            error = TranslationMessage.Of(BridgeKeys.BadMessage);
            return null;
        }

        return build(Human, issued, id);
    }

    private static OfferTerms? TermsOf(JsonElement args, out TranslationMessage? error)
    {
        error = null;
        if (LongOf(args, "salary") is not long salary || IntOf(args, "years") is not int years)
        {
            error = TranslationMessage.Of(BridgeKeys.BadMessage);
            return null;
        }

        SeatStatus? seat = null;
        if (TextOf(args, "seat") is { } seatText)
        {
            if (!Enum.TryParse<SeatStatus>(seatText, ignoreCase: false, out var parsed) || !Enum.IsDefined(parsed))
            {
                error = TranslationMessage.Of(BridgeKeys.BadMessage);
                return null;
            }

            seat = parsed;
        }

        OfferOption? option = null;
        if (TextOf(args, "optionHolder") is { } holderText)
        {
            if (!Enum.TryParse<OptionHolder>(holderText, ignoreCase: false, out var holder) || !Enum.IsDefined(holder) || IntOf(args, "optionYears") is not int extra)
            {
                error = TranslationMessage.Of(BridgeKeys.BadMessage);
                return null;
            }

            option = new OfferOption(holder, extra);
        }

        ExitClause? exit = null;
        if (IntOf(args, "exitWorseThan") is int worse)
        {
            exit = new ExitClause(worse);
        }

        return new OfferTerms(salary, LongOf(args, "pointsBonus") ?? 0, LongOf(args, "winBonus") ?? 0, LongOf(args, "titleBonus") ?? 0, years, seat, option, exit);
    }

    private static bool TrySubject(string text, out NegotiationSubject subject)
    {
        if (text == "driver")
        {
            subject = NegotiationSubject.DriverSeat;
            return true;
        }

        const string prefix = "staff:";
        if (text.StartsWith(prefix, StringComparison.Ordinal)
            && Enum.TryParse<StaffRole>(text[prefix.Length..], ignoreCase: false, out var role)
            && Enum.IsDefined(role))
        {
            subject = NegotiationSubject.Staff(role);
            return true;
        }

        subject = default;
        return false;
    }

    private static OrganizationId Organization(string text) =>
        text.StartsWith("org:", StringComparison.Ordinal) && long.TryParse(text.AsSpan(4), NumberStyles.None, CultureInfo.InvariantCulture, out var sequence)
            ? OrganizationId.Generated(sequence)
            : OrganizationId.Real(text);

    private static PersonId Person(string text) =>
        text.StartsWith("gen:", StringComparison.Ordinal) && long.TryParse(text.AsSpan(4), NumberStyles.None, CultureInfo.InvariantCulture, out var sequence)
            ? PersonId.Generated(sequence)
            : PersonId.Real(text);

    private static ContractId Contract(string text)
    {
        if (!text.StartsWith("con:", StringComparison.Ordinal) || !long.TryParse(text.AsSpan(4), NumberStyles.None, CultureInfo.InvariantCulture, out var sequence))
        {
            throw new ArgumentException("Contract id '" + text + "' is not a contract.");
        }

        return ContractId.Generated(sequence);
    }

    private string SeedText() => _suggestedSeed.ToString(CultureInfo.InvariantCulture);

    private string RequireData() => _dataRoot ?? throw new InvalidOperationException("The bridge has no data root.");

    private string SavesDirectory()
    {
        var data = RequireData();
        var parent = Directory.GetParent(data)?.FullName ?? data;
        return Path.Combine(parent, "saves");
    }

    private string SavePath(string name)
    {
        var file = Path.GetFileName(name);
        if (!string.Equals(file, name, StringComparison.Ordinal) || file.Contains("..", StringComparison.Ordinal))
        {
            throw new ArgumentException("Save name '" + name + "' is not a file name.");
        }

        if (!file.EndsWith(".paddock", StringComparison.Ordinal))
        {
            file += ".paddock";
        }

        var directory = SavesDirectory();
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, file);
    }

    private string ResolveSave(string text)
    {
        if (Path.IsPathRooted(text))
        {
            return text;
        }

        var file = text.EndsWith(".paddock", StringComparison.Ordinal) ? text : text + ".paddock";
        return Path.Combine(SavesDirectory(), Path.GetFileName(file));
    }

    private static string? TextOf(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var text = value.GetString();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    private static int? IntOf(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var number))
        {
            return null;
        }

        return number;
    }

    private static long? LongOf(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Number || !value.TryGetInt64(out var number))
        {
            return null;
        }

        return number;
    }

    private static ulong? ULongOf(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Number || !value.TryGetUInt64(out var number))
        {
            return null;
        }

        return number;
    }
}

/// <summary>What one player command produced for the bridge host.</summary>
public sealed record PlayStep(JsonNode? Data, TranslationMessage? Error, bool Inbox)
{
    public static PlayStep Ok(JsonNode? data, bool inbox) => new(data, null, inbox);

    public static PlayStep Fail(TranslationMessage error) => new(null, error, false);
}
