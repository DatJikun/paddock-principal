using Paddock.Domain.Pool;
using Paddock.Domain.Sponsors;
using Paddock.Domain.Time;
using Paddock.Domain.World;

namespace Paddock.Application.Sponsors;

/// <summary>
/// Nationality wishes of sponsors (#268, owner decision). A wish is rare (only the sponsors whose authored file names a nationality have one),
/// it exists only while a driver of that nationality can be had, and it is a bonus: a team that has such a driver in the right place gets
/// <see cref="SponsorEstimates.WishBonusMilli"/> of the annual amount on top, and a team that has not loses nothing and keeps the sponsor's
/// trust. Everything here is a pure read of the world; nothing draws a random number. Nationality of a driver is public, so no hidden truth
/// is read.
/// </summary>
public static class SponsorWishes
{
    /// <summary>
    /// The wish the sponsor would make, whether or not anyone could satisfy it: its authored nationality condition. A sponsor with a
    /// budget level of at least <see cref="SponsorEstimates.BigSponsorBudgetLevel"/> wants the driver in a race seat, a smaller one accepts a reserve.
    /// </summary>
    public static SponsorWish? WishOf(SponsorDefinition sponsor)
    {
        ArgumentNullException.ThrowIfNull(sponsor);
        return sponsor.Objective is { Kind: SponsorObjectiveSpec.DriverNationalityInLineup } spec
            ? new SponsorWish(spec.Value, sponsor.BudgetLevel >= SponsorEstimates.BigSponsorBudgetLevel)
            : null;
    }

    /// <summary>The wish the sponsor makes today: its wish, but only when a driver of that nationality is on the grid or on the market.</summary>
    public static SponsorWish? Offered(WorldState world, SponsorDefinition sponsor, GameDate today)
    {
        ArgumentNullException.ThrowIfNull(world);
        return WishOf(sponsor) is { } wish && DriverExists(world, wish.Nationality, today) ? wish : null;
    }

    /// <summary>
    /// True when a driver of the nationality drives for some team (the grid) or is free to sign (the market): not retired, of racing age,
    /// and not a junior in the talent pool.
    /// </summary>
    public static bool DriverExists(WorldState world, string nationality, GameDate today)
    {
        ArgumentNullException.ThrowIfNull(world);
        var pool = world.Section<TalentPoolSection>(TalentPoolSection.SectionName);
        foreach (var person in world.Persons)
        {
            if (person.IsRetired
                || !SponsorNationality.Same(person.Nationality, nationality)
                || !person.Roles.Any(role => role.IsDriver)
                || today.Year - person.BirthDate.Year is < 18 or > 45
                || pool?.Find(person.Id) is not null)
            {
                continue;
            }

            return true;
        }

        return false;
    }

    /// <summary>True when the organization has a driver of the wished nationality under contract today, in a race seat when the wish asks for one.</summary>
    public static bool Satisfied(WorldState world, OrganizationId organization, SponsorWish wish, GameDate today)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(wish);
        foreach (var contract in world.Contracts)
        {
            if (contract.OrganizationId != organization || !contract.Role.IsDriver || !contract.IsActiveOn(today))
            {
                continue;
            }

            if (wish.RaceSeat && contract.Role.Seat == SeatStatus.Reserve)
            {
                continue;
            }

            var person = world.GetPerson(contract.PersonId);
            if (!person.IsRetired && SponsorNationality.Same(person.Nationality, wish.Nationality))
            {
                return true;
            }
        }

        return false;
    }
}
