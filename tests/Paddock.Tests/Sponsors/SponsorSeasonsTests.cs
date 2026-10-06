using Paddock.Application.Career;
using Paddock.Application.Contracts;
using Paddock.Application.Inbox;
using Paddock.Application.Managers;
using Paddock.Application.Sponsors;
using Paddock.Data.Authored;
using Paddock.Domain.Career;
using Paddock.Domain.Sponsors;
using Paddock.Domain.Time;
using Paddock.Domain.World;
using Paddock.Tests.Career;


namespace Paddock.Tests.Sponsors;

/// <summary>
/// #254 and PP-065: a Maserati career from 1955 found every sponsor "unavailable" by January 1957, because the AI teams held the few sponsors
/// there were. Now every team has its own pool of sponsors, new each season, and no team blocks another.
/// </summary>
public sealed class SponsorSeasonsTests
{
    private const ulong Seed = 7;

    [Fact]
    public void EveryTeamHasSponsorCandidatesInEverySeasonOfAThreeSeasonRun()
    {
        for (var year = 1955; year <= 1957; year++)
        {
            // A run is deterministic, so each January is a fresh run to that morning (a session cannot attach its modules twice).
            var session = CareerKit.Open(CareerPreset.Chaos, 1955, Seed);
            var result = CareerHost.RunUntil(session, new GameDate(year, 1, 2), null, CareerKit.Options);
            var environment = result.Modules.TryGet<SponsorEnvironment>()!;
            var today = session.Date;
            var era = environment.Eras.Era(today.Year);
            var teams = CareerTeams.Active(session.World, today);
            Assert.NotEmpty(teams);
            foreach (var team in teams)
            {
                foreach (var player in new[] { false, true })
                {
                    var candidates = environment.Catalog.CandidatesFor(today.Year, SlotKind.Technical, era, team.Id, player);
                    Assert.True(
                        candidates.Count >= SponsorEstimates.MinCandidatesPerSlot,
                        today + " " + team.Id.Value + " has only " + candidates.Count + " candidates (player team: " + player + ").");
                }
            }
        }
    }

    [Fact]
    public void AiManagersAreNotGivenSponsorNoticesNobodyWillRead()
    {
        var session = CareerKit.Open(CareerPreset.Chaos, 1955, Seed);

        var result = CareerHost.RunUntil(session, new GameDate(1958, 1, 2), null, CareerKit.Options);

        var inbox = result.Modules.Require<InboxBook>();
        var registry = result.Host.Managers;
        var aiNotices = inbox.Section.Items
            .Where(item => item.Kind is SponsorKeys.InboxKind or SponsorOfferCodes.OfferKind)
            .Where(item => registry.KindOf(new ManagerId(item.ManagerId)) == ManagerKind.Ai)
            .ToArray();
        Assert.Empty(aiNotices);
        Assert.NotEmpty(result.Modules.Require<SponsorBook>().Section.Deals);
    }

    [Fact]
    public void AnAiTeamsDealNeverChangesWhatThePlayerIsOffered()
    {
        var kit = new SponsorKit(new GameDate(1955, 1, 1), SponsorsLoader.ToCatalog(new SponsorKit(new GameDate(1955, 1, 1)).SponsorsFile, localMarket: true));
        var gamma = OrganizationId.Real("gamma");
        ((ControlTable)kit.Control).Assign(new ManagerId("ai:gamma"), gamma);
        Assert.False(kit.Environment.IsPlayerTeam(gamma));
        Assert.True(kit.Environment.IsPlayerTeam(SponsorKit.Alfa));
        var day = new GameDate(1955, 1, 2);
        string[] Offered() => ((SponsorView.Own)SponsorQuery.Read(Paddock.Application.Access.AccessContext.Developer, SponsorKit.Alfa, kit.Book, kit.Environment, kit.ObjectiveQuery(), day))
            .Slots.SelectMany(slot => slot.Candidates).Select(candidate => candidate.SponsorId + "/" + candidate.Blocked?.Key).ToArray();
        var before = Offered();

        // The AI team signs every sponsor of its own pool and every one of the player's named sponsors it can reach.
        var era = kit.Environment.Eras.Era(1955);
        foreach (var sponsor in kit.Environment.Catalog.CandidatesFor(1955, SlotKind.Technical, era, gamma, playerTeam: false))
        {
            var (sponsors, objectives, _) = SponsorRules.Sign(kit.Book.Section, kit.Book.Objectives, kit.Environment, sponsor, gamma, 1, SlotKind.Technical, 100000, day);
            kit.Book.Write(sponsors, objectives);
        }

        Assert.NotEmpty(before);
        Assert.Equal(before, Offered());
        Assert.NotNull(SponsorRules.CanBegin(kit.Book, kit.Environment, gamma, "vestoil_works", 1, day));
        Assert.Null(SponsorRules.CanBegin(kit.Book, kit.Environment, SponsorKit.Alfa, "vestoil_works", 1, day));
    }

    [Fact]
    public void ATeamPoolIsTheSameEveryTimeDiffersByTeamAndSeasonAndAnIdFindsItAgain()
    {
        var catalog = new SponsorCatalog([], localMarket: true);
        var maserati = OrganizationId.Real("maserati");
        var first = LocalSponsorMarket.For(1956, maserati);
        var second = LocalSponsorMarket.For(1956, maserati);

        Assert.Equal(first.Select(sponsor => sponsor.Id), second.Select(sponsor => sponsor.Id));
        Assert.Equal(first.Count, first.Select(sponsor => sponsor.Name).Distinct().Count());
        foreach (var sponsor in first)
        {
            Assert.True(sponsor.Local);
            Assert.Equal("maserati", sponsor.Team);
            Assert.Equal(sponsor.Name, catalog.Find(sponsor.Id)?.Name);
            Assert.True(SponsorCatalog.OfferedTo(sponsor, maserati, playerTeam: true));
            Assert.False(SponsorCatalog.OfferedTo(sponsor, OrganizationId.Real("cooper"), playerTeam: true));
        }

        var names = (OrganizationId team, int year) => LocalSponsorMarket.For(year, team).Select(sponsor => sponsor.Name).ToArray();
        Assert.NotEqual(names(maserati, 1956), names(maserati, 1957));
        Assert.NotEqual(names(maserati, 1956), names(OrganizationId.Real("cooper"), 1956));
        Assert.Equal(OrganizationId.Generated(7).Value, LocalSponsorMarket.For(1960, OrganizationId.Generated(7))[0].Team);
        Assert.NotNull(catalog.Find(LocalSponsorMarket.For(1960, OrganizationId.Generated(7))[3].Id));

        Assert.Null(new SponsorCatalog([]).Find(first[0].Id));
        Assert.Null(catalog.Find("local:1956:maserati:t99"));
        Assert.Null(catalog.Find("local:x"));
    }

    [Fact]
    public void TeamBackersAreOfferedOnlyInTheirSeasonRespectTheEraAndNamedSponsorsGoToPlayerTeamsOnly()
    {
        var data = new SponsorKit(new GameDate(1955, 1, 1)).Data;
        var era = SponsorEra.ForYear(data.EraPeriods, 1955);
        var catalog = new SponsorCatalog([], localMarket: true);
        var team = OrganizationId.Real("maserati");

        var now = catalog.CandidatesFor(1957, SlotKind.Technical, era, team, playerTeam: false);

        Assert.Equal(SponsorEstimates.LocalBackersPerSeason, now.Count);
        Assert.All(now, sponsor => Assert.Equal(1957, sponsor.FromYear));
        Assert.Empty(catalog.CandidatesFor(1955, SlotKind.Main, era, team, playerTeam: false));

        var named = SponsorsLoader.ToCatalog(new SponsorKit(new GameDate(1955, 1, 1)).SponsorsFile, localMarket: true);
        Assert.DoesNotContain(named.CandidatesFor(1955, SlotKind.Technical, era, team, playerTeam: false), sponsor => !sponsor.Local);
        Assert.Contains(named.CandidatesFor(1955, SlotKind.Technical, era, team, playerTeam: true), sponsor => !sponsor.Local);
    }
}
