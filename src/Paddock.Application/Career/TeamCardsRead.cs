using Paddock.Domain.Board;
using Paddock.Domain.Cars;
using Paddock.Domain.Finance;
using Paddock.Domain.Supply;
using Paddock.Domain.Time;
using Paddock.Domain.World;

namespace Paddock.Application.Career;

/// <summary>A driver on a team card: the public facts of a line-up (name, nationality, age), never a rating.</summary>
public sealed record TeamCardDriver(string Name, string Nationality, int Age, string Seat);

/// <summary>The engine a team runs. <see cref="SupplyType"/> is the authored supply kind (works, customer, partner, badged).</summary>
public sealed record TeamCardEngine(string Name, string Supplier, string SupplyType);

/// <summary>
/// What a principal can know about a team before he takes it. <see cref="Budget"/> is the finance tier the books start from
/// (low, typical, top). <see cref="LastSeason"/> is last season's constructors' place when the authored order has it.
/// <see cref="Expected"/> is the position the board will ask for, from the same rule it uses on appointment.
/// </summary>
public sealed record TeamCardView(
    string Id,
    string Name,
    IReadOnlyList<TeamCardDriver> Drivers,
    TeamCardEngine? Engine,
    string Budget,
    int? LastSeason,
    int Expected,
    int FieldSize);

/// <summary>
/// The team cards of a career's first morning, for the screen that picks a team. A pure read of a world the caller already
/// built: no state change, no random number (INV-005). It reads only public facts (INV-003).
/// </summary>
public static class TeamCardsRead
{
    public static IReadOnlyList<TeamCardView> Of(
        WorldState world,
        GameDate on,
        IReadOnlyList<SupplyLink> supplies,
        ITeamTierSource tiers,
        IReadOnlyDictionary<string, int> lastSeason,
        ICarStrengthSource? carStrength = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(supplies);
        ArgumentNullException.ThrowIfNull(tiers);
        ArgumentNullException.ThrowIfNull(lastSeason);
        var teams = CareerTeams.Active(world, on);
        var ranking = new PublicRankKeys(tiers, carStrength);
        var cards = new List<TeamCardView>(teams.Count);
        foreach (var team in teams)
        {
            var drivers = new List<(SeatStatus Seat, Person Person)>();
            foreach (var contract in world.Contracts)
            {
                if (contract.OrganizationId != team.Id || !contract.Role.IsDriver || !contract.IsActiveOn(on))
                {
                    continue;
                }

                var person = world.GetPerson(contract.PersonId);
                if (!person.IsRetired)
                {
                    drivers.Add((contract.Role.Seat, person));
                }
            }

            var line = drivers
                .OrderBy(entry => entry.Seat)
                .ThenBy(entry => entry.Person.Id.Value, StringComparer.Ordinal)
                .Select(entry => new TeamCardDriver(entry.Person.Name, entry.Person.Nationality, AgeOn(entry.Person.BirthDate, on), entry.Seat.ToString()))
                .ToArray();

            TeamCardEngine? engine = null;
            foreach (var link in supplies)
            {
                if (link.Constructor == team.Id)
                {
                    engine = new TeamCardEngine(link.EngineName, world.GetOrganization(link.Supplier).NameOn(on), link.SupplyType);
                    break;
                }
            }

            var rank = PublicStrength.BudgetRank(world, team.Id, on, ranking);
            cards.Add(new TeamCardView(
                team.Id.Value,
                team.NameOn(on),
                line,
                engine,
                tiers.TierOf(team.Id, on.Year).ToString().ToLowerInvariant(),
                lastSeason.TryGetValue(team.Id.Value, out var place) ? place : null,
                ReputationModel.ExpectedPosition(null, rank, teams.Count),
                teams.Count));
        }

        return cards;
    }

    private static int AgeOn(GameDate born, GameDate on)
    {
        var age = on.Year - born.Year;
        if (on.Month < born.Month || (on.Month == born.Month && on.Day < born.Day))
        {
            age--;
        }

        return Math.Max(0, age);
    }
}
