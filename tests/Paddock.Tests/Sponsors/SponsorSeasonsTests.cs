using Paddock.Application.Career;
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
/// #254: a Maserati career from 1955 found every sponsor "unavailable" by January 1957, no income, and no word about it. The AI
/// teams renewed forever and the authored sponsors back one team at a time, so they held all of them. These tests run whole seasons.
/// </summary>
public sealed class SponsorSeasonsTests
{
    private const ulong Seed = 7;

    /// <summary>ESTIMATE: free sponsors an unsponsored team should still see for a technical slot, with a modest prestige need. PP-050 names three.</summary>
    private const int FreeCandidatesWanted = SponsorEstimates.MinCandidatesPerSlot;

    private const double ModestPrestige = 0.1;

    [Fact]
    public void AnUnsponsoredTeamStillHasFreeSponsorsAtTheStartOfEveryOfThreeSeasons()
    {
        for (var year = 1956; year <= 1958; year++)
        {
            // A run is deterministic, so each January is a fresh run to that morning (a session cannot attach its modules twice).
            var session = CareerKit.Open(CareerPreset.Chaos, 1955, Seed);
            var result = CareerHost.RunUntil(session, new GameDate(year, 1, 2), null, CareerKit.Options);
            var book = result.Modules.TryGet<SponsorBook>()!;
            var environment = result.Modules.TryGet<SponsorEnvironment>()!;
            var today = session.Date;
            var era = environment.Eras.Era(today.Year);
            var free = environment.Catalog.Candidates(today.Year, SlotKind.Technical, era)
                .Where(sponsor => !book.Section.SponsorInDeal(sponsor.Id) && !book.Section.IsTaken(sponsor.Id, today))
                .Where(sponsor => sponsor.PrestigeNeed <= ModestPrestige)
                .ToArray();

            Assert.True(free.Length >= FreeCandidatesWanted, today + ": only " + free.Length + " free sponsors for a technical slot.");
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
        var sponsors = result.Modules.Require<SponsorBook>().Section;
        Assert.NotEmpty(sponsors.Deals);
    }

    [Fact]
    public void LocalBackersAreTheSameEveryTimeAndAnIdFindsThemAgain()
    {
        var catalog = new SponsorCatalog([], localMarket: true);
        var first = LocalSponsorMarket.ActiveIn(1956);
        var second = LocalSponsorMarket.ActiveIn(1956);

        Assert.Equal(first.Select(sponsor => sponsor.Id), second.Select(sponsor => sponsor.Id));
        Assert.Equal(first.Count, first.Select(sponsor => sponsor.Id).Distinct().Count());
        Assert.Equal(first.Count, first.Select(sponsor => sponsor.Name).Distinct().Count());
        foreach (var sponsor in first)
        {
            Assert.True(sponsor.Local);
            Assert.Equal(sponsor.Name, catalog.Find(sponsor.Id)?.Name);
        }

        Assert.Null(new SponsorCatalog([]).Find(first[0].Id));
        Assert.Null(catalog.Find("local_1956_t99"));
        Assert.Null(catalog.Find("local_x"));
    }

    [Fact]
    public void LocalBackersLeaveAfterTheirSeasonsAndRespectTheEra()
    {
        var data = new SponsorKit(new GameDate(1955, 1, 1)).Data;
        var era = SponsorEra.ForYear(data.EraPeriods, 1955);
        var catalog = new SponsorCatalog([], localMarket: true);

        var now = catalog.Candidates(1957, SlotKind.Technical, era);

        Assert.All(now, sponsor => Assert.InRange(sponsor.FromYear, 1956, 1957));
        Assert.DoesNotContain(now, sponsor => sponsor.FromYear == 1955);
        Assert.Empty(catalog.Candidates(1955, SlotKind.Main, era));
        Assert.Equal(SponsorEstimates.LocalBackersPerSeason * SponsorEstimates.LocalBackerSeasons, now.Count);
    }
}
