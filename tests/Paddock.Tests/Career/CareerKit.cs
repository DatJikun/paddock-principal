using Paddock.Application.Career;
using Paddock.Data.Authored;
using Paddock.Data.Historical;
using Paddock.Data.World;
using Paddock.Domain.Career;
using Paddock.Simulation.Career;

namespace Paddock.Tests.Career;

/// <summary>
/// A real opening world from the authored data, built the way <c>SimRunner run</c> builds it. There is no Jolpica cache in the
/// repository (PP-041), so the people provider is the empty one and the people source decides who drives (Chaos generates everyone).
/// </summary>
internal static class CareerKit
{
    private static readonly Lazy<AuthoredData> Authored = new(() => AuthoredDataLoader.Load(DataRoot));

    public static string DataRoot => Path.Combine(RepoPaths.Root(), "data");

    public static AuthoredData Data => Authored.Value;

    public static CareerSession Open(CareerPreset preset, int startYear, ulong seed)
    {
        var config = CareerConfig.FromPreset(preset).WithStartYear(startYear);
        var provider = EmptyPeopleProvider.Instance;
        var created = WorldInitializer.Create(config, Data, provider, seed);
        var arrivals = TalentIntakeSchedule.AfterStart(config, provider, created.World, seed);
        return new CareerSession(
            created.World,
            seed,
            created.TalentPool,
            arrivals,
            new CareerSessionOptions { LastSeasons = LastSeasons.From(provider) });
    }
}
