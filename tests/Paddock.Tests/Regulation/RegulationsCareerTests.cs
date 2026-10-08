using Paddock.Application.Career;
using Paddock.Application.Regulation;
using Paddock.Domain.Career;
using Paddock.Domain.Finance;
using Paddock.Domain.Racing;
using Paddock.Domain.Time;
using Paddock.Simulation.Career;
using Paddock.Simulation.Regulation;
using Paddock.Tests.Career;

namespace Paddock.Tests.Regulation;

/// <summary>Voting v2 in a real career: the modules, the day order, the money, the calendar and the vote mode of the career-start choice (#275).</summary>
public class RegulationsCareerTests
{
    private const ulong Seed = 7;

    private static (OpenedCareer Career, CareerRunResult Result) Run(CareerPreset preset, GameDate until, Func<CareerConfig, CareerConfig>? tweak = null)
    {
        var opened = CareerKit.Opened(preset, 1955, Seed, tweak);
        var result = CareerHost.RunUntil(opened.Session, until, null, CareerKit.OptionsFor(opened));
        return (opened, result);
    }

    private static SeriesRegulations Series(CareerSession session) =>
        session.World.Section<RegulationsSection>(RegulationsSection.SectionName)!.Find(SeriesIds.WorldChampionship)!;

    [Fact]
    public void AVotedCareerLivesAYearOfPoliticsWithFiaVotesFeesAndResultsAndTheSameSeedGivesTheSameHash()
    {
        var (first, _) = Run(CareerPreset.Chaos, new GameDate(1956, 1, 3));
        var (second, _) = Run(CareerPreset.Chaos, new GameDate(1956, 1, 3));
        var series = Series(first.Session);

        Assert.Equal(first.Session.World.StateHash(), second.Session.World.StateHash());
        Assert.Equal(1956, series.Season);
        Assert.InRange(series.Ballot.Count(item => item.Origin == BallotOrigin.Fia), 4, 6);
        Assert.All(series.Ballot, item =>
        {
            Assert.True(item.IsResolved);
            Assert.False(string.IsNullOrWhiteSpace(item.Result!.ReasonKey));
        });

        // Every team has a leaning and a cooldown; the AI teams differ, and the proposals that were filed cost their fee.
        var teams = CareerTeams.Active(first.Session.World, new GameDate(1955, 6, 1));
        Assert.All(teams, team => Assert.NotNull(series.TeamOf(team.Id.Value)));
        Assert.True(series.Teams.Select(team => team.ProposeFromSeason).Distinct().Count() > 1);
        var finance = first.Session.World.Section<FinanceSection>(FinanceSection.SectionName)!;
        var fees = teams.SelectMany(team => finance.EntriesOf(team.Id)).Where(entry => entry.ReasonKey == RegulationKeys.LedgerProposalFee).ToArray();
        var proposed = series.Ballot.Where(item => item.Origin == BallotOrigin.Teams).SelectMany(item => item.Variants).SelectMany(variant => variant.ProposerTeamIds).ToArray();
        Assert.Equal(proposed.Length, fees.Length);
        Assert.All(fees, fee => Assert.True(fee.AmountCents < 0));
        Assert.All(proposed, team => Assert.True(series.TeamOf(team)!.ProposeFromSeason >= 1958));
    }

    [Fact]
    public void TheVoteModeOfTheCareerStartReachesTheVote()
    {
        var (oneVote, _) = Run(CareerPreset.Chaos, new GameDate(1955, 12, 31));
        var (banked, _) = Run(CareerPreset.Chaos, new GameDate(1955, 12, 31), config => config.WithVoteMode(VoteMode.VoteBank));

        Assert.All(Series(oneVote.Session).Teams, team => Assert.Equal(0, team.Bank));
        Assert.Contains(Series(banked.Session).Teams, team => team.Bank > 0);
        Assert.Contains(
            Series(banked.Session).Ballot.SelectMany(item => item.Result!.Stances),
            stance => stance.Banked);
    }

    [Fact]
    public void AHistoricalCareerRegistersNoPoliticsAtAll()
    {
        var (historical, result) = Run(CareerPreset.Balanced, new GameDate(1955, 3, 1));

        Assert.Null(result.Modules.TryGet<RegulationPolitics>());
        Assert.Null(historical.Session.World.Section(RegulationsSection.SectionName));
        Assert.DoesNotContain(RegulationDayHandler.HandlerOrder, historical.Session.DayHandlers.Select(handler => handler.Order));

        var (voted, votedResult) = Run(CareerPreset.Chaos, new GameDate(1955, 3, 1));
        Assert.NotNull(votedResult.Modules.TryGet<RegulationPolitics>());
        Assert.Contains(RegulationDayHandler.HandlerOrder, voted.Session.DayHandlers.Select(handler => handler.Order));
    }

    [Fact]
    public void TheHumanTeamStartsFreeAndTheAiTeamsDoNotAllStartTogether()
    {
        var opened = CareerKit.Opened(CareerPreset.Chaos, 1955, Seed);
        var options = new CareerRunOptions
        {
            Inputs = CareerKit.OptionsFor(opened).Inputs,
            Humans = [new CareerHuman("human:" + opened.PlayerTeam, "Player", opened.PlayerTeam)],
        };

        CareerHost.RunUntil(opened.Session, opened.Session.Date, null, options);
        var series = Series(opened.Session);

        Assert.Equal(1955, series.TeamOf(opened.PlayerTeam)!.ProposeFromSeason);
        Assert.Contains(series.Teams.Where(team => team.TeamId != opened.PlayerTeam), team => team.ProposeFromSeason > 1955);
        Assert.Contains(series.Teams.Where(team => team.TeamId != opened.PlayerTeam), team => team.ProposeFromSeason == 1955);
    }

    [Fact]
    public void ADropVotedForTheNextSeasonIsLaidOutOnTheLastDayAndTheSeasonInProgressKeepsItsCalendar()
    {
        var layouts = CareerKit.Data.Layouts;
        var assignments = CareerKit.Data.RaceAssignments;
        string Circuit(string layout) => layouts.First(l => l.Id == layout).CircuitId;
        var authored1955 = assignments.Where(a => a.Season == 1955).Select(a => Circuit(a.LayoutId)).ToArray();
        var authored1956 = assignments.Where(a => a.Season == 1956).Select(a => Circuit(a.LayoutId)).ToArray();
        var circuit = authored1955.Intersect(authored1956).Order(StringComparer.Ordinal).First();

        var opened = CareerKit.Opened(CareerPreset.Chaos, 1955, Seed);
        var options = CareerKit.OptionsFor(opened);
        var first = CareerHost.RunUntil(opened.Session, new GameDate(1955, 3, 1), null, options);
        var section = opened.Session.World.Section<RegulationsSection>(RegulationsSection.SectionName)!;
        var series = section.Find(SeriesIds.WorldChampionship)!;
        var policy = series.Calendar.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        policy[CalendarPolicy.DimensionOf(circuit)] = CalendarPolicy.Dropped;
        opened.Session.StoreWorld(opened.Session.World.WithSection(section.WithSeries(
            series.WithNext(series.Values.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal), policy))));

        var directory = Directory.CreateTempSubdirectory("paddock-calendar-");
        CareerSession session;
        try
        {
            var path = Path.Combine(directory.FullName, "vote.paddock");
            CareerKit.Save(path, opened, opened.Session, first.Host);
            var (resumed, host) = CareerKit.Resume(path);
            CareerHost.RunUntil(resumed, new GameDate(1956, 1, 3), host, CareerKit.Options);
            session = resumed;
        }
        finally
        {
            directory.Delete(recursive: true);
        }

        var calendar = session.World.Section<RaceCalendarSection>(RaceCalendarSection.SectionName)!;
        var races1955 = calendar.SeasonSessions(1955).Where(s => s.TypeId == Paddock.Simulation.Time.ScheduledEventType.Race).Select(s => Circuit(s.LayoutId)).ToArray();
        var races1956 = calendar.SeasonSessions(1956).Where(s => s.TypeId == Paddock.Simulation.Time.ScheduledEventType.Race).Select(s => Circuit(s.LayoutId)).ToArray();
        var inForce = Series(session).Calendar.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        var expected = CalendarPolicy.RoundsFor(1956, layouts, assignments, null, inForce);

        Assert.Equal(authored1955, races1955);
        Assert.Contains(circuit, authored1956);
        Assert.DoesNotContain(circuit, races1956);
        Assert.Equal(expected.Count, races1956.Length);
        Assert.Equal(expected.Select(row => row.Layout.CircuitId).Order(StringComparer.Ordinal), races1956.Order(StringComparer.Ordinal));
        var queued = session.Clock.Queue.Events
            .Where(e => e.Payload is Paddock.Simulation.Time.RaceSessionPayload payload && payload.Season == 1956 && e.TypeId == Paddock.Simulation.Time.ScheduledEventType.Race)
            .Count();
        Assert.Equal(races1956.Length, queued);
    }

    [Fact]
    public void ARuleVotedInSeasonNIsNotTheRuleOfSeasonNButIsTheRuleOfSeasonNPlusOne()
    {
        var (career, _) = Run(CareerPreset.Chaos, new GameDate(1956, 1, 3));
        var series = Series(career.Session);
        var authored1955 = CareerKit.Data.RuleSetFor(1955);
        var changes = series.Ballot
            .Where(item => item.Season == 1955 && item.Result!.Outcome == BallotOutcome.Adopted && !CalendarPolicy.IsCalendarDimension(item.DimensionId))
            .ToArray();

        // In 1956 each adopted change is in force; the rules of 1955 are not stored any more, and the authored ones are what 1955 used.
        Assert.NotEmpty(changes);
        foreach (var item in changes)
        {
            var value = item.VariantOf(item.Result!.WinningOption)!.Value;
            Assert.Equal(value, series.RuleSetFor(1956)!.Value(item.DimensionId));
            Assert.NotEqual(value, authored1955.Value(item.DimensionId));
        }
    }
}
