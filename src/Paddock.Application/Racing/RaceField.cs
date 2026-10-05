using System.Collections.Immutable;
using Paddock.Application.Career;
using Paddock.Domain.Cars;
using Paddock.Domain.Finance;
using Paddock.Domain.People;
using Paddock.Domain.Supply;
using Paddock.Domain.Time;
using Paddock.Domain.World;
using Paddock.Simulation.Cars;
using Paddock.Simulation.Racing.Pace;
using Paddock.Simulation.Racing.Pits;
using Paddock.Simulation.Racing.Tyres;
using Paddock.Simulation.Racing.Weekend;
using Paddock.Simulation.Supply;

namespace Paddock.Application.Racing;

/// <summary>Who starts a championship round, and who stays home because the race running cost is above the cash on hand.</summary>
public sealed record RaceField(ImmutableArray<RaceEntry> Entries, IReadOnlyList<string> SkippedTeamIds);

/// <summary>
/// Builds the grid from the world (T47, open question 3, PP-050). Each team starts the cars it owns this season, one
/// contracted driver per car. A team whose cash is below the race running cost (the same ESTIMATE the ledger charges)
/// does not start. No private entries and no shared drives.
/// </summary>
public static class RaceFieldBuilder
{
    public static RaceField Build(
        WorldState world,
        GameDate today,
        RuleSet rules,
        int racesInSeason,
        SupplySection? supply,
        ISupplierProfiles? profiles)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentOutOfRangeException.ThrowIfLessThan(racesInSeason, 1);
        var cars = world.Section<CarsSection>(CarsSection.SectionName);
        var finance = world.Section<FinanceSection>(FinanceSection.SectionName);
        if (cars is null)
        {
            return new RaceField([], []);
        }

        var people = new Dictionary<string, Person>(world.Persons.Count, StringComparer.Ordinal);
        foreach (var person in world.Persons)
        {
            people[person.Id.Value] = person;
        }

        var races = finance is { Races: > 0 } ? finance.Races : racesInSeason;
        var running = finance is null ? 0L : RunningCost(finance, races);
        var limits = EraPerformanceLimits.EstimateFor(today.Year);
        var formula = rules.Values.TryGetValue("engine_formula", out var engineFormula) ? engineFormula : "";
        var usedDrivers = new HashSet<string>(StringComparer.Ordinal);
        var entries = ImmutableArray.CreateBuilder<RaceEntry>();
        var skipped = new List<string>();
        foreach (var team in CareerTeams.Active(world, today))
        {
            var owned = cars.Of(team.Id)
                .Where(car => car.Season == today.Year && car.Driver is not null)
                .OrderBy(car => car.Id, StringComparer.Ordinal)
                .ToArray();
            if (owned.Length == 0)
            {
                continue;
            }

            if (finance is not null && running > 0 && finance.HasBook(team.Id) && finance.BalanceOf(team.Id) < running)
            {
                skipped.Add(team.Id.Value);
                continue;
            }

            if (finance is not null && !finance.HasBook(team.Id))
            {
                continue;
            }

            var (tyreProfile, partnerTuned) = supply is null
                ? (TyreSupplierProfile.Neutral, false)
                : SupplyPerformance.TyresFor(supply, team.Id, today);
            foreach (var car in owned)
            {
                if (car.Driver is not PersonId seated)
                {
                    continue;
                }

                var driverId = seated.Value;
                if (!people.TryGetValue(driverId, out var person) || person.IsRetired || !usedDrivers.Add(driverId))
                {
                    continue;
                }

                var performance = supply is null || profiles is null
                    ? CarPerformanceFor.Resolve(car, limits)
                    : SupplyPerformance.Resolve(car, limits, supply, profiles, today);
                var engineName = car.EngineKey is not null && supply?.FindDeal(car.EngineKey) is { } deal ? deal.EngineName : null;
                entries.Add(new RaceEntry(
                    car.Id,
                    team.Id.Value,
                    [RaceInputMapping.DriverFrom(driverId, AttributesOf(person.Truth), RacingEstimates.DefaultAggression)],
                    performance,
                    RaceInputMapping.UniformComponents(performance.Reliability),
                    new PitCrew(RacingEstimates.NeutralPitCrewQuality),
                    RacingEstimates.NeutralStrategistSkill,
                    RacingEstimates.NeutralForecastQuality,
                    tyreProfile,
                    partnerTuned,
                    FuelOf(formula, engineName)));
            }
        }

        skipped.Sort(StringComparer.Ordinal);
        return new RaceField(entries.ToImmutable(), skipped);
    }

    /// <summary>
    /// ESTIMATE: the same per-race running cost <see cref="RevenueModels"/> posts, so "cannot afford the race" is that bill
    /// and not a second threshold. <paramref name="races"/> is the season length the ledger is splitting the year across.
    /// </summary>
    public static long RunningCost(FinanceSection finance, int races)
    {
        ArgumentNullException.ThrowIfNull(finance);
        if (finance.TypicalCents <= 0 || races < 1)
        {
            return 0;
        }

        var popularity = finance.PopularityMilli / 1000.0;
        return Money.RoundCents(finance.TypicalCents * FinanceEstimates.RaceRunningShare * popularity / races);
    }

    /// <summary>
    /// ESTIMATE mapping from the era formula (and, when the deal names one, the engine) onto the three fuel models.
    /// A mixed formula stays naturally aspirated unless the engine name says turbo. There is no per-car supercharging list.
    /// </summary>
    internal static FuelEngineType FuelOf(string formula, string? engineName)
    {
        if (formula.Contains("hybrid", StringComparison.Ordinal))
        {
            return FuelEngineType.Hybrid;
        }

        if (string.Equals(formula, "1500_forced_only", StringComparison.Ordinal))
        {
            return FuelEngineType.Turbo;
        }

        if (engineName is not null && engineName.Contains("turbo", StringComparison.OrdinalIgnoreCase))
        {
            return FuelEngineType.Turbo;
        }

        return FuelEngineType.NaturallyAspirated;
    }

    private static DriverAttributes AttributesOf(PersonTruth truth) => new(
        truth.Value("cornering"),
        truth.Value("braking"),
        truth.Value("smoothness"),
        truth.Value("overtaking"),
        truth.Value("defending"),
        truth.Value("consistency"),
        truth.Value("composure"),
        truth.Value("adaptability"),
        truth.Value("wet_weather"),
        truth.Value("fitness"),
        truth.Value("feedback"));
}
