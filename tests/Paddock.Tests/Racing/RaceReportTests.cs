using System.Collections.Immutable;
using System.Reflection;
using Paddock.Application.Localization;
using Paddock.Application.Racing;
using Paddock.Domain.Racing;
using Paddock.Simulation.Racing.Incidents;
using Paddock.Simulation.Racing.Pits;
using Paddock.Simulation.Racing.Points;
using Paddock.Simulation.Racing.Qualifying;
using Paddock.Simulation.Racing.Reliability;
using Paddock.Simulation.Racing.Weather;
using Paddock.Simulation.Racing.Weekend;
using Paddock.Tests.Racing.Weekend;

namespace Paddock.Tests.Racing;

/// <summary>The readable race report: a small handcrafted race in both languages, and the keys behind every event.</summary>
public class RaceReportTests
{
    private static readonly TranslationCatalog Catalog = TranslationLoader.LoadDirectory(Path.Combine(RepoPaths.Root(), "strings"));

    // ---- The handcrafted race --------------------------------------------------------------------------------------

    private static readonly string[] ExpectedEn =
    [
        "Race report: 1955 season, round 4, testring, 6 laps scheduled",
        "",
        "== Conditions ==",
        "A damp track at the start, air temperature 12 °C.",
        "",
        "== Qualifying ==",
        "ann (alpha) took pole position with 1:30.000, 0.500 s ahead of bob.",
        "Second on the grid: bob (beta), 1:30.500 (+0.500 s).",
        "Third on the grid: cy (gamma), 1:31.000 (+1.000 s).",
        "fay starts from the pit lane.",
        "1 car did not qualify: hal.",
        "",
        "== The opening, laps 1-1 ==",
        "bob led after the first lap; pole-sitter ann was P4 by then.",
        "fay gained 3 places on the opening lap (grid 5, now P2).",
        "ann lost 3 places on the opening lap (grid 1, now P4).",
        "",
        "== The middle of the race, laps 2-5 ==",
        "Lap 2: the track is wet.",
        "Lap 2: a minor incident involving dee, fay.",
        "Lap 3: a major incident involving dee.",
        "Lap 3: dee (delta) is out after an accident.",
        "dee was lightly injured.",
        "Lap 3: the safety car is out.",
        "Lap 3: ann takes the lead from bob.",
        "Lap 4: the safety car comes in and racing resumes.",
        "Lap 4: virtual safety car.",
        "Lap 5: the virtual safety car ends.",
        "Lap 5: red flag, the race is stopped and later restarted.",
        "Lap 5: fay (epsilon) is out with gearbox failure.",
        "",
        "== The closing stages, laps 6-6 ==",
        "Nothing of note happened.",
        "",
        "== Pit stops ==",
        "6 pit stops were made in all.",
        "Finished without a stop: gus.",
        "1 stop: ann, cy.",
        "2 stops: bob, fay.",
        "Lap 4: ann lost time with an error in the pits, 41.500 s.",
        "Lap 4: fay stopped for repairs and spent 95.000 s in the pits.",
        "1 slow stop, the worst: fay, lap 2, 31.000 s.",
        "Fastest routine stop: bob, lap 4, 24.900 s.",
        "Slowest routine stop: cy, lap 3, 28.000 s.",
        "",
        "== Shared drives ==",
        "Lap 3: cy hands the gamma car to cy2.",
        "",
        "== Result ==",
        "ann (alpha) wins in 9:00.000.",
        "Margin over bob: 12.345 s.",
        "Fastest lap: bob (beta), 1:28.000 on lap 4.",
        "4 of 6 starters were running at the flag.",
        "Retirements: 2 (1 mechanical, 1 accidents, 0 other).",
        "2 cars finished on the winner's lap.",
        "1 lap down: cy.",
        "2 laps down: gus.",
        "The lead changed hands 1 time.",
        "Most time in front: ann, 4 of 6 laps.",
        "",
        "== Points ==",
        "ann: 9 points",
        "bob: 6 points",
        "cy: 4.5 points",
        "gus: 1 point",
        "alpha: 9 constructor points",
        "beta: 6 constructor points",
    ];

    private static readonly string[] ExpectedPl =
    [
        "Relacja z wyścigu: sezon 1955, runda 4, tor testring, dystans 6 okr.",
        "",
        "== Warunki ==",
        "Wilgotny tor na starcie, temperatura powietrza 12 °C.",
        "",
        "== Kwalifikacje ==",
        "ann (alpha) zdobywa pole position z czasem 1:30.000, o 0,500 s przed bob.",
        "Drugie pole: bob (beta), 1:30.500 (+0,500 s).",
        "Trzecie pole: cy (gamma), 1:31.000 (+1,000 s).",
        "fay startuje z alei serwisowej.",
        "1 samochód nie zakwalifikował się: hal.",
        "",
        "== Początek, okrążenia 1-1 ==",
        "bob prowadzi po pierwszym okrążeniu; ann, startujący z pole position, jest wtedy na pozycji 4.",
        "fay zyskuje 3 miejsca na pierwszym okrążeniu (pole 5, teraz pozycja 2).",
        "ann traci 3 miejsca na pierwszym okrążeniu (pole 1, teraz pozycja 4).",
        "",
        "== Środek wyścigu, okrążenia 2-5 ==",
        "Okrążenie 2: tor jest mokry.",
        "Okrążenie 2: drobny incydent z udziałem: dee, fay.",
        "Okrążenie 3: poważny incydent z udziałem: dee.",
        "Okrążenie 3: dee (delta) odpada po wypadku.",
        "dee doznaje lekkiej kontuzji.",
        "Okrążenie 3: na tor wyjeżdża samochód bezpieczeństwa.",
        "Okrążenie 3: ann obejmuje prowadzenie po bob.",
        "Okrążenie 4: samochód bezpieczeństwa zjeżdża, wyścig zostaje wznowiony.",
        "Okrążenie 4: wirtualny samochód bezpieczeństwa.",
        "Okrążenie 5: koniec wirtualnego samochodu bezpieczeństwa.",
        "Okrążenie 5: czerwona flaga, wyścig zostaje przerwany i później wznowiony.",
        "Okrążenie 5: fay (epsilon) odpada z powodu awarii skrzyni biegów.",
        "",
        "== Końcówka, okrążenia 6-6 ==",
        "Nic godnego uwagi się nie wydarzyło.",
        "",
        "== Postoje w boksach ==",
        "Łącznie 6 postojów w boksach.",
        "Do mety bez postoju: gus.",
        "1 postój: ann, cy.",
        "2 postoje: bob, fay.",
        "Okrążenie 4: ann traci czas przez błąd w boksach, 41,500 s.",
        "Okrążenie 4: fay zjeżdża na naprawę i spędza w boksach 95,000 s.",
        "1 wolny postój, najgorszy: fay, okrążenie 2, 31,000 s.",
        "Najszybszy zwykły postój: bob, okrążenie 4, 24,900 s.",
        "Najwolniejszy zwykły postój: cy, okrążenie 3, 28,000 s.",
        "",
        "== Zmiany kierowców ==",
        "Okrążenie 3: zmiana kierowców w samochodzie zespołu gamma: cy oddaje kierownicę, jedzie cy2.",
        "",
        "== Wynik ==",
        "ann (alpha) wygrywa w czasie 9:00.000.",
        "Przewaga nad bob: 12,345 s.",
        "Najszybsze okrążenie: bob (beta), 1:28.000 na okrążeniu 4.",
        "4 z 6 samochodów dojechało do mety.",
        "Wycofania: 2 (awarie: 1, wypadki: 1, inne: 0).",
        "2 samochody na okrążeniu zwycięzcy.",
        "Stracili 1 okrążenie: cy.",
        "Stracili 2 okrążenia: gus.",
        "Prowadzenie zmieniło właściciela 1 raz.",
        "Najdłużej na prowadzeniu: ann, 4 z 6 okrążeń.",
        "",
        "== Punkty ==",
        "ann: 9 punktów",
        "bob: 6 punktów",
        "cy: 4,5 punktu",
        "gus: 1 punkt",
        "alpha: 9 punktów w klasyfikacji konstruktorów",
        "beta: 6 punktów w klasyfikacji konstruktorów",
    ];

    [Fact]
    public void AHandmadeRace_ReadsAsExpected_InEnglish() =>
        Assert.Equal(ExpectedEn, Render(Kit.Input(), Language.En));

    [Fact]
    public void AHandmadeRace_ReadsAsExpected_InPolish() =>
        Assert.Equal(ExpectedPl, Render(Kit.Input(), Language.Pl));

    [Fact]
    public void TheReportIsPure_TheSameInputGivesTheSameReport_AndTheInputIsNotChanged()
    {
        var input = Kit.Input();
        var hashBefore = input.Tape.Hash;

        var first = Render(input, Language.En);
        var second = Render(input, Language.En);

        Assert.Equal(first, second);
        Assert.Equal(hashBefore, input.Tape.Hash);
    }

    [Fact]
    public void Verbose_AddsTheLapByLapSection_AndNothingChangesBeforeIt()
    {
        var plain = Render(Kit.Input(), Language.En);
        var verbose = Render(Kit.Input(), Language.En, verbose: true);

        Assert.Equal(plain, verbose.Take(plain.Count));
        Assert.Contains("== Lap by lap ==", verbose);
        var log = verbose.Skip(plain.Count + 2).ToList();
        Assert.Contains("The race starts. Cars: 6. Laps: 6.", log);
        Assert.Contains("Lap 3: ann leads, lap time 1:31.000.", log);
        Assert.Contains("Lap 5: red flag, the race is stopped.", log);
        Assert.Contains("Lap 4: bob rejoins after 24.900 s in the pits.", log);
        Assert.Contains("Lap 4: fastest lap of the race so far: bob, 1:28.000.", log);
        Assert.Contains("ann finishes P1 (laps: 6, time: 9:00.000).", log);
        Assert.Contains("Lap 6: the race is over.", log);
        // The lap chart is the leader's laps only.
        Assert.DoesNotContain(log, l => l.Contains("bob leads", StringComparison.Ordinal) && l.StartsWith("Lap 3", StringComparison.Ordinal));
    }

    [Fact]
    public void TheSafetyCarIsReportedOnlyWhereTheTapeHasOne()
    {
        var input = Kit.Input();
        var calm = input with
        {
            Tape = RaceTape.From(input.Tape.Events.Where(e => e is not (SafetyCar or RedFlag))),
            Neutralisations = [],
        };

        var text = Render(calm, Language.En);

        Assert.DoesNotContain(text, l => l.Contains("safety car", StringComparison.OrdinalIgnoreCase) || l.Contains("red flag", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(Render(input, Language.En), l => l.Contains("safety car", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void SharedDrivesAndPitStopsAppearOnlyWhenTheyHappened()
    {
        var input = Kit.Input();
        var bare = input with
        {
            PitStops = [],
            CarResults = [.. input.CarResults.Select(c => c with { DriversWhoDrove = [c.DriverId], Stops = 0 })],
        };

        var text = Render(bare, Language.En);

        Assert.DoesNotContain("== Shared drives ==", text);
        Assert.Contains("Nobody made a pit stop.", text);
    }

    [Fact]
    public void ARaceWithoutAFinisher_SaysSoInsteadOfNamingAWinner()
    {
        var input = Kit.Input();
        var none = input with { CarResults = [.. input.CarResults.Select(c => c with { Status = FinishStatus.Mechanical })] };

        var text = Render(none, Language.En);

        Assert.Contains("Nobody finished the race.", text);
        Assert.DoesNotContain(text, l => l.Contains("wins in", StringComparison.Ordinal));
    }

    [Fact]
    public void NamesComeFromTheCaller_NotFromTheIds()
    {
        var names = new RaceReportNames(id => id.ToUpperInvariant(), id => "Team " + id);

        var text = Render(Kit.Input(), Language.En, names: names);

        Assert.Contains("ANN (Team alpha) wins in 9:00.000.", text);
    }

    [Fact]
    public void TheInputHoldsNoTrueWeatherAndNoLapRecords_OnlyWhatASpectatorSees()
    {
        var types = typeof(RaceReportInput).GetProperties().Select(p => p.PropertyType).ToList();

        Assert.DoesNotContain(typeof(TruthWeather), types);
        Assert.DoesNotContain(typeof(ImmutableArray<CarLapRecord>), types);
        Assert.Equal(["AirTempC", "Track"], typeof(RaceReportConditions).GetProperties().Select(p => p.Name).Order(StringComparer.Ordinal).ToList());
    }

    [Fact]
    [Trait("Category", "Slow")]
    public void TheConditionsAreTheStateAtTheStartOnly_NotTheOnsetOrThePeak()
    {
        // Same race, all seeds: the conditions line is one of the four start lines and carries nothing but the track state and a temperature.
        foreach (var result in WeekendTestKit.Races(1988, 6))
        {
            var report = RaceReportBuilder.Build(RaceReportInput.From(result, 1988, 1, "x"));
            var conditions = report.Sections[0];

            var line = Assert.Single(conditions.Lines);
            Assert.StartsWith("report.conditions.start.", line.Key, StringComparison.Ordinal);
            Assert.Equal(["temp"], line.Args.Select(a => a.Key));
            Assert.Equal(RaceReportKeys.ConditionStartKey(result.TruthWeather.At(0).WetnessBand), line.Key);
        }
    }

    // ---- Keys ------------------------------------------------------------------------------------------------------

    [Fact]
    public void EveryEventKindMapsToAKeyThatExistsInBothLanguages_Snapshot()
    {
        var tape = Kit.Input().Tape;
        var seenKinds = tape.Events.Select(e => e.Kind).Distinct().Order().ToList();
        Assert.Equal(Enum.GetValues<RaceEventKind>().Order().ToList(), seenKinds);

        var map = tape.Events
            .GroupBy(e => e.Kind)
            .OrderBy(g => g.Key)
            .Select(g => $"{g.Key}: {string.Join(", ", g.Select(RaceReportKeys.For).Distinct().Order(StringComparer.Ordinal))}")
            .ToList();

        Assert.Equal(
            [
                "RaceStarted: report.event.raceStarted",
                "LapCompleted: report.event.lap",
                "PitStop: report.event.pitIn, report.event.pitOut",
                "PositionChange: report.event.position",
                "Incident: report.incident.major, report.incident.minor",
                "Retirement: report.retire.accident, report.retire.mechanical",
                "WeatherChange: report.conditions.change.wet",
                "SafetyCar: report.neutral.safetyCar.deployed, report.neutral.safetyCar.ending, report.neutral.virtual.deployed, report.neutral.virtual.ending",
                "RedFlag: report.neutral.redFlag",
                "FastestLap: report.event.fastestLap",
                "Finished: report.event.finished",
                "RaceEnded: report.event.raceEnded",
            ],
            map);

        foreach (var e in tape.Events)
        {
            AssertKeyInBothLanguages(RaceReportKeys.For(e));
        }
    }

    [Fact]
    public void EveryTapeEventKindHasAnEventSubclassAndAKey()
    {
        // Adding a RaceEvent subclass without a report key must fail here, not at the first race that contains it.
        var subclasses = typeof(RaceEvent).Assembly.GetTypes().Where(t => t.IsSubclassOf(typeof(RaceEvent)) && !t.IsAbstract).ToList();

        Assert.Equal(Enum.GetValues<RaceEventKind>().Length, subclasses.Count);
    }

    [Fact]
    public void EveryRetirementReasonAndFailedPartMapsToAKey()
    {
        foreach (var reason in Enum.GetValues<RetirementReason>())
        {
            AssertKeyInBothLanguages(RaceReportKeys.RetirementKey(reason, null));
            foreach (var part in Enum.GetValues<MechanicalComponent>())
            {
                AssertKeyInBothLanguages(RaceReportKeys.RetirementKey(reason, part));
            }
        }

        // Every failed part has a key of its own.
        var keys = Enum.GetValues<MechanicalComponent>().Select(p => RaceReportKeys.RetirementKey(RetirementReason.Mechanical, p)).ToList();
        Assert.Equal(keys.Count, keys.Distinct().Count());
        Assert.DoesNotContain(RaceReportKeys.RetireMechanical, keys);
        AssertKeyInBothLanguages(RaceReportKeys.RetirementKey(RetirementReason.Mechanical, MechanicalComponent.Engine, sudden: true));
        Assert.Equal(RaceReportKeys.RetireSudden, RaceReportKeys.RetirementKey(RetirementReason.Mechanical, null, sudden: true));
    }

    [Fact]
    public void EverySeverityGradeTrackStateAndSafetyCarPhaseMapsToAKey()
    {
        foreach (var severity in Enum.GetValues<IncidentSeverity>())
        {
            AssertKeyInBothLanguages(RaceReportKeys.IncidentKey(severity));
        }

        foreach (var grade in Enum.GetValues<InjuryGrade>().Where(g => g != InjuryGrade.None))
        {
            AssertKeyInBothLanguages(RaceReportKeys.InjuryKey(grade));
        }

        foreach (var band in Enum.GetValues<WetnessBand>())
        {
            AssertKeyInBothLanguages(RaceReportKeys.ConditionStartKey(band));
            AssertKeyInBothLanguages(RaceReportKeys.ConditionChangeKey(band));
            Assert.Equal(RaceReportKeys.ConditionChangeKey(band), RaceReportKeys.ConditionChangeKey(band.ToString().ToLowerInvariant()));
        }

        // An unknown condition text never leaks into the report.
        Assert.Equal(RaceReportKeys.ChangeOther, RaceReportKeys.ConditionChangeKey("sleet"));
        foreach (var phase in Enum.GetValues<SafetyCarPhase>())
        {
            foreach (var virtualCar in new[] { false, true })
            {
                AssertKeyInBothLanguages(RaceReportKeys.For(new SafetyCar(1, 1, 1, phase, virtualCar)));
            }
        }
    }

    [Fact]
    public void EveryKeyConstantExistsInBothLanguages()
    {
        var constants = typeof(RaceReportKeys)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f is { IsLiteral: true, FieldType.Name: "String" })
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToList();

        Assert.True(constants.Count > 80);
        Assert.Equal(constants.Count, constants.Distinct().Count());
        foreach (var key in constants)
        {
            AssertKeyInBothLanguages(key);
        }
    }

    [Fact]
    [Trait("Category", "Slow")]
    public void EveryLineThatCarriesACountIsAPluralEntryInBothLanguages_OnRealRacesOfManyEras()
    {
        var counted = new HashSet<string>(StringComparer.Ordinal);
        foreach (var season in new[] { 1950, 1955, 1968, 1976, 1988, 2000, 2012, 2022 })
        {
            foreach (var result in WeekendTestKit.Races(season, 4))
            {
                var report = RaceReportBuilder.Build(RaceReportInput.From(result, season, 1, "x"), new RaceReportOptions(Verbose: true));
                foreach (var line in report.Sections.SelectMany(s => s.Lines))
                {
                    AssertKeyInBothLanguages(line.Key);
                    if (line.Count is not null)
                    {
                        counted.Add(line.Key);
                    }
                }
            }
        }

        Assert.NotEmpty(counted);
        foreach (var key in counted)
        {
            foreach (var language in new[] { Language.Pl, Language.En })
            {
                Assert.True(Catalog.Entries(language)[key].IsPlural, $"{key} carries a count but is not a plural entry in {language}.");
            }
        }
    }

    [Theory]
    [Trait("Category", "Slow")]
    [InlineData(1950)]
    [InlineData(1955)]
    [InlineData(1970)]
    [InlineData(1988)]
    [InlineData(2022)]
    public void RealRacesRenderInBothLanguagesWithoutAFallbackAndWithoutRawNames(int season)
    {
        foreach (var result in WeekendTestKit.Races(season, 4))
        {
            foreach (var language in new[] { Language.Pl, Language.En })
            {
                var sink = new CollectingMissingKeySink();
                var report = RaceReportBuilder.Build(RaceReportInput.From(result, season, 1, "x"), new RaceReportOptions(Verbose: true));

                var lines = RaceReportRenderer.Render(report, new Localizer(Catalog, language, sink));

                Assert.Empty(sink.Reports);
                Assert.DoesNotContain(lines, l => l.Contains("report.", StringComparison.Ordinal) || l.Contains('{', StringComparison.Ordinal));
                foreach (var raw in new[] { "RaceStarted", "LapCompleted", "PitStop", "PositionChange", "WeatherChange", "FastestLap", "RaceEnded" })
                {
                    Assert.DoesNotContain(lines, l => l.Contains(raw, StringComparison.Ordinal));
                }
            }
        }
    }

    // ---- Helpers ---------------------------------------------------------------------------------------------------

    private static void AssertKeyInBothLanguages(string key)
    {
        foreach (var language in new[] { Language.Pl, Language.En })
        {
            Assert.True(Catalog.TryGet(language, key, out _), $"{key} is missing in {language}.");
        }
    }

    private static List<string> Render(RaceReportInput input, Language language, bool verbose = false, RaceReportNames? names = null)
    {
        var sink = new CollectingMissingKeySink();
        var report = RaceReportBuilder.Build(input, new RaceReportOptions(verbose));
        var lines = RaceReportRenderer.Render(report, new Localizer(Catalog, language, sink), names);
        Assert.Empty(sink.Reports);
        return [.. lines];
    }

    /// <summary>
    /// A six-car race of six laps made by hand: a damp start that turns wet, a lead change, a safety car, a virtual safety car and a
    /// red flag that restarts the race, an accident with an injury, a gearbox failure, a shared car that swaps drivers, an error and a
    /// repair in the pits, a lapped car and a car that never stops, and every kind of event the tape has.
    /// </summary>
    private static class Kit
    {
        public static RaceReportInput Input()
        {
            var tape = new RaceTape.Builder();
            var seq = 0;
            long time = 0;

            void Add(Func<int, long, RaceEvent> make, long advanceMs = 1_000)
            {
                time += advanceMs;
                tape.Append(make(seq++, time));
            }

            tape.Append(new RaceStarted(seq++, 0, 0, 6, ["ann", "bob", "cy", "dee", "fay", "gus"]));

            // Lap 1: bob leads, the pole-sitter ann falls to fourth, fay gains three places from the back of the grid.
            Add((s, t) => new LapCompleted(s, 1, t, "bob", 91_000, 1, 0));
            Add((s, t) => new PositionChange(s, 1, t, "bob", 2, 1));
            Add((s, t) => new LapCompleted(s, 1, t, "fay", 92_000, 2, 1_000));
            Add((s, t) => new PositionChange(s, 1, t, "fay", 5, 2));
            Add((s, t) => new LapCompleted(s, 1, t, "cy", 92_500, 3, 2_000));
            Add((s, t) => new LapCompleted(s, 1, t, "ann", 93_000, 4, 3_000));
            Add((s, t) => new PositionChange(s, 1, t, "ann", 1, 4));
            Add((s, t) => new LapCompleted(s, 1, t, "dee", 93_500, 5, 4_000));
            Add((s, t) => new PositionChange(s, 1, t, "dee", 4, 5));
            Add((s, t) => new LapCompleted(s, 1, t, "gus", 94_000, 6, 5_000));

            // Lap 2: the track turns wet, a minor touch, two stops (one slow).
            Add((s, t) => new WeatherChange(s, 2, t, "wet"));
            Add((s, t) => new Incident(s, 2, t, ["dee", "fay"], IncidentSeverity.Minor));
            Add((s, t) => new PitStop(s, 2, t, "bob", PitLanePhase.In, 0, "wet"));
            Add((s, t) => new PitStop(s, 2, t, "bob", PitLanePhase.Out, 25_100, "wet"));
            Add((s, t) => new PitStop(s, 2, t, "fay", PitLanePhase.In, 0, "wet"));
            Add((s, t) => new PitStop(s, 2, t, "fay", PitLanePhase.Out, 31_000, "wet"));
            Add((s, t) => new LapCompleted(s, 2, t, "bob", 90_000, 1, 0));

            // Lap 3: an accident ends dee's race, the safety car comes out, cy swaps drivers, ann takes the lead.
            Add((s, t) => new Incident(s, 3, t, ["dee"], IncidentSeverity.Major));
            Add((s, t) => new Retirement(s, 3, t, "dee", RetirementReason.Accident), 0);
            Add((s, t) => new SafetyCar(s, 3, t, SafetyCarPhase.Deployed, false));
            Add((s, t) => new PitStop(s, 3, t, "cy", PitLanePhase.In, 0, "wet"));
            Add((s, t) => new PitStop(s, 3, t, "cy", PitLanePhase.Out, 28_000, "wet"));
            Add((s, t) => new LapCompleted(s, 3, t, "ann", 91_000, 1, 0));

            // Lap 4: racing resumes, a virtual safety car, an error and a repair in the pits, the fastest lap.
            Add((s, t) => new SafetyCar(s, 4, t, SafetyCarPhase.Ending, false));
            Add((s, t) => new SafetyCar(s, 4, t, SafetyCarPhase.Deployed, true));
            Add((s, t) => new PitStop(s, 4, t, "ann", PitLanePhase.In, 0, "wet"));
            Add((s, t) => new PitStop(s, 4, t, "ann", PitLanePhase.Out, 41_500, "wet"));
            Add((s, t) => new PitStop(s, 4, t, "bob", PitLanePhase.In, 0, "wet"));
            Add((s, t) => new PitStop(s, 4, t, "bob", PitLanePhase.Out, 24_900, "wet"));
            Add((s, t) => new PitStop(s, 4, t, "fay", PitLanePhase.In, 0, "wet"));
            Add((s, t) => new PitStop(s, 4, t, "fay", PitLanePhase.Out, 95_000, "wet"));
            Add((s, t) => new FastestLap(s, 4, t, "bob", 88_000));
            Add((s, t) => new LapCompleted(s, 4, t, "ann", 92_000, 1, 0));

            // Lap 5: the virtual safety car ends, a red flag that restarts the race, fay's gearbox fails.
            Add((s, t) => new SafetyCar(s, 5, t, SafetyCarPhase.Ending, true));
            Add((s, t) => new RedFlag(s, 5, t));
            Add((s, t) => new Retirement(s, 5, t, "fay", RetirementReason.Mechanical));
            Add((s, t) => new LapCompleted(s, 5, t, "ann", 90_500, 1, 0));

            // Lap 6: the flag. Cy and gus are lapped.
            Add((s, t) => new LapCompleted(s, 6, t, "ann", 90_000, 1, 0));
            Add((s, t) => new Finished(s, 6, t, "ann", 1, 6, 540_000));
            Add((s, t) => new Finished(s, 6, t, "bob", 2, 6, 552_345));
            Add((s, t) => new Finished(s, 5, t, "cy", 3, 5, 560_000));
            Add((s, t) => new Finished(s, 4, t, "gus", 4, 4, 570_000));
            Add((s, t) => new RaceEnded(s, 6, t));

            return new RaceReportInput(
                1955,
                4,
                "testring",
                new RaceReportConditions(WetnessBand.Damp, 12.4),
                Qualifying(),
                tape.Build(),
                Cars(),
                Classification(),
                [
                    new PersonRaceOutcome("dee", "car-d", 2, true, RetirementReason.Accident, null, 3, InjuryGrade.Light, false),
                    new PersonRaceOutcome("fay", "car-f", 4, true, RetirementReason.Mechanical, MechanicalComponent.Gearbox, 5, InjuryGrade.None, false),
                ],
                Stops(),
                [new NeutralisationRecord(NeutralisationKind.SafetyCar, 3, 4, false), new NeutralisationRecord(NeutralisationKind.RedFlag, 5, 5, true)],
                6,
                6);
        }

        private static QualifyingResult Qualifying() => new(
            [
                Slot("ann", "alpha", 1, 90.0),
                Slot("bob", "beta", 2, 90.5),
                Slot("cy", "gamma", 3, 91.0),
                Slot("dee", "delta", 4, 91.5),
                Slot("gus", "zeta", 6, 92.5),
                Slot("fay", "epsilon", 0, 93.0),
            ],
            [new QualifyingNonQualifier("hal", "delta", NonQualifierReason.OutsideCutoff, 99.0, [])],
            [],
            12,
            []);

        private static QualifyingGridSlot Slot(string driver, string team, int position, double seconds) =>
            new(driver, team, position, seconds, seconds, []);

        private static ImmutableArray<CarRaceResult> Cars() =>
        [
            new("car-a", "ann", "alpha", 1, FinishStatus.Classified, 6, 540.0, null, null, ["ann"], 1, ["wet", "wet"], false),
            new("car-b", "bob", "beta", 2, FinishStatus.Classified, 6, 552.345, null, null, ["bob"], 2, ["wet", "wet", "wet"], true),
            new("car-c", "cy", "gamma", 3, FinishStatus.Classified, 5, 560.0, null, null, ["cy", "cy2"], 1, ["wet", "wet"], false),
            new("car-g", "gus", "zeta", 6, FinishStatus.Classified, 4, 570.0, null, null, ["gus"], 0, ["wet"], false),
            new("car-f", "fay", "epsilon", 5, FinishStatus.Mechanical, 4, 380.0, 5, MechanicalComponent.Gearbox, ["fay"], 2, ["wet", "wet", "wet"], false),
            new("car-d", "dee", "delta", 4, FinishStatus.Accident, 2, 180.0, 3, null, ["dee"], 0, ["wet"], false),
        ];

        private static ImmutableArray<PitStopRecord> Stops() =>
        [
            new("car-b", 2, 25.1, true, "wet", 0, false, false, PitStopFault.None),
            new("car-f", 2, 31.0, true, "wet", 0, false, false, PitStopFault.SlowStop),
            new("car-c", 3, 28.0, true, "wet", 0, true, false, PitStopFault.None),
            new("car-a", 4, 41.5, true, "wet", 0, false, false, PitStopFault.Error),
            new("car-b", 4, 24.9, true, "wet", 0, false, false, PitStopFault.None),
            new("car-f", 4, 95.0, false, "wet", 0, false, true, PitStopFault.None),
        ];

        private static RaceClassification Classification() => new(
            [
                new ClassifiedCar(1, true, "alpha", ["ann"], 9m, 9m),
                new ClassifiedCar(2, true, "beta", ["bob"], 6m, 6m),
                new ClassifiedCar(3, true, "gamma", ["cy", "cy2"], 4.5m, 4.5m),
                new ClassifiedCar(4, true, "zeta", ["gus"], 1m, 0m),
                new ClassifiedCar(5, false, "epsilon", ["fay"], 0m, 0m),
                new ClassifiedCar(6, false, "delta", ["dee"], 0m, 0m),
            ],
            [
                new DriverScore("ann", 9m, 1),
                new DriverScore("bob", 6m, 2),
                new DriverScore("cy", 4.5m, 3),
                new DriverScore("cy2", 0m, null),
                new DriverScore("gus", 1m, 4),
            ],
            [
                new ConstructorScore("alpha", 9m, [1]),
                new ConstructorScore("beta", 6m, [2]),
                new ConstructorScore("zeta", 0m, [4]),
            ]);
    }
}
