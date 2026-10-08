using Paddock.Application.Access;
using Paddock.Application.Commands;
using Paddock.Application.Sponsors;
using Paddock.Domain.Sponsors;
using Paddock.Domain.Time;

namespace Paddock.Tests.Sponsors;

/// <summary>
/// The partnership rules of the first playtest (#268, owner answers): a longer deal pays the same a year, a satisfied sponsor raises the amount at
/// each anniversary and says why, the screen can tell before the player signs how open a sponsor is, and the player may ask for a little more than the
/// quote. Every figure is an ESTIMATE in <see cref="SponsorEstimates"/>; these tests pin the rules, not the numbers.
/// </summary>
public sealed class SponsorPartnershipTests
{
    private static readonly GameDate Opening = new(1955, 1, 1);

    private static SponsorView.Own View(SponsorKit kit, GameDate today) =>
        (SponsorView.Own)SponsorQuery.Read(AccessContext.Developer, SponsorKit.Alfa, kit.Book, kit.Environment, kit.ObjectiveQuery(), today);

    // ------------------------------------------------------------------ a longer deal pays the same

    [Fact]
    public void EveryLengthOfADealQuotesTheSameAmountAYear()
    {
        var kit = new SponsorKit(Opening);
        var market = View(kit, Opening).Market.First(row => row.SponsorId == "vestoil_works");

        foreach (var ambition in market.Quotes.Select(quote => quote.Ambition).Distinct())
        {
            var amounts = market.Quotes.Where(quote => quote.Ambition == ambition).Select(quote => quote.AnnualCents).Distinct().ToArray();
            Assert.Single(amounts);
        }

        Assert.Equal(3, market.Quotes.Select(quote => quote.Years).Distinct().Count());
    }

    [Fact]
    public void ARenewalOfALongerDealAsksTheSameAmountAYearAsOfAShortOne()
    {
        var kit = new SponsorKit(Opening);
        kit.SignDeal("vestoil_works", 1, Opening);
        kit.Podiums = 1;
        kit.Live(Opening, 330);

        var offer = View(kit, Opening.AddDays(330)).Offers.Single();

        Assert.Equal(3, offer.Quotes.Select(quote => quote.Years).Distinct().Count());
        foreach (var ambition in offer.Quotes.Select(quote => quote.Ambition).Distinct())
        {
            Assert.Single(offer.Quotes.Where(quote => quote.Ambition == ambition).Select(quote => quote.AnnualCents).Distinct());
        }
    }

    // ------------------------------------------------------------------ the anniversary raise

    [Theory]
    [InlineData(60, SponsorEstimates.AnniversaryRaiseOpenMilli)]
    [InlineData(90, SponsorEstimates.AnniversaryRaiseEagerMilli)]
    public void ASatisfiedSponsorRaisesTheAmountAtTheAnniversaryAndSaysWhy(int trust, int milli)
    {
        var kit = new SponsorKit(Opening);
        kit.SetTrust("vestoil_works", trust);
        var deal = kit.SignDeal("vestoil_works", 1, Opening, new SponsorTerms(2, SponsorAmbition.Standard));

        kit.Podiums = 1;
        kit.Live(Opening, 366);

        var midway = kit.Book.Section.FindDeal(deal.Id)!;
        Assert.Equal(SponsorPartnership.Raised(deal.AnnualCents, milli), midway.AnnualCents);
        Assert.True(midway.AnnualCents > deal.AnnualCents);
        Assert.Contains(SponsorKeys.InboxRaisedSubject, kit.InboxSubjects(SponsorKit.Anna));

        // The new amount is what the second year's instalments pay, and the objective granted for that year pays its bonus on it.
        kit.Podiums = 2;
        kit.Live(Opening.AddDays(366), 380);
        var instalments = kit.SponsorEntries(SponsorKit.Alfa).Where(entry => entry.ReasonKey == SponsorReason.Instalment).ToList();
        Assert.Equal(deal.AnnualCents + midway.AnnualCents, instalments.Sum(entry => entry.AmountCents));
    }

    [Fact]
    public void ASponsorThatIsReservedRaisesNothingEvenAfterAGoodYear()
    {
        var kit = new SponsorKit(Opening);
        kit.SetTrust("vestoil_works", 20);
        var deal = kit.SignDeal("vestoil_works", 1, Opening, new SponsorTerms(2, SponsorAmbition.Standard));

        // Meeting the condition raises the trust by ten, which is still not enough for a small sponsor with no history with the team.
        kit.Podiums = 1;
        kit.Live(Opening, 366);

        var midway = kit.Book.Section.FindDeal(deal.Id)!;
        Assert.Equal(deal.AnnualCents, midway.AnnualCents);
        Assert.DoesNotContain(SponsorKeys.InboxRaisedSubject, kit.InboxSubjects(SponsorKit.Anna));
    }

    [Fact]
    public void ADealWhoseConditionWasMissedIsNotRaisedItEnds()
    {
        var kit = new SponsorKit(Opening);
        kit.SetTrust("vestoil_works", 90);
        var deal = kit.SignDeal("vestoil_works", 1, Opening, new SponsorTerms(2, SponsorAmbition.Standard));

        kit.Podiums = 0;
        kit.Live(Opening, 366);

        var ended = kit.Book.Section.FindDeal(deal.Id)!;
        Assert.Equal(DealStatus.Ended, ended.Status);
        Assert.Equal(deal.AnnualCents, ended.AnnualCents);
        Assert.DoesNotContain(SponsorKeys.InboxRaisedSubject, kit.InboxSubjects(SponsorKit.Anna));
    }

    [Fact]
    public void ARulesInTheDomainNeverRaiseForAnUnsettledOrFailedYear()
    {
        var kit = new SponsorKit(Opening);
        kit.SetTrust("vestoil_works", 90);
        var sponsor = kit.Environment.Catalog.Find("vestoil_works")!;
        var deal = kit.SignDeal("vestoil_works", 1, Opening, new SponsorTerms(2, SponsorAmbition.Standard));

        Assert.Equal(0, SponsorRules.AnniversaryRaiseMilli(kit.Book.Section, sponsor, deal with { Outcome = DealObjectiveOutcome.None }));
        Assert.Equal(0, SponsorRules.AnniversaryRaiseMilli(kit.Book.Section, sponsor, deal with { Outcome = DealObjectiveOutcome.Failed }));
        Assert.True(SponsorRules.AnniversaryRaiseMilli(kit.Book.Section, sponsor, deal with { Outcome = DealObjectiveOutcome.Met }) > 0);
    }

    [Fact]
    public void ASponsorWithNoConditionRaisesWhenItIsOpenAndOnlyThen()
    {
        // A big sponsor with nothing to miss starts open; a small one needs the history first.
        var open = new SponsorKit(Opening, skill: 20);
        var big = open.SignDeal("rheinwerk_motoren", 1, Opening, new SponsorTerms(2, SponsorAmbition.Standard));
        open.Live(Opening, 366);
        Assert.True(open.Book.Section.FindDeal(big.Id)!.AnnualCents > big.AnnualCents);

        var reserved = new SponsorKit(Opening, skill: 20);
        reserved.SetTrust("rheinwerk_motoren", 20);
        var small = reserved.SignDeal("rheinwerk_motoren", 1, Opening, new SponsorTerms(2, SponsorAmbition.Standard));
        reserved.Live(Opening, 366);
        Assert.Equal(small.AnnualCents, reserved.Book.Section.FindDeal(small.Id)!.AnnualCents);
    }

    // ------------------------------------------------------------------ the signal before the player signs

    [Fact]
    public void TheScreenSaysHowOpenASponsorIsBeforeTheTermsAreChosenAndItFollowsTheTeamsOwnHistory()
    {
        var kit = new SponsorKit(Opening);

        string Band() => View(kit, Opening).Market.First(row => row.SponsorId == "vestoil_works").Partnership!.Band;

        kit.SetTrust("vestoil_works", 20);
        Assert.Equal("reserved", Band());
        kit.SetTrust("vestoil_works", 60);
        Assert.Equal("open", Band());
        kit.SetTrust("vestoil_works", 80);
        Assert.Equal("eager", Band());
        Assert.True(View(kit, Opening).Market.First(row => row.SponsorId == "vestoil_works").Partnership!.RaiseMilli > 0);

        kit.SetTrust("vestoil_works", 20);
        Assert.Equal(0, View(kit, Opening).Market.First(row => row.SponsorId == "vestoil_works").Partnership!.RaiseMilli);
    }

    [Fact]
    public void ABigSponsorAndADealCompletedTogetherMakeASponsorMoreOpenAndNothingHiddenDoes()
    {
        var kit = new SponsorKit(Opening);
        var small = kit.Environment.Catalog.Find("vestoil_works")!;
        var big = kit.Environment.Catalog.Find("rheinwerk_motoren")!;
        Assert.True(big.BudgetLevel >= SponsorEstimates.BigSponsorBudgetLevel && small.BudgetLevel < SponsorEstimates.BigSponsorBudgetLevel);

        Assert.True(
            SponsorPartnership.Score(50, 0, bigSponsor: true) > SponsorPartnership.Score(50, 0, bigSponsor: false));
        Assert.True(SponsorPartnership.Score(50, 1, bigSponsor: false) > SponsorPartnership.Score(50, 0, bigSponsor: false));

        // The band is a function of the team's own section: the same for a developer's read and for the team's manager.
        var developer = View(kit, Opening).Market.First(row => row.SponsorId == "vestoil_works").Partnership;
        var manager = (SponsorView.Own)SponsorQuery.Read(
            AccessContext.ForManager(new Paddock.Application.Access.ManagerId(SponsorKit.Anna.Value)),
            SponsorKit.Alfa,
            kit.Book,
            kit.Environment,
            kit.ObjectiveQuery(),
            Opening);
        Assert.Equal(developer, manager.Market.First(row => row.SponsorId == "vestoil_works").Partnership);
    }

    [Fact]
    public void ADealCompletedTogetherCountsInTheScore()
    {
        var kit = new SponsorKit(Opening);
        kit.SetTrust("vestoil_works", 30);
        kit.SignDeal("vestoil_works", 1, Opening);
        kit.Podiums = 1;
        kit.Live(Opening, 400);
        var sponsor = kit.Environment.Catalog.Find("vestoil_works")!;
        Assert.Equal(DealStatus.Completed, kit.Book.Section.Deals.Single().Status);

        var trust = kit.Book.Section.TrustOf("vestoil_works", SponsorKit.Alfa);
        var band = SponsorRules.PartnershipOf(kit.Book.Section, sponsor, SponsorKit.Alfa);

        Assert.Equal(SponsorPartnership.BandOf(SponsorPartnership.Score(trust, 1, false)), band);
        Assert.NotEqual(PartnershipBand.Eager, SponsorPartnership.BandOf(SponsorPartnership.Score(trust, 0, false)));
    }

    // ------------------------------------------------------------------ asking for a little more

    [Theory]
    [InlineData(80, 30, SponsorAskOutcome.Accepted, 30)]
    [InlineData(80, 100, SponsorAskOutcome.Countered, 60)]
    [InlineData(60, 30, SponsorAskOutcome.Accepted, 30)]
    [InlineData(60, 60, SponsorAskOutcome.Countered, 30)]
    [InlineData(20, 30, SponsorAskOutcome.Countered, 0)]
    [InlineData(20, 0, SponsorAskOutcome.Accepted, 0)]
    public void ASponsorPaysAnAskWithinWhatItWillPayOrHoldsAtItsLimit(int trust, int askMilli, SponsorAskOutcome outcome, int appliedMilli)
    {
        var band = SponsorPartnership.BandOf(SponsorPartnership.Score(trust, 0, false));

        var answer = SponsorPartnership.Answer(askMilli, band);

        Assert.Equal(outcome, answer.Outcome);
        Assert.Equal(appliedMilli, answer.AppliedMilli);
    }

    [Fact]
    public void TheQuoteTableShowsWhatTheSponsorAnswersToEachAskBeforeTheDealIsSigned()
    {
        var kit = new SponsorKit(Opening);
        kit.SetTrust("vestoil_works", 60);
        var talk = kit.OpenTalk("vestoil_works", 1, Opening);

        var view = View(kit, Opening).Talks.Single(item => item.Id == talk.Id);
        var quote = view.Quotes.Single(item => item.Years == 1 && item.Ambition == "standard");

        Assert.Equal(SponsorEstimates.AskSteps, quote.Asks.Select(ask => ask.Milli));
        Assert.Equal(quote.AnnualCents, quote.Asks[0].AnnualCents);
        var ask = quote.Asks.Single(item => item.Milli == 60);
        Assert.Equal("countered", ask.Outcome);
        Assert.Equal(SponsorEstimates.AskOpenLimitMilli, ask.AppliedMilli);
        Assert.Equal(SponsorPartnership.Raised(quote.AnnualCents, SponsorEstimates.AskOpenLimitMilli), ask.AnnualCents);
        Assert.Equal("accepted", quote.Asks.Single(item => item.Milli == 30).Outcome);
    }

    [Fact]
    public void SigningWithAnAskPaysWhatTheTableShowedAndNothingMore()
    {
        var kit = new SponsorKit(Opening);
        kit.SetTrust("vestoil_works", 60);
        var talk = kit.OpenTalk("vestoil_works", 1, Opening);
        var quote = View(kit, Opening).Talks.Single().Quotes.Single(item => item.Years == 1 && item.Ambition == "standard");

        Assert.Null(kit.Run(new SignAtCurrentTermsCommand { ManagerId = SponsorKit.Anna, IssuedOn = SponsorKit.Day(Opening), OrganizationId = SponsorKit.Alfa.Value, TalkId = talk.Id, AskMilli = 100 }));

        var deal = kit.Book.Section.Deals.Single();
        Assert.Equal(quote.Asks.Single(ask => ask.Milli == 100).AnnualCents, deal.AnnualCents);
        Assert.Equal(SponsorPartnership.Raised(quote.AnnualCents, SponsorEstimates.AskOpenLimitMilli), deal.AnnualCents);
    }

    [Fact]
    public void AReservedSponsorHoldsAtTheQuoteAndAnAskInsideTheBandIsPaid()
    {
        var reserved = new SponsorKit(Opening);
        reserved.SetTrust("vestoil_works", 20);
        var talk = reserved.OpenTalk("vestoil_works", 1, Opening);
        var quoted = talk.AnnualCentsOn(Opening);
        Assert.Null(reserved.Run(new SignAtCurrentTermsCommand { ManagerId = SponsorKit.Anna, IssuedOn = SponsorKit.Day(Opening), OrganizationId = SponsorKit.Alfa.Value, TalkId = talk.Id, AskMilli = 60 }));
        Assert.Equal(quoted, reserved.Book.Section.Deals.Single().AnnualCents);

        var open = new SponsorKit(Opening);
        open.SetTrust("vestoil_works", 60);
        var other = open.OpenTalk("vestoil_works", 1, Opening);
        Assert.Null(open.Run(new SignAtCurrentTermsCommand { ManagerId = SponsorKit.Anna, IssuedOn = SponsorKit.Day(Opening), OrganizationId = SponsorKit.Alfa.Value, TalkId = other.Id, AskMilli = 30 }));
        Assert.Equal(SponsorPartnership.Raised(other.AnnualCentsOn(Opening), 30), open.Book.Section.Deals.Single().AnnualCents);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    [InlineData(5000)]
    public void AnAskThatIsNotANumberTheSponsorWillDiscussIsRefusedWithAReason(int askMilli)
    {
        var kit = new SponsorKit(Opening);
        var talk = kit.OpenTalk("vestoil_works", 1, Opening);

        var rejection = kit.Run(new SignAtCurrentTermsCommand { ManagerId = SponsorKit.Anna, IssuedOn = SponsorKit.Day(Opening), OrganizationId = SponsorKit.Alfa.Value, TalkId = talk.Id, AskMilli = askMilli });

        Assert.Equal(SponsorKeys.BadAsk, rejection);
        Assert.Empty(kit.Book.Section.Deals);
        Assert.True(kit.Book.Section.FindTalk(talk.Id)!.IsOpen);
    }

    [Fact]
    public void ARenewalCanBeAskedForMoreWithTheSameTermsAndTheInboxFollows()
    {
        var kit = new SponsorKit(Opening);
        kit.SetTrust("vestoil_works", 60);
        kit.SignDeal("vestoil_works", 1, Opening);
        kit.Podiums = 1;
        kit.Live(Opening, 330);
        var offer = kit.Book.Section.Offers.Single(item => item.IsOpen);

        Assert.Equal(SponsorKeys.SameTerms, kit.Run(new CounterSponsorOfferCommand
        {
            ManagerId = SponsorKit.Anna, IssuedOn = SponsorKit.Day(Opening.AddDays(330)), OrganizationId = SponsorKit.Alfa.Value, OfferId = offer.Id, Years = offer.Years, Ambition = offer.Ambition,
        }));

        var open = kit.Book.Section.Offers.Single(item => item.IsOpen);
        var band = SponsorPartnership.BandOf(SponsorPartnership.Score(kit.Book.Section.TrustOf("vestoil_works", SponsorKit.Alfa), 0, false));
        var answer = SponsorPartnership.Answer(30, band);
        Assert.Null(kit.Run(new CounterSponsorOfferCommand
        {
            ManagerId = SponsorKit.Anna, IssuedOn = SponsorKit.Day(Opening.AddDays(330)), OrganizationId = SponsorKit.Alfa.Value, OfferId = open.Id, Years = open.Years, Ambition = open.Ambition, AskMilli = 30,
        }));

        var countered = kit.Book.Section.FindOffer(open.Id)!;
        Assert.Equal(SponsorPartnership.Raised(open.AnnualCents, answer.AppliedMilli), countered.AnnualCents);
        Assert.Equal(open.Rounds + 1, countered.Rounds);
    }

    [Fact]
    public void TheAskTravelsInTheCommandLogAndAnOlderBodyStillReads()
    {
        var sign = new SignAtCurrentTermsCommand { ManagerId = SponsorKit.Anna, IssuedOn = SponsorKit.Day(Opening), OrganizationId = "alfa", TalkId = "spt:1", AskMilli = 30 };
        var written = CommandCodec.Production.Encode(sign);
        var read = (SignAtCurrentTermsCommand)CommandCodec.Production.Decode("sponsor.sign/1", written.Text, SponsorKit.Anna, 1, SponsorKit.Day(Opening));
        Assert.Equal(30, read.AskMilli);

        var plain = CommandCodec.Production.Encode(sign with { AskMilli = 0 });
        Assert.DoesNotContain("ask", plain.Text, StringComparison.Ordinal);
        Assert.Equal(0, ((SignAtCurrentTermsCommand)CommandCodec.Production.Decode("sponsor.sign/1", plain.Text, SponsorKit.Anna, 1, SponsorKit.Day(Opening))).AskMilli);

        var counter = new CounterSponsorOfferCommand
        {
            ManagerId = SponsorKit.Anna, IssuedOn = SponsorKit.Day(Opening), OrganizationId = "alfa", OfferId = "spo:1", Years = 2, Ambition = SponsorAmbition.Standard, AskMilli = 60,
        };
        var back = (CounterSponsorOfferCommand)CommandCodec.Production.Decode("sponsor.counter/1", CommandCodec.Production.Encode(counter).Text, SponsorKit.Anna, 1, SponsorKit.Day(Opening));
        Assert.Equal(60, back.AskMilli);
        Assert.DoesNotContain("ask", CommandCodec.Production.Encode(counter with { AskMilli = 0 }).Text, StringComparison.Ordinal);
    }
}
