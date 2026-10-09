using System.Text.Json;
using Paddock.Application.Newspaper;
using Paddock.Application.Racing;
using Paddock.Domain.Contracts;
using Paddock.Domain.People;
using Paddock.Domain.Racing;
using Paddock.Domain.Time;
using Paddock.Domain.World;
using Paddock.Tests.Pool;

namespace Paddock.Tests.Newspaper;

/// <summary>
/// The newspaper of the main screen (#324). Every person, team, date and layout here is synthetic test data. The read is pure, so
/// each test builds a small world, reads the paper and checks the tiles; none of them changes the world.
/// </summary>
public class NewspaperReadTests
{
    private static readonly GameDate RaceDay1 = new(1955, 5, 1);

    private static readonly GameDate Today = new(1955, 5, 3);

    private sealed record Cast(
        WorldState World,
        PersonId OwnOne,
        PersonId OwnTwo,
        PersonId RivalOne,
        PersonId RivalTwo,
        PersonId RivalJunior);

    [Fact]
    public void AfterARaceThePaperNamesTheWinnerAndTheResultOfTheOwnTeam()
    {
        var cast = Build(salary: 100_000, attributes: 12);
        var race = Race(cast, order: [cast.RivalOne, cast.OwnOne, cast.RivalTwo, cast.OwnTwo]);
        var paper = Read(cast.World.WithSection(RaceResultsSection.Restore([race])));

        var win = Assert.Single(paper.Headlines, tile => tile.Kind == HeadlineKinds.Race);
        Assert.Equal(NewspaperKeys.RaceWin, win.Title.Key);
        Assert.Equal(Name(cast, cast.RivalOne), win.Title.Parameters["driver"]);
        Assert.Equal("Circuit layout_a", win.Title.Parameters["circuit"]);
        Assert.Equal(NewspaperKeys.RacePodium, win.Line!.Key);
        Assert.Equal(RaceDay1.ToString(), win.Date);
        Assert.Equal(new HeadlineLink(LinkKinds.Race, "1"), win.Link);

        var own = Assert.Single(paper.Headlines, tile => tile.Kind == HeadlineKinds.Own);
        Assert.True(own.Mine);
        Assert.Equal(NewspaperKeys.RaceOwn, own.Title.Key);
        Assert.Equal("2", own.Title.Parameters["position"]);
        Assert.Equal(Name(cast, cast.OwnOne), own.Title.Parameters["driver"]);
        Assert.Equal(NewspaperKeys.RaceOwnPoints, own.Line!.Key);
        Assert.Equal("9", own.Line.Parameters["count"]);
        Assert.Equal(new HeadlineLink(LinkKinds.Race, "1"), own.Link);
    }

    [Fact]
    public void WhenBothOwnCarsRetireTheOwnTileSaysSoAndScoresNothing()
    {
        var cast = Build(100_000, 12);
        var race = Race(cast, order: [cast.RivalOne, cast.RivalTwo], retired: [cast.OwnOne, cast.OwnTwo]);
        var paper = Read(cast.World.WithSection(RaceResultsSection.Restore([race])));

        var own = Assert.Single(paper.Headlines, tile => tile.Kind == HeadlineKinds.Own);
        Assert.Equal(NewspaperKeys.RaceOwnOut, own.Title.Key);
        Assert.Null(own.Line);
    }

    [Fact]
    public void ARaceOfAnEarlierSeasonStillLeadsSomewhereSensible()
    {
        var cast = Build(100_000, 12);
        var race = Race(cast, order: [cast.RivalOne, cast.RivalTwo, cast.OwnOne]);
        var world = cast.World.WithSection(RaceResultsSection.Restore([race]));
        var paper = NewspaperRead.Read(Sources(world, new GameDate(1956, 1, 10), new GameDate(1955, 12, 20)));
        Assert.Equal(2, paper.Headlines.Count);
        Assert.All(paper.Headlines, tile => Assert.Equal(LinkKinds.Driver, tile.Link.Kind));
    }

    [Fact]
    public void AnInjuryLineOfTheRaceReportBecomesATileAndAFatalOneSilencesTheRetirement()
    {
        var cast = Build(100_000, 12);
        var injured = new StoredReportSection(
            new StoredReportLine("report.section.drives", null, []),
            [
                new StoredReportLine(RaceReportKeys.InjurySerious, null, [new RaceReportArg("driver", "person:" + cast.RivalTwo.Value)]),
                new StoredReportLine(RaceReportKeys.InjuryFatal, null, [new RaceReportArg("driver", "person:" + cast.RivalJunior.Value)]),
            ]);
        var race = Race(cast, order: [cast.RivalOne, cast.OwnOne], retired: [cast.RivalTwo, cast.RivalJunior], sections: [injured]);
        var world = cast.World.WithSection(RaceResultsSection.Restore([race]));
        world = world.RetirePerson(cast.RivalJunior, RaceDay1);
        world = world.RetirePerson(cast.RivalTwo, Today);
        var paper = Read(world);

        var injuries = paper.Headlines.Where(tile => tile.Kind == HeadlineKinds.Injury).ToArray();
        Assert.Equal(2, injuries.Length);
        Assert.Contains(injuries, tile => tile.Title.Key == NewspaperKeys.InjurySerious && tile.Title.Parameters["driver"] == Name(cast, cast.RivalTwo));
        Assert.Contains(injuries, tile => tile.Title.Key == NewspaperKeys.InjuryFatal);
        var retirements = paper.Headlines.Where(tile => tile.Kind == HeadlineKinds.Retirement).ToArray();
        var retired = Assert.Single(retirements);
        Assert.Equal(Name(cast, cast.RivalTwo), retired.Title.Parameters["driver"]);
        Assert.Equal(NewspaperKeys.RetiredAge, retired.Line!.Key);
    }

    [Fact]
    public void AnOwnSigningIsMarkedAsMineAndAnOpenOfferIsNoNews()
    {
        var cast = Build(100_000, 12);
        var world = cast.World;
        // The rival one moves from the rival team to the player's: a contract that starts today, after the old one ended.
        (world, var signed) = world.AddContract(new ContractSpec(
            cast.RivalJunior,
            PoolKit.Alpha,
            ContractRole.Driver(SeatStatus.Equal),
            Today,
            new GameDate(1956, 12, 31),
            90_000,
            true,
            null,
            null));
        var done = Agreed(1, PoolKit.Alpha, cast.RivalJunior, signed, Today);
        var open = Open(2, PoolKit.Bravo, cast.OwnTwo);
        world = world.WithSection(ContractsSection.Restore(3, [], [done, open], []));
        var paper = Read(world);

        var signing = Assert.Single(paper.Headlines, tile => tile.Kind == HeadlineKinds.Signing);
        Assert.Equal(NewspaperKeys.SigningDriver, signing.Title.Key);
        Assert.Equal(Name(cast, cast.RivalJunior), signing.Title.Parameters["driver"]);
        Assert.True(signing.Mine);
        Assert.Equal(new HeadlineLink(LinkKinds.Driver, cast.RivalJunior.Value), signing.Link);
        Assert.Single(paper.Headlines);
    }

    [Fact]
    public void ARivalSigningCarriesTheTeamTheDriverLeft()
    {
        var cast = Build(100_000, 12);
        var world = cast.World;
        (world, var signed) = world.AddContract(new ContractSpec(
            cast.OwnTwo,
            PoolKit.Bravo,
            ContractRole.Driver(SeatStatus.NumberOne),
            Today,
            new GameDate(1957, 12, 31),
            80_000,
            true,
            null,
            null));
        world = world.WithSection(ContractsSection.Restore(2, [], [Agreed(1, PoolKit.Bravo, cast.OwnTwo, signed, Today)], []));
        var paper = Read(world);

        var signing = Assert.Single(paper.Headlines);
        Assert.False(signing.Mine);
        Assert.Equal(NewspaperKeys.SigningFrom, signing.Line!.Key);
        Assert.Equal(PoolKit.TeamAlpha, signing.Line.Parameters["team"]);
    }

    [Fact]
    public void AnAcademyJuniorOfARivalIsNoNews()
    {
        var cast = Build(100_000, 12);
        var world = cast.World;
        (world, var signed) = world.AddContract(new ContractSpec(
            cast.RivalJunior,
            PoolKit.Bravo,
            ContractRole.Driver(SeatStatus.Reserve),
            Today,
            new GameDate(1957, 12, 31),
            0,
            true,
            null,
            null));
        world = world.WithSection(ContractsSection.Restore(2, [], [Agreed(1, PoolKit.Bravo, cast.RivalJunior, signed, Today)], []));
        Assert.Empty(Read(world).Headlines);
    }

    [Fact]
    public void NewsOlderThanTheWindowIsGoneAndTheNewestComesFirst()
    {
        var cast = Build(100_000, 12);
        var world = cast.World.WithSection(RaceResultsSection.Restore([Race(cast, order: [cast.RivalOne, cast.RivalTwo, cast.OwnTwo])]));
        var late = Today.AddDays(NewspaperEstimates.WindowDays + 1);
        Assert.Empty(NewspaperRead.Read(Sources(world, late)).Headlines);

        world = world.RetirePerson(cast.OwnTwo, Today);
        var paper = Read(world);
        var dates = paper.Headlines.Select(tile => tile.Date).ToArray();
        Assert.Equal(dates.OrderByDescending(date => date, StringComparer.Ordinal), dates);
        Assert.Equal(Today.ToString(), dates[0]);
    }

    [Fact]
    public void ThePaperNeverHoldsMoreThanItsEstimateOfTiles()
    {
        var cast = Build(100_000, 12);
        var races = Enumerable.Range(1, 40).Select(round => Race(cast, order: [cast.RivalOne, cast.OwnOne], round: round)).ToArray();
        var world = cast.World.WithSection(RaceResultsSection.Restore(races));
        var paper = NewspaperRead.Read(new NewspaperSources
        {
            World = world,
            Today = Today,
            Own = PoolKit.Alpha,
            RaceDays = (_, round) => new RaceDay(Today.AddDays(-round % 20), "layout_a"),
            CircuitName = layout => "Circuit " + layout,
        });
        Assert.Equal(NewspaperEstimates.MaxHeadlines, paper.Headlines.Count);
    }

    [Fact]
    public void WhatAPlayerMayNotKnowAboutARivalNeverReachesAHeadline()
    {
        // Two worlds that differ only in what is private: the attributes of every rival, the salary of his contract, the terms of an
        // offer another team is still making. The paper must come out identical, byte for byte.
        var first = PaperOf(Build(salary: 40_000, attributes: 6), offerSalary: 10_000);
        var second = PaperOf(Build(salary: 900_000, attributes: 19), offerSalary: 800_000);
        Assert.Equal(first, second);
        Assert.NotEqual("{\"headlines\":[]}", first);
        Assert.DoesNotContain("40000", first, StringComparison.Ordinal);
        Assert.DoesNotContain("900000", first, StringComparison.Ordinal);
    }

    [Fact]
    public void AKeyStaffHireIsTeamNewsLabelledWithTheRoleAndNeverCrowdsOutTheRest()
    {
        var cast = Build(100_000, 12);
        var world = cast.World.WithSection(RaceResultsSection.Restore([Race(cast, order: [cast.RivalOne, cast.OwnOne])]));
        var negotiations = new List<Negotiation>();
        for (var i = 0; i < 6; i++)
        {
            PersonId scout;
            (world, scout) = world.AddPerson(PoolKit.Scout("scout_" + (char)('a' + i), 10, 10));
            negotiations.Add(new Negotiation(
                i + 1,
                "ai",
                i == 0 ? PoolKit.Alpha : PoolKit.Bravo,
                scout,
                NegotiationSubject.Staff(StaffRole.Scout),
                null,
                Today.AddDays(-10 - i),
                Today.AddDays(-5 - i),
                5,
                2,
                700,
                NegotiationStatus.Agreed,
                null,
                null,
                null,
                0,
                [],
                [],
                Today.AddDays(-5 - i),
                ContractId.Generated(900 + i)));
        }

        world = world.WithSection(ContractsSection.Restore(7, [], negotiations, []));
        var paper = Read(world);

        var hires = paper.Headlines.Where(tile => tile.Kind == HeadlineKinds.Team).ToArray();
        Assert.Equal(NewspaperEstimates.MaxTeamNews, hires.Length);
        Assert.All(hires, tile => Assert.Equal("staff.role.Scout", tile.Label.Key));
        Assert.All(hires, tile => Assert.Equal(NewspaperKeys.TeamHired, tile.Title.Key));
        var own = Assert.Single(hires, tile => tile.Mine);
        Assert.Equal(LinkKinds.Person, own.Link.Kind);
        Assert.All(hires.Where(tile => !tile.Mine), tile => Assert.Equal(LinkKinds.Standings, tile.Link.Kind));
        Assert.Contains(paper.Headlines, tile => tile.Kind == HeadlineKinds.Race);
    }

    [Fact]
    public void ACountedVoteIsNewsWithTheRuleAndItsNewValueAndOpensTheNoticeInTheInbox()
    {
        var cast = Build(100_000, 12);
        var variant = new BallotVariant("v1", "top10_25_18_15_12_10_8_6_4_2_1", ["bravo"]);
        var result = new BallotResult(BallotOutcome.Adopted, "v1", "regulation.result.adopted", false, [], []);
        var item = new BallotItem(
            "ballot:1955:points_scale",
            1955,
            "points_scale",
            BallotOrigin.Teams,
            Today.AddDays(-20),
            Today.AddDays(-1),
            "top6_9_6_4_3_2_1",
            "regulation.reason.teamProposal",
            [],
            [variant],
            [],
            result);
        var series = RegulationsSection.OpeningSeries(
            SeriesIds.WorldChampionship,
            1955,
            new Dictionary<string, string> { ["points_scale"] = "top6_9_6_4_3_2_1" }) with { Ballot = [item] };
        var world = cast.World.WithSection(RegulationsSection.Create([series]));
        var notice = new Paddock.Application.Inbox.InboxItemView(
            "inb:9",
            "human:player",
            "regulation.result",
            new DateOnly(1955, 5, 2),
            Paddock.Application.Commands.TranslationMessage.Of("regulation.inbox.result.subject", ("item", item.Id)),
            false,
            [],
            null,
            null,
            Paddock.Domain.Inbox.InboxStatus.Open,
            null,
            null,
            false);
        var paper = NewspaperRead.Read(new NewspaperSources
        {
            World = world,
            Today = Today,
            Own = PoolKit.Alpha,
            Inbox = [notice],
            RaceDays = (_, _) => null,
            CircuitName = layout => layout,
        });

        var tile = Assert.Single(paper.Headlines);
        Assert.Equal(HeadlineKinds.Rules, tile.Kind);
        Assert.Equal("regulation.dimension.points_scale", tile.Label.Key);
        Assert.Equal(NewspaperKeys.RulesAdopted, tile.Title.Key);
        Assert.Equal("1956", tile.Title.Parameters["season"]);
        Assert.Equal("regulation.value.points_scale.top10_25_18_15_12_10_8_6_4_2_1", tile.Line!.Key);
        Assert.Equal(new HeadlineLink(LinkKinds.Inbox, "inb:9"), tile.Link);
        Assert.Equal(Today.AddDays(-1).ToString(), tile.Date);
    }

    [Fact]
    public void ReadingThePaperChangesNothingInTheWorld()
    {
        var cast = Build(100_000, 12);
        var world = cast.World.WithSection(RaceResultsSection.Restore([Race(cast, order: [cast.RivalOne, cast.OwnOne])]));
        var before = world.StateHash();
        var first = JsonSerializer.Serialize(Read(world));
        var second = JsonSerializer.Serialize(Read(world));
        Assert.Equal(before, world.StateHash());
        Assert.Equal(first, second);
    }

    private static string PaperOf(Cast cast, long offerSalary)
    {
        var world = cast.World;
        (world, var signed) = world.AddContract(new ContractSpec(
            cast.OwnTwo,
            PoolKit.Bravo,
            ContractRole.Driver(SeatStatus.NumberOne),
            Today,
            new GameDate(1957, 12, 31),
            offerSalary,
            true,
            null,
            null));
        var talks = new Negotiation(
            3,
            "ai",
            PoolKit.Bravo,
            cast.OwnOne,
            NegotiationSubject.DriverSeat,
            null,
            Today,
            Today.AddDays(10),
            5,
            1,
            800,
            NegotiationStatus.AwaitingResponse,
            new OfferTerms(offerSalary, 0, 0, 0, 2, SeatStatus.NumberOne, null, null),
            null,
            null,
            0,
            [],
            [],
            null,
            null);
        world = world.WithSection(ContractsSection.Restore(
            4,
            [],
            [Agreed(1, PoolKit.Bravo, cast.OwnTwo, signed, Today), talks],
            []));
        world = world.WithSection(RaceResultsSection.Restore([Race(cast, order: [cast.RivalOne, cast.RivalTwo, cast.OwnOne])]));
        return JsonSerializer.Serialize(Read(world));
    }

    private static Cast Build(long salary, int attributes)
    {
        var world = PoolKit.EmptyWorld(Today);
        PersonId Add(string id, int birthYear)
        {
            (world, var person) = world.AddPerson(PoolKit.RealDriver(id, birthYear, attributes, 3));
            return person;
        }

        var ownOne = Add("own_one", 1925);
        var ownTwo = Add("own_two", 1926);
        var rivalOne = Add("rival_one", 1924);
        var rivalTwo = Add("rival_two", 1927);
        var junior = Add("rival_junior", 1935);
        foreach (var (person, team, seat) in new[]
        {
            (ownOne, PoolKit.Alpha, SeatStatus.NumberOne),
            (ownTwo, PoolKit.Alpha, SeatStatus.NumberTwo),
            (rivalOne, PoolKit.Bravo, SeatStatus.NumberOne),
            (rivalTwo, PoolKit.Bravo, SeatStatus.NumberTwo),
        })
        {
            (world, _) = world.AddContract(new ContractSpec(
                person,
                team,
                ContractRole.Driver(seat),
                new GameDate(1955, 1, 1),
                Today.AddDays(-1),
                salary,
                true,
                null,
                null));
        }

        return new Cast(world, ownOne, ownTwo, rivalOne, rivalTwo, junior);
    }

    private static StoredRace Race(
        Cast cast,
        IReadOnlyList<PersonId> order,
        IReadOnlyList<PersonId>? retired = null,
        IReadOnlyList<StoredReportSection>? sections = null,
        int round = 1)
    {
        var rows = new List<RaceResultRow>();
        var points = new[] { "8", "6", "4", "3", "2", "0" };
        for (var i = 0; i < order.Count; i++)
        {
            rows.Add(new RaceResultRow(i + 1, true, order[i].Value, TeamOf(cast, order[i]), points[Math.Min(i, points.Length - 1)], string.Empty));
        }

        foreach (var driver in retired ?? [])
        {
            rows.Add(new RaceResultRow(rows.Count + 1, false, driver.Value, TeamOf(cast, driver), "0", "report.retire.engine"));
        }

        return new StoredRace(1955, round, "layout_a", rows, sections ?? []);
    }

    private static string TeamOf(Cast cast, PersonId person) =>
        person == cast.OwnOne || person == cast.OwnTwo ? PoolKit.Alpha.Value : PoolKit.Bravo.Value;

    private static Negotiation Agreed(long number, OrganizationId proposer, PersonId person, ContractId contract, GameDate closed) =>
        new(
            number,
            "ai",
            proposer,
            person,
            NegotiationSubject.DriverSeat,
            null,
            closed.AddDays(-5),
            closed.AddDays(5),
            5,
            2,
            700,
            NegotiationStatus.Agreed,
            null,
            null,
            null,
            0,
            [],
            [],
            closed,
            contract);

    private static Negotiation Open(long number, OrganizationId proposer, PersonId person) =>
        Negotiation.Start(number, "ai", proposer, person, NegotiationSubject.DriverSeat, null, Today, Today.AddDays(10), 5);

    private static string Name(Cast cast, PersonId person) => cast.World.GetPerson(person).Name;

    private static NewspaperView Read(WorldState world) => NewspaperRead.Read(Sources(world, Today));

    private static NewspaperSources Sources(WorldState world, GameDate today, GameDate? raceDay = null) => new()
    {
        World = world,
        Today = today,
        Own = PoolKit.Alpha,
        Contracts = world.Section<ContractsSection>(ContractsSection.SectionName),
        RaceDays = (season, round) => season == 1955 && round >= 1 ? new RaceDay(raceDay ?? RaceDay1, "layout_a") : null,
        CircuitName = layout => "Circuit " + layout,
    };
}
