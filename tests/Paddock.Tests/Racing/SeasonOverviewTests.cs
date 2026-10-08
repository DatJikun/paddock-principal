using Paddock.Application.Career;
using Paddock.Application.Racing;
using Paddock.Data.Historical;
using Paddock.Domain.Career;
using Paddock.Domain.Racing;
using Paddock.Domain.Time;
using Paddock.Simulation.Career;
using Paddock.Tests.Career;

namespace Paddock.Tests.Racing;

/// <summary>#267: the season grid, the podium count, and the circuit page's past races. None of it reads the local Jolpica cache.</summary>
public sealed class SeasonOverviewTests
{
    private const ulong Seed = 7;

    private static CareerSession RunToFirstRace()
    {
        var session = CareerKit.Open(CareerPreset.Chaos, 1955, Seed);
        CareerHost.RunUntil(session, new GameDate(1955, 3, 4), null, CareerKit.Options);
        return session;
    }

    private static Dictionary<string, CircuitLabel> Circuits() =>
        CareerKit.Data.Circuits.Circuits
            .SelectMany(circuit => circuit.Layouts.Select(layout => (layout.LayoutId, Label: new CircuitLabel(circuit.CircuitId, circuit.Name, circuit.Country))))
            .ToDictionary(pair => pair.LayoutId, pair => pair.Label, StringComparer.Ordinal);

    private static Dictionary<string, TrackFacts> Tracks() =>
        CareerKit.Data.Circuits.Circuits
            .SelectMany(circuit => circuit.Layouts.Select(layout => new TrackFacts(layout.LayoutId, circuit.CircuitId, circuit.Name, circuit.Country, layout.LengthKm, layout.Character.ToArray(), [])))
            .ToDictionary(facts => facts.LayoutId, StringComparer.Ordinal);

    [Fact]
    public void TheGridHasOneColumnPerRoundAndTheStoredFinishOfEveryDriver()
    {
        var session = RunToFirstRace();
        var inputs = CareerKit.Options.Inputs!;
        var overview = SeasonOverviewRead.Read(session, inputs, Circuits());
        var stored = session.World.Section<RaceResultsSection>(RaceResultsSection.SectionName)!.Latest()!;

        Assert.Equal(1955, overview.Season);
        // #264: a constructors' table always exists, also in seasons with no official title.
        Assert.NotEmpty(overview.Constructors);
        Assert.True(overview.Rounds.Count > 1);
        Assert.Equal(1, overview.Rounds.Count(round => round.Finished));
        Assert.All(overview.Rounds, round => Assert.False(string.IsNullOrEmpty(round.Country)));
        Assert.Equal(stored.Rows.Count, overview.Drivers.Count);
        Assert.All(overview.Drivers, driver => Assert.Equal(overview.Rounds.Count, driver.Cells.Count));

        var winner = stored.Rows.Single(row => row.Position == 1);
        var cell = overview.Drivers.Single(driver => driver.Id == winner.DriverId).Cells.Single(item => item.Round == stored.Round);
        var result = Assert.Single(cell.Results);
        Assert.Equal(1, result.Position);
        Assert.True(result.Classified);

        var retired = stored.Rows.FirstOrDefault(row => !row.Classified);
        if (retired is not null)
        {
            var shown = Assert.Single(overview.Drivers.Single(driver => driver.Id == retired.DriverId).Cells.Single(item => item.Round == stored.Round).Results);
            Assert.False(shown.Classified);
            Assert.NotEmpty(shown.RetirementKey);
        }

        var later = overview.Drivers[0].Cells.Last();
        Assert.Empty(later.Results);
    }

    [Fact]
    public void AConstructorCellListsEveryCarOfTheTeamBestFirst()
    {
        var session = CareerKit.Open(CareerPreset.Chaos, 1958, Seed);
        CareerHost.RunUntil(session, new GameDate(1958, 3, 4), null, CareerKit.Options);
        var overview = SeasonOverviewRead.Read(session, CareerKit.Options.Inputs!, Circuits());
        var stored = session.World.Section<RaceResultsSection>(RaceResultsSection.SectionName)!.Latest()!;

        Assert.True(overview.HasConstructorTitle);
        Assert.NotEmpty(overview.Constructors);
        foreach (var team in overview.Constructors)
        {
            var cars = stored.Rows.Where(row => row.TeamId == team.Id).Select(row => row.Position).Order().ToArray();
            var cell = team.Cells.Single(item => item.Round == stored.Round);
            Assert.Equal(cars, cell.Results.Select(result => result.Position).ToArray());
        }
    }

    [Fact]
    public void StandingsCountTopThreeFinishesAsPodiums()
    {
        var session = RunToFirstRace();
        var standings = ChampionshipRead.Standings(session, CareerKit.Options.Inputs!);
        var stored = session.World.Section<RaceResultsSection>(RaceResultsSection.SectionName)!.Latest()!;
        foreach (var driver in standings.Drivers)
        {
            var podium = stored.Rows.Count(row => row.DriverId == driver.Id && row.Classified && row.Position <= 3);
            Assert.Equal(podium, driver.Podiums);
        }

        Assert.All(standings.Drivers, driver => Assert.True(driver.Podiums >= driver.Wins));
    }

    [Fact]
    public void ACircuitPageListsCareerRoundsThenRealPastRacesOfThatCircuitOnly()
    {
        var session = RunToFirstRace();
        var stored = session.World.Section<RaceResultsSection>(RaceResultsSection.SectionName)!.Latest()!;
        var tracks = Tracks();
        var circuit = tracks[stored.LayoutId].CircuitId;
        PastPodiumView[] Podium(string name) => [new PastPodiumView(1, name, "Italian", "ferrari", "Ferrari")];
        var history = new[]
        {
            new HistoricalRaceFact(1952, circuit, Podium("Older Winner")),
            new HistoricalRaceFact(1954, circuit, Podium("Newer Winner")),
            new HistoricalRaceFact(1953, "elsewhere", Podium("Other Track")),
        };

        var view = TrackRead.Read(session, tracks, stored.LayoutId, history);

        Assert.Equal(3, view.Past.Count);
        Assert.Equal(PastRaceSource.Career, view.Past[0].Source);
        Assert.Equal(1955, view.Past[0].Season);
        Assert.InRange(view.Past[0].Podium.Count, 1, 3);
        Assert.Equal([1954, 1952], view.Past.Skip(1).Select(race => race.Season).ToArray());
        Assert.All(view.Past.Skip(1), race => Assert.Equal(PastRaceSource.Archive, race.Source));
        Assert.DoesNotContain(view.Past, race => race.Podium.Any(place => place.DriverName == "Other Track"));
    }

    [Fact]
    public void ACircuitPageWithoutTheLocalCacheStillListsTheCareersOwnRounds()
    {
        var session = RunToFirstRace();
        var stored = session.World.Section<RaceResultsSection>(RaceResultsSection.SectionName)!.Latest()!;
        var view = TrackRead.Read(session, Tracks(), stored.LayoutId);
        var only = Assert.Single(view.Past);
        Assert.Equal(PastRaceSource.Career, only.Source);
        Assert.Equal(1, only.Podium[0].Position);
    }

    [Fact]
    public void PastPodiumsJoinNamesAndDropRacesFromTheCareersOwnPeriod()
    {
        const string races = """{"schemaVersion":1,"races":[{"season":1950,"round":1,"name":"A","circuitId":"silverstone","date":"1950-05-13","time":null,"url":"","isIndianapolis500":false},{"season":1956,"round":1,"name":"B","circuitId":"silverstone","date":"1956-05-13","time":null,"url":"","isIndianapolis500":false}]}""";
        const string results = """
            {"schemaVersion":1,"results":[
            {"season":1950,"round":1,"driverId":"farina","constructorId":"alfa","carNumber":"2","position":1,"positionText":"1","status":"Finished","points":9,"grid":1,"laps":70,"time":null,"timeMillis":null,"isClassified":true,"isDisqualified":false,"isSharedDrive":false},
            {"season":1950,"round":1,"driverId":"fagioli","constructorId":"alfa","carNumber":"3","position":2,"positionText":"2","status":"Finished","points":6,"grid":2,"laps":70,"time":null,"timeMillis":null,"isClassified":true,"isDisqualified":false,"isSharedDrive":false},
            {"season":1950,"round":1,"driverId":"fagioli","constructorId":"alfa","carNumber":"4","position":4,"positionText":"R","status":"Engine","points":0,"grid":3,"laps":10,"time":null,"timeMillis":null,"isClassified":false,"isDisqualified":false,"isSharedDrive":false},
            {"season":1956,"round":1,"driverId":"farina","constructorId":"alfa","carNumber":"2","position":1,"positionText":"1","status":"Finished","points":8,"grid":1,"laps":70,"time":null,"timeMillis":null,"isClassified":true,"isDisqualified":false,"isSharedDrive":false}]}
            """;
        const string drivers = """{"schemaVersion":1,"drivers":[{"driverId":"farina","givenName":"Nino","familyName":"Farina","dateOfBirth":null,"nationality":"Italian","code":null,"permanentNumber":null,"url":null},{"driverId":"fagioli","givenName":"Luigi","familyName":"Fagioli","dateOfBirth":null,"nationality":"Italian","code":null,"permanentNumber":null,"url":null}]}""";
        const string constructors = """{"schemaVersion":1,"constructors":[{"constructorId":"alfa","name":"Alfa Romeo","nationality":"Italian","url":""}]}""";

        var podiums = PastPodiumLoader.Parse(races, results, drivers, constructors, beforeSeason: 1955);

        var race = Assert.Single(podiums);
        Assert.Equal(1950, race.Season);
        Assert.Equal("silverstone", race.CircuitId);
        Assert.Equal(["Nino Farina", "Luigi Fagioli"], race.Places.Select(place => place.DriverName).ToArray());
        Assert.Equal("Alfa Romeo", race.Places[0].ConstructorName);
        Assert.Equal("alfa", race.Places[0].ConstructorId);
    }

    [Fact]
    public void WithoutTheCacheNoPastPodiumsAreLoaded()
    {
        var empty = Directory.CreateTempSubdirectory("paddock-267-").FullName;
        try
        {
            Assert.Empty(PastPodiumLoader.Load(empty, 1955));
        }
        finally
        {
            Directory.Delete(empty, recursive: true);
        }
    }
}
