using Paddock.Domain.Time;
using Paddock.Domain.World;

namespace Paddock.Domain.Cars;

/// <summary>
/// Points each car at a race driver who actually holds the seat on that day.
/// A driver already on a car stays there (PP-050: no mid-season swap between the two cars).
/// A driver whose contract has ended, or who has retired, is cleared. An empty seat is filled
/// from the race drivers who are not already on a car, in seat order.
/// </summary>
public static class CarSeatSync
{
    public static WorldState Apply(WorldState world, GameDate on)
    {
        ArgumentNullException.ThrowIfNull(world);
        var section = world.Section<CarsSection>(CarsSection.SectionName);
        if (section is null || section.Cars.Count == 0)
        {
            return world;
        }

        var next = section;
        foreach (var organization in section.Cars.Select(car => car.Organization).Distinct().OrderBy(id => id.Value, StringComparer.Ordinal))
        {
            var active = InitialCarFactory.RaceDrivers(world, organization, on);
            var seated = new HashSet<string>(StringComparer.Ordinal);
            foreach (var car in next.Of(organization).Where(car => car.Season == on.Year).OrderBy(car => car.Id, StringComparer.Ordinal))
            {
                if (car.Driver is PersonId driver && active.Any(id => id == driver) && seated.Add(driver.Value))
                {
                    continue;
                }

                if (car.Driver is not null)
                {
                    next = next.Replace(car.WithDriver(null));
                }
            }

            var open = next.Of(organization)
                .Where(car => car.Season == on.Year && car.Driver is null)
                .OrderBy(car => car.Id, StringComparer.Ordinal)
                .ToArray();
            var waiting = active.Where(id => !seated.Contains(id.Value)).ToArray();
            var count = Math.Min(open.Length, waiting.Length);
            for (var i = 0; i < count; i++)
            {
                next = next.Replace(open[i].WithDriver(waiting[i]));
            }
        }

        return ReferenceEquals(next, section) ? world : world.WithSection(next);
    }
}
