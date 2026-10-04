using Paddock.Domain.Cars;
using Paddock.Domain.Contracts;
using Paddock.Domain.Finance;
using Paddock.Domain.People;
using Paddock.Domain.Supply;
using Paddock.Domain.Time;
using Paddock.Domain.World;
using Paddock.Tests.Contracts;

namespace Paddock.Tests.Principals;

/// <summary>
/// The SYNTHETIC world of the AI principal tests: invented teams, people and numbers, nothing historical and nothing calibrated.
/// What a team believes about a person never depends on the true attributes: beliefs are built from the "believed" value, and the
/// true value is that value plus a hidden shift that stays inside the believed band. A test that changes the shift changes the truth and
/// leaves every belief alone. The shift moves drivers, free staff and principals. It does not move the engineers a team already employs:
/// the bands an engineering department reports about its own car are themselves made from its true skill (T41), so those bands are knowledge
/// that legitimately follows that truth.
/// </summary>
internal sealed record PrincipalWorldOptions(
    int Teams = 8,
    int HiddenShift = 0,
    int FreeDrivers = 14,
    int FreeStaff = 4,
    long OpeningCashPerTeamDollars = 60_000);

internal static class PrincipalWorld
{
    public static readonly GameDate Opening = GameDate.SeasonStart(1955);

    public static readonly string[] TeamNames = ["pt_alfa", "pt_beta", "pt_gamma", "pt_delta", "pt_eps", "pt_zeta", "pt_eta", "pt_theta", "pt_iota", "pt_kappa", "pt_lambda", "pt_mu"];

    public static readonly string[] SupplierNames = ["supplier:ps_acme", "supplier:ps_bolt", "supplier:ps_crux"];

    public static OrganizationId Team(int index) => OrganizationId.Real(TeamNames[index]);

    public static OrganizationId Supplier(int index) => OrganizationId.Real(SupplierNames[index]);

    /// <summary>The believed value of a person's attributes, from his index: 7 to 15, spread so no two neighbours match.</summary>
    public static int Believed(int person) => 7 + ((person * 5) % 9);

    public static WorldState Build(PrincipalWorldOptions options, EraFinanceFacts facts, out IReadOnlyList<PersonId> freeDrivers)
    {
        var world = WorldState.At(Opening);
        var budgetStep = 12_000;
        for (var i = 0; i < options.Teams; i++)
        {
            var key = TeamNames[i];
            (world, _) = world.AddOrganization(new OrganizationSpec(
                OrganizationKind.Team,
                true,
                key,
                new GameDate(1950, 1, 1),
                null,
                40_000 + (i * budgetStep),
                [new OrganizationNameSpan("Synthetic " + key, new GameDate(1950, 1, 1), null)]));
        }

        foreach (var key in SupplierNames)
        {
            (world, _) = world.AddOrganization(new OrganizationSpec(
                OrganizationKind.EngineSupplier,
                true,
                key,
                new GameDate(1950, 1, 1),
                null,
                0,
                [new OrganizationNameSpan("Synthetic " + key, new GameDate(1950, 1, 1), null)]));
        }

        var market = new List<(PersonId Id, int Believed, bool Driver, StaffRole? Role)>();
        var person = 0;

        // ---- contracted drivers: two per team, contracts ending between 1955 and 1958
        for (var i = 0; i < options.Teams; i++)
        {
            for (var seat = 0; seat < 2; seat++)
            {
                var believed = Believed(person);
                var id = PersonId.Real($"pd_{person:00}");
                var birth = new GameDate(1924 + (person % 8), 1 + (person % 12), 1 + (person % 27));
                (world, _) = world.AddPerson(new PersonSpec(
                    "Driver" + person.ToString("00"), "Synthetic", birth, "GBR", true, id.Value, [PersonRole.Driver], DriverTruth(believed, options.HiddenShift, person)));
                market.Add((id, believed, true, null));
                var end = new GameDate(1955 + ((i + seat) % 4), 12, 31);
                (world, _) = world.AddContract(new ContractSpec(
                    id,
                    Team(i),
                    ContractRole.Driver(seat == 0 ? SeatStatus.Equal : SeatStatus.Equal),
                    new GameDate(1955, 1, 1),
                    end,
                    4_000 + ((person % 5) * 1_500),
                    true,
                    null,
                    null));
                person++;
            }
        }

        // ---- free drivers
        var free = new List<PersonId>();
        for (var k = 0; k < options.FreeDrivers; k++)
        {
            var believed = Believed(person);
            var id = PersonId.Real($"pd_{person:00}");
            var birth = new GameDate(1923 + (person % 12), 1 + (person % 12), 1 + (person % 27));
            (world, _) = world.AddPerson(new PersonSpec(
                "Driver" + person.ToString("00"), "Synthetic", birth, "GBR", true, id.Value, [PersonRole.Driver], DriverTruth(believed, options.HiddenShift, person)));
            market.Add((id, believed, true, null));
            free.Add(id);
            person++;
        }

        // ---- staff: a technical director and a chief designer per team under contract, plus free ones; the principal of each team
        foreach (var role in new[] { StaffRole.TechnicalDirector, StaffRole.ChiefDesigner })
        {
            for (var i = 0; i < options.Teams; i++)
            {
                var believed = Believed(person);
                var id = PersonId.Real($"ps_{person:00}");
                (world, _) = world.AddPerson(new PersonSpec(
                    "Staff" + person.ToString("00"), "Synthetic", new GameDate(1915 + (person % 15), 3, 3), "GBR", true, id.Value, [PersonRole.Staff(role)], StaffTruthShifted(role, believed, 0)));
                market.Add((id, believed, false, role));
                (world, _) = world.AddContract(new ContractSpec(
                    id, Team(i), ContractRole.Staff(role), new GameDate(1955, 1, 1), new GameDate(1956 + ((i + person) % 4), 12, 31), 1_000, true, null, null));
                person++;
            }

            for (var k = 0; k < options.FreeStaff; k++)
            {
                var believed = Believed(person);
                var id = PersonId.Real($"ps_{person:00}");
                (world, _) = world.AddPerson(new PersonSpec(
                    "Staff" + person.ToString("00"), "Synthetic", new GameDate(1915 + (person % 15), 3, 3), "GBR", true, id.Value, [PersonRole.Staff(role)], StaffTruthShifted(role, believed, options.HiddenShift)));
                market.Add((id, believed, false, role));
                person++;
            }
        }

        var principals = new List<(PersonId Id, int Believed)>();
        for (var i = 0; i < options.Teams; i++)
        {
            var believed = 4 + ((i * 3) % 15);
            var id = PersonId.Real($"pp_{i:00}");
            (world, _) = world.AddPerson(new PersonSpec(
                "Principal" + i.ToString("00"), "Synthetic", new GameDate(1900 + (i * 4 % 25), 2, 2), "GBR", true, id.Value, [PersonRole.TeamPrincipal], StaffTruthShifted(StaffRole.TeamPrincipal, believed, options.HiddenShift)));
            principals.Add((id, believed));
            (world, _) = world.AddContract(new ContractSpec(
                id, Team(i), ContractRole.Staff(StaffRole.TeamPrincipal), new GameDate(1955, 1, 1), new GameDate(2030, 12, 31), 0, true, null, null));
        }

        // ---- what every team believes: the same bands for all of them, built from the believed value only
        for (var i = 0; i < options.Teams; i++)
        {
            foreach (var (id, believed, driver, role) in market)
            {
                var keys = driver ? GenerationEstimates.DriverAttributeKeys : StaffCatalogue.AttributeKeys(role!.Value);
                var attributes = keys
                    .Select(key => new KnownAttribute(key, new AttributeBand(Math.Max(1, believed - 2), Math.Min(20, believed + 2))))
                    .ToArray();
                AttributeBand? potential = driver ? new AttributeBand(Math.Min(20, believed + 1), Math.Min(20, believed + 4)) : null;
                world = world.SetKnowledge(new PersonKnowledge(Team(i), id, attributes, potential));
            }

            var own = principals[i];
            var principalAttributes = StaffCatalogue.AttributeKeys(StaffRole.TeamPrincipal)
                .Select(key => new KnownAttribute(key, new AttributeBand(Math.Max(1, own.Believed - 1), Math.Min(20, own.Believed + 1))))
                .ToArray();
            world = world.SetKnowledge(new PersonKnowledge(Team(i), own.Id, principalAttributes, null));
        }

        // ---- books, cars and engine deals
        var finance = FinanceSection.Empty;
        for (var i = 0; i < options.Teams; i++)
        {
            finance = finance.Open(Team(i), Opening, options.OpeningCashPerTeamDollars, facts);
        }

        var cars = new List<TeamCar>();
        long next = 1;
        for (var i = 0; i < options.Teams; i++)
        {
            for (var seat = 0; seat < CarEstimates.CarsPerTeam; seat++)
            {
                var effects = ConceptMapping.Effects(CarConcept.Neutral, 60 + (i % 3 * 4));
                cars.Add(new TeamCar(
                    CarIds.Format(next++),
                    Team(i),
                    Opening.Year,
                    CarConcept.Neutral,
                    ConceptMapping.StartingLevels(CarConcept.Neutral, 60 + (i % 3 * 4)),
                    60 + (i % 3 * 4),
                    CarEstimates.InitialUnderstanding,
                    effects.TyreWearMultiplier,
                    effects.SupplierChangeCost,
                    null,
                    null));
            }
        }

        var supply = SupplySection.Empty;
        var referenceBudget = Money.FromDollars(facts.TypicalDollars).Cents;
        for (var i = 0; i < options.Teams; i++)
        {
            var kind = i % 3 == 0 ? SupplyKind.Partner : SupplyKind.Customer;
            var seasons = 1 + (i % 3);
            var shape = new SupplyTerms(1, seasons, false);
            var price = Math.Max(1, SupplyPricing.FloorCents(SupplyItem.Engine, kind, shape, referenceBudget));
            (supply, _) = supply.AddDeal(new SupplyDeal(
                supply.NextDeal,
                SupplyItem.Engine,
                kind,
                Supplier(i % SupplierNames.Length),
                Team(i),
                Opening.Year,
                new SupplyTerms(price, seasons, false),
                Opening,
                SupplyDealStatus.Active,
                null,
                0,
                null));
        }

        world = world
            .WithSection(finance)
            .WithSection(CarsSection.Restore(next, 1, cars, []))
            .WithSection(supply);
        freeDrivers = free;
        return world;
    }

    public static PersonTruth DriverTruth(int believed, int hiddenShift, int person)
    {
        // The hidden shift moves the true value inside the believed band (plus or minus 2); the sign alternates by person.
        var shift = hiddenShift == 0 ? 0 : (person % 2 == 0 ? hiddenShift : -hiddenShift);
        var truth = Math.Clamp(believed + shift, 1, 20);
        return ContractKit.Truth(truth, truth);
    }

    public static PersonTruth StaffTruthShifted(StaffRole role, int believed, int hiddenShift) =>
        ContractKit.StaffTruth(role, Math.Clamp(believed + hiddenShift, 1, 20));
}
