using Paddock.Application.Access;
using Paddock.Application.Commands;
using Paddock.Application.Sponsors;
using Paddock.Domain.Inbox;
using Paddock.Domain.Objectives;
using Paddock.Domain.Sponsors;
using Paddock.Domain.Time;
using Paddock.Domain.World;

namespace Paddock.Tests.Sponsors;

/// <summary>
/// Sponsor terms of the first playtest (#268): length, difficulty of the condition, nationality wishes as a bonus, industry bonuses and the
/// negotiation of a renewal. Every amount and share here is an ESTIMATE in <see cref="SponsorEstimates"/>; these tests pin the rules, not the numbers.
/// </summary>
public sealed class SponsorTermsTests
{
    private static readonly GameDate Opening = new(1955, 1, 1);

    private static readonly GameDate Livery = new(1969, 1, 1);

    // ------------------------------------------------------------------ the price of the terms

    [Fact]
    public void ALongerDealPaysTheSameAYearAndAHarderConditionMore()
    {
        Assert.Equal(1000, SponsorTerms.Default.PayMilli);
        var two = new SponsorTerms(2, SponsorAmbition.Standard).PayMilli;
        var three = new SponsorTerms(3, SponsorAmbition.Standard).PayMilli;
        Assert.Equal(1000, two);
        Assert.Equal(1000, three);
        foreach (var ambition in SponsorAmbitions.All)
        {
            Assert.Equal(new SponsorTerms(1, ambition).PayMilli, new SponsorTerms(2, ambition).PayMilli);
            Assert.Equal(new SponsorTerms(1, ambition).PayMilli, new SponsorTerms(3, ambition).PayMilli);
        }

        Assert.True(new SponsorTerms(1, SponsorAmbition.Lighter).PayMilli < 1000);
        Assert.True(new SponsorTerms(1, SponsorAmbition.Harder).PayMilli > 1000);
        Assert.Equal(1_000_000, SponsorTerms.Default.AnnualCents(1_000_000));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    [InlineData(-1)]
    public void ADealRunsOneToThreeYears(int years)
    {
        Assert.False(new SponsorTerms(years, SponsorAmbition.Standard).IsValid);
        Assert.True(new SponsorTerms(1, SponsorAmbition.Standard).IsValid);
        Assert.True(new SponsorTerms(3, SponsorAmbition.Harder).IsValid);
    }

    [Fact]
    public void AHarderConditionAsksMoreOfTheTeamAndAnEasierOneLess()
    {
        var podiums = new SponsorObjectiveSpec(SponsorObjectiveSpec.PodiumsAtLeast, "4", 364);
        int Target(SponsorAmbition ambition) =>
            int.Parse(SponsorObjectiveScale.Scale(podiums, 4, 10, ambition).Value, System.Globalization.CultureInfo.InvariantCulture);

        Assert.True(Target(SponsorAmbition.Lighter) < Target(SponsorAmbition.Standard));
        Assert.True(Target(SponsorAmbition.Standard) < Target(SponsorAmbition.Harder));
        Assert.Equal(Target(SponsorAmbition.Standard), int.Parse(SponsorObjectiveScale.Scale(podiums, 4, 10).Value, System.Globalization.CultureInfo.InvariantCulture));

        // A position is better when it is smaller, so a harder condition names a smaller number, and it never leaves the field.
        var position = new SponsorObjectiveSpec(SponsorObjectiveSpec.ChampionshipPositionAtMost, "6", 364);
        int Place(SponsorAmbition ambition) =>
            int.Parse(SponsorObjectiveScale.Scale(position, 5, 10, ambition).Value, System.Globalization.CultureInfo.InvariantCulture);

        Assert.True(Place(SponsorAmbition.Harder) < Place(SponsorAmbition.Standard));
        Assert.True(Place(SponsorAmbition.Standard) < Place(SponsorAmbition.Lighter));
        Assert.InRange(Place(SponsorAmbition.Lighter), 1, 10);

        // A team expected to win is asked for a podium-level place, not for the impossible: even the hardest condition stays inside the field.
        var topHarder = int.Parse(SponsorObjectiveScale.Scale(position, 1, 10, SponsorAmbition.Harder).Value, System.Globalization.CultureInfo.InvariantCulture);
        Assert.InRange(topHarder, 1, 3);
    }

    [Fact]
    public void ABiggerTargetPaysABiggerBonusAndNeverTheOtherWayRound()
    {
        var podiums = new SponsorObjectiveSpec(SponsorObjectiveSpec.PodiumsAtLeast, "2", 364);
        int Bonus(SponsorAmbition ambition) =>
            SponsorObjectiveScale.Reward(podiums, SponsorObjectiveScale.Scale(podiums, 3, 10, ambition)).BonusMilli;

        Assert.True(Bonus(SponsorAmbition.Lighter) <= Bonus(SponsorAmbition.Standard));
        Assert.True(Bonus(SponsorAmbition.Standard) <= Bonus(SponsorAmbition.Harder));
    }

    // ------------------------------------------------------------------ talks on terms

    [Fact]
    public void TalksOnTermsRememberThemAndThePriceFollows()
    {
        var kit = new SponsorKit(Opening);
        var terms = new SponsorTerms(2, SponsorAmbition.Harder);

        Assert.Null(kit.Begin(SponsorKit.Anna, SponsorKit.Alfa, "vestoil_works", 1, Opening, terms));
        var talk = kit.Book.Section.Talks.Last();

        Assert.Equal(2, talk.Years);
        Assert.Equal(SponsorAmbition.Harder, talk.Ambition);
        Assert.Equal(talk.FullAnnualCents * 800 / 1000 * terms.PayMilli / 1000, talk.AnnualCentsOn(Opening));
        Assert.True(talk.AnnualCentsOn(Opening, SponsorTerms.Default) != talk.AnnualCentsOn(Opening));
        Assert.Equal(talk.FullAnnualCents * talk.CapMilli / 1000 * terms.PayMilli / 1000, talk.CappedAnnualCents);
    }

    [Fact]
    public void TalksWithoutTermsAreTheOneYearStandardDealTheyAlwaysWere()
    {
        var kit = new SponsorKit(Opening);
        var talk = kit.OpenTalk("vestoil_works", 1, Opening);

        Assert.True(talk.Terms.IsDefault);
        Assert.Equal(talk.FullAnnualCents * 800 / 1000, talk.AnnualCentsOn(Opening));
    }

    [Theory]
    [InlineData("vestoil_works", 4, SponsorAmbition.Standard)]
    [InlineData("vestoil_works", 0, SponsorAmbition.Standard)]
    [InlineData("rheinwerk_motoren", 1, SponsorAmbition.Harder)]
    [InlineData("club_verdane", 1, SponsorAmbition.Lighter)]
    public void TermsThatMakeNoSenseAreRefusedWithAReason(string sponsor, int years, SponsorAmbition ambition)
    {
        var kit = new SponsorKit(Opening);

        Assert.Equal(SponsorKeys.BadTerms, kit.Begin(SponsorKit.Anna, SponsorKit.Alfa, sponsor, 1, Opening, new SponsorTerms(years, ambition)));
        Assert.Empty(kit.Book.Section.Talks);
    }

    [Fact]
    public void ProposingTermsChangesAnOpenTalkOnly()
    {
        var kit = new SponsorKit(Opening, skill: 20);
        var talk = kit.OpenTalk("vestoil_works", 1, Opening);

        Assert.Null(kit.Run(Propose(SponsorKit.Anna, talk.Id, 3, SponsorAmbition.Lighter)));
        var changed = kit.Book.Section.FindTalk(talk.Id)!;
        Assert.Equal(new SponsorTerms(3, SponsorAmbition.Lighter), changed.Terms);
        Assert.True(changed.AnnualCentsOn(Opening) < talk.AnnualCentsOn(Opening));

        Assert.Equal(SponsorKeys.BadTerms, kit.Run(Propose(SponsorKit.Anna, talk.Id, 5, SponsorAmbition.Standard)));
        Assert.Equal(SponsorKeys.NotYourOrganization, kit.Run(Propose(SponsorKit.Bram, talk.Id, 2, SponsorAmbition.Standard)));
        Assert.Equal(SponsorKeys.UnknownTalk, kit.Run(Propose(SponsorKit.Anna, "spt:99", 2, SponsorAmbition.Standard)));

        Assert.Null(kit.Run(new WalkAwayFromTalksCommand { ManagerId = SponsorKit.Anna, IssuedOn = SponsorKit.Day(Opening), OrganizationId = SponsorKit.Alfa.Value, TalkId = talk.Id }));
        Assert.Equal(SponsorKeys.TalkClosed, kit.Run(Propose(SponsorKit.Anna, talk.Id, 2, SponsorAmbition.Standard)));
    }

    [Fact]
    public void TheCommandsOfTheTermsAreSavedAndReadBack()
    {
        var plain = new BeginSponsorTalksCommand { ManagerId = SponsorKit.Anna, IssuedOn = SponsorKit.Day(Opening), OrganizationId = "alfa", SponsorId = "vestoil_works", Slot = 1 };
        var terms = plain with { Years = 3, Ambition = SponsorAmbition.Harder };
        var counter = new CounterSponsorOfferCommand { ManagerId = SponsorKit.Anna, IssuedOn = SponsorKit.Day(Opening), OrganizationId = "alfa", OfferId = "spo:4", Years = 2, Ambition = SponsorAmbition.Lighter };
        var propose = Propose(SponsorKit.Anna, "spt:2", 2, SponsorAmbition.Harder);

        // A command with the default terms is written exactly as before (an older save reads the same), the others carry the two fields.
        var encodedPlain = CommandCodec.Production.Encode(plain);
        Assert.DoesNotContain("years", encodedPlain.Text, StringComparison.Ordinal);
        foreach (var command in new ICommand[] { plain, terms, counter, propose })
        {
            var encoded = CommandCodec.Production.Encode(command);
            var decoded = CommandCodec.Production.Decode(encoded.Tag, encoded.Text, SponsorKit.Anna, 7, SponsorKit.Day(Opening));
            Assert.Equal(command.WithSubmissionNumber(7), decoded);
        }

        // The body an older build wrote still reads.
        var old = CommandCodec.Production.Decode("sponsor.beginTalks/1", """{"organization":"alfa","sponsor":"vestoil_works","slot":"1"}""", SponsorKit.Anna, 1, SponsorKit.Day(Opening));
        Assert.True(((BeginSponsorTalksCommand)old).Terms.IsDefault);
    }

    // ------------------------------------------------------------------ deals of several years

    [Fact]
    public void ATwoYearDealRunsTwoYearsPaysTwentyFourInstalmentsAndGetsAFreshConditionEachYear()
    {
        var kit = new SponsorKit(Opening);
        var deal = kit.SignDeal("vestoil_works", 1, Opening, new SponsorTerms(2, SponsorAmbition.Standard));

        Assert.Equal(Opening.AddDays(729), deal.End);
        var first = deal.ObjectiveId;
        Assert.NotNull(first);

        // The team keeps winning podiums, so the condition of each year is met.
        kit.Podiums = 1;
        kit.Live(Opening, 366);
        var midway = kit.Book.Section.FindDeal(deal.Id)!;
        Assert.Equal(DealStatus.Active, midway.Status);
        Assert.NotEqual(first, midway.ObjectiveId);
        Assert.Equal(DealObjectiveOutcome.None, midway.Outcome);
        Assert.Equal(2, kit.Book.Objectives.Objectives.Count);

        kit.Podiums = 2;
        kit.Live(Opening.AddDays(366), 380);
        var done = kit.Book.Section.FindDeal(deal.Id)!;
        Assert.Equal(DealStatus.Completed, done.Status);
        Assert.Equal(24, done.InstalmentsPaid);
        var instalments = kit.SponsorEntries(SponsorKit.Alfa).Where(entry => entry.ReasonKey == SponsorReason.Instalment).ToList();
        Assert.Equal(24, instalments.Count);

        // The first year met its condition and the sponsor trusts the team now, so the second year pays more (see SponsorPartnershipTests).
        Assert.Equal(SponsorPartnership.Raised(deal.AnnualCents, SponsorEstimates.AnniversaryRaiseOpenMilli), midway.AnnualCents);
        Assert.Equal(deal.AnnualCents + midway.AnnualCents, instalments.Sum(entry => entry.AmountCents));
        Assert.True(done.BonusCents > 0);
    }

    [Fact]
    public void AMissedConditionEndsADealOfSeveralYearsAtTheEndOfThatYear()
    {
        var kit = new SponsorKit(Opening);
        var deal = kit.SignDeal("vestoil_works", 1, Opening, new SponsorTerms(3, SponsorAmbition.Standard));

        kit.Podiums = 0;
        kit.Live(Opening, 370);

        var ended = kit.Book.Section.FindDeal(deal.Id)!;
        Assert.Equal(DealStatus.Ended, ended.Status);
        Assert.Equal(DealObjectiveOutcome.Failed, ended.Outcome);
        Assert.True(ended.InstalmentsPaid <= SponsorEstimates.InstalmentsPerYear);
        Assert.Contains(SponsorKeys.InboxFailedSubject, kit.InboxSubjects(SponsorKit.Anna));
    }

    [Fact]
    public void ADealOfThreeYearsPaysTheSameAYearAsADealOfOne()
    {
        var kit = new SponsorKit(Opening, skill: 20);
        var one = kit.SignDeal("vestoil_works", 1, Opening);
        var other = new SponsorKit(Opening, skill: 20);
        var three = other.SignDeal("vestoil_works", 1, Opening, new SponsorTerms(3, SponsorAmbition.Standard));

        Assert.Equal(one.AnnualCents, three.AnnualCents);
    }

    // ------------------------------------------------------------------ nationality wishes

    [Fact]
    public void ANationalityWishIsNeverAnObjectiveAndExistsOnlyWhileSuchADriverCanBeHad()
    {
        var empty = new SponsorKit(Opening);
        var without = empty.SignDeal("club_verdane", 1, Opening);
        Assert.Null(without.WishNationality);
        Assert.Null(without.ObjectiveId);

        var kit = new SponsorKit(Opening);
        kit.AddDriver("FRA", seat: null);
        var deal = kit.SignDeal("club_verdane", 1, Opening);

        Assert.Equal("FRA", deal.WishNationality);
        Assert.False(deal.WishRaceSeat);
        Assert.Null(deal.ObjectiveId);
        Assert.Empty(kit.Book.Objectives.Objectives);
    }

    [Fact]
    public void ARetiredOrAJuniorOrATooYoungDriverDoesNotMakeAWishPossible()
    {
        var kit = new SponsorKit(Opening);
        kit.AddDriver("FRA", seat: null, bornYear: 1945);

        Assert.False(SponsorWishes.DriverExists(kit.World, "FRA", Opening));
        Assert.True(SponsorWishes.DriverExists(kit.World, "FRA", new GameDate(1965, 1, 1)));
    }

    [Fact]
    public void AMissedWishCostsNothingButTheBonus()
    {
        var kit = new SponsorKit(Opening);
        kit.AddDriver("FRA", seat: null);
        var deal = kit.SignDeal("club_verdane", 1, Opening);
        var trust = kit.Book.Section.TrustOf("club_verdane", SponsorKit.Alfa);

        kit.Live(Opening, 370);

        var done = kit.Book.Section.FindDeal(deal.Id)!;
        Assert.Equal(DealStatus.Completed, done.Status);
        Assert.True(kit.Book.Section.TrustOf("club_verdane", SponsorKit.Alfa) >= trust);
        var entries = kit.SponsorEntries(SponsorKit.Alfa);
        Assert.DoesNotContain(entries, entry => entry.ReasonKey == SponsorReason.Nationality);
        Assert.Equal(deal.AnnualCents, entries.Where(entry => entry.ReasonKey == SponsorReason.Instalment).Sum(entry => entry.AmountCents));
        Assert.DoesNotContain(SponsorKeys.InboxFailedSubject, kit.InboxSubjects(SponsorKit.Anna));
    }

    [Fact]
    public void ASmallSponsorAcceptsAReserveAndPaysTheBonusEveryMonthItIsMet()
    {
        var kit = new SponsorKit(Opening);
        kit.AddDriver("FRA", SeatStatus.Reserve);
        var deal = kit.SignDeal("club_verdane", 1, Opening);

        kit.Live(Opening, 370);

        var bonus = kit.SponsorEntries(SponsorKit.Alfa).Where(entry => entry.ReasonKey == SponsorReason.Nationality).ToList();
        Assert.Equal(12, bonus.Count);
        Assert.Equal(deal.AnnualCents * SponsorEstimates.WishBonusMilli / 1000, bonus.Sum(entry => entry.AmountCents));
    }

    [Fact]
    public void ABigSponsorWantsTheNationalityInARaceSeatAndReadsGermanAsGermanyUnderEitherCode()
    {
        var kit = new SponsorKit(Livery, skill: 20);
        kit.AddDriver("DEU", SeatStatus.Reserve);
        var deal = kit.SignDeal("brauhaus_elsner", 1, Livery);

        Assert.Equal("GER", deal.WishNationality);
        Assert.True(deal.WishRaceSeat);

        // A reserve is not enough for a big sponsor, a race driver is, and the code "DEU" of the people files is the sponsor file's "GER".
        kit.Live(Livery, 40);
        Assert.DoesNotContain(kit.SponsorEntries(SponsorKit.Alfa), entry => entry.ReasonKey == SponsorReason.Nationality);

        kit.AddDriver("DEU", SeatStatus.NumberOne);
        kit.Live(Livery.AddDays(40), 60);
        Assert.Contains(kit.SponsorEntries(SponsorKit.Alfa), entry => entry.ReasonKey == SponsorReason.Nationality);
    }

    // ------------------------------------------------------------------ industry bonuses

    [Fact]
    public void FuelAndOilSponsorsSupplyGoodsWithEveryInstalment()
    {
        var kit = new SponsorKit(Opening);
        var deal = kit.SignDeal("vestoil_works", 1, Opening);

        kit.Live(Opening, 365);

        var goods = kit.SponsorEntries(SponsorKit.Alfa).Where(entry => entry.ReasonKey == SponsorReason.InKind).ToList();
        Assert.Equal(12, goods.Count);
        Assert.Equal(deal.AnnualCents * SponsorEstimates.InKindMilli / 1000, goods.Sum(entry => entry.AmountCents));
    }

    [Fact]
    public void AFinanceSponsorPaysASigningBonusOnceWithTheFirstInstalment()
    {
        var kit = new SponsorKit(Livery, skill: 20);
        var deal = kit.SignDeal("norcastle_insurance", 1, Livery);

        kit.Live(Livery, 400);

        var signing = Assert.Single(kit.SponsorEntries(SponsorKit.Alfa), entry => entry.ReasonKey == SponsorReason.Signing);
        Assert.Equal(deal.AnnualCents * SponsorEstimates.SigningBonusMilli / 1000, signing.AmountCents);
        Assert.Equal(new GameDate(1969, 1, 15), signing.Date);
    }

    [Fact]
    public void AnIndustryWithoutABonusAddsNothing()
    {
        Assert.Equal(IndustryBonusKind.None, SponsorIndustryBonus.KindOf(SponsorIndustries.Tobacco));
        Assert.Equal(IndustryBonusKind.None, SponsorIndustryBonus.KindOf(SponsorIndustries.Patron));
        Assert.Equal(0, SponsorIndustryBonus.Milli(SponsorIndustries.Alcohol));
    }

    // ------------------------------------------------------------------ renewal as a negotiation

    [Fact]
    public void ASatisfiedSponsorOffersMoreThanItPaidAndAMoreSatisfiedOneOffersMoreStill()
    {
        decimal Offered(int trust)
        {
            var kit = new SponsorKit(Opening);
            var deal = kit.SignDeal("rheinwerk_motoren", 1, Opening);
            kit.SetTrust("rheinwerk_motoren", trust);
            kit.Live(Opening, 310);
            var offer = Assert.Single(kit.Book.Section.OpenOffersOf(SponsorKit.Alfa));
            Assert.True(offer.AnnualCents >= deal.AnnualCents * SponsorEstimates.RenewalRaiseBaseMilli / 1000, "The offer is not above the old deal.");
            return offer.AnnualCents;
        }

        Assert.True(Offered(90) > Offered(55));
    }

    [Fact]
    public void ARenewalCanBeNegotiatedForALimitedNumberOfRoundsAndTheInboxFollows()
    {
        var kit = new SponsorKit(Opening);
        var deal = kit.SignDeal("vestoil_works", 1, Opening);
        kit.Podiums = 1;
        kit.Live(Opening, 310);
        var offer = Assert.Single(kit.Book.Section.OpenOffersOf(SponsorKit.Alfa));
        var day = new GameDate(1955, 11, 10);
        Assert.Single(OpenOfferItems(kit));

        Assert.Equal(SponsorKeys.SameTerms, kit.Run(Counter(SponsorKit.Anna, offer.Id, 1, SponsorAmbition.Standard, day)));
        Assert.Equal(SponsorKeys.BadTerms, kit.Run(Counter(SponsorKit.Anna, offer.Id, 4, SponsorAmbition.Standard, day)));
        Assert.Equal(SponsorKeys.NotYourOrganization, kit.Run(Counter(SponsorKit.Bram, offer.Id, 2, SponsorAmbition.Standard, day)));

        Assert.Null(kit.Run(Counter(SponsorKit.Anna, offer.Id, 2, SponsorAmbition.Harder, day)));
        var countered = kit.Book.Section.FindOffer(offer.Id)!;
        Assert.Equal(new SponsorTerms(2, SponsorAmbition.Harder), countered.Terms);
        Assert.Equal(1, countered.Rounds);
        Assert.NotEqual(offer.AnnualCents, countered.AnnualCents);
        Assert.Equal(
            SponsorRules.RenewalCents(kit.Book, kit.Environment, deal, kit.Environment.Catalog.Find("vestoil_works")!, countered.Terms),
            countered.AnnualCents);

        // The decision in the inbox is replaced, never left quoting the old terms, and the clock stays held until the player decides.
        var items = OpenOfferItems(kit);
        var current = Assert.Single(items);
        Assert.Equal(SponsorKeys.InboxOfferSubject2, current.Draft.SubjectKey);
        Assert.Equal(SponsorNotices.Dollars(countered.AnnualCents), current.Arguments["amount"]);
        Assert.Contains(kit.Inbox.Section.Items, item => item.Status == InboxStatus.Expired && item.Kind == SponsorOfferCodes.OfferKind);

        Assert.Null(kit.Run(Counter(SponsorKit.Anna, offer.Id, 3, SponsorAmbition.Lighter, day)));
        Assert.Null(kit.Run(Counter(SponsorKit.Anna, offer.Id, 1, SponsorAmbition.Harder, day)));
        Assert.Equal(SponsorEstimates.MaxCounterRounds, kit.Book.Section.FindOffer(offer.Id)!.Rounds);
        Assert.Equal(SponsorKeys.NoRoundsLeft, kit.Run(Counter(SponsorKit.Anna, offer.Id, 2, SponsorAmbition.Standard, day)));

        // Accepting signs what is on the table.
        Assert.Null(kit.Run(new RespondToSponsorOfferCommand { ManagerId = SponsorKit.Anna, IssuedOn = SponsorKit.Day(day), OrganizationId = SponsorKit.Alfa.Value, OfferId = offer.Id, Accept = true }));
        var renewal = kit.Book.Section.Deals.Last();
        var final = kit.Book.Section.FindOffer(offer.Id)!;
        Assert.Equal(final.Terms, renewal.Terms);
        Assert.Equal(final.AnnualCents, renewal.AnnualCents);
        Assert.Equal(deal.End.AddDays(1), renewal.Start);
    }

    [Fact]
    public void AnOfferTheInboxAcceptsSignsTheTermsOfTheOfferAfterANegotiation()
    {
        var kit = new SponsorKit(Opening);
        kit.SignDeal("vestoil_works", 1, Opening);
        kit.Live(Opening, 310);
        var offer = Assert.Single(kit.Book.Section.OpenOffersOf(SponsorKit.Alfa));
        Assert.Null(kit.Run(Counter(SponsorKit.Anna, offer.Id, 3, SponsorAmbition.Standard, new GameDate(1955, 11, 10))));
        var item = Assert.Single(OpenOfferItems(kit));

        kit.Inbox.Resolvers.Find(SponsorOfferCodes.OfferKind)!.Execute(item, SponsorOfferCodes.OptionAccept, kit.Context);

        var renewal = kit.Book.Section.Deals.Last();
        Assert.Equal(3, renewal.Years);
        Assert.Equal(SponsorKeys.InboxOfferSubject3, item.Draft.SubjectKey);
    }

    // ------------------------------------------------------------------ what the player sees

    [Fact]
    public void TheMarketListsEachSponsorOnceInTheFirstFreeSlotItFitsWithTheTableOfTerms()
    {
        var kit = new SponsorKit(Livery, skill: 20);
        kit.AddDriver("FRA", seat: null);
        var own = Read(kit, Livery);

        Assert.Equal(own.Market.Count, own.Market.Select(row => row.SponsorId).Distinct().Count());
        var norcastle = Assert.Single(own.Market, row => row.SponsorId == "norcastle_insurance");
        Assert.Equal(1, norcastle.Slot);
        Assert.Equal(9, norcastle.Quotes.Count);
        Assert.Equal(
            norcastle.IndicativeAnnualCents,
            Assert.Single(norcastle.Quotes, quote => quote.Years == 1 && quote.Ambition == "standard").AnnualCents);
        Assert.All(norcastle.Quotes, quote => Assert.NotNull(quote.Condition));
        Assert.Equal("signing", norcastle.IndustryBonus!.Kind);

        var rheinwerk = Assert.Single(own.Market, row => row.SponsorId == "rheinwerk_motoren");
        Assert.Equal(3, rheinwerk.Quotes.Count);
        Assert.False(rheinwerk.AmbitionOpen);
        Assert.All(rheinwerk.Quotes, quote => Assert.Null(quote.Condition));

        var verdane = Assert.Single(own.Market, row => row.SponsorId == "club_verdane");
        Assert.Equal("FRA", verdane.Wish!.Nationality);
        Assert.False(verdane.Wish.RaceSeat);

        // Once the slot is taken the sponsors for it leave the market, and the next free slot takes the livery sponsors.
        kit.SignDeal("norcastle_insurance", 1, Livery);
        var after = Read(kit, Livery);
        Assert.DoesNotContain(after.Market, row => row.SponsorId == "norcastle_insurance");
        Assert.Equal(2, Assert.Single(after.Market, row => row.SponsorId == "brightline_watches").Slot);
    }

    [Fact]
    public void TheQuotesOfAnOpenTalkAreTheOnesTheCommandsWouldSignWith()
    {
        var kit = new SponsorKit(Opening);
        var talk = kit.OpenTalk("vestoil_works", 1, Opening);

        var view = Assert.Single(Read(kit, Opening).Talks);

        Assert.Equal(talk.AnnualCentsOn(Opening), view.CurrentAnnualCents);
        foreach (var quote in view.Quotes)
        {
            SponsorAmbitions.TryParse(quote.Ambition, out var ambition);
            var terms = new SponsorTerms(quote.Years, ambition);
            Assert.Equal(talk.AnnualCentsOn(Opening, terms), quote.AnnualCents);
            Assert.Equal(talk.CappedAnnualCentsFor(terms), quote.CapCents);
        }
    }

    [Fact]
    public void ADealShowsItsLengthAndAWishShowsWhetherItIsMet()
    {
        var kit = new SponsorKit(Opening);
        kit.AddDriver("FRA", SeatStatus.NumberTwo);
        kit.SignDeal("club_verdane", 1, Opening, SponsorTerms.Default);

        var deal = Assert.Single(Read(kit, Opening).Deals);

        Assert.Equal(1, deal.Years);
        Assert.True(deal.Wish!.Met);
        Assert.Null(deal.IndustryBonus);
    }

    // ------------------------------------------------------------------ the section on disk

    [Fact]
    public void ASectionOfDefaultTermsHasTheTextItAlwaysHadAndOneWithTermsAddsThem()
    {
        var plain = new SponsorKit(Opening);
        plain.SignDeal("vestoil_works", 1, Opening);
        var termed = new SponsorKit(Opening);
        termed.SignDeal("vestoil_works", 1, Opening, new SponsorTerms(2, SponsorAmbition.Harder));

        Assert.DoesNotContain(" terms ", Canonical(plain.Book.Section), StringComparison.Ordinal);
        Assert.Contains(" terms 2 harder", Canonical(termed.Book.Section), StringComparison.Ordinal);
    }

    [Fact]
    public void RestoringARowKeepsTheTerms()
    {
        var kit = new SponsorKit(Opening);
        kit.AddDriver("FRA", seat: null);
        kit.SignDeal("club_verdane", 1, Opening, SponsorTerms.Default);
        var deal = kit.Book.Section.Deals.Single();
        Assert.Equal("FRA", deal.WishNationality);

        var restored = SponsorsSection.Restore(kit.Book.Section.Next, kit.Book.Section.Talks, kit.Book.Section.Deals, [], [], []);

        Assert.Equal(deal, restored.Deals.Single());
    }

    // ------------------------------------------------------------------ helpers

    private static string Canonical(SponsorsSection section)
    {
        var writer = new CanonicalWriter();
        section.WriteCanonical(writer);
        return writer.ToString();
    }

    private static SponsorView.Own Read(SponsorKit kit, GameDate day) =>
        Assert.IsType<SponsorView.Own>(SponsorQuery.Read(AccessContext.Developer, SponsorKit.Alfa, kit.Book, kit.Environment, kit.ObjectiveQuery(), day));

    private static ProposeSponsorTermsCommand Propose(Paddock.Application.Managers.ManagerId manager, string talk, int years, SponsorAmbition ambition) =>
        new()
        {
            ManagerId = manager,
            IssuedOn = SponsorKit.Day(Opening),
            OrganizationId = SponsorKit.Alfa.Value,
            TalkId = talk,
            Years = years,
            Ambition = ambition,
        };

    private static CounterSponsorOfferCommand Counter(Paddock.Application.Managers.ManagerId manager, string offer, int years, SponsorAmbition ambition, GameDate day) =>
        new()
        {
            ManagerId = manager,
            IssuedOn = SponsorKit.Day(day),
            OrganizationId = SponsorKit.Alfa.Value,
            OfferId = offer,
            Years = years,
            Ambition = ambition,
        };

    private static IReadOnlyList<InboxItem> OpenOfferItems(SponsorKit kit) =>
        kit.Inbox.Section.ItemsOf(SponsorKit.Anna.Value).Where(item => item.IsOpen && item.Kind == SponsorOfferCodes.OfferKind).ToArray();
}
