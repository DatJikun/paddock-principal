using Paddock.Domain.Board;
using Paddock.Domain.Cars;
using Paddock.Domain.Finance;
using Paddock.Domain.Infrastructure;
using Paddock.Domain.Supply;
using Paddock.Domain.Time;
using Paddock.Domain.World;

namespace Paddock.Application.Career;

/// <summary>A driver on a team card: the public facts of a line-up (name, nationality, age), never a rating.</summary>
public sealed record TeamCardDriver(string Name, string Nationality, int Age, string Seat);

/// <summary>The engine a team runs. <see cref="SupplyType"/> is the authored supply kind (works, customer, partner, badged).</summary>
public sealed record TeamCardEngine(string Name, string Supplier, string SupplyType);

/// <summary>
/// Rough public levels of a team, 1 (weakest) to <see cref="TeamCardsRead.MaxLevel"/> (strongest), by where its score lies between the weakest and the
/// strongest team of the same season (#327). The scale is relative to the season's grid and keeps the order of the scores, so a far
/// stronger team is not shown level with a merely good one (a rank band would put the first three teams at the top). A level is null
/// when the world holds nothing to judge that area by.
/// </summary>
public sealed record TeamCardLevels(int? Car, int? Infrastructure, int? Drivers, int? Staff);

/// <summary>
/// What a principal can know about a team before he takes it. <see cref="Budget"/> is the finance tier the books start from
/// (low, typical, top) and <see cref="BudgetCents"/> the nominal opening budget of that tier in cents (an ESTIMATE).
/// <see cref="LastSeason"/> is last season's constructors' place when the authored order has it.
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
    int FieldSize,
    long BudgetCents,
    TeamCardLevels Levels);

/// <summary>
/// The team cards of a career's first morning, for the screen that picks a team. A pure read of a world the caller already
/// built: no state change, no random number (INV-005). It reads only public facts (INV-003); the four levels are coarse bands.
/// </summary>
public static class TeamCardsRead
{
    /// <summary>The top of the level scale of a team card (a design choice); the UI draws this many pips.</summary>
    public const int MaxLevel = 10;

    public static IReadOnlyList<TeamCardView> Of(
        WorldState world,
        GameDate on,
        IReadOnlyList<SupplyLink> supplies,
        ITeamTierSource tiers,
        IReadOnlyDictionary<string, int> lastSeason,
        ICarStrengthSource? carStrength = null,
        FacilityCatalog? facilities = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(supplies);
        ArgumentNullException.ThrowIfNull(tiers);
        ArgumentNullException.ThrowIfNull(lastSeason);
        var teams = CareerTeams.Active(world, on);
        var ranking = new PublicRankKeys(tiers, carStrength);
        var cards = new List<TeamCardView>(teams.Count);
        var carScores = new Dictionary<OrganizationId, double>();
        var infraScores = new Dictionary<OrganizationId, double>();
        var driverScores = new Dictionary<OrganizationId, double>();
        var staffScores = new Dictionary<OrganizationId, double>();
        foreach (var team in teams)
        {
            if (carStrength is not null && team.Id.IsReal && carStrength.TryGet(team.Id.Value, on.Year, out var strength))
            {
                carScores[team.Id] = strength;
            }

            if (InfrastructureScore(facilities, team.Id, on.Year) is double built)
            {
                infraScores[team.Id] = built;
            }

            var raceSide = new List<double>();
            var staffSide = new List<double>();
            foreach (var contract in world.Contracts)
            {
                if (contract.OrganizationId != team.Id || !contract.IsActiveOn(on))
                {
                    continue;
                }

                var member = world.GetPerson(contract.PersonId);
                if (member.IsRetired)
                {
                    continue;
                }

                if (contract.Role.IsDriver && contract.Role.Seat != SeatStatus.Reserve)
                {
                    raceSide.Add(member.Truth.Attributes.Average(item => (double)item.Value));
                }
                else if (contract.Role.IsStaff)
                {
                    staffSide.Add(member.Truth.Attributes.Average(item => (double)item.Value));
                }
            }

            if (raceSide.Count > 0)
            {
                driverScores[team.Id] = raceSide.Average();
            }

            if (staffSide.Count > 0)
            {
                staffScores[team.Id] = staffSide.Average();
            }
        }

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
                teams.Count,
                team.Budget * 100L,
                new TeamCardLevels(
                    LevelOf(carScores, team.Id),
                    LevelOf(infraScores, team.Id),
                    LevelOf(driverScores, team.Id),
                    LevelOf(staffScores, team.Id))));
        }

        return cards;
    }

    /// <summary>Mean working quality of the unlocked facilities against the frontier of the year; null when the catalog has none.</summary>
    private static double? InfrastructureScore(FacilityCatalog? facilities, OrganizationId team, int year)
    {
        if (facilities is null)
        {
            return null;
        }

        var shares = new List<double>();
        foreach (var spec in facilities.Kinds)
        {
            if (year < spec.UnlockYear)
            {
                continue;
            }

            var quality = facilities.StartingQualityMilli(team.Value, spec.Kind);
            if (quality > 0)
            {
                shares.Add(InfrastructureMath.Relative(quality, year, building: false));
            }
        }

        return shares.Count == 0 ? null : shares.Average();
    }

    /// <summary>
    /// 1 to <see cref="MaxLevel"/> by the score's place between the lowest and the highest score of the grid (a monotone
    /// map, so equal scores share a level and a stronger one never shows lower); null when this team has no score. A grid where
    /// every score is equal sits in the middle.
    /// </summary>
    private static int? LevelOf(IReadOnlyDictionary<OrganizationId, double> scores, OrganizationId team)
    {
        if (!scores.TryGetValue(team, out var own))
        {
            return null;
        }

        var low = scores.Values.Min();
        var high = scores.Values.Max();
        if (high <= low)
        {
            return (MaxLevel + 1) / 2;
        }

        var share = (own - low) / (high - low);
        return 1 + (int)Math.Round(share * (MaxLevel - 1), MidpointRounding.AwayFromZero);
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
