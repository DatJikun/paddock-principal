using System.Collections.Immutable;
using Paddock.Application.Career;
using Paddock.Domain.Cars;
using Paddock.Domain.Finance;
using Paddock.Domain.Inbox;
using Paddock.Domain.People;
using Paddock.Domain.Spy;
using Paddock.Domain.Supply;
using Paddock.Domain.Time;
using Paddock.Domain.World;
using Paddock.Simulation.Cars;
using Paddock.Simulation.Racing.Incidents;
using Paddock.Simulation.Racing.Pace;
using Paddock.Simulation.Racing.Pits;
using Paddock.Simulation.Racing.Tyres;
using Paddock.Simulation.Racing.Weekend;
using Paddock.Simulation.Supply;

namespace Paddock.Application.Racing;

/// <summary>Who starts a championship round, and who stays home because the race running cost is above the cash on hand.</summary>
public sealed record RaceField(
    ImmutableArray<RaceEntry> Entries,
    IReadOnlyList<string> SkippedTeamIds,
    ImmutableArray<StandInFact> StandIns = default);

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
        ISupplierProfiles? profiles,
        int round = 1,
        ITraceSink? traceSink = null,
        string? circuitCountry = null,
        IReadOnlyDictionary<string, string>? teamCountries = null)
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
        var typical = finance?.TypicalCents ?? 0L;
        var limits = EraPerformanceLimits.EstimateFor(today.Year);
        var formula = rules.Values.TryGetValue("engine_formula", out var engineFormula) ? engineFormula : "";
        var usedDrivers = new HashSet<string>(StringComparer.Ordinal);
        var entries = ImmutableArray.CreateBuilder<RaceEntry>();
        var standIns = ImmutableArray.CreateBuilder<StandInFact>();
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

            var transport = TransportCost(team.Id, typical, circuitCountry, teamCountries);
            if (finance is not null && running + transport > 0
                && finance.HasBook(team.Id)
                && finance.BalanceOf(team.Id) < running + transport)
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
                if (!people.TryGetValue(driverId, out var person) || person.IsRetired)
                {
                    continue;
                }

                Person driverToEnter = person;
                StandInFact? standInFact = null;

                if (person.IsInjured(today))
                {
                    Person? standIn = null;
                    var inbox = world.Section<InboxSection>(InboxSection.SectionName);
                    if (inbox is not null)
                    {
                        var item = inbox.Items
                            .Where(i => i.Kind == StandInResolver.Kind
                                     && i.Arguments.TryGetValue("driverId", out var dId) && dId == person.Id.Value
                                     && i.Arguments.TryGetValue("raceDate", out var rDate) && rDate == today.ToString())
                            .OrderByDescending(i => i.Number)
                            .FirstOrDefault();

                        if (item is { ChosenOptionId: { } chosen })
                        {
                            if (chosen == StandInResolver.OptionSkip)
                            {
                                continue;
                            }

                            if (people.TryGetValue(chosen, out var chosenPerson)
                                && !chosenPerson.IsRetired
                                && !chosenPerson.IsInjured(today)
                                && !usedDrivers.Contains(chosenPerson.Id.Value))
                            {
                                standIn = chosenPerson;
                            }
                        }
                    }

                    if (standIn is null)
                    {
                        var candidates = StandInCandidateFinder.FindCandidates(world, team.Id, today, usedDrivers);
                        if (candidates.Count > 0)
                        {
                            standIn = candidates[0];

                            if (traceSink is not null && traceSink.IsEnabled)
                            {
                                var traceOptions = candidates.Select(c => new TraceOption(c.Id.Value, 1.0, [], true)).ToList();
                                traceOptions.Add(new TraceOption(StandInResolver.OptionSkip, 0.0, [], true));
                                traceSink.Record(new DecisionTrace(
                                    new WeekendKey(today.Year, round),
                                    "ai:" + team.Id.Value,
                                    1,
                                    StandInResolver.Kind,
                                    traceOptions,
                                    standIn.Id.Value,
                                    $"Selected stand-in {standIn.Name} for injured driver {person.Name}",
                                    null,
                                    false,
                                    ImmutableDictionary<string, string>.Empty));
                            }
                        }
                    }

                    if (standIn is null)
                    {
                        // Nobody available: car does not start!
                        continue;
                    }

                    driverToEnter = standIn;
                    standInFact = new StandInFact(driverToEnter.Id.Value, person.Id.Value, team.Id.Value);
                }

                if (!usedDrivers.Add(driverToEnter.Id.Value))
                {
                    continue;
                }

                if (standInFact is not null)
                {
                    standIns.Add(standInFact);
                }

                var paceMultiplier = 1.0;
                if (driverToEnter.InjuredUntil is GameDate prevInjured
                    && today > prevInjured
                    && today <= prevInjured.AddDays(IncidentConstants.LightPaceDays))
                {
                    paceMultiplier = 1.0 - IncidentConstants.LightInjuryPacePenalty;
                }

                var performance = supply is null || profiles is null
                    ? CarPerformanceFor.Resolve(car, limits)
                    : SupplyPerformance.Resolve(car, limits, supply, profiles, today);
                var engineName = car.EngineKey is not null && supply?.FindDeal(car.EngineKey) is { } deal ? deal.EngineName : null;
                entries.Add(new RaceEntry(
                    car.Id,
                    team.Id.Value,
                    [RaceInputMapping.DriverFrom(driverToEnter.Id.Value, AttributesOf(driverToEnter.Truth), RacingEstimates.DefaultAggression, paceMultiplier: paceMultiplier)],
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
        return new RaceField(entries.ToImmutable(), skipped, standIns.ToImmutable());
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
    /// ESTIMATE extra logistics on top of the uniform race-running share: lorries on the same continent, a ship overseas
    /// (Argentina in the 1950s). Missing countries are treated as a European lorry hop.
    /// </summary>
    public static long TransportCost(
        OrganizationId organization,
        long typicalCents,
        string? circuitCountry,
        IReadOnlyDictionary<string, string>? teamCountries)
    {
        if (typicalCents <= 0)
        {
            return 0L;
        }

        string? home = null;
        if (teamCountries is not null && organization.IsAssigned)
        {
            teamCountries.TryGetValue(organization.Value, out home);
        }

        return Paddock.Domain.Infrastructure.LogisticsMath.CostCents(home, circuitCountry, typicalCents);
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
