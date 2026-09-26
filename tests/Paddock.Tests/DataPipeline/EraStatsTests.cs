using System.Text.Json;
using Paddock.Data.Historical;
using Paddock.DataPipeline;

namespace Paddock.Tests.DataPipeline;

public class EraStatsTests
{
    [Fact]
    public void StatusTableSeparatesFinishesNonStartsAndTheThreeDnfBuckets()
    {
        Assert.Equal(FinishStatus.Kind.ClassifiedFinish, FinishStatus.Classify("Finished"));
        Assert.Equal(FinishStatus.Kind.ClassifiedFinish, FinishStatus.Classify("Lapped"));
        Assert.Equal(FinishStatus.Kind.ClassifiedFinish, FinishStatus.Classify("+1 Lap"));
        Assert.Equal(FinishStatus.Kind.ClassifiedFinish, FinishStatus.Classify("+6 Laps"));
        Assert.Equal(FinishStatus.Kind.ClassifiedFinish, FinishStatus.Classify("+46 Laps"));
        Assert.Equal(FinishStatus.Kind.NonStart, FinishStatus.Classify("Withdrew"));
        Assert.Equal(FinishStatus.Kind.NonStart, FinishStatus.Classify("Did not start"));
        Assert.Equal(FinishStatus.Kind.NonStart, FinishStatus.Classify("Did not qualify"));
        Assert.Equal(FinishStatus.Kind.Mechanical, FinishStatus.Classify("Oil leak"));
        Assert.Equal(FinishStatus.Kind.Mechanical, FinishStatus.Classify("Overheating"));
        Assert.Equal(FinishStatus.Kind.Mechanical, FinishStatus.Classify("Engine"));
        Assert.Equal(FinishStatus.Kind.Accident, FinishStatus.Classify("Accident"));
        Assert.Equal(FinishStatus.Kind.Accident, FinishStatus.Classify("Collision damage"));
        Assert.Equal(FinishStatus.Kind.Accident, FinishStatus.Classify("Spun off"));
        Assert.Equal(FinishStatus.Kind.Other, FinishStatus.Classify("Not classified"));
        Assert.Equal(FinishStatus.Kind.Other, FinishStatus.Classify("Disqualified"));
        Assert.Equal(FinishStatus.Kind.Other, FinishStatus.Classify("Retired"));
        Assert.Equal(FinishStatus.Kind.Unmapped, FinishStatus.Classify("Flux capacitor"));
        Assert.NotEmpty(FinishStatus.DeliberatelyUnmappedStatuses);
        Assert.Equal(FinishStatus.DeliberatelyUnmappedStatuses.Count, FinishStatus.DeliberatelyUnmappedStatuses.Distinct(StringComparer.Ordinal).Count());
        Assert.All(FinishStatus.DeliberatelyUnmappedStatuses, status => Assert.Equal(FinishStatus.Kind.Unmapped, FinishStatus.Classify(status)));
    }

    [Fact]
    public void ChampionshipRulesFollowTheRegulationsTimeline()
    {
        for (var year = 1950; year <= 2025; year++)
        {
            Assert.True(ChampionshipRules.ForSeason(year).WinPoints > 0);
        }

        var early = ChampionshipRules.ForSeason(1950);
        Assert.Equal(8, early.WinPoints);
        Assert.Equal(1, early.FastestLapBonus);
        Assert.False(early.DoublePointsFinale);
        var best = Assert.IsType<BestResults>(early.Quota);
        Assert.Equal(4, best.Count);
        Assert.Equal(9m, ChampionshipRules.MaximumForRound(early, 7, 7));
        Assert.Equal(22m, ChampionshipRules.CountRounds([(1, 9m), (2, 6m), (3, 4m), (4, 3m), (5, 2m)], early));

        var split = ChampionshipRules.ForSeason(1967);
        var halves = Assert.IsType<SplitResults>(split.Quota);
        Assert.Equal(6, halves.FirstHalfLastRound);
        Assert.Equal(5, halves.KeepFirst);
        Assert.Equal(4, halves.KeepSecond);
        Assert.Equal(
            46m,
            ChampionshipRules.CountRounds(
                [(1, 9m), (2, 6m), (3, 4m), (4, 3m), (5, 2m), (6, 1m), (7, 9m), (8, 6m), (9, 4m), (10, 3m), (11, 1m)],
                split));

        var finale = ChampionshipRules.ForSeason(2014);
        Assert.True(finale.DoublePointsFinale);
        Assert.IsType<AllResults>(finale.Quota);
        Assert.Equal(25m, ChampionshipRules.MaximumForRound(finale, 1, 19));
        Assert.Equal(50m, ChampionshipRules.MaximumForRound(finale, 19, 19));
        Assert.Equal(26m, ChampionshipRules.MaximumForRound(ChampionshipRules.ForSeason(2019), 1, 21));
        Assert.Equal(25m, ChampionshipRules.MaximumForRound(ChampionshipRules.ForSeason(2025), 1, 24));
        Assert.Equal(7, Assert.IsType<SplitResults>(ChampionshipRules.ForSeason(1979).Quota).FirstHalfLastRound);
    }

    [Fact]
    public async Task StatsOnTheFixturesExcludesSharedDrivesAndIndyFromTheMainFigures()
    {
        var cache = Nest(JolpicaFixtures.Materialize());
        var root = Directory.GetParent(cache)!.FullName;
        try
        {
            var stderr = new StringWriter();
            var stdout = new StringWriter();
            var code = await PipelineCommands.ExecuteAsync(
                ["stats", "--cache", cache, "--from", "1950", "--to", "1950"],
                stdout,
                stderr);

            Assert.Equal(0, code);
            Assert.Equal(string.Empty, stderr.ToString());
            var lines = JolpicaFixtures.Lines(stdout);
            Assert.Equal(
            [
                "era stats 1950-1950",
                "main races: 1",
                "shared-drive rows: 2",
                "shared-drive races: 1",
                "indianapolis 500 races: 1",
                "unmapped statuses: none",
                "decade 1950s: races 1, starters/race 3.00, classified 33.3%, dnf mechanical 1, accident 0, other 1, unmapped 0, median margin n/a, wins from pole 100.0%, winners 1, pole sitters 1, champion share 100.0%, titles to final race 1/1, top constructor alfa 100.0%",
            ],
            lines[..^2]);
            Assert.StartsWith("wrote ", lines[^2], StringComparison.Ordinal);
            Assert.StartsWith("wrote ", lines[^1], StringComparison.Ordinal);

            var report = JsonSerializer.Deserialize<EraStatsReport>(
                File.ReadAllText(Path.Combine(root, "reports", "era_stats.json")),
                HistoricalJson.Options);
            Assert.NotNull(report);
            Assert.Empty(report.UnmappedStatuses);
            var season = Assert.Single(report.Main.Seasons);
            Assert.Equal(1950, season.Season);
            Assert.Equal(3, season.Starters);
            Assert.Equal(1, season.Classified);
            Assert.Equal(1, season.DnfMechanical);
            Assert.Equal(0, season.DnfAccident);
            Assert.Equal(1, season.DnfOther);
            Assert.Equal("farina", season.ChampionDriverId);
            Assert.Equal("Nino Farina", season.ChampionName);
            Assert.Equal(9m, season.ChampionPoints);
            Assert.Equal(9m, season.PointsCounted);
            Assert.Equal(1m, season.ChampionShare);
            Assert.True(season.TitleDecidedInFinalRace);
            Assert.Equal("alfa", season.TopConstructorId);

            var indy = Assert.Single(report.Indianapolis500.Seasons);
            Assert.Equal(1, indy.Wins);
            Assert.Equal(0, indy.WinsFromPole);
            Assert.Equal("kurtis_kraft", indy.TopConstructorId);
            Assert.Null(indy.ChampionDriverId);

            var shared = Assert.Single(report.SharedDrives.Seasons);
            Assert.Equal(2, shared.Entries);
            Assert.Equal(2, shared.Classified);
            Assert.Equal(0, shared.Wins);

            var markdown = File.ReadAllText(Path.Combine(root, "reports", "era_stats.md"));
            Assert.Contains("Nino Farina (farina)", markdown, StringComparison.Ordinal);
            Assert.Contains("Alfa Romeo (alfa)", markdown, StringComparison.Ordinal);
            Assert.Contains("## Unmapped statuses", markdown, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task StatsRejectsARangeWithAMissingSeasonAndWritesNothing()
    {
        var cache = Nest(JolpicaFixtures.Materialize());
        var root = Directory.GetParent(cache)!.FullName;
        try
        {
            var stderr = new StringWriter();
            var code = await PipelineCommands.ExecuteAsync(
                ["stats", "--cache", cache, "--from", "1950", "--to", "1951"],
                new StringWriter(),
                stderr);

            Assert.Equal(1, code);
            Assert.Contains("Missing seasons: 1951", stderr.ToString(), StringComparison.Ordinal);
            Assert.False(Directory.Exists(Path.Combine(root, "reports")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void MarginMedianUnmappedStatusAndTitleClinchUseTheIncludedRowsOnly()
    {
        var rows = new List<HistoricalResult>
        {
            Row(2010, 1, "a", "alfa", "1", "Finished", 25m, 1, 100_000),
            Row(2010, 1, "b", "ferrari", "2", "Finished", 18m, 2, 101_000),
            Row(2010, 2, "a", "alfa", "1", "Finished", 25m, 1, 200_000),
            Row(2010, 2, "b", "ferrari", "R", "Engine", 0m, 2, position: 4),
            Row(2010, 3, "a", "alfa", "R", "Accident", 0m, 2, position: 6),
            Row(2010, 3, "b", "ferrari", "R", "Gearbox", 0m, 1, position: 7),
            Row(2011, 1, "a", "alfa", "1", "Finished", 25m, 1, 100_000),
            Row(2011, 1, "b", "ferrari", "2", "Finished", 18m, 2, 103_000),
            Row(2011, 2, "a", "alfa", "2", "Finished", 15m, 2, 108_000),
            Row(2011, 2, "b", "ferrari", "1", "Finished", 25m, 1, 105_000),
            Row(1991, 1, "a", "alfa", "1", "Finished", 10m, 1, 5_000),
            Row(1991, 1, "b", "ferrari", "2", "+1 Lap", 6m, 2, 6_000),
            Row(1991, 2, "a", "alfa", "1", "Finished", 10m, 1, 5_000),
            Row(1991, 2, "b", "ferrari", "2", "Finished", 6m, 3, 8_000),
            Row(1950, 1, "c", "alfa", "R", "Puncture", 0m, 4, position: 8),
            Row(1950, 2, "shared_a", "maserati", "1", "Finished", 4m, 1, 9_000, shared: true),
            Row(1950, 2, "d", "alta", "R", "Oil leak", 0m, 5, position: 9),
        };
        var races = new List<HistoricalRace>
        {
            Race(2010, 1), Race(2010, 2), Race(2010, 3),
            Race(2011, 1), Race(2011, 2),
            Race(1991, 1), Race(1991, 2),
            Race(1950, 1), Race(1950, 2),
            Race(1950, 3, indy: true),
        };
        rows.Add(Row(1950, 3, "indy", "kurtis_kraft", "1", "Finished", 9m, 5, 50_000));

        var report = EraStats.Build(Data(rows, races), 1950, 2011);
        Assert.Equal(["Puncture"], report.UnmappedStatuses.Select(item => item.Status).ToArray());
        Assert.Equal(1, Assert.Single(report.UnmappedStatuses).Count);

        var clinched = Assert.Single(report.Main.Seasons, season => season.Season == 2010);
        Assert.Equal("a", clinched.ChampionDriverId);
        Assert.Equal(50m, clinched.ChampionPoints);
        Assert.Equal(68m, clinched.PointsCounted);
        Assert.Equal(50m / 68m, clinched.ChampionShare);
        Assert.False(clinched.TitleDecidedInFinalRace);
        Assert.Equal(1000m, clinched.MedianWinningMarginMillis);
        Assert.Equal(1, clinched.RacesWithWinningMargin);
        Assert.Equal(2, clinched.Wins);
        Assert.Equal(2, clinched.WinsFromPole);
        Assert.Equal(2, clinched.DifferentPoleSitters);
        Assert.Equal(2, clinched.DnfMechanical);
        Assert.Equal(1, clinched.DnfAccident);

        var open = Assert.Single(report.Main.Seasons, season => season.Season == 2011);
        Assert.Equal("b", open.ChampionDriverId);
        Assert.Equal(43m, open.ChampionPoints);
        Assert.True(open.TitleDecidedInFinalRace);
        Assert.Equal(3000m, open.MedianWinningMarginMillis);

        var margins = Assert.Single(report.Main.Seasons, season => season.Season == 1991);
        Assert.Equal(2000m, margins.MedianWinningMarginMillis);
        Assert.Equal(2, margins.RacesWithWinningMargin);

        var early = Assert.Single(report.Main.Seasons, season => season.Season == 1950);
        Assert.Equal(1, early.DnfUnmapped);
        Assert.Equal(1, early.DnfMechanical);
        Assert.Equal(0, early.Wins);
        Assert.Equal(1, Assert.Single(report.SharedDrives.Seasons, season => season.Season == 1950).Wins);
        Assert.Equal(1, Assert.Single(report.Indianapolis500.Seasons).Wins);
        Assert.DoesNotContain(report.Main.Seasons, season => season.ChampionDriverId == "indy");

        var decade = Assert.Single(report.Main.Decades, item => item.DecadeStart == 2010);
        Assert.Equal(1, decade.TitlesDecidedInFinalRace);
        Assert.Equal(2, decade.SeasonsWithChampionship);
        Assert.Equal(2, decade.DifferentWinners);
        Assert.Equal((50m / 68m + 43m / 83m) / 2m, decade.MeanChampionShare);
    }

    private static string Nest(string cacheLeaf)
    {
        var root = Path.Combine(Path.GetTempPath(), "paddock-stats-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var cache = Path.Combine(root, "jolpica");
        Directory.Move(cacheLeaf, cache);
        return cache;
    }

    private static NormalizedHistoricalData Data(List<HistoricalResult> rows, List<HistoricalRace> races)
    {
        return new NormalizedHistoricalData(
            new HistoricalDriversDocument(HistoricalSchema.Version, []),
            new HistoricalConstructorsDocument(HistoricalSchema.Version, []),
            new HistoricalCircuitsDocument(HistoricalSchema.Version, []),
            new HistoricalRacesDocument(HistoricalSchema.Version, races),
            new HistoricalResultsDocument(HistoricalSchema.Version, rows),
            new HistoricalQualifyingDocument(HistoricalSchema.Version, []));
    }

    private static HistoricalRace Race(int season, int round, bool indy = false)
    {
        return new HistoricalRace(
            season,
            round,
            indy ? "Indianapolis 500" : "Grand Prix",
            indy ? "indianapolis" : "silverstone",
            "2010-01-01",
            null,
            "http://example.test",
            indy);
    }

    private static HistoricalResult Row(
        int season,
        int round,
        string driver,
        string constructor,
        string positionText,
        string status,
        decimal points,
        int grid,
        long? millis = null,
        bool shared = false,
        int position = 0)
    {
        var classified = positionText.Length > 0 && positionText.All(char.IsAsciiDigit);
        var place = classified ? int.Parse(positionText, System.Globalization.CultureInfo.InvariantCulture) : position;
        return new HistoricalResult(
            season,
            round,
            driver,
            constructor,
            shared ? "10" : "1",
            place,
            positionText,
            status,
            points,
            grid,
            10,
            millis is null ? null : "time",
            millis,
            classified,
            status == "Disqualified",
            shared);
    }
}
