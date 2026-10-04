using Paddock.Application.Sponsors;
using Paddock.Domain.Finance;
using Paddock.Domain.Objectives;
using Paddock.Domain.Sponsors;
using Paddock.Domain.Time;
using Paddock.Domain.World;
using Paddock.Simulation.Objectives;

namespace Paddock.Tests.Sponsors;

/// <summary>Sponsors over lived days: instalments, objective outcomes, the rival roll, renewals. Finance is the T37 ledger.</summary>
public class SponsorDayTests
{
    private static readonly GameDate Opening = new(1955, 1, 1);

    [Fact]
    public void TheAnnualAmountIsPaidInTwelveInstalmentsToTheLedgerAndAddsUpExactly()
    {
        var kit = new SponsorKit(Opening);
        var deal = kit.SignDeal("vestoil_works", 1, Opening);
        var cashBefore = kit.Cash(SponsorKit.Alfa);

        kit.Live(Opening, 365);

        var entries = kit.SponsorEntries(SponsorKit.Alfa);
        Assert.Equal(12, entries.Count);
        Assert.All(entries, entry => Assert.Equal("vestoil_works", entry.Counterparty));
        Assert.All(entries, entry => Assert.Equal(SponsorReason.Instalment, entry.ReasonKey));
        Assert.Equal(new GameDate(1955, 1, 15), entries[0].Date);
        Assert.Equal(new GameDate(1955, 12, 15), entries[^1].Date);
        Assert.Equal(deal.AnnualCents, entries.Sum(entry => entry.AmountCents));
        Assert.Equal(cashBefore + deal.AnnualCents, kit.Cash(SponsorKit.Alfa));
        Assert.Equal(12, kit.Book.Section.FindDeal(deal.Id)!.InstalmentsPaid);
    }

    [Fact]
    public void AFinishedDealCompletesAndTheSlotIsFreeAgain()
    {
        var kit = new SponsorKit(Opening);
        var deal = kit.SignDeal("rheinwerk_motoren", 1, Opening);

        kit.Live(Opening, 367);

        var done = kit.Book.Section.FindDeal(deal.Id)!;
        Assert.Equal(DealStatus.Completed, done.Status);
        Assert.Equal(new GameDate(1956, 1, 1), done.EndedOn);
        Assert.Null(kit.Begin(SponsorKit.Anna, SponsorKit.Alfa, "dunmore_tyres", 1, new GameDate(1956, 1, 2)));
    }

    [Fact]
    public void AMetConditionPaysTheBonusOnceRaisesTrustAndShowsInTheLedgerAndInbox()
    {
        var kit = new SponsorKit(Opening);
        var deal = kit.SignDeal("vestoil_works", 1, Opening);
        kit.Podiums = 1;

        var events = kit.Live(Opening, 366);

        Assert.Contains(events, item => item.TypeId == ObjectiveEventTypes.Met);
        var done = kit.Book.Section.FindDeal(deal.Id)!;
        var bonus = deal.AnnualCents * SponsorEstimates.BonusMilli / 1000;
        Assert.Equal(DealObjectiveOutcome.Met, done.Outcome);
        Assert.Equal(bonus, done.BonusCents);
        var bonuses = kit.SponsorEntries(SponsorKit.Alfa).Where(entry => entry.ReasonKey == SponsorReason.Bonus).ToArray();
        Assert.Equal(bonus, Assert.Single(bonuses).AmountCents);
        Assert.Equal(ObjectiveStatus.Met, kit.Book.Objectives.Find(deal.ObjectiveId!)!.Status);
        Assert.True(kit.Book.Section.TrustOf("vestoil_works", SponsorKit.Alfa) >= SponsorEstimates.StartTrust + SponsorEstimates.TrustOnMet);
        Assert.Contains(SponsorKeys.InboxMetSubject, kit.InboxSubjects(SponsorKit.Anna));

        // Applying the same events again pays nothing more.
        var cash = kit.Cash(SponsorKit.Alfa);
        SponsorOutcomes.Apply(kit.Book, kit.Environment, events, kit.Inbox, kit.Managers);
        Assert.Equal(cash, kit.Cash(SponsorKit.Alfa));
    }

    [Fact]
    public void AFailedConditionEndsTheDealOnTheDeadlineAndLowersTrust()
    {
        var quick = QuickCatalog();
        var kit = new SponsorKit(Opening, quick);
        var deal = kit.SignDeal("quick_oil", 1, Opening);
        kit.Podiums = 0;

        kit.Live(Opening, 120);

        var done = kit.Book.Section.FindDeal(deal.Id)!;
        Assert.Equal(DealStatus.Ended, done.Status);
        Assert.Equal(DealObjectiveOutcome.Failed, done.Outcome);
        Assert.Equal(Opening.AddDays(60), done.EndedOn);
        Assert.Equal(ObjectiveStatus.Failed, kit.Book.Objectives.Find(deal.ObjectiveId!)!.Status);
        Assert.Equal(SponsorEstimates.StartTrust - SponsorEstimates.TrustOnFailed, kit.Book.Section.TrustOf("quick_oil", SponsorKit.Alfa));
        Assert.Contains(SponsorKeys.InboxFailedSubject, kit.InboxSubjects(SponsorKit.Anna));

        // The sponsor left: instalments stopped and the deal pays nothing more.
        var paid = kit.SponsorEntries(SponsorKit.Alfa);
        Assert.True(paid.Count < SponsorEstimates.InstalmentsPerYear);
        Assert.All(paid, entry => Assert.True(entry.Date <= Opening.AddDays(60)));
        Assert.DoesNotContain(paid, entry => entry.ReasonKey == SponsorReason.Bonus);
    }

    [Fact]
    public void ARivalMayTakeTheSponsorWhileYouWaitAndOnlyTheMarketStreamIsUsed()
    {
        var lost = 0;
        var kept = 0;
        for (ulong seed = 1; seed <= 40; seed++)
        {
            var kit = new SponsorKit(Opening, seed: seed);
            var talk = kit.OpenTalk("vestoil_works", 1, Opening);

            kit.Live(Opening, 400);

            var after = kit.Book.Section.FindTalk(talk.Id)!;
            Assert.NotEqual(RivalState.Undecided, after.Rival);
            Assert.All(kit.LastState!.Value.RngStates.Keys, slot => Assert.Equal("Market", slot.Name));
            if (after.Status == TalkStatus.LostToRival)
            {
                lost++;
                Assert.True(kit.Book.Section.IsTaken("vestoil_works", after.ClosedOn!.Value));
                Assert.Contains(SponsorKeys.InboxLostSubject, kit.InboxSubjects(SponsorKit.Anna));
                Assert.Equal(SponsorKeys.SponsorUnavailable, kit.Begin(SponsorKit.Anna, SponsorKit.Alfa, "vestoil_works", 1, after.ClosedOn.Value));
            }
            else
            {
                kept++;
                Assert.Equal(TalkStatus.Open, after.Status);
            }
        }

        Assert.True(lost > 0, "no seed lost the sponsor, so the roll is never exercised");
        Assert.True(kept > 0, "every seed lost the sponsor, so the roll is not a roll");
    }

    [Fact]
    public void AnUnrelatedTalkElsewhereDoesNotChangeThisOrganizationsRivalRolls()
    {
        var checkedSomething = 0;
        for (ulong seed = 1; seed <= 25; seed++)
        {
            var alone = new SponsorKit(Opening, seed: seed);
            var talkAlone = alone.OpenTalk("vestoil_works", 1, Opening);
            alone.Live(Opening, 300);

            var crowded = new SponsorKit(Opening, seed: seed);
            crowded.OpenTalk("dunmore_tyres", 1, Opening, SponsorKit.Beta);
            crowded.SignDeal("corvane_fuels", 3, Opening);
            var talkCrowded = crowded.OpenTalk("vestoil_works", 1, Opening);
            crowded.Live(Opening, 300);

            var one = alone.Book.Section.FindTalk(talkAlone.Id)!;
            var other = crowded.Book.Section.FindTalk(talkCrowded.Id)!;
            Assert.Equal(one.Rival, other.Rival);
            Assert.Equal(one.Status, other.Status);
            Assert.Equal(one.ClosedOn, other.ClosedOn);
            checkedSomething += one.Rival == RivalState.Present ? 1 : 0;
        }

        Assert.True(checkedSomething > 0);
    }

    [Fact]
    public void TheSameSeedAndCommandsGiveTheSameStateHashTwice()
    {
        string Run()
        {
            var kit = new SponsorKit(Opening, seed: 7UL, skill: 20);
            kit.OpenTalk("vestoil_works", 1, Opening);
            kit.SignDeal("rheinwerk_motoren", 2, Opening);
            kit.OpenTalk("dunmore_tyres", 1, Opening, SponsorKit.Beta);
            kit.Podiums = 1;
            kit.Live(Opening, 400);
            return kit.World.StateHash();
        }

        Assert.Equal(Run(), Run());
    }

    [Fact]
    public void ASponsorWithEnoughTrustOffersARenewalAndTheOfferCanBeAcceptedOrDeclined()
    {
        var kit = new SponsorKit(Opening);
        var deal = kit.SignDeal("rheinwerk_motoren", 1, Opening);

        kit.Live(Opening, 310);

        var offer = Assert.Single(kit.Book.Section.OpenOffersOf(SponsorKit.Alfa));
        Assert.Equal(deal.Number, offer.DealNumber);
        Assert.Equal(offer.ValidUntil, deal.End);
        Assert.Contains(SponsorKeys.InboxOfferSubject, kit.InboxSubjects(SponsorKit.Anna));
        var wrong = kit.Run(new RespondToSponsorOfferCommand { ManagerId = SponsorKit.Bram, IssuedOn = SponsorKit.Day(new GameDate(1955, 11, 10)), OrganizationId = SponsorKit.Alfa.Value, OfferId = offer.Id, Accept = true });
        Assert.Equal(SponsorKeys.NotYourOrganization, wrong);

        Assert.Null(kit.Run(new RespondToSponsorOfferCommand { ManagerId = SponsorKit.Anna, IssuedOn = SponsorKit.Day(new GameDate(1955, 11, 10)), OrganizationId = SponsorKit.Alfa.Value, OfferId = offer.Id, Accept = true }));

        var renewal = kit.Book.Section.Deals.Last();
        Assert.Equal(OfferStatus.Accepted, kit.Book.Section.FindOffer(offer.Id)!.Status);
        Assert.Equal(deal.End.AddDays(1), renewal.Start);
        Assert.Equal(offer.AnnualCents, renewal.AnnualCents);
        Assert.Equal(SponsorKeys.OfferClosed, kit.Run(new RespondToSponsorOfferCommand { ManagerId = SponsorKit.Anna, IssuedOn = SponsorKit.Day(new GameDate(1955, 11, 11)), OrganizationId = SponsorKit.Alfa.Value, OfferId = offer.Id, Accept = false }));

        kit.Live(new GameDate(1955, 11, 6), 120);
        Assert.Equal(2, kit.SponsorEntries(SponsorKit.Alfa).Count(entry => entry.Date >= new GameDate(1956, 1, 1)));
    }

    [Fact]
    public void ADistrustfulSponsorMakesNoRenewalOffer()
    {
        var quick = QuickCatalog();
        var kit = new SponsorKit(Opening, quick);
        kit.SignDeal("quick_oil", 1, Opening);
        kit.Podiums = 0;

        kit.Live(Opening, 330);

        Assert.Empty(kit.Book.Section.Offers);
    }

    [Fact]
    public void AnOfferNobodyAnswersLapses()
    {
        var kit = new SponsorKit(Opening);
        kit.SignDeal("rheinwerk_motoren", 1, Opening);

        kit.Live(Opening, 370);

        var offer = Assert.Single(kit.Book.Section.Offers);
        Assert.Equal(OfferStatus.Lapsed, offer.Status);
    }

    private static SponsorCatalog QuickCatalog() => new(
    [
        new SponsorDefinition(
            "quick_oil",
            "Quick Oil",
            SponsorIndustries.Oil,
            "GBR",
            0.1,
            0.08,
            1950,
            null,
            [SlotKind.Technical],
            new SponsorObjectiveSpec(SponsorObjectiveSpec.PodiumsAtLeast, "1", 60)),
    ]);
}
