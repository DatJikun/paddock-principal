using Paddock.Application.Contracts;
using Paddock.Application.Racing;
using Paddock.Desktop.Bridge;
using Paddock.Domain.People;
using Paddock.Domain.Racing;
using Paddock.Domain.World;
using static Paddock.Tests.Contracts.ContractKit;

namespace Paddock.Tests.Contracts;

/// <summary>
/// #331: a driver's season row carries his place in the championship and his points. A past season has no stored table, so the
/// place and points come from the plain totals of that season; the season in progress takes the official standings.
/// </summary>
public sealed class DriverSeasonPlaceTests
{
    private static readonly PersonId Rival = PersonId.Real("fixture_driver_rival");

    private static readonly PersonId Third = PersonId.Real("fixture_driver_third");

    private static WorldState WithArchive(WorldState world) =>
        world.WithSection(RaceResultsSection.Restore(
        [
            new StoredRace(1955, 1, "fixture_layout", [
                new RaceResultRow(1, true, Rival.Value, TeamB.Value, "8", ""),
                new RaceResultRow(2, true, Veteran.Value, TeamA.Value, "6", ""),
                new RaceResultRow(3, true, Third.Value, TeamC.Value, "4", ""),
            ], []),
            new StoredRace(1955, 2, "fixture_layout", [
                new RaceResultRow(1, true, Rival.Value, TeamB.Value, "8", ""),
                new RaceResultRow(2, true, Third.Value, TeamC.Value, "6", ""),
                new RaceResultRow(3, false, Veteran.Value, TeamA.Value, "0", "fixture.retired"),
            ], []),
        ]));

    [Fact]
    public void APastSeasonGivesThePlaceAndPointsOfTheWholeField()
    {
        var lab = new Lab();

        var profile = DriverProfileRead.Of(WithArchive(lab.World), TeamA, lab.Today, Veteran.Value);

        var row = Assert.Single(profile.Seasons);
        Assert.Equal(1955, row.Season);
        Assert.Equal(3, row.Place);
        Assert.Equal("6", row.Points);
        Assert.Equal((2, 0, 1, 1), (row.Starts, row.Wins, row.Podiums, row.Retirements));
    }

    [Fact]
    public void TheSeasonInProgressTakesThePlaceAndPointsOfTheStandings()
    {
        var lab = new Lab();
        var standings = new StandingsView(
            1955,
            2,
            8,
            null,
            [new StandingRowView(2, Veteran.Value, "Veteran", "7.5", 0, "GBR", TeamA.Value, "Team A", 1)],
            []);

        var profile = DriverProfileRead.Of(WithArchive(lab.World), TeamA, lab.Today, Veteran.Value, null, standings);

        var row = Assert.Single(profile.Seasons);
        Assert.Equal((2, "7.5"), (row.Place, row.Points));
        Assert.Equal(2, row.Starts);
    }

    [Fact]
    public void TheBridgeSendsThePlaceAndThePointsOfEachSeason()
    {
        var lab = new Lab();

        var node = BridgeValues.ToNode(DriverProfileRead.Of(WithArchive(lab.World), TeamA, lab.Today, Veteran.Value));

        var season = node!["seasons"]!.AsArray().Single()!;
        Assert.Equal((3L, "6"), (season["place"]!.GetValue<long>(), season["points"]!.GetValue<string>()));
    }

    [Fact]
    public void ADriverWithoutAStandingRowKeepsThePlainTotals()
    {
        var lab = new Lab();
        var standings = new StandingsView(1955, 2, 8, null, [new StandingRowView(1, Rival.Value, "Rival", "16", 2, "GBR", TeamB.Value, "Team B", 2)], []);

        var row = Assert.Single(DriverProfileRead.Of(WithArchive(lab.World), TeamA, lab.Today, Veteran.Value, null, standings).Seasons);

        Assert.Equal((3, "6"), (row.Place, row.Points));
    }
}
