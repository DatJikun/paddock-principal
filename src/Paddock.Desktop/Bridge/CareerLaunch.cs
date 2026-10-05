using Paddock.Application.Board;
using Paddock.Application.Career;
using Paddock.Application.Commands;
using Paddock.Data.Authored;
using Paddock.Data.Historical;
using Paddock.Data.World;
using Paddock.Domain.Career;
using Paddock.Domain.Time;
using Paddock.Domain.World;
using Paddock.SimRunner;
using Paddock.Simulation.Career;

namespace Paddock.Desktop.Bridge;

/// <summary>What the player asked the start screen to open. The bridge applies no extra rule of its own.</summary>
public sealed record CareerStartRequest(
    string Preset,
    int Year,
    string Fatality,
    string TeamId,
    string GivenName,
    string FamilyName,
    string Nationality,
    string Tilt,
    ulong Seed,
    string CareerName);

/// <summary>A career the host can keep, or the reason key the page should show.</summary>
public sealed record CareerOpenResult(CareerBridge? Career, string? Key, IReadOnlyDictionary<string, string>? Parameters)
{
    public static CareerOpenResult Ready(CareerBridge career) => new(career, null, null);

    public static CareerOpenResult Refuse(string key, IReadOnlyDictionary<string, string>? parameters = null) => new(null, key, parameters);
}

/// <summary>
/// Starts and resumes a career the same way the play shell does: <see cref="WorldInitializer"/>, then
/// <see cref="TakeOverTeamCommand"/>, then <see cref="CareerShell"/>. It does not read stdin.
/// </summary>
public static class CareerLaunch
{
    public static CareerOpenResult Start(string dataRoot, CareerStartRequest request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataRoot);
        ArgumentNullException.ThrowIfNull(request);
        if (!TryPreset(request.Preset, out var preset))
        {
            return CareerOpenResult.Refuse(PlayKeys.BadPreset, new Dictionary<string, string> { ["preset"] = request.Preset });
        }

        if (!TryFatality(request.Fatality, out var fatality))
        {
            return CareerOpenResult.Refuse(PlayKeys.BadFatality);
        }

        if (CareerStartPath.IsFounding(request.TeamId))
        {
            return CareerOpenResult.Refuse(PlayKeys.OwnTeam);
        }

        if (!PlayerPrincipal.TryTilt(request.Tilt, out _))
        {
            return CareerOpenResult.Refuse(PlayKeys.BadTilt, new Dictionary<string, string> { ["tilt"] = request.Tilt });
        }

        var config = CareerConfig.FromPreset(preset)
            .WithStartYear(request.Year)
            .WithFatalityLevel(fatality)
            .WithPlayerTeam(request.TeamId);
        var peopleFiles = RunCommand.ResolveProviderFiles(dataRoot, null, null);
        string? notice = null;
        if (peopleFiles is null && config.PeopleSource != PeopleSource.FullyGenerated)
        {
            config = config.WithPeopleSource(PeopleSource.FullyGenerated);
            if (config.AiBehavior == AiBehavior.ReplayHistory)
            {
                config = config.WithAiBehavior(AiBehavior.ReactToSituation);
            }

            notice = BridgeKeys.GeneratedPeople;
        }
        else if (peopleFiles is null)
        {
            notice = BridgeKeys.GeneratedPeople;
        }

        var validation = config.Validate();
        if (!validation.IsValid)
        {
            return CareerOpenResult.Refuse(validation.Errors[0].Code, IssueParameters(validation.Errors[0]));
        }

        var data = AuthoredDataLoader.Load(dataRoot);
        var provider = RunCommand.LoadProvider(peopleFiles);
        WorldInitResult created;
        try
        {
            created = WorldInitializer.Create(config, data, provider, request.Seed);
        }
        catch (WorldInitException ex)
        {
            return CareerOpenResult.Refuse(ex.Code, InitParameters(ex));
        }

        var arrivals = TalentIntakeSchedule.AfterStart(config, provider, created.World, request.Seed);
        var session = new CareerSession(
            created.World,
            request.Seed,
            created.TalentPool,
            arrivals,
            new CareerSessionOptions { LastSeasons = LastSeasons.From(provider) });
        var name = request.GivenName + " " + request.FamilyName;
        var shell = CareerShell.Open(
            session,
            new CareerRunOptions { Inputs = CareerInputsLoader.Load(dataRoot, data, created.EngineSupplies) },
            name);
        var today = new DateOnly(shell.Date.Year, shell.Date.Month, shell.Date.Day);
        var taken = shell.Submit(new TakeOverTeamCommand
        {
            ManagerId = shell.Player,
            IssuedOn = today,
            OrganizationId = request.TeamId,
            GivenName = request.GivenName,
            FamilyName = request.FamilyName,
            Nationality = request.Nationality,
            Tilt = request.Tilt,
        });
        if (taken is CommandResult.Rejected rejected)
        {
            return CareerOpenResult.Refuse(rejected.Reason.Key, Parameters(rejected.Reason));
        }

        shell.BeginDay();
        return CareerOpenResult.Ready(CareerBridge.Adopt(
            shell,
            config,
            RunCommand.HashWorldData(dataRoot, peopleFiles),
            request.CareerName,
            notice,
            data.Layouts,
            data.RaceAssignments,
            data.DimensionIds,
            data.Periods));
    }

    public static CareerOpenResult Load(string dataRoot, string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        LoadedCareer loaded;
        try
        {
            loaded = CareerSaveReader.Read(path);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or SaveNotResumableException or Paddock.Domain.Codec.UnknownTagException)
        {
            return CareerOpenResult.Refuse(PlayKeys.SaveFailed, new Dictionary<string, string> { ["reason"] = ex.GetType().Name });
        }

        var peopleFiles = RunCommand.ResolveProviderFiles(dataRoot, null, null);
        var now = RunCommand.HashWorldData(dataRoot, peopleFiles);
        if (!string.Equals(now, loaded.Meta.WorldDataHash, StringComparison.Ordinal))
        {
            return CareerOpenResult.Refuse(
                PlayKeys.DataChanged,
                new Dictionary<string, string> { ["saved"] = loaded.Meta.WorldDataHash, ["now"] = now });
        }

        var provider = RunCommand.LoadProvider(peopleFiles);
        var date = loaded.Session.World.CurrentDate;
        var standIn = loaded.Session.World.WithDate(date.IsSeasonStart ? GameDate.SeasonStart(date.Year - 1) : date);
        var arrivals = TalentIntakeSchedule.AfterStart(loaded.Meta.CareerConfig, provider, standIn, loaded.Meta.MasterSeed);
        var session = CareerSession.Resume(
            loaded.Session,
            arrivals,
            new CareerSessionOptions { LastSeasons = LastSeasons.From(provider) });
        var player = HumanOf(loaded);
        if (player is null)
        {
            return CareerOpenResult.Refuse(PlayKeys.NoCareer);
        }

        var data = AuthoredDataLoader.Load(dataRoot);
        var shell = CareerShell.Resume(
            session,
            loaded.Host,
            new CareerRunOptions { Inputs = CareerInputsLoader.Load(dataRoot, data) },
            player.Value);
        string? notice = peopleFiles is null ? BridgeKeys.GeneratedPeople : null;
        return CareerOpenResult.Ready(CareerBridge.Adopt(
            shell,
            loaded.Meta.CareerConfig,
            now,
            loaded.Meta.CareerName,
            notice,
            data.Layouts,
            data.RaceAssignments,
            data.DimensionIds,
            data.Periods));
    }

    public static IReadOnlyList<PublicTeamView> Teams(string dataRoot, int year)
    {
        var data = AuthoredDataLoader.Load(dataRoot);
        var teams = new List<PublicTeamView>();
        foreach (var team in WorldInitializer.PublicTeams(data, year))
        {
            teams.Add(new PublicTeamView(team.Id, team.Name));
        }

        return teams;
    }

    private static Paddock.Application.Managers.ManagerId? HumanOf(LoadedCareer loaded)
    {
        foreach (var manager in loaded.Host.Managers.All)
        {
            if (manager.Kind == Paddock.Application.Managers.ManagerKind.Human
                && string.Equals(manager.DisplayName, loaded.Meta.ManagerName, StringComparison.Ordinal))
            {
                return manager.Id;
            }
        }

        foreach (var manager in loaded.Host.Managers.All)
        {
            if (manager.Kind == Paddock.Application.Managers.ManagerKind.Human)
            {
                return manager.Id;
            }
        }

        return null;
    }

    private static bool TryPreset(string text, out CareerPreset preset)
    {
        if (Enum.TryParse(text, ignoreCase: true, out preset) && preset != CareerPreset.Custom && Enum.IsDefined(preset))
        {
            return true;
        }

        preset = default;
        return false;
    }

    private static bool TryFatality(string text, out FatalityLevel fatality) =>
        Enum.TryParse(text, ignoreCase: true, out fatality) && Enum.IsDefined(fatality);

    private static Dictionary<string, string> IssueParameters(CareerConfigIssue issue)
    {
        var args = new Dictionary<string, string>(StringComparer.Ordinal);
        if (issue.Code is CareerConfigCodes.HistoryStrengthRange or CareerConfigCodes.RandomnessRange && issue.Arguments.Count >= 2)
        {
            args["min"] = issue.Arguments[0];
            args["max"] = issue.Arguments[1];
        }
        else if (issue.Arguments.Count >= 1)
        {
            args["year"] = issue.Arguments[0];
        }

        return args;
    }

    private static Dictionary<string, string> InitParameters(WorldInitException exception)
    {
        var args = new Dictionary<string, string>(StringComparer.Ordinal);
        if (exception.Code == WorldInitErrorCodes.UnknownPlayerTeam && exception.Arguments.Count >= 2)
        {
            args["team"] = exception.Arguments[0];
            args["year"] = exception.Arguments[1];
        }
        else if (exception.Arguments.Count > 0)
        {
            args["codes"] = string.Join(", ", exception.Arguments);
        }

        return args;
    }

    private static Dictionary<string, string> Parameters(TranslationMessage message)
    {
        var copy = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in message.Parameters)
        {
            copy[pair.Key] = pair.Value;
        }

        return copy;
    }
}
