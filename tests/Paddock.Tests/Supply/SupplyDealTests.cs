using Paddock.Application.Commands;
using Paddock.Application.Supply;
using Paddock.Domain.Cars;
using Paddock.Domain.Contracts;
using Paddock.Domain.Finance;
using Paddock.Domain.Supply;
using Paddock.Domain.Time;
using Paddock.Domain.World;
using Paddock.Simulation.Cars;
using Paddock.Simulation.Racing.Pace;
using Paddock.Simulation.Supply;

namespace Paddock.Tests.Supply;

/// <summary>Deal kinds, prices, the authored start data and what a deal does to the car vector. Every price and rating is an ESTIMATE.</summary>
public class SupplyDealTests
{
    private static readonly GameDate Opening = new(1955, 1, 1);

    [Fact]
    public void WorksGetsANewVersionFirstAndALastYearEngineIsCheaperAndSlower()
    {
        var profiles = new EstimateSupplierProfiles(1955);
        SupplyDeal Deal(SupplyKind kind) => new(
            1,
            SupplyItem.Engine,
            kind,
            SupplyKit.Acme,
            kind == SupplyKind.Works ? SupplyKit.Alfa : SupplyKit.Beta,
            1957,
            new SupplyTerms(1, 1, false),
            Opening,
            SupplyDealStatus.Active,
            null,
            0,
            null);

        var works = SupplyContribution.Of(Deal(SupplyKind.Works), profiles, 1957);
        var customer = SupplyContribution.Of(Deal(SupplyKind.Customer), profiles, 1957);
        var partner = SupplyContribution.Of(Deal(SupplyKind.Partner), profiles, 1957);
        var lastYear = SupplyContribution.Of(Deal(SupplyKind.LastYearEngine), profiles, 1957);

        Assert.True(SupplyEstimates.LagSeasons(SupplyKind.Works) < SupplyEstimates.LagSeasons(SupplyKind.Customer));
        Assert.True(works.PowerOffset > customer.PowerOffset);
        Assert.True(customer.PowerOffset > lastYear.PowerOffset);
        Assert.Equal(customer.PowerOffset, partner.PowerOffset);
        Assert.True(partner.ReliabilityOffset > customer.ReliabilityOffset);

        var budget = 100_000_000L;
        var shape = new SupplyTerms(1, 1, false);
        long Floor(SupplyKind kind) => SupplyPricing.FloorCents(SupplyItem.Engine, kind, shape, budget);
        Assert.Equal(0, Floor(SupplyKind.Works));
        Assert.True(Floor(SupplyKind.LastYearEngine) < Floor(SupplyKind.Customer));
        Assert.True(Floor(SupplyKind.Customer) < Floor(SupplyKind.Partner));
    }

    [Fact]
    public void LongerDealsAreCheaperPerYearAndExclusivityCostsMore()
    {
        long Floor(int seasons, bool exclusive) =>
            SupplyPricing.FloorCents(SupplyItem.Engine, SupplyKind.Customer, new SupplyTerms(1, seasons, exclusive), 100_000_000L);

        Assert.True(Floor(3, false) < Floor(1, false));
        Assert.True(Floor(1, true) > Floor(1, false));
        Assert.Equal(Floor(5, false), Floor(5, false));
    }

    [Fact]
    public void EveryAuthoredSupplyTypeMapsAndTheDefaultMappingIsTheDocumentedOne()
    {
        var kit = new SupplyKit(Opening);
        var file = Path.Combine(RepoPaths.Root(), "data", "authored", "teams", "engines.json");
        using var document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(file));
        var ids = document.RootElement.GetProperty("supply_types").EnumerateArray().Select(row => row.GetProperty("id").GetString()!).ToArray();

        foreach (var id in ids)
        {
            _ = SupplyKindMap.FromAuthored(id);
        }

        Assert.Equal(SupplyKind.Works, SupplyKindMap.FromAuthored("works"));
        Assert.Equal(SupplyKind.Partner, SupplyKindMap.FromAuthored("partner"));
        Assert.Equal(SupplyKind.Customer, SupplyKindMap.FromAuthored("customer"));
        Assert.Equal(SupplyKind.Customer, SupplyKindMap.FromAuthored("badged"));
        Assert.Equal(SupplyKind.Customer, SupplyKindMap.FromAuthored("unknown"));
        Assert.Throws<ArgumentException>(() => SupplyKindMap.FromAuthored("lease"));
        Assert.All(kit.Data.EngineSupplies, supply => SupplyKindMap.FromAuthored(supply.SupplyType));
    }

    [Fact]
    public void The1955StartGetsTheEngineDealsOfTheAuthoredDataOneForEveryTeam()
    {
        var kit = new SupplyKit(Opening);
        var rows = kit.Data.EngineSupplies.Where(supply => supply.Season == 1955).ToArray();
        Assert.NotEmpty(rows);

        var world = WorldState.At(Opening);
        var links = new List<SupplyLink>();
        foreach (var row in rows)
        {
            var team = OrganizationId.Real(row.ConstructorId);
            var supplier = OrganizationId.Real("supplier:" + row.Supplier.ToLowerInvariant().Replace(' ', '-'));
            foreach (var (id, kind, name) in new[] { (team, OrganizationKind.Team, row.ConstructorId), (supplier, OrganizationKind.EngineSupplier, row.Supplier) })
            {
                if (world.Organizations.All(existing => existing.Id != id))
                {
                    (world, _) = world.AddOrganization(new OrganizationSpec(kind, true, id.Value, Opening, null, 0, [new OrganizationNameSpan(name, Opening, null)]));
                }
            }

            links.Add(new SupplyLink(team, supplier, row.EngineName, row.SupplyType));
        }

        var installed = InitialSupplyFactory.Install(world, links, 1955, kit.Budget);
        var section = installed.Section<SupplySection>(SupplySection.SectionName)!;

        var teams = rows.Select(row => row.ConstructorId).Distinct().ToArray();
        Assert.Equal(teams.Length, section.Deals.Count);
        Assert.All(section.Deals, deal => Assert.Equal(SupplyItem.Engine, deal.Item));
        foreach (var team in teams)
        {
            var first = rows.First(row => row.ConstructorId == team);
            var deal = Assert.Single(section.DealsOf(OrganizationId.Real(team)));
            Assert.Equal(SupplyKindMap.FromAuthored(first.SupplyType), deal.Kind);
            Assert.Equal(first.EngineName, deal.EngineName);
            Assert.Equal(1955, deal.FirstSeason);
            Assert.True(deal.IsInForceOn(Opening));
        }

        var ferrari = Assert.Single(section.DealsOf(OrganizationId.Real("ferrari")));
        Assert.Equal(SupplyKind.Works, ferrari.Kind);
        Assert.Equal("Ferrari 555", ferrari.EngineName);
        Assert.Equal(SupplyKind.Customer, Assert.Single(section.DealsOf(OrganizationId.Real("cooper"))).Kind);
    }

    [Fact]
    public void InstallingPointsTheCarsAtTheirEngineDealAndSkipsRowsOfOrganizationsThatAreNotTeams()
    {
        var kit = new SupplyKit(Opening);
        var links = new[]
        {
            new SupplyLink(SupplyKit.Alfa, SupplyKit.Acme, "Acme V8", "customer"),
            new SupplyLink(SupplyKit.Beta, SupplyKit.Bolt, "Bolt 6", "works"),
            new SupplyLink(SupplyKit.Acme, SupplyKit.Bolt, "Not a team", "customer"),
        };

        var world = InitialSupplyFactory.Install(kit.World, links, 1955, kit.Budget);
        var section = world.Section<SupplySection>(SupplySection.SectionName)!;
        var cars = world.Section<CarsSection>(CarsSection.SectionName)!;

        Assert.Equal(2, section.Deals.Count);
        var alfaDeal = Assert.Single(section.DealsOf(SupplyKit.Alfa));
        Assert.All(cars.Of(SupplyKit.Alfa), car => Assert.Equal(alfaDeal.Id, car.EngineKey));
        Assert.All(cars.Of(SupplyKit.Beta), car => Assert.Equal(Assert.Single(section.DealsOf(SupplyKit.Beta)).Id, car.EngineKey));
    }

    [Fact]
    public void AnEngineDealChangesTheCarVectorAndNoDealLeavesItExactlyAsItWas()
    {
        var kit = new SupplyKit(Opening);
        var car = kit.World.Section<CarsSection>(CarsSection.SectionName)!.Of(SupplyKit.Alfa)[0];
        var limits = EraPerformanceLimits.EstimateFor(1955);
        var profiles = kit.Environment.Profiles;
        var empty = SupplySection.Empty;

        Assert.Equal(CarPerformanceFor.Resolve(car, limits), SupplyPerformance.Resolve(car, limits, empty, profiles, Opening));
        Assert.Equal(CarPerformanceFor.Resolve(car, limits), CarPerformanceFor.Resolve(car, limits, EngineContribution.None));

        var installed = InitialSupplyFactory.Install(kit.World, [new SupplyLink(SupplyKit.Alfa, SupplyKit.Acme, "Acme V8", "works")], 1955, kit.Budget);
        var supply = installed.Section<SupplySection>(SupplySection.SectionName)!;
        var withEngine = installed.Section<CarsSection>(CarsSection.SectionName)!.Of(SupplyKit.Alfa)[0];
        var baseline = CarPerformanceFor.Resolve(withEngine, limits);
        var resolved = SupplyPerformance.Resolve(withEngine, limits, supply, profiles, Opening);

        Assert.NotEqual(baseline.Power, resolved.Power);
        Assert.Equal(baseline.Downforce, resolved.Downforce);
        Assert.InRange(resolved.Power, 0, 100);
        Assert.InRange(resolved.Reliability, 0, 100);
        Assert.Equal(CarPerformanceFor.Resolve(withEngine, limits), SupplyPerformance.Resolve(withEngine, limits, supply, profiles, new GameDate(1961, 1, 1)));
    }

    [Fact]
    public void TheSupplierProfileIsTheSameEveryTimeAndProgressesEachSeason()
    {
        var profiles = new EstimateSupplierProfiles(1955);

        Assert.Equal(profiles.EngineOf(SupplyKit.Acme, 1955), new EstimateSupplierProfiles(1955).EngineOf(SupplyKit.Acme, 1955));
        Assert.True(profiles.EngineOf(SupplyKit.Acme, 1957).Power > profiles.EngineOf(SupplyKit.Acme, 1955).Power);
        Assert.NotEqual(profiles.EngineOf(SupplyKit.Acme, 1955), profiles.EngineOf(SupplyKit.Bolt, 1955));
    }

    [Fact]
    public void ATyreDealGivesTheT30ProfileAndOnlyAPartnerDealIsTuned()
    {
        var supply = SupplySection.Empty;
        (supply, _) = supply.AddDeal(new SupplyDeal(1, SupplyItem.Tyres, SupplyKind.Partner, SupplyKit.Acme, SupplyKit.Alfa, 1955, new SupplyTerms(5, 1, false), Opening, SupplyDealStatus.Active, null, 0, null));
        (supply, _) = supply.AddDeal(new SupplyDeal(2, SupplyItem.Tyres, SupplyKind.Customer, SupplyKit.Acme, SupplyKit.Beta, 1955, new SupplyTerms(5, 1, false), Opening, SupplyDealStatus.Active, null, 0, null));

        var partner = SupplyPerformance.TyresFor(supply, SupplyKit.Alfa, Opening);
        var customer = SupplyPerformance.TyresFor(supply, SupplyKit.Beta, Opening);
        var none = SupplyPerformance.TyresFor(supply, OrganizationId.Real("gamma"), Opening);

        Assert.True(partner.PartnerTuned);
        Assert.False(customer.PartnerTuned);
        Assert.Equal(partner.Profile.GripBalance, customer.Profile.GripBalance);
        Assert.Same(Paddock.Simulation.Racing.Tyres.TyreSupplierProfile.Neutral, none.Profile);
    }

    [Fact]
    public void SupplyCommandsAreRegisteredInTheSaveCodecAndRoundTrip()
    {
        ICommand[] commands =
        [
            new ProposeSupplyDealCommand
            {
                ManagerId = SupplyKit.Anna, IssuedOn = new DateOnly(1955, 3, 1), OrganizationId = "alfa", SupplierId = "supplier:acme", Item = SupplyItem.Engine,
                Kind = SupplyKind.Partner, FirstSeason = 1955, AnnualPriceCents = 123_456_789, Seasons = 3, Exclusive = true, NegotiationId = "sneg:4", SubmissionNumber = 1,
            },
            new ProposeSupplyDealCommand
            {
                ManagerId = SupplyKit.Anna, IssuedOn = new DateOnly(1955, 3, 1), OrganizationId = "alfa", SupplierId = "supplier:acme", Item = SupplyItem.Fuel,
                Kind = SupplyKind.Customer, FirstSeason = 1956, AnnualPriceCents = 5, Seasons = 1, SubmissionNumber = 2,
            },
            new RespondToSupplyOfferCommand { ManagerId = SupplyKit.Bram, IssuedOn = new DateOnly(1955, 3, 2), OrganizationId = "beta", NegotiationId = "sneg:1", Accept = true, SubmissionNumber = 3 },
            new RespondToSupplyOfferCommand { ManagerId = SupplyKit.Bram, IssuedOn = new DateOnly(1955, 3, 2), OrganizationId = "beta", NegotiationId = "sneg:1", Accept = false, SubmissionNumber = 4 },
        ];

        foreach (var command in commands)
        {
            var encoded = CommandCodec.Production.Encode(command);
            var decoded = CommandCodec.Production.Decode(encoded.Tag, encoded.Text, command.ManagerId, command.SubmissionNumber, command.IssuedOn);
            Assert.Equal(command, decoded);
        }
    }

    [Fact]
    public void ASupplierThatIsFullOrExclusivelyBoundRefusesWithItsReason()
    {
        var offer = new SupplyTerms(1_000_000, 1, false);
        var negotiation = SupplyNegotiation.Start(1, "human:anna", SupplyKit.Alfa, SupplyKit.Acme, SupplyItem.Engine, SupplyKind.Customer, 1955, Opening, offer);

        var full = SupplierResponder.Respond(negotiation, new SupplierSituation(100_000_000, SupplyEstimates.MaxCustomersPerSupplier, false));
        var bound = SupplierResponder.Respond(negotiation, new SupplierSituation(100_000_000, 1, true));

        Assert.Equal(SupplyAnswerKind.Refuse, full.Kind);
        Assert.Contains(SupplyReasons.NoCapacity, full.Reasons);
        Assert.Equal(SupplyAnswerKind.Refuse, bound.Kind);
        Assert.Contains(SupplyReasons.ExclusiveTaken, bound.Reasons);
    }

    [Fact]
    public void ThePriceTheSupplierAcceptsRisesAsInterestFalls()
    {
        var offer = new SupplyTerms(1, 1, false);
        var fresh = SupplyNegotiation.Start(1, "human:anna", SupplyKit.Alfa, SupplyKit.Acme, SupplyItem.Engine, SupplyKind.Customer, 1955, Opening, offer);
        var tired = fresh with { Interest = NegotiationEstimates.InterestStart - NegotiationEstimates.NudgePenalty };
        var situation = new SupplierSituation(100_000_000, 0, false);

        var freshAnswer = SupplierResponder.Respond(fresh, situation);
        var tiredAnswer = SupplierResponder.Respond(tired, situation);

        Assert.Equal(freshAnswer.FloorCents, tiredAnswer.FloorCents);
        Assert.True(tiredAnswer.AcceptCents > freshAnswer.AcceptCents);
        Assert.Contains(NegotiationReasons.NoRealChange, tiredAnswer.Reasons);
    }
}
