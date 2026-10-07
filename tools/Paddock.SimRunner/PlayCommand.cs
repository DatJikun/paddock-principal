using System.Globalization;
using Paddock.Application.Access;
using Paddock.Career;
using Paddock.Application.Board;
using Paddock.Application.Career;
using Paddock.Application.Cars;
using Paddock.Application.Commands;
using Paddock.Application.Contracts;
using Paddock.Application.Development;
using Paddock.Application.Finance;
using Paddock.Application.Inbox;
using Paddock.Application.Localization;
using Paddock.Application.Managers;
using Paddock.Application.Objectives;
using Paddock.Application.Pool;
using Paddock.Application.Sponsors;
using Paddock.Data.Authored;
using Paddock.Data.Historical;
using Paddock.Data.World;
using Paddock.Domain.Career;
using Paddock.Domain.Racing;
using Paddock.Domain.Contracts;
using Paddock.Domain.Objectives;
using Paddock.Domain.Pool;
using Paddock.Domain.Time;
using Paddock.Domain.World;
using Paddock.Persistence;
using Paddock.Simulation.Career;
using AccessManagerId = Paddock.Application.Access.ManagerId;
using HostManagerId = Paddock.Application.Managers.ManagerId;

namespace Paddock.SimRunner;

/// <summary>
/// <c>play [--load file] [--lang en|pl] [--seed N] [--data-root dir] [--name text] [--autosave path] [--watch x10]</c>.
/// <c>--watch</c> replays a race through the playback scheduler at that speed. Without it, a race prints the result only.
/// A new career is a wizard on stdin (path A only: take over an existing team). After <c>start</c>, or after
/// <c>--load</c>, the same stdin is the shell. Every printed line is a translation key. The process draws no RNG;
/// the world is built from <c>--seed</c>.
/// </summary>
public static class PlayCommand
{
    public const string Name = "play";

    public static int Execute(string[] args, TextReader stdin, TextWriter stdout, TextWriter stderr) =>
        Execute(args, stdin, stdout, stderr, null);

    public static int Execute(string[] args, TextReader stdin, TextWriter stdout, TextWriter stderr, CollectingMissingKeySink? sink)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(stdin);
        ArgumentNullException.ThrowIfNull(stdout);
        ArgumentNullException.ThrowIfNull(stderr);

        if (args.Length == 0 || !string.Equals(args[0], Name, StringComparison.Ordinal))
        {
            stderr.WriteLine("Unknown command. Expected: play [--load <file>] [--lang en|pl] [--seed <N>]");
            return 1;
        }

        string? load = null;
        string? language = null;
        string? dataRoot = null;
        string? careerName = null;
        string? autosave = null;
        double? watch = null;
        ulong seed = 1;
        for (var i = 1; i < args.Length; i++)
        {
            var flag = args[i];
            if (flag is not ("--load" or "--lang" or "--seed" or "--data-root" or "--name" or "--autosave" or "--watch"))
            {
                stderr.WriteLine("Unknown argument: " + flag);
                return 1;
            }

            if (i + 1 >= args.Length)
            {
                stderr.WriteLine("Missing value for " + flag + ".");
                return 1;
            }

            var value = args[++i];
            switch (flag)
            {
                case "--load":
                    if (load is not null)
                    {
                        return Duplicate(stderr, flag);
                    }

                    load = value;
                    break;
                case "--lang":
                    if (language is not null)
                    {
                        return Duplicate(stderr, flag);
                    }

                    language = value;
                    break;
                case "--data-root":
                    if (dataRoot is not null)
                    {
                        return Duplicate(stderr, flag);
                    }

                    dataRoot = value;
                    break;
                case "--name":
                    if (careerName is not null)
                    {
                        return Duplicate(stderr, flag);
                    }

                    careerName = value;
                    break;
                case "--autosave":
                    if (autosave is not null)
                    {
                        return Duplicate(stderr, flag);
                    }

                    autosave = value;
                    break;
                case "--watch":
                    if (watch is not null)
                    {
                        return Duplicate(stderr, flag);
                    }

                    if (!TryWatch(value, out var speed))
                    {
                        stderr.WriteLine("Invalid --watch value: " + value);
                        return 1;
                    }

                    watch = speed;
                    break;
                default:
                    if (!ulong.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out seed))
                    {
                        stderr.WriteLine("Invalid --seed value: " + value);
                        return 1;
                    }

                    break;
            }
        }

        if (language is not null and not ("en" or "pl"))
        {
            stderr.WriteLine("--lang must be en or pl.");
            return 1;
        }

        var catalog = StringTable.LoadCatalog();
        sink ??= new CollectingMissingKeySink();
        var localizer = new Localizer(catalog, language == "pl" ? Language.Pl : Language.En, sink);
        var session = new PlaySession(stdin, stdout, localizer, sink, seed, careerName ?? "career", autosave, dataRoot, watch);
        var code = load is null ? session.NewCareer() : session.Load(load);
        if (sink.Reports.Count > 0)
        {
            foreach (var report in sink.Reports)
            {
                stderr.WriteLine(report.Kind + " " + report.Key + (report.Detail is null ? string.Empty : " " + report.Detail));
            }

            return 1;
        }

        return code;
    }

    private static bool TryWatch(string text, out double speed)
    {
        var body = text.StartsWith('x') || text.StartsWith('X') ? text[1..] : text;
        return double.TryParse(body, NumberStyles.Float, CultureInfo.InvariantCulture, out speed)
            && speed > 0
            && !double.IsInfinity(speed);
    }

    private static int Duplicate(TextWriter stderr, string flag)
    {
        stderr.WriteLine("Duplicate option: " + flag);
        return 1;
    }

    private sealed class PlaySession
    {
        private readonly TextReader _stdin;
        private readonly TextWriter _stdout;
        private readonly CollectingMissingKeySink _sink;
        private readonly ulong _seed;
        private readonly string _careerName;
        private readonly string? _autosave;
        private readonly string? _dataRootFlag;
        private readonly double? _watch;
        private Localizer _localizer;
        private CareerConfig? _config;
        private string? _dataRoot;
        private string? _worldHash;
        private (string Schedule, string Drivers)? _peopleFiles;

        public PlaySession(
            TextReader stdin,
            TextWriter stdout,
            Localizer localizer,
            CollectingMissingKeySink sink,
            ulong seed,
            string careerName,
            string? autosave,
            string? dataRoot,
            double? watch)
        {
            _stdin = stdin;
            _stdout = stdout;
            _localizer = localizer;
            _sink = sink;
            _seed = seed;
            _careerName = careerName;
            _autosave = autosave;
            _dataRootFlag = dataRoot;
            _watch = watch;
        }

        public int NewCareer()
        {
            var wizard = new Wizard();
            string? line;
            while ((line = Read()) is not null)
            {
                if (line.Length == 0 || line[0] == '#')
                {
                    continue;
                }

                var parts = Split(line);
                if (parts.Length == 0)
                {
                    continue;
                }

                if (wizard.Started)
                {
                    if (Shell(wizard.Shell!, wizard.Config!, parts) is int code)
                    {
                        return code;
                    }

                    continue;
                }

                if (!WizardLine(wizard, parts))
                {
                    return 1;
                }
            }

            Say(PlayKeys.Quit);
            return wizard.Started ? 0 : 1;
        }

        public int Load(string path)
        {
            try
            {
                if (!PrepareData())
                {
                    return 1;
                }

                var loaded = CareerSaveReader.Read(path);
                var now = RunCommand.HashWorldData(_dataRoot!, _peopleFiles);
                if (!string.Equals(now, loaded.Meta.WorldDataHash, StringComparison.Ordinal))
                {
                    Say(PlayKeys.DataChanged, ("saved", loaded.Meta.WorldDataHash), ("now", now));
                    return 1;
                }

                var provider = RunCommand.LoadProvider(_peopleFiles);
                var date = loaded.Session.World.CurrentDate;
                var standIn = loaded.Session.World.WithDate(date.IsSeasonStart ? GameDate.SeasonStart(date.Year - 1) : date);
                var arrivals = TalentIntakeSchedule.AfterStart(loaded.Meta.CareerConfig, provider, standIn, loaded.Meta.MasterSeed);
                var session = CareerSession.Resume(loaded.Session, arrivals, new CareerSessionOptions { LastSeasons = LastSeasons.From(provider) });
                HostManagerId? player = null;
                foreach (var manager in loaded.Host.Managers.All)
                {
                    if (manager.Kind == Paddock.Application.Managers.ManagerKind.Human
                        && string.Equals(manager.DisplayName, loaded.Meta.ManagerName, StringComparison.Ordinal))
                    {
                        player = manager.Id;
                        break;
                    }
                }

                if (player is null)
                {
                    var fallback = loaded.Host.Managers.All.FirstOrDefault(manager => manager.Kind == Paddock.Application.Managers.ManagerKind.Human);
                    if (fallback is not null)
                    {
                        player = fallback.Id;
                    }
                }

                if (player is not HostManagerId human || !human.IsAssigned)
                {
                    Say(PlayKeys.NoCareer);
                    return 1;
                }

                var data = AuthoredDataLoader.Load(_dataRoot!);
                var starting = CareerData.LoadStartingSources(_dataRoot!, data, session.OpenedYear);
                var shell = CareerShell.Resume(session, loaded.Host, new CareerRunOptions { Inputs = CareerInputsLoader.Load(_dataRoot!, data, career: loaded.Meta.CareerConfig, starting: starting) }, human);
                _config = loaded.Meta.CareerConfig;
                _worldHash = now;
                Say(PlayKeys.Loaded, ("date", DateText(shell.Date)), ("team", loaded.Meta.PlayerTeamId), ("hash", shell.WorldHash));
                return ShellLoop(shell, loaded.Meta.CareerConfig);
            }
            catch (Exception ex) when (ex is IOException or InvalidOperationException or SaveNotResumableException or Paddock.Domain.Codec.UnknownTagException)
            {
                Say(PlayKeys.SaveFailed, ("reason", ex.GetType().Name));
                return 1;
            }
        }

        private int ShellLoop(CareerShell shell, CareerConfig config)
        {
            string? line;
            while ((line = Read()) is not null)
            {
                if (line.Length == 0 || line[0] == '#')
                {
                    continue;
                }

                var parts = Split(line);
                if (parts.Length == 0)
                {
                    continue;
                }

                if (Shell(shell, config, parts) is int code)
                {
                    return code;
                }
            }

            Say(PlayKeys.Quit);
            return 0;
        }

        private bool WizardLine(Wizard wizard, string[] parts)
        {
            switch (parts[0])
            {
                case "lang":
                    if (parts.Length != 2 || parts[1] is not ("en" or "pl"))
                    {
                        Say(PlayKeys.BadLanguage);
                        return true;
                    }

                    _localizer = new Localizer(StringTable.LoadCatalog(), parts[1] == "pl" ? Language.Pl : Language.En, _sink);
                    return true;
                case "preset":
                    if (parts.Length != 2 || !TryPreset(parts[1], out var preset))
                    {
                        Say(PlayKeys.BadPreset, ("preset", parts.Length > 1 ? parts[1] : string.Empty));
                        return true;
                    }

                    wizard.Config = CareerConfig.FromPreset(preset);
                    return true;
                case "axis":
                    if (wizard.Config is null)
                    {
                        Say(PlayKeys.Missing, ("what", "preset"));
                        return true;
                    }

                    if (parts.Length != 3)
                    {
                        Say(PlayKeys.BadLine);
                        return true;
                    }

                    if (!TryAxis(wizard.Config, parts[1], parts[2], out var updated))
                    {
                        return true;
                    }

                    wizard.Config = updated;
                    if (parts[1] == "year")
                    {
                        wizard.YearSet = true;
                    }

                    if (parts[1] == "fatality")
                    {
                        wizard.FatalitySet = true;
                    }

                    if (parts[1] == "team" && !CareerStartPath.IsFounding(parts[2]))
                    {
                        wizard.Team = parts[2];
                    }

                    return true;
                case "year":
                    if (wizard.Config is null)
                    {
                        Say(PlayKeys.Missing, ("what", "preset"));
                        return true;
                    }

                    if (parts.Length != 2 || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var year))
                    {
                        Say(PlayKeys.BadYear);
                        return true;
                    }

                    wizard.Config = wizard.Config.WithStartYear(year);
                    wizard.YearSet = true;
                    return true;
                case "fatality":
                    if (wizard.Config is null)
                    {
                        Say(PlayKeys.Missing, ("what", "preset"));
                        return true;
                    }

                    if (parts.Length != 2 || !TryFatality(parts[1], out var fatality))
                    {
                        Say(PlayKeys.BadFatality);
                        return true;
                    }

                    wizard.Config = wizard.Config.WithFatalityLevel(fatality);
                    wizard.FatalitySet = true;
                    return true;
                case "tilt":
                    if (parts.Length != 2 || !PlayerPrincipal.TryTilt(parts[1], out _))
                    {
                        Say(PlayKeys.BadTilt, ("tilt", parts.Length > 1 ? parts[1] : string.Empty));
                        return true;
                    }

                    wizard.Tilt = parts[1];
                    return true;
                case "teams":
                    return ListTeams(wizard);
                case "team":
                    if (parts.Length != 2)
                    {
                        Say(PlayKeys.BadLine);
                        return true;
                    }

                    if (CareerStartPath.IsFounding(parts[1]))
                    {
                        Say(PlayKeys.OwnTeam);
                        return true;
                    }

                    wizard.Team = parts[1];
                    return true;
                case "player":
                    if (parts.Length != 4)
                    {
                        Say(BoardKeys.TakeOverBadName);
                        return true;
                    }

                    wizard.Given = parts[1];
                    wizard.Family = parts[2];
                    wizard.Nationality = parts[3];
                    return true;
                case "start":
                    return Start(wizard);
                case "help":
                    Help();
                    return true;
                default:
                    Say(PlayKeys.UnknownCommand, ("command", parts[0]));
                    return true;
            }
        }

        private bool ListTeams(Wizard wizard)
        {
            if (wizard.Config is null || !wizard.YearSet)
            {
                Say(PlayKeys.Missing, ("what", "year"));
                return true;
            }

            if (!PrepareData())
            {
                return false;
            }

            var data = AuthoredDataLoader.Load(_dataRoot!);
            foreach (var team in WorldInitializer.PublicTeams(data, wizard.Config.StartYear))
            {
                Say(PlayKeys.TeamLine, ("id", team.Id), ("name", team.Name));
            }

            return true;
        }

        private bool Start(Wizard wizard)
        {
            if (wizard.Config is null)
            {
                Say(PlayKeys.Missing, ("what", "preset"));
                return true;
            }

            if (!wizard.YearSet)
            {
                Say(PlayKeys.Missing, ("what", "year"));
                return true;
            }

            if (!wizard.FatalitySet)
            {
                Say(PlayKeys.Missing, ("what", "fatality"));
                return true;
            }

            if (wizard.Tilt is null)
            {
                Say(PlayKeys.Missing, ("what", "tilt"));
                return true;
            }

            if (wizard.Team is null || wizard.Given is null || wizard.Family is null || wizard.Nationality is null)
            {
                Say(PlayKeys.Missing, ("what", "team"));
                return true;
            }

            if (CareerStartPath.IsFounding(wizard.Team))
            {
                Say(PlayKeys.OwnTeam);
                return true;
            }

            var config = wizard.Config.WithPlayerTeam(wizard.Team);
            var validation = config.Validate();
            foreach (var error in validation.Errors)
            {
                SayIssue(error);
            }

            foreach (var warning in validation.Warnings)
            {
                SayIssue(warning);
            }

            if (!validation.IsValid)
            {
                return true;
            }

            if (!PrepareData())
            {
                return false;
            }

            try
            {
                var data = AuthoredDataLoader.Load(_dataRoot!);
                var provider = RunCommand.LoadProvider(_peopleFiles);
                var starting = CareerData.LoadStartingSources(_dataRoot!, data, config.StartYear);
                var created = WorldInitializer.Create(config, data, provider, _seed, new WorldInitOptions(CarStrength: starting.CarStrength, Tiers: starting.Tiers));
                var arrivals = TalentIntakeSchedule.AfterStart(config, provider, created.World, _seed);
                var session = new CareerSession(
                    created.World,
                    _seed,
                    created.TalentPool,
                    arrivals,
                    new CareerSessionOptions { LastSeasons = LastSeasons.From(provider) });
                var name = wizard.Given + " " + wizard.Family;
                var shell = CareerShell.Open(session, new CareerRunOptions { Inputs = CareerInputsLoader.Load(_dataRoot!, data, career: config, starting: starting) }, name);
                var today = new DateOnly(shell.Date.Year, shell.Date.Month, shell.Date.Day);
                var result = shell.Submit(new TakeOverTeamCommand
                {
                    ManagerId = shell.Player,
                    IssuedOn = today,
                    OrganizationId = wizard.Team,
                    GivenName = wizard.Given,
                    FamilyName = wizard.Family,
                    Nationality = wizard.Nationality,
                    Tilt = wizard.Tilt,
                });
                if (result is CommandResult.Rejected rejected)
                {
                    SayMessage(rejected.Reason);
                    return true;
                }

                shell.BeginDay();
                wizard.Shell = shell;
                wizard.Config = config;
                wizard.Started = true;
                _config = config;
                _worldHash = RunCommand.HashWorldData(_dataRoot!, _peopleFiles);
                Say(PlayKeys.Started, ("team", wizard.Team), ("date", DateText(shell.Date)), ("manager", name));
                Say(
                    PlayKeys.Estimates,
                    ("average", PlayerEstimates.Average.ToString(CultureInfo.InvariantCulture)),
                    ("tilt", PlayerEstimates.TiltBonus.ToString(CultureInfo.InvariantCulture)));
                return true;
            }
            catch (WorldInitException ex)
            {
                SayInit(ex);
                return true;
            }
        }

        private int? Shell(CareerShell shell, CareerConfig config, string[] parts)
        {
            switch (parts[0])
            {
                case "help":
                    Help();
                    return null;
                case "quit":
                    Say(PlayKeys.Quit);
                    return 0;
                case "hash":
                    Say(PlayKeys.Hash, ("hash", shell.WorldHash));
                    return null;
                case "save":
                    if (parts.Length != 2)
                    {
                        Say(PlayKeys.BadLine);
                        return null;
                    }

                    Save(shell, config, parts[1]);
                    return null;
                case "state":
                    State(shell);
                    return null;
                case "inbox":
                    if (parts.Length == 1)
                    {
                        Inbox(shell);
                        return null;
                    }

                    if (parts.Length == 4 && parts[1] == "resolve")
                    {
                        Submit(shell, new ResolveInboxItemCommand
                        {
                            ManagerId = shell.Player,
                            IssuedOn = Today(shell),
                            ItemId = parts[2],
                            OptionId = parts[3],
                        });
                        return null;
                    }

                    Say(PlayKeys.BadLine);
                    return null;
                case "market":
                    Market(shell, parts.Length == 2 ? parts[1] : null);
                    return null;
                case "negotiate":
                    Negotiate(shell, parts);
                    return null;
                case "pool":
                    Pool(shell);
                    return null;
                case "scout":
                    if (parts.Length != 2)
                    {
                        Say(PlayKeys.BadLine);
                        return null;
                    }

                    var scout = shell.Submit(new AssignScoutFocusCommand
                    {
                        ManagerId = shell.Player,
                        IssuedOn = Today(shell),
                        PersonHandle = parts[1] == "all" ? null : parts[1],
                    });
                    if (scout is CommandResult.Rejected scoutRejected)
                    {
                        SayMessage(scoutRejected.Reason);
                        return null;
                    }

                    Say(PlayKeys.ScoutSet, ("focus", parts[1]));
                    return null;
                case "car":
                    Cars(shell);
                    return null;
                case "dev":
                    Dev(shell, parts);
                    return null;
                case "sponsors":
                    Sponsors(shell);
                    return null;
                case "finance":
                    Finance(shell);
                    return null;
                case "board":
                    Board(shell);
                    return null;
                case "why":
                    Why(shell, parts.Length == 2 ? parts[1] : null);
                    return null;
                case "ready":
                    shell.Ready(shell.Player);
                    Say(PlayKeys.Ready);
                    return null;
                case "advance":
                    Advance(shell, parts);
                    return null;
                default:
                    Say(PlayKeys.UnknownCommand, ("command", parts[0]));
                    return null;
            }
        }

        private void Advance(CareerShell shell, string[] parts)
        {
            if (parts.Length == 2 && parts[1] == "until")
            {
                var until = shell.AdvanceUntilBlocked();
                if (until is AdvanceResult.Refused refusedUntil)
                {
                    SayMessage(refusedUntil.Refusal.Reason);
                    Say(PlayKeys.Stopped, ("date", DateText(shell.Date)));
                    return;
                }

                Say(PlayKeys.Advanced, ("date", DateText(shell.Date)));
                PrintRace(shell);
                Autosave(shell);
                return;
            }

            if (parts.Length != 1)
            {
                Say(PlayKeys.BadLine);
                return;
            }

            var result = shell.Advance();
            if (result is AdvanceResult.Refused refused)
            {
                SayMessage(refused.Refusal.Reason);
                foreach (var waiting in refused.Refusal.WaitingFor)
                {
                    SayMessage(waiting.Reason);
                }

                return;
            }

            Say(PlayKeys.Advanced, ("date", DateText(shell.Date)));
            PrintRace(shell);
            Autosave(shell);
        }

        private void PrintRace(CareerShell shell)
        {
            if (shell.Modules.TryGet<Paddock.Application.Racing.RaceWatch>() is not { } watch
                || !watch.TryTake(out _, out var round, out var layout, out var tape, out var lines, out var skipped))
            {
                return;
            }

            if (_watch is double speed && tape is not null)
            {
                Say(
                    PlayKeys.WatchHeader,
                    ("round", round.ToString(CultureInfo.InvariantCulture)),
                    ("speed", speed.ToString(CultureInfo.InvariantCulture)));
                var laps = 0;
                RacePlayback.Play(tape, speed, raceEvent =>
                {
                    switch (raceEvent)
                    {
                        case LapCompleted:
                            laps++;
                            break;
                        case PitStop pit:
                            Say(PlayKeys.WatchPit, ("driver", PersonName(shell, pit.DriverId)));
                            break;
                        case Incident:
                            Say(PlayKeys.WatchIncident);
                            break;
                        case Retirement retirement:
                            Say(
                                PlayKeys.WatchRetirement,
                                ("driver", PersonName(shell, retirement.DriverId)),
                                ("reason", _localizer.Get(ReasonKey(retirement.Reason))));
                            break;
                        case SafetyCar:
                            Say(PlayKeys.WatchSafety);
                            break;
                        case Finished finished:
                            Say(
                                PlayKeys.WatchFinished,
                                ("driver", PersonName(shell, finished.DriverId)),
                                ("position", finished.Position.ToString(CultureInfo.InvariantCulture)));
                            break;
                        case RaceEnded:
                            Say(PlayKeys.WatchEnded);
                            break;
                    }
                });
                Say(PlayKeys.WatchLaps, ("count", laps.ToString(CultureInfo.InvariantCulture)));
            }

            Say(
                PlayKeys.RaceRound,
                ("round", round.ToString(CultureInfo.InvariantCulture)),
                ("layout", layout));
            foreach (var line in lines)
            {
                Say(
                    PlayKeys.RaceRow,
                    ("position", line.Position.ToString(CultureInfo.InvariantCulture)),
                    ("driver", PersonName(shell, line.DriverId)),
                    ("team", TeamName(shell, line.TeamId)),
                    ("points", line.Points));
            }

            if (shell.TeamOf(shell.Player) is OrganizationId player)
            {
                foreach (var teamId in skipped)
                {
                    if (teamId == player.Value)
                    {
                        Say(PlayKeys.RaceSkipped, ("team", TeamName(shell, teamId)));
                    }
                }
            }
        }

        private static string ReasonKey(RetirementReason reason) => reason switch
        {
            RetirementReason.Mechanical => "race.status.mechanical",
            RetirementReason.Accident => "race.status.accident",
            _ => "race.status.other",
        };

        private static string PersonName(CareerShell shell, string id)
        {
            foreach (var person in shell.Session.World.Persons)
            {
                if (person.Id.Value == id)
                {
                    return person.Name;
                }
            }

            return id;
        }

        private static string TeamName(CareerShell shell, string id)
        {
            foreach (var organization in shell.Session.World.Organizations)
            {
                if (organization.Id.Value == id)
                {
                    return organization.NameOn(shell.Date);
                }
            }

            return id;
        }

        private void State(CareerShell shell)
        {
            Say(PlayKeys.StateDate, ("date", DateText(shell.Date)));
            var team = shell.TeamOf(shell.Player);
            if (team is not OrganizationId organization)
            {
                Say(PlayKeys.StateNoTeam);
                Say(PlayKeys.StateNoRace);
                return;
            }

            var name = shell.Session.World.GetOrganization(organization).NameOn(shell.Date);
            Say(PlayKeys.StateTeam, ("team", name));
            var access = View(shell.Player);
            var money = FinanceQuery.Read(access, organization, shell.Session.World, shell.Date, shell.Modules.Require<IOrganizationControl>());
            if (money is FinanceView.Own own)
            {
                Say(PlayKeys.StateCash, ("cash", Dollars(own.CashCents)));
                Say(PlayKeys.StateForecast, ("cash", Dollars(own.ForecastCashCents)));
            }
            else
            {
                Say(PlayKeys.FinanceUnknown);
            }

            var board = BoardView(shell);
            if (board.Own is { } mine && mine.Why.CurrentPosition is decimal current && mine.Why.ExpectedPosition is int expected)
            {
                Say(PlayKeys.StateStanding, ("current", current.ToString(CultureInfo.InvariantCulture)), ("expected", expected.ToString(CultureInfo.InvariantCulture)));
            }
            else
            {
                Say(PlayKeys.StateNoStanding);
            }

            var next = shell.Modules.TryGet<Paddock.Application.Development.INextRaceSource>()?.NextRaceOnOrAfter(organization, shell.Date);
            if (next is GameDate raceDay)
            {
                Say(PlayKeys.StateNextRace, ("date", DateText(raceDay)));
            }
            else
            {
                Say(PlayKeys.StateNoRace);
            }

            var inbox = new InboxQuery(shell.Modules.Require<InboxBook>()).View(access);
            Say(PlayKeys.StateInbox, ("open", inbox.OpenCount.ToString(CultureInfo.InvariantCulture)), ("decisions", inbox.OpenDecisionCount.ToString(CultureInfo.InvariantCulture)));
        }

        private void Inbox(CareerShell shell)
        {
            var view = new InboxQuery(shell.Modules.Require<InboxBook>()).View(View(shell.Player));
            if (view.Items.Count == 0)
            {
                Say(PlayKeys.InboxEmpty);
                return;
            }

            foreach (var item in view.Items)
            {
                if (item.Status != Paddock.Domain.Inbox.InboxStatus.Open)
                {
                    continue;
                }

                Say(PlayKeys.InboxItem, ("id", item.Id), ("kind", item.Kind), ("subject", Text(item.Subject)));
                foreach (var option in item.Options)
                {
                    Say(PlayKeys.InboxOption, ("id", option.Id), ("label", Text(option.Label)));
                }
            }
        }

        private void Market(CareerShell shell, string? subjectText)
        {
            var team = RequireTeam(shell);
            if (team is not OrganizationId organization)
            {
                return;
            }

            NegotiationSubject? subject = null;
            if (subjectText is not null)
            {
                try
                {
                    subject = NegotiationSubject.Parse(subjectText);
                }
                catch (ArgumentException)
                {
                    Say(PlayKeys.BadLine);
                    return;
                }
            }

            var people = new FreeAgentQuery(shell.Modules.Require<ContractBook>()).List(View(shell.Player), organization, shell.Date, subject);
            if (people.Count == 0)
            {
                Say(PlayKeys.MarketEmpty);
                return;
            }

            foreach (var person in people)
            {
                Say(PlayKeys.MarketPerson, ("id", person.Person.Value), ("name", person.Name), ("nationality", person.Nationality));
            }
        }

        private void Negotiate(CareerShell shell, string[] parts)
        {
            if (parts.Length == 1 || parts[1] == "list")
            {
                var talks = new NegotiationQuery(shell.Modules.Require<ContractBook>()).View(View(shell.Player));
                if (talks.Items.Count == 0)
                {
                    Say(PlayKeys.NegotiationEmpty);
                    return;
                }

                foreach (var talk in talks.Items)
                {
                    Say(PlayKeys.NegotiationLine, ("id", talk.Id), ("status", Text(talk.StatusText)), ("person", talk.PersonName));
                }

                return;
            }

            if (parts[1] == "open")
            {
                Open(shell, parts);
                return;
            }

            if (parts.Length < 3)
            {
                Say(PlayKeys.BadLine);
                return;
            }

            var today = Today(shell);
            ICommand? command = parts[1] switch
            {
                "accept" => new AcceptCounterOfferCommand { ManagerId = shell.Player, IssuedOn = today, NegotiationId = parts[2] },
                "walk" => new WalkAwayCommand { ManagerId = shell.Player, IssuedOn = today, NegotiationId = parts[2] },
                "offer" when parts.Length >= 5 && long.TryParse(parts[3], NumberStyles.None, CultureInfo.InvariantCulture, out var salary) && int.TryParse(parts[4], NumberStyles.None, CultureInfo.InvariantCulture, out var years) =>
                    new SubmitOfferCommand
                    {
                        ManagerId = shell.Player,
                        IssuedOn = today,
                        NegotiationId = parts[2],
                        Terms = Terms(salary, years),
                    },
                _ => null,
            };
            if (command is null)
            {
                Say(PlayKeys.BadLine);
                return;
            }

            Submit(shell, command);
        }

        private void Open(CareerShell shell, string[] parts)
        {
            var team = RequireTeam(shell);
            if (team is not OrganizationId organization)
            {
                return;
            }

            // negotiate open <person|first> <subject>
            if (parts.Length != 4)
            {
                Say(PlayKeys.BadLine);
                return;
            }

            NegotiationSubject subject;
            try
            {
                subject = NegotiationSubject.Parse(parts[3]);
            }
            catch (ArgumentException)
            {
                Say(PlayKeys.BadLine);
                return;
            }

            PersonId person;
            if (parts[2] == "first")
            {
                var people = new FreeAgentQuery(shell.Modules.Require<ContractBook>()).List(View(shell.Player), organization, shell.Date, subject);
                if (people.Count == 0)
                {
                    Say(PlayKeys.MarketEmpty);
                    return;
                }

                person = people[0].Person;
            }
            else if (!TryPerson(parts[2], out person))
            {
                Say(PlayKeys.BadLine);
                return;
            }

            var result = shell.Submit(new OpenNegotiationCommand
            {
                ManagerId = shell.Player,
                IssuedOn = Today(shell),
                Organization = organization,
                Person = person,
                Subject = subject,
            });
            if (result is CommandResult.Rejected rejected)
            {
                SayMessage(rejected.Reason);
                return;
            }

            var opened = (result as CommandResult.Accepted)?.Events.OfType<NegotiationOpened>().FirstOrDefault();
            Say(PlayKeys.NegotiationOpened, ("id", opened?.NegotiationId ?? string.Empty), ("person", person.Value));
        }

        private void Pool(CareerShell shell)
        {
            var view = new PoolQuery(PoolBook.ForSession(shell.Session), shell.Modules.Require<IManagerOrganizations>()).View(View(shell.Player));
            if (view.Focus is ScoutFocusKind focus)
            {
                Say(PlayKeys.PoolFocus, ("focus", focus.ToString()));
            }

            if (view.Items.Count == 0)
            {
                Say(PlayKeys.PoolEmpty);
                return;
            }

            foreach (var item in view.Items)
            {
                Say(PlayKeys.PoolPerson, ("handle", item.Handle), ("name", item.GivenName + " " + item.FamilyName), ("age", item.Age.ToString(CultureInfo.InvariantCulture)));
            }
        }

        private void Cars(CareerShell shell)
        {
            var book = new CarBook(() => shell.Session.World, shell.Session.StoreWorld, shell.Session.Clock.MasterSeed);
            var roster = new CarQuery(book, shell.Modules.Require<IOrganizationControl>()).View(View(shell.Player));
            if (roster.Own.Count == 0 && roster.Rivals.Count == 0)
            {
                Say(PlayKeys.CarEmpty);
                return;
            }

            foreach (var car in roster.Own)
            {
                Say(PlayKeys.CarOwn, ("id", car.CarId), ("season", car.Season.ToString(CultureInfo.InvariantCulture)));
            }

            foreach (var car in roster.Rivals)
            {
                Say(PlayKeys.CarRival, ("id", car.CarId), ("team", car.OrganizationId));
            }
        }

        private void Dev(CareerShell shell, string[] parts)
        {
            if (parts.Length == 1)
            {
                DevView(shell);
                return;
            }

            if (parts.Length < 5 || parts[1] != "split"
                || !int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out var current)
                || !int.TryParse(parts[3], NumberStyles.None, CultureInfo.InvariantCulture, out var account)
                || !int.TryParse(parts[4], NumberStyles.None, CultureInfo.InvariantCulture, out var next))
            {
                Say(PlayKeys.BadLine);
                return;
            }

            var team = RequireTeam(shell);
            if (team is not OrganizationId organization)
            {
                return;
            }

            Submit(shell, new SetDevelopmentSplitCommand
            {
                ManagerId = shell.Player,
                IssuedOn = Today(shell),
                OrganizationId = organization.Value,
                CurrentPercent = current,
                AccountPercent = account,
                NextYearPercent = next,
            });
            DevView(shell);
        }

        private void DevView(CareerShell shell)
        {
            var view = shell.Development.View(View(shell.Player));
            if (view.Own.Count == 0)
            {
                Say(PlayKeys.DevEmpty);
                return;
            }

            foreach (var own in view.Own)
            {
                Say(
                    PlayKeys.DevLine,
                    ("current", own.CurrentPercent.ToString(CultureInfo.InvariantCulture)),
                    ("account", own.AccountPercent.ToString(CultureInfo.InvariantCulture)),
                    ("next", own.NextYearPercent.ToString(CultureInfo.InvariantCulture)));
            }
        }

        private void Sponsors(CareerShell shell)
        {
            var team = RequireTeam(shell);
            if (team is not OrganizationId organization)
            {
                return;
            }

            var objectives = new ObjectiveQuery(shell.Modules.Require<IObjectiveFacts>(), shell.Modules.Require<IManagerOrganizations>());
            var view = SponsorQuery.Read(
                View(shell.Player),
                organization,
                shell.Modules.Require<SponsorBook>(),
                shell.Modules.Require<SponsorEnvironment>(),
                objectives,
                shell.Date);
            if (view is not SponsorView.Own own || own.Slots.Count == 0)
            {
                Say(PlayKeys.SponsorsEmpty);
                return;
            }

            foreach (var slot in own.Slots)
            {
                Say(PlayKeys.SponsorSlot, ("slot", slot.Slot.ToString(CultureInfo.InvariantCulture)), ("kind", Text(slot.Kind)));
            }
        }

        private void Finance(CareerShell shell)
        {
            var team = RequireTeam(shell);
            if (team is not OrganizationId organization)
            {
                return;
            }

            var money = FinanceQuery.Read(View(shell.Player), organization, shell.Session.World, shell.Date, shell.Modules.Require<IOrganizationControl>());
            if (money is not FinanceView.Own own)
            {
                Say(PlayKeys.FinanceUnknown);
                return;
            }

            Say(PlayKeys.StateCash, ("cash", Dollars(own.CashCents)));
            Say(PlayKeys.StateForecast, ("cash", Dollars(own.ForecastCashCents)));
        }

        private void Board(CareerShell shell)
        {
            var view = BoardView(shell);
            if (view.Own is not { } own)
            {
                Say(PlayKeys.BoardObserver);
                return;
            }

            Say(PlayKeys.BoardLine, ("team", own.OrganizationId), ("confidence", own.State.Confidence.ToString(CultureInfo.InvariantCulture)));
            SayMessage(own.State.Band);
            SayMessage(own.Forecast.Message);
        }

        private void Why(CareerShell shell, string? decisionId)
        {
            var book = shell.Modules.Require<ContractBook>();
            var talks = new NegotiationQuery(book).View(View(shell.Player)).Items.Select(item => item.Id).ToArray();
            var lines = WhyQuery.Visible(book.Environment.Trace, View(shell.Player), shell.TeamOf(shell.Player), talks, decisionId);
            if (lines.Count == 0)
            {
                if (decisionId is null)
                {
                    Say(PlayKeys.WhyEmpty);
                }
                else
                {
                    Say(PlayKeys.WhyUnknown, ("id", decisionId));
                }

                return;
            }

            foreach (var line in lines)
            {
                Say(PlayKeys.WhyLine, ("id", line.DecisionId), ("reason", line.PlayerReason ?? string.Empty));
                foreach (var factor in line.Factors)
                {
                    Say(PlayKeys.WhyFactor, ("name", factor.Name), ("contribution", factor.Contribution));
                }
            }
        }

        private BoardView BoardView(CareerShell shell)
        {
            var objectives = new ObjectiveQuery(shell.Modules.Require<IObjectiveFacts>(), shell.Modules.Require<IManagerOrganizations>());
            return new BoardQuery(shell.Modules.Require<BoardBook>(), shell.Modules.Require<InboxBook>(), objectives).View(View(shell.Player), shell.Date);
        }

        private void Submit(CareerShell shell, ICommand command)
        {
            var result = shell.Submit(command);
            if (result is CommandResult.Rejected rejected)
            {
                SayMessage(rejected.Reason);
            }
        }

        private OrganizationId? RequireTeam(CareerShell shell)
        {
            var team = shell.TeamOf(shell.Player);
            if (team is null)
            {
                Say(PlayKeys.StateNoTeam);
            }

            return team;
        }

        private void Save(CareerShell shell, CareerConfig config, string path)
        {
            if (_worldHash is null || !shell.QueueIsEmpty)
            {
                Say(PlayKeys.SaveFailed, ("reason", "queue"));
                return;
            }

            var team = shell.TeamOf(shell.Player);
            if (team is not OrganizationId organization)
            {
                Say(PlayKeys.SaveFailed, ("reason", "team"));
                return;
            }

            try
            {
                var name = shell.Modules.Managers.Get(shell.Player).DisplayName;
                CareerSaveWriter.Write(path, shell.Session, config, organization.Value, _worldHash, _careerName, shell.HostState);
                Say(PlayKeys.Saved, ("path", path), ("hash", shell.WorldHash));
            }
            catch (Exception ex) when (ex is IOException or InvalidOperationException or UnstableSaveException)
            {
                Say(PlayKeys.SaveFailed, ("reason", ex.GetType().Name));
            }
        }

        private void Autosave(CareerShell shell)
        {
            if (_autosave is not null && _config is not null)
            {
                // The same path is the slot for every stable morning, so the previous autosave is replaced.
                if (File.Exists(_autosave))
                {
                    File.Delete(_autosave);
                }

                Save(shell, _config, _autosave);
            }
        }

        private void Help()
        {
            Say(PlayKeys.HelpHeader);
            Say(PlayKeys.HelpCommands);
        }

        private bool PrepareData()
        {
            if (_dataRoot is not null)
            {
                return true;
            }

            _dataRoot = _dataRootFlag ?? RunCommand.FindDataRoot();
            if (_dataRoot is null)
            {
                Say(PlayKeys.SaveFailed, ("reason", "data"));
                return false;
            }

            _peopleFiles = RunCommand.ResolveProviderFiles(_dataRoot, null, null);
            return true;
        }

        private bool TryAxis(CareerConfig config, string name, string value, out CareerConfig updated)
        {
            updated = config;
            if (name == "team" && CareerStartPath.IsFounding(value))
            {
                Say(PlayKeys.OwnTeam);
                return false;
            }

            switch (name)
            {
                case "people":
                    if (!TryNamed(value, out PeopleSource people))
                    {
                        Say(PlayKeys.BadAxisValue, ("axis", name), ("value", value));
                        return false;
                    }

                    updated = config.WithPeopleSource(people);
                    return true;
                case "rules":
                    if (!TryNamed(value, out RulesSource rules))
                    {
                        Say(PlayKeys.BadAxisValue, ("axis", name), ("value", value));
                        return false;
                    }

                    updated = config.WithRulesSource(rules);
                    return true;
                case "ai":
                    if (!TryNamed(value, out AiBehavior ai))
                    {
                        Say(PlayKeys.BadAxisValue, ("axis", name), ("value", value));
                        return false;
                    }

                    updated = config.WithAiBehavior(ai);
                    return true;
                case "history":
                    if (!int.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var history))
                    {
                        Say(PlayKeys.BadAxisValue, ("axis", name), ("value", value));
                        return false;
                    }

                    updated = config.WithHistoryStrength(history);
                    return true;
                case "randomness":
                    if (!int.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var randomness))
                    {
                        Say(PlayKeys.BadAxisValue, ("axis", name), ("value", value));
                        return false;
                    }

                    updated = config.WithRandomnessLevel(randomness);
                    return true;
                case "fatality":
                    if (!TryFatality(value, out var fatality))
                    {
                        Say(PlayKeys.BadFatality);
                        return false;
                    }

                    updated = config.WithFatalityLevel(fatality);
                    return true;
                case "year":
                    if (!int.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var year))
                    {
                        Say(PlayKeys.BadYear);
                        return false;
                    }

                    updated = config.WithStartYear(year);
                    return true;
                case "team":
                    updated = config.WithPlayerTeam(value);
                    return true;
                case "no-numbers":
                    if (value is not ("true" or "false"))
                    {
                        Say(PlayKeys.BadAxisValue, ("axis", name), ("value", value));
                        return false;
                    }

                    updated = config.WithNoNumbers(value == "true");
                    return true;
                default:
                    Say(PlayKeys.BadAxis, ("axis", name));
                    return false;
            }
        }

        private void SayIssue(CareerConfigIssue issue)
        {
            var args = new Dictionary<string, object?>(StringComparer.Ordinal);
            if (issue.Code is CareerConfigCodes.HistoryStrengthRange or CareerConfigCodes.RandomnessRange && issue.Arguments.Count >= 2)
            {
                args["min"] = issue.Arguments[0];
                args["max"] = issue.Arguments[1];
            }
            else if (issue.Arguments.Count >= 1)
            {
                args["year"] = issue.Arguments[0];
            }

            _stdout.WriteLine(_localizer.Get(issue.Code, args));
        }

        private void SayInit(WorldInitException exception)
        {
            var args = new Dictionary<string, object?>(StringComparer.Ordinal);
            if (exception.Code == WorldInitErrorCodes.UnknownPlayerTeam && exception.Arguments.Count >= 2)
            {
                args["team"] = exception.Arguments[0];
                args["year"] = exception.Arguments[1];
            }
            else if (exception.Arguments.Count > 0)
            {
                args["codes"] = string.Join(", ", exception.Arguments);
            }

            _stdout.WriteLine(_localizer.Get(exception.Code, args));
        }

        private void Say(string key, params (string Name, string Value)[] args)
        {
            var map = new Dictionary<string, object?>(args.Length, StringComparer.Ordinal);
            foreach (var (name, value) in args)
            {
                map[name] = value;
            }

            _stdout.WriteLine(_localizer.Get(key, map));
        }

        private void SayMessage(TranslationMessage message)
        {
            var map = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var pair in message.Parameters)
            {
                map[pair.Key] = pair.Value;
            }

            _stdout.WriteLine(_localizer.Get(message.Key, map));
        }

        private string Text(TranslationMessage message)
        {
            var map = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var pair in message.Parameters)
            {
                map[pair.Key] = pair.Value;
            }

            return _localizer.Get(message.Key, map);
        }

        private string? Read()
        {
            var line = _stdin.ReadLine();
            return line?.Trim();
        }

        private static string[] Split(string line) =>
            line.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        private static DateOnly Today(CareerShell shell) => new(shell.Date.Year, shell.Date.Month, shell.Date.Day);

        private static string DateText(GameDate date) =>
            date.Year.ToString("0000", CultureInfo.InvariantCulture) + "-"
            + date.Month.ToString("00", CultureInfo.InvariantCulture) + "-"
            + date.Day.ToString("00", CultureInfo.InvariantCulture);

        private static string Dollars(long cents)
        {
            var sign = cents < 0 ? "-" : string.Empty;
            var abs = Math.Abs(cents);
            return sign + (abs / 100).ToString(CultureInfo.InvariantCulture) + "." + (abs % 100).ToString("00", CultureInfo.InvariantCulture);
        }

        private static AccessContext View(HostManagerId manager) => AccessContext.ForManager(new AccessManagerId(manager.Value));

        private static OfferTerms Terms(long salary, int years) =>
            new(salary, 0, 0, 0, years, SeatStatus.Equal, null, null);

        private static bool TryPerson(string text, out PersonId id)
        {
            if (text.StartsWith("gen:", StringComparison.Ordinal)
                && long.TryParse(text.AsSpan(4), NumberStyles.None, CultureInfo.InvariantCulture, out var sequence)
                && sequence >= 1)
            {
                id = PersonId.Generated(sequence);
                return true;
            }

            try
            {
                id = PersonId.Real(text);
                return true;
            }
            catch (ArgumentException)
            {
                id = default;
                return false;
            }
        }

        private static bool TryPreset(string text, out CareerPreset preset) =>
            TryNamed(text, out preset) && preset != CareerPreset.Custom;

        private static bool TryFatality(string text, out FatalityLevel fatality) => TryNamed(text, out fatality);

        private static bool TryNamed<TEnum>(string value, out TEnum parsed)
            where TEnum : struct, Enum
        {
            foreach (var candidate in Enum.GetValues<TEnum>())
            {
                if (string.Equals(candidate.ToString(), value, StringComparison.Ordinal))
                {
                    parsed = candidate;
                    return true;
                }
            }

            parsed = default;
            return false;
        }
    }

    private sealed class Wizard
    {
        public CareerConfig? Config { get; set; }

        public bool YearSet { get; set; }

        public bool FatalitySet { get; set; }

        public string? Tilt { get; set; }

        public string? Team { get; set; }

        public string? Given { get; set; }

        public string? Family { get; set; }

        public string? Nationality { get; set; }

        public bool Started { get; set; }

        public CareerShell? Shell { get; set; }
    }
}
