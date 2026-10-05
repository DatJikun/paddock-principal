using Paddock.Application.Career;
using Paddock.Data.Authored;
using Paddock.Data.Historical;
using Paddock.Data.World;
using Paddock.Domain.Career;
using Paddock.Domain.Time;
using Paddock.Persistence;
using Paddock.Simulation.Career;
using Paddock.Career;
using Paddock.SimRunner;

namespace Paddock.Tests.Career;

/// <summary>
/// A real opening world from the authored data, built the way <c>SimRunner run</c> builds it. There is no Jolpica cache in the
/// repository (PP-041), so the people provider is the empty one and the people source decides who drives (Chaos generates everyone).
/// The run options carry the same data inputs the tool loads: era pay and budgets, the fictional sponsors, and the ESTIMATE of the
/// previous season's order.
/// </summary>
internal static class CareerKit
{
    private static readonly Lazy<AuthoredData> Authored = new(() => AuthoredDataLoader.Load(DataRoot));

    public static string DataRoot => Path.Combine(RepoPaths.Root(), "data");

    public static AuthoredData Data => Authored.Value;

    /// <summary>The run options of the tool: every career module and the authored data inputs.</summary>
    public static CareerRunOptions Options =>
        new() { Inputs = CareerInputsLoader.Load(DataRoot, Data, career: CareerConfig.FromPreset(CareerPreset.Chaos)) };

    public static CareerRunOptions OptionsFor(OpenedCareer career) =>
        new() { Inputs = CareerInputsLoader.Load(DataRoot, Data, career.Supplies, career.Config) };

    public static CareerSession Open(CareerPreset preset, int startYear, ulong seed) => Opened(preset, startYear, seed).Session;

    public static bool HasRealPeopleCache =>
        File.Exists(Path.Combine(DataRoot, "cache", "reports", "people_schedule.json"))
        && File.Exists(Path.Combine(DataRoot, "cache", "jolpica", "normalized", "drivers.json"));

    public static IPeopleProvider RealPeopleProvider()
    {
        var cache = Path.Combine(Path.GetFullPath(DataRoot), "cache");
        var schedulePath = Path.Combine(cache, "reports", "people_schedule.json");
        var driversPath = Path.Combine(cache, "jolpica", "normalized", "drivers.json");
        if (!File.Exists(schedulePath) || !File.Exists(driversPath))
        {
            return EmptyPeopleProvider.Instance;
        }

        var schedule = System.Text.Json.JsonSerializer.Deserialize<PeopleScheduleReport>(File.ReadAllText(schedulePath), HistoricalJson.Options)
            ?? throw new System.Text.Json.JsonException("The people schedule file is empty.");
        var drivers = System.Text.Json.JsonSerializer.Deserialize<HistoricalDriversDocument>(File.ReadAllText(driversPath), HistoricalJson.Options)
            ?? throw new System.Text.Json.JsonException("The drivers file is empty.");
        return new ScheduleBackedPeopleProvider(schedule, drivers.Drivers);
    }

    public static OpenedCareer Opened(CareerPreset preset, int startYear, ulong seed, string? playerTeam = null, IPeopleProvider? peopleProvider = null)
    {
        var config = CareerConfig.FromPreset(preset).WithStartYear(startYear);
        if (playerTeam is not null)
        {
            config = config.WithPlayerTeam(playerTeam);
        }
        var provider = peopleProvider ?? (preset == CareerPreset.Chaos ? EmptyPeopleProvider.Instance : RealPeopleProvider());
        var created = WorldInitializer.Create(config, Data, provider, seed);
        var arrivals = TalentIntakeSchedule.AfterStart(config, provider, created.World, seed);
        var session = new CareerSession(
            created.World,
            seed,
            created.TalentPool,
            arrivals,
            new CareerSessionOptions { LastSeasons = LastSeasons.From(provider) });
        return new OpenedCareer(session, config, created.PlayerOrganization.Value, seed, created.EngineSupplies);
    }

    /// <summary>Writes the career to a save file the way <c>run --save</c> does, so a test can read it back and resume it.</summary>
    public static void Save(string path, OpenedCareer career, CareerSession session, CareerHostState host) =>
        CareerSaveWriter.Write(path, session, career.Config, career.PlayerTeam, new string('c', 64), "kit", host);

    /// <summary>Reads a save and resumes the session the way <c>run --resume</c> does.</summary>
    public static (CareerSession Session, CareerHostState Host) Resume(string path)
    {
        var loaded = CareerSaveReader.Read(path);
        var config = loaded.Meta.CareerConfig;
        var seed = loaded.Meta.MasterSeed;
        var date = loaded.Session.World.CurrentDate;
        var provider = EmptyPeopleProvider.Instance;
        var standIn = loaded.Session.World.WithDate(date.IsSeasonStart ? GameDate.SeasonStart(date.Year - 1) : date);
        var arrivals = TalentIntakeSchedule.AfterStart(config, provider, standIn, seed);
        var session = CareerSession.Resume(loaded.Session, arrivals, new CareerSessionOptions { LastSeasons = LastSeasons.From(provider) });
        return (session, loaded.Host);
    }
}

/// <summary>A career opened by <see cref="CareerKit"/>: the session and what a save needs besides it.</summary>
internal sealed record OpenedCareer(CareerSession Session, CareerConfig Config, string PlayerTeam, ulong Seed, IReadOnlyList<EngineSupplyLink> Supplies);
