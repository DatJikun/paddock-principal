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

/// <summary>Who starts a championship round. Cash is never a gate (#264): a team with no money still races and the bill posts to its ledger.</summary>
public sealed record RaceField(
    ImmutableArray<RaceEntry> Entries,
    ImmutableArray<StandInFact> StandIns = default);

/// <summary>
/// Builds the grid from the world (T47, open question 3, PP-050). Each team starts the cars it owns this season, one
/// contracted driver per car. Cash is not a gate: the running cost and transport post to the ledger after the race and
/// may push the balance below zero (consequences come from finance and the board). No private entries and no shared drives.
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
            return new RaceField([]);
        }

        var people = new Dictionary<string, Person>(world.Persons.Count, StringComparer.Ordinal);
        foreach (var person in world.Persons)
        {
            people[person.Id.Value] = person;
        }

        var limits = EraPerformanceLimits.EstimateFor(today.Year);
        var formula = rules.Values.TryGetValue("engine_formula", out var engineFormula) ? engineFormula : "";
        var usedDrivers = new HashSet<string>(StringComparer.Ordinal);
        var entries = ImmutableArray.CreateBuilder<RaceEntry>();
        var standIns = ImmutableArray.CreateBuilder<StandInFact>();
        foreach (var team in CareerTeams.Active(world, today))
        {
            var owned = cars.Of(team.Id)
                .Where(car => car.Season == today.Year)
                .OrderBy(car => car.Id, StringComparer.Ordinal)
                .ToArray();
            if (owned.Length == 0)
            {
                continue;
            }

            var staff = RaceStaff.Of(world, team.Id, today);

            if (finance is not null && !finance.HasBook(team.Id))
            {
                continue;
            }

            var (tyreProfile, partnerTuned) = supply is null
                ? (TyreSupplierProfile.Neutral, false)
                : SupplyPerformance.TyresFor(supply, team.Id, today);
            foreach (var car in owned)
            {
                // A car with nobody in it (every contract of its seat ended, #253) is filled the way an injured driver's car is:
                // the player's choice from the inbox, else reserve, free agent, talent pool; the car stays out only when nobody is left.
                var vacant = car.Driver is null;
                Person? person = null;
                if (car.Driver is PersonId seated
                    && (!people.TryGetValue(seated.Value, out person) || person.IsRetired))
                {
                    continue;
                }

                var subjectId = vacant ? StandInResolver.VacantPrefix + car.Id : person!.Id.Value;
                var subjectName = vacant ? car.Id : person!.Name;
                Person? driverToEnter = person;
                StandInFact? standInFact = null;

                if (vacant || person!.IsInjured(today))
                {
                    Person? standIn = null;
                    var inbox = world.Section<InboxSection>(InboxSection.SectionName);
                    if (inbox is not null)
                    {
                        var item = inbox.Items
                            .Where(i => i.Kind == StandInResolver.Kind
                                     && i.Arguments.TryGetValue("driverId", out var dId) && dId == subjectId
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
                                    $"Selected stand-in {standIn.Name} for {(vacant ? "empty seat" : "injured driver")} {subjectName}",
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
                    if (!vacant)
                    {
                        standInFact = new StandInFact(driverToEnter.Id.Value, person!.Id.Value, team.Id.Value);
                    }
                }

                if (driverToEnter is null)
                {
                    continue;
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
                    new PitCrew(staff.PitCrewQuality),
                    staff.StrategistSkill,
                    staff.ForecastQuality,
                    tyreProfile,
                    partnerTuned,
                    FuelOf(formula, engineName)));
            }
        }

        return new RaceField(entries.ToImmutable(), standIns.ToImmutable());
    }

    /// <summary>
    /// ESTIMATE extra logistics on top of the uniform race-running share: lorries on the same continent, a ship overseas
    /// (Argentina in the 1950s). Missing countries are treated as a European lorry hop. The season's transport is one share of the era's typical
    /// budget split over the rounds of the season being raced (<paramref name="seasonCircuits"/>), so a longer calendar makes a round cheaper.
    /// </summary>
    public static long TransportCost(
        OrganizationId organization,
        long typicalCents,
        string? circuitCountry,
        IReadOnlyDictionary<string, string>? teamCountries,
        IReadOnlyList<string?>? seasonCircuits = null)
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

        return Paddock.Domain.Infrastructure.LogisticsMath.CostCents(home, circuitCountry, typicalCents, seasonCircuits);
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
