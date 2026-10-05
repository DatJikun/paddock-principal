using Paddock.Application.Access;
using Paddock.Application.Supply;
using Paddock.Domain.Cars;
using Paddock.Domain.Contracts;
using Paddock.Domain.Finance;
using Paddock.Domain.Spy;
using Paddock.Domain.Supply;
using Paddock.Domain.Time;
using Paddock.Domain.World;
using AccessManagerId = Paddock.Application.Access.ManagerId;

namespace Paddock.Tests.Supply;

/// <summary>Proposals, the supplier's answer on later days, signing, the ledger, knowledge and determinism. Prices are ESTIMATES.</summary>
public class SupplyNegotiationTests
{
    private static readonly GameDate Opening = new(1955, 1, 1);

    [Fact]
    public void AProposalOpensTalksAndChangesNothingElse()
    {
        var kit = new SupplyKit(Opening);
        var cash = kit.Book.Finance.BalanceOf(SupplyKit.Alfa);

        var talks = kit.Propose(SupplyKit.Anna, SupplyKit.Alfa, SupplyKit.Acme, kit.Floor(SupplyItem.Engine, SupplyKind.Customer), Opening);

        Assert.Equal("sneg:1", talks.Id);
        Assert.Equal(NegotiationStatus.AwaitingResponse, talks.Status);
        Assert.Empty(kit.Book.Section.Deals);
        Assert.Equal(cash, kit.Book.Finance.BalanceOf(SupplyKit.Alfa));
        Assert.All(kit.World.Section<CarsSection>(CarsSection.SectionName)!.Cars, car => Assert.Null(car.EngineKey));
    }

    [Theory]
    [InlineData("notYours")]
    [InlineData("works")]
    [InlineData("self")]
    [InlineData("unknownSupplier")]
    [InlineData("notAnEngineSupplier")]
    [InlineData("lastYearTyres")]
    [InlineData("zeroPrice")]
    [InlineData("tooLong")]
    [InlineData("farFuture")]
    [InlineData("duplicateTalks")]
    public void BadProposalsAreRefusedWithAKeyAndChangeNothing(string scenario)
    {
        var kit = new SupplyKit(Opening);
        var price = kit.Floor(SupplyItem.Engine, SupplyKind.Customer);
        var good = kit.Proposal(SupplyKit.Anna, SupplyKit.Alfa, SupplyKit.Acme, price, Opening);
        if (scenario == "duplicateTalks")
        {
            kit.Propose(SupplyKit.Anna, SupplyKit.Alfa, SupplyKit.Acme, price, Opening);
        }

        var (command, key) = scenario switch
        {
            "notYours" => (good with { ManagerId = SupplyKit.Bram }, SupplyKeys.NotYourOrganization),
            "works" => (good with { Kind = SupplyKind.Works }, SupplyKeys.WorksNotOffered),
            "self" => (good with { SupplierId = SupplyKit.Alfa.Value }, SupplyKeys.UnknownSupplier),
            "unknownSupplier" => (good with { SupplierId = "supplier:nobody" }, SupplyKeys.UnknownSupplier),
            "notAnEngineSupplier" => (good with { SupplierId = SupplyKit.Beta.Value }, SupplyKeys.SupplierCannotSupply),
            "lastYearTyres" => (good with { Item = SupplyItem.Tyres, Kind = SupplyKind.LastYearEngine }, SupplyKeys.LastYearEnginesOnly),
            "zeroPrice" => (good with { AnnualPriceCents = 0 }, SupplyKeys.BadTerms),
            "tooLong" => (good with { Seasons = 6 }, SupplyKeys.BadTerms),
            "farFuture" => (good with { FirstSeason = 1958 }, SupplyKeys.BadSeason),
            _ => (good, SupplyKeys.TalksOpen),
        };
        var before = kit.World.StateHash();

        Assert.Equal(key, kit.Run(command));
        Assert.Equal(before, kit.World.StateHash());
    }

    [Fact]
    public void TheSupplierAnswersOnALaterDayAndAnOfferAtItsFloorSignsTheDealAndPointsTheCars()
    {
        var kit = new SupplyKit(Opening);
        var price = kit.Floor(SupplyItem.Engine, SupplyKind.Customer);
        kit.Propose(SupplyKit.Anna, SupplyKit.Alfa, SupplyKit.Acme, price, Opening);

        kit.Live(Opening, 1);
        Assert.Equal(NegotiationStatus.AwaitingResponse, kit.Book.Section.Negotiations[0].Status);
        Assert.NotNull(kit.Book.Section.Negotiations[0].RespondOn);

        kit.Live(Opening.AddDays(1), 8);

        var negotiation = kit.Book.Section.Negotiations[0];
        var deal = Assert.Single(kit.Book.Section.Deals);
        Assert.Equal(NegotiationStatus.Agreed, negotiation.Status);
        Assert.Equal(deal.Number, negotiation.SignedDeal);
        Assert.Equal((SupplyItem.Engine, SupplyKind.Customer, price), (deal.Item, deal.Kind, deal.AnnualPriceCents));
        Assert.All(kit.World.Section<CarsSection>(CarsSection.SectionName)!.Of(SupplyKit.Alfa), car => Assert.Equal(deal.Id, car.EngineKey));
        Assert.All(kit.World.Section<CarsSection>(CarsSection.SectionName)!.Of(SupplyKit.Beta), car => Assert.Null(car.EngineKey));
        Assert.Contains(SupplyKeys.InboxSignedSubject, kit.InboxSubjects(SupplyKit.Anna));
        Assert.Empty(kit.InboxSubjects(SupplyKit.Bram));
    }

    [Fact]
    public void TheFeeGoesToTheLedgerOncePerSeasonAndTheSupplierBooksTheSale()
    {
        var kit = new SupplyKit(Opening);
        var price = kit.Floor(SupplyItem.Engine, SupplyKind.Customer, seasons: 2);
        kit.Propose(SupplyKit.Anna, SupplyKit.Alfa, SupplyKit.Acme, price, Opening, seasons: 2);

        kit.Live(Opening, 300);

        var fees = kit.SupplyEntries(SupplyKit.Alfa);
        Assert.Single(fees);
        Assert.Equal(-price, fees[0].AmountCents);
        Assert.Equal(SupplyReasons.Fee, fees[0].ReasonKey);
        Assert.Equal(SupplyKit.Acme.Value, fees[0].Counterparty);
        var sale = Assert.Single(kit.SupplyEntries(SupplyKit.Acme));
        Assert.Equal(price, sale.AmountCents);
        Assert.Equal(SupplyReasons.Sale, sale.ReasonKey);

        kit.Live(Opening.AddDays(300), 800);

        Assert.Equal(2, kit.SupplyEntries(SupplyKit.Alfa).Count);
        var deal = Assert.Single(kit.Book.Section.Deals);
        Assert.Equal(SupplyDealStatus.Ended, deal.Status);
        Assert.All(kit.World.Section<CarsSection>(CarsSection.SectionName)!.Of(SupplyKit.Alfa), car => Assert.Null(car.EngineKey));
        Assert.Contains(SupplyKeys.InboxEndedSubject, kit.InboxSubjects(SupplyKit.Anna));
    }

    [Fact]
    public void ASupplierWithoutBooksStillSellsAndOnlyTheCustomerPays()
    {
        var kit = new SupplyKit(Opening, supplierBooks: false);
        var price = kit.Floor(SupplyItem.Engine, SupplyKind.Customer);
        kit.Propose(SupplyKit.Anna, SupplyKit.Alfa, SupplyKit.Acme, price, Opening);

        kit.Live(Opening, 10);

        Assert.Single(kit.SupplyEntries(SupplyKit.Alfa));
        Assert.Empty(kit.SupplyEntries(SupplyKit.Acme));
    }

    [Fact]
    public void ALowOfferIsCounteredAndAcceptingTheCounterSignsAtTheCounterPrice()
    {
        var kit = new SupplyKit(Opening);
        var floor = kit.Floor(SupplyItem.Engine, SupplyKind.Customer);
        kit.Propose(SupplyKit.Anna, SupplyKit.Alfa, SupplyKit.Acme, floor / 2, Opening);
        kit.Live(Opening, 8);

        var countered = kit.Book.Section.Negotiations[0];
        Assert.Equal(NegotiationStatus.Countered, countered.Status);
        Assert.Equal(floor, countered.Counter!.AnnualPriceCents);
        Assert.Contains(SupplyReasons.PriceTooLow, countered.Reasons);
        Assert.Contains(SupplyKeys.InboxCounteredSubject, kit.InboxSubjects(SupplyKit.Anna));
        Assert.Empty(kit.Book.Section.Deals);

        var accept = new RespondToSupplyOfferCommand { ManagerId = SupplyKit.Anna, IssuedOn = SupplyKit.Day(Opening.AddDays(9)), OrganizationId = SupplyKit.Alfa.Value, NegotiationId = countered.Id, Accept = true };
        Assert.Null(kit.Run(accept));

        var deal = Assert.Single(kit.Book.Section.Deals);
        Assert.Equal(floor, deal.AnnualPriceCents);
        Assert.Equal(NegotiationStatus.Agreed, kit.Book.Section.Negotiations[0].Status);
        Assert.All(kit.World.Section<CarsSection>(CarsSection.SectionName)!.Of(SupplyKit.Alfa), car => Assert.Equal(deal.Id, car.EngineKey));
    }

    [Fact]
    public void ARepeatedNudgeCostsInterestAndRaisesTheNextCounterAndWalkingAwayEndsTheTalks()
    {
        var kit = new SupplyKit(Opening);
        var floor = kit.Floor(SupplyItem.Engine, SupplyKind.Customer);
        var low = floor / 2;
        var talks = kit.Propose(SupplyKit.Anna, SupplyKit.Alfa, SupplyKit.Acme, low, Opening);
        kit.Live(Opening, 8);
        var first = kit.Book.Section.Negotiations[0];

        Assert.Null(kit.Run(kit.Proposal(SupplyKit.Anna, SupplyKit.Alfa, SupplyKit.Acme, low + 1, Opening.AddDays(9), negotiation: talks.Id)));
        kit.Live(Opening.AddDays(9), 8);
        var second = kit.Book.Section.Negotiations[0];

        Assert.Equal(first.Interest - NegotiationEstimates.NudgePenalty, second.Interest);
        Assert.Equal(NegotiationStatus.Countered, second.Status);
        Assert.Contains(NegotiationReasons.NoRealChange, second.Reasons);
        Assert.True(second.Counter!.AnnualPriceCents > first.Counter!.AnnualPriceCents);
        Assert.Equal(first.RoundsUsed + 1, second.RoundsUsed);

        var walk = new RespondToSupplyOfferCommand { ManagerId = SupplyKit.Anna, IssuedOn = SupplyKit.Day(Opening.AddDays(20)), OrganizationId = SupplyKit.Alfa.Value, NegotiationId = talks.Id, Accept = false };
        Assert.Null(kit.Run(walk));
        Assert.Equal(NegotiationStatus.WalkedAway, kit.Book.Section.Negotiations[0].Status);
        Assert.Empty(kit.Book.Section.Deals);
        Assert.Equal(SupplyKeys.NegotiationClosed, kit.Run(walk));
    }

    [Fact]
    public void TheCounterCanOnlyBeAnsweredByItsOwnerAndOnlyWhenThereIsOne()
    {
        var kit = new SupplyKit(Opening);
        var talks = kit.Propose(SupplyKit.Anna, SupplyKit.Alfa, SupplyKit.Acme, kit.Floor(SupplyItem.Engine, SupplyKind.Customer), Opening);
        RespondToSupplyOfferCommand Answer(Paddock.Application.Managers.ManagerId manager, string organization, string id) =>
            new() { ManagerId = manager, IssuedOn = SupplyKit.Day(Opening), OrganizationId = organization, NegotiationId = id, Accept = true };

        Assert.Equal(SupplyKeys.NotYourOrganization, kit.Run(Answer(SupplyKit.Bram, SupplyKit.Alfa.Value, talks.Id)));
        Assert.Equal(SupplyKeys.UnknownNegotiation, kit.Run(Answer(SupplyKit.Bram, SupplyKit.Beta.Value, talks.Id)));
        Assert.Equal(SupplyKeys.NotCountered, kit.Run(Answer(SupplyKit.Anna, SupplyKit.Alfa.Value, talks.Id)));
    }

    [Fact]
    public void ExclusivityIsRefusedWhenOthersAreServedAndWhenSomeoneElseHoldsIt()
    {
        var kit = new SupplyKit(Opening);
        kit.Propose(SupplyKit.Bram, SupplyKit.Beta, SupplyKit.Acme, kit.Floor(SupplyItem.Engine, SupplyKind.Customer), Opening);
        kit.Live(Opening, 8);
        Assert.Single(kit.Book.Section.Deals);

        kit.Propose(SupplyKit.Anna, SupplyKit.Alfa, SupplyKit.Acme, kit.Floor(SupplyItem.Engine, SupplyKind.Customer, exclusive: true) * 2, Opening.AddDays(9), exclusive: true);
        kit.Live(Opening.AddDays(9), 8);

        var refused = kit.Book.Section.Negotiations.Single(negotiation => negotiation.Customer == SupplyKit.Alfa);
        Assert.Equal(NegotiationStatus.Refused, refused.Status);
        Assert.Contains(SupplyReasons.ExclusiveUnavailable, refused.Reasons);

        var held = new SupplyKit(Opening);
        held.Propose(SupplyKit.Bram, SupplyKit.Beta, SupplyKit.Bolt, held.Floor(SupplyItem.Engine, SupplyKind.Customer, exclusive: true), Opening, exclusive: true);
        held.Live(Opening, 8);
        Assert.True(Assert.Single(held.Book.Section.Deals).Terms.Exclusive);
        held.Propose(SupplyKit.Anna, SupplyKit.Alfa, SupplyKit.Bolt, held.Floor(SupplyItem.Engine, SupplyKind.Customer) * 3, Opening.AddDays(9));
        held.Live(Opening.AddDays(9), 8);
        Assert.Contains(SupplyReasons.ExclusiveTaken, held.Book.Section.Negotiations.Single(negotiation => negotiation.Customer == SupplyKit.Alfa).Reasons);
    }

    [Fact]
    public void TalksThatNobodyAnswersLapseAtTheDeadline()
    {
        var kit = new SupplyKit(Opening);
        var talks = kit.Propose(SupplyKit.Anna, SupplyKit.Alfa, SupplyKit.Acme, kit.Floor(SupplyItem.Engine, SupplyKind.Customer) / 2, Opening);
        kit.Live(Opening, 8);
        Assert.Equal(NegotiationStatus.Countered, kit.Book.Section.Negotiations[0].Status);

        kit.Live(Opening.AddDays(8), 40);

        var closed = kit.Book.Section.FindNegotiation(talks.Id)!;
        Assert.Equal(NegotiationStatus.Lapsed, closed.Status);
        Assert.Contains(NegotiationReasons.DeadlinePassed, closed.Reasons);
        Assert.Contains(SupplyKeys.InboxLapsedSubject, kit.InboxSubjects(SupplyKit.Anna));
    }

    [Fact]
    public void ANewDealCannotOverlapAnExistingOneButCanFollowIt()
    {
        var kit = new SupplyKit(Opening);
        kit.Propose(SupplyKit.Anna, SupplyKit.Alfa, SupplyKit.Acme, kit.Floor(SupplyItem.Engine, SupplyKind.Customer), Opening);
        kit.Live(Opening, 8);

        var overlapping = kit.Proposal(SupplyKit.Anna, SupplyKit.Alfa, SupplyKit.Bolt, kit.Floor(SupplyItem.Engine, SupplyKind.Customer), Opening.AddDays(9));
        var next = overlapping with { FirstSeason = 1956 };

        Assert.Equal(SupplyKeys.AlreadySupplied, kit.Run(overlapping));
        Assert.Null(kit.Run(next));
        kit.Live(Opening.AddDays(9), 8);
        Assert.Equal(2, kit.Book.Section.Deals.Count);
        Assert.Single(kit.World.Section<CarsSection>(CarsSection.SectionName)!.Of(SupplyKit.Alfa).Select(car => car.EngineKey).Distinct());
    }

    [Fact]
    public void ATeamWithAnEngineProgrammeCanSupplyAndATeamWithoutOneCannot()
    {
        var without = new SupplyKit(Opening);
        var price = without.Floor(SupplyItem.Engine, SupplyKind.Customer);
        Assert.Equal(SupplyKeys.SupplierCannotSupply, without.Run(without.Proposal(SupplyKit.Bram, SupplyKit.Beta, SupplyKit.Alfa, price, Opening)));

        var with = new SupplyKit(Opening, programmes: new FixedProgrammes(SupplyKit.Alfa));
        Assert.Null(with.Run(with.Proposal(SupplyKit.Bram, SupplyKit.Beta, SupplyKit.Alfa, price, Opening)));
        with.Live(Opening, 8);
        Assert.Equal(SupplyKit.Alfa, Assert.Single(with.Book.Section.Deals).Supplier);
    }

    [Fact]
    public void EverySupplierAnswerLeavesATraceThatOnlyStatesTheReasonsToThePlayer()
    {
        var sink = new MemorySink();
        var kit = new SupplyKit(Opening, trace: sink);
        kit.Propose(SupplyKit.Anna, SupplyKit.Alfa, SupplyKit.Acme, kit.Floor(SupplyItem.Engine, SupplyKind.Customer) / 2, Opening);

        kit.Live(Opening, 8);

        var trace = Assert.Single(sink.Traces);
        Assert.Equal(SupplyKit.Acme.Value, trace.Who);
        Assert.Equal("counter", trace.ChosenOptionId);
        Assert.Equal(SupplyReasons.PriceTooLow, trace.PlayerReason);
        Assert.True(trace.TruthContext.ContainsKey("floorCents"));
    }

    [Fact]
    public void ASupplierSeesNoOtherTeamsDealsAndAManagerSeesOnlyBandsOfItsOwnEngine()
    {
        var kit = new SupplyKit(Opening);
        kit.Propose(SupplyKit.Anna, SupplyKit.Alfa, SupplyKit.Acme, kit.Floor(SupplyItem.Engine, SupplyKind.Customer), Opening);
        kit.Propose(SupplyKit.Bram, SupplyKit.Beta, SupplyKit.Bolt, kit.Floor(SupplyItem.Engine, SupplyKind.Customer), Opening);
        kit.Live(Opening, 8);
        var query = new SupplyQuery(kit.Book, kit.Environment);

        var anna = query.View(AccessContext.ForManager(new AccessManagerId("human:anna")));

        var deal = Assert.Single(anna.Deals);
        Assert.Equal(SupplyKit.Acme.Value, deal.SupplierId);
        Assert.Single(anna.Talks);
        var engine = Assert.IsType<OwnEngineView>(deal.Engine);
        var truth = kit.Environment.Profiles.EngineOf(SupplyKit.Acme, engine.VersionSeason);
        Assert.InRange(truth.Power, engine.Power.Low, engine.Power.High);
        Assert.True(engine.Power.High > engine.Power.Low);
        Assert.Equal(1, engine.LagSeasons);
        Assert.Throws<InvalidOperationException>(() => query.View(AccessContext.Developer));
    }

    [Fact]
    public void TheSameSeedAndCommandsGiveTheSameHashAndANewSeedKeepsTheRulesButMayMoveTheAnswerDay()
    {
        string Run(ulong seed)
        {
            var kit = new SupplyKit(Opening, seed);
            kit.Propose(SupplyKit.Anna, SupplyKit.Alfa, SupplyKit.Acme, kit.Floor(SupplyItem.Engine, SupplyKind.Customer) / 2, Opening);
            kit.Propose(SupplyKit.Bram, SupplyKit.Beta, SupplyKit.Bolt, kit.Floor(SupplyItem.Engine, SupplyKind.Customer), Opening);
            kit.Live(Opening, 60, includeFinance: true);
            return kit.World.StateHash();
        }

        Assert.Equal(Run(7), Run(7));
    }

    [Fact]
    public void AnUnrelatedNegotiationNeverMovesAnotherOnesAnswerDay()
    {
        GameDate? AnswerDay(bool betaToo, ulong seed)
        {
            var kit = new SupplyKit(Opening, seed);
            kit.Propose(SupplyKit.Anna, SupplyKit.Alfa, SupplyKit.Acme, kit.Floor(SupplyItem.Engine, SupplyKind.Customer), Opening);
            if (betaToo)
            {
                kit.Propose(SupplyKit.Bram, SupplyKit.Beta, SupplyKit.Bolt, kit.Floor(SupplyItem.Engine, SupplyKind.Customer), Opening);
            }

            kit.Live(Opening, 1);
            return kit.Book.Section.FindNegotiation("sneg:1")!.RespondOn;
        }

        for (ulong seed = 1; seed <= 12; seed++)
        {
            Assert.Equal(AnswerDay(false, seed), AnswerDay(true, seed));
        }

        Assert.True(Enumerable.Range(1, 12).Select(seed => AnswerDay(false, (ulong)seed)).Distinct().Count() > 1);
    }

    [Fact]
    public void TheEngineDealFeedsTheCarVectorThroughTheKeyTheCommandSet()
    {
        var kit = new SupplyKit(Opening);
        kit.Propose(SupplyKit.Anna, SupplyKit.Alfa, SupplyKit.Acme, kit.Floor(SupplyItem.Engine, SupplyKind.Customer), Opening);
        kit.Live(Opening, 8);
        var car = kit.World.Section<CarsSection>(CarsSection.SectionName)!.Of(SupplyKit.Alfa)[0];
        var supply = kit.Book.Section;

        var contribution = Paddock.Simulation.Supply.SupplyPerformance.EngineFor(car, supply, kit.Environment.Profiles, Opening);

        Assert.NotEqual(EngineContribution.None, contribution);
        Assert.Equal(
            SupplyContribution.Of(Assert.Single(supply.Deals), kit.Environment.Profiles, 1955),
            contribution);
    }

    [Fact]
    public void AcceptingACounterIsRefusedWhenTheSupplierBecameExclusive()
    {
        var kit = new SupplyKit(Opening);
        var floor = kit.Floor(SupplyItem.Engine, SupplyKind.Customer);
        kit.Propose(SupplyKit.Anna, SupplyKit.Alfa, SupplyKit.Acme, floor / 2, Opening);
        kit.Live(Opening, 8);
        var countered = kit.Book.Section.Negotiations[0];
        Assert.Equal(NegotiationStatus.Countered, countered.Status);

        var (withDeal, _) = kit.Book.Section.AddDeal(new SupplyDeal(
            kit.Book.Section.NextDeal,
            SupplyItem.Engine,
            SupplyKind.Customer,
            SupplyKit.Acme,
            SupplyKit.Beta,
            1955,
            new SupplyTerms(floor, 1, exclusive: true),
            Opening,
            SupplyDealStatus.Active,
            null,
            0,
            null));
        kit.Book.Write(withDeal);

        var accept = new RespondToSupplyOfferCommand
        {
            ManagerId = SupplyKit.Anna,
            IssuedOn = SupplyKit.Day(Opening.AddDays(9)),
            OrganizationId = SupplyKit.Alfa.Value,
            NegotiationId = countered.Id,
            Accept = true,
        };
        Assert.Equal(SupplyReasons.ExclusiveTaken, kit.Run(accept));
        Assert.Single(kit.Book.Section.Deals);
        Assert.Equal(NegotiationStatus.Countered, kit.Book.Section.FindNegotiation(countered.Id)!.Status);
    }

    [Fact]
    public void ADealForASeasonAlreadyOverIsNotSigned()
    {
        var offered = new GameDate(1955, 12, 31);
        var kit = new SupplyKit(offered);
        var price = kit.Floor(SupplyItem.Engine, SupplyKind.Customer);
        kit.Propose(SupplyKit.Anna, SupplyKit.Alfa, SupplyKit.Acme, price, offered, seasons: 1);

        kit.Live(offered, 8);

        Assert.Empty(kit.Book.Section.Deals);
        var talks = Assert.Single(kit.Book.Section.Negotiations);
        Assert.Equal(NegotiationStatus.Refused, talks.Status);
        Assert.Contains(SupplyKeys.BadSeason, talks.Reasons);
        Assert.Contains(SupplyKeys.InboxRefusedSubject, kit.InboxSubjects(SupplyKit.Anna));
        Assert.DoesNotContain(SupplyKeys.InboxSignedSubject, kit.InboxSubjects(SupplyKit.Anna));
    }

    private sealed class FixedProgrammes : IEngineProgrammes
    {
        private readonly OrganizationId _organization;

        public FixedProgrammes(OrganizationId organization) => _organization = organization;

        public bool Sells(OrganizationId organization, int season) => organization == _organization;

        public bool Voids(OrganizationId organization, int season) => false;
    }
}
