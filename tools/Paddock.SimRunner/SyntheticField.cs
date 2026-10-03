using System.Collections.Immutable;
using Paddock.Simulation.Racing.Pace;
using Paddock.Simulation.Racing.Pits;
using Paddock.Simulation.Racing.Tyres;
using Paddock.Simulation.Racing.Weekend;

namespace Paddock.SimRunner;

/// <summary>
/// The SYNTHETIC fixture field of an era, for the race tool until the world initializer (T20) exists. It has no real names
/// and no real history: a graded set of constructors (the best car first) with two drivers each, and every number is an
/// ESTIMATE made to give a plausible spread, not a recorded fact. The field depends only on the season, never on the seed,
/// so the same season always races the same cars.
/// <list type="bullet">
/// <item>Before 1960: 9 constructors, 18 cars, the four weakest cars have a co-driver (shared drives). Naturally aspirated.</item>
/// <item>1960 to 1993: 13 constructors, 26 cars; before 1989 the stronger half runs turbo engines, from 1989 all are naturally aspirated.</item>
/// <item>From 1994: 12 constructors, 24 cars; hybrid engines from 2014.</item>
/// </list>
/// </summary>
public static class SyntheticField
{
    public const string FixtureName = "fixture";

    public static ImmutableArray<RaceEntry> For(int season)
    {
        var (teams, sharedCars) = season switch
        {
            < 1960 => (9, 4),
            < 1994 => (13, 0),
            _ => (12, 0),
        };

        var entries = ImmutableArray.CreateBuilder<RaceEntry>(teams * 2);
        var sharedLeft = sharedCars;
        for (var t = 0; t < teams; t++)
        {
            var tier = teams == 1 ? 0d : (double)t / (teams - 1); // 0 = best car, 1 = worst
            for (var c = 0; c < 2; c++)
            {
                var index = (t * 2) + c;
                var shared = sharedLeft > 0 && t >= teams - 2;
                if (shared)
                {
                    sharedLeft--;
                }

                entries.Add(Entry(season, t, c, index, tier, shared));
            }
        }

        return entries.ToImmutable();
    }

    private static RaceEntry Entry(int season, int team, int slot, int index, double tier, bool shared)
    {
        // A small, fixed wobble per car so cars of equal tier are not clones (no RNG: the same every time).
        double Wobble(int salt, double amplitude) => (((((index * 7) + (salt * 13)) % 11) - 5) / 5d) * amplitude;

        var baseRating = 78d - (36d * tier);
        var car = new CarPerformance(
            Math.Clamp(baseRating + Wobble(1, 5d), 5, 95),
            Math.Clamp(baseRating + Wobble(2, 5d), 5, 95),
            Math.Clamp(baseRating + Wobble(3, 5d), 5, 95),
            Math.Clamp(baseRating + Wobble(4, 5d), 5, 95),
            Math.Clamp(72d - (30d * tier) + Wobble(5, 6d), 10, 95));

        var constructorId = "team-" + (char)('a' + team);
        var carId = "car-" + (index + 1).ToString("00", System.Globalization.CultureInfo.InvariantCulture);
        var drivers = ImmutableArray.CreateBuilder<DriverEntry>(2);
        drivers.Add(Driver(index, (slot == 0 ? 0d : 6d) + (tier * 18d)));
        if (shared)
        {
            drivers.Add(Driver(100 + index, 20d + (tier * 10d)));
        }

        var engine = season switch
        {
            >= 2014 => FuelEngineType.Hybrid,
            >= 1989 => FuelEngineType.NaturallyAspirated,
            >= 1977 when tier < 0.5d => FuelEngineType.Turbo,
            _ => FuelEngineType.NaturallyAspirated,
        };

        return new RaceEntry(
            carId,
            constructorId,
            drivers.ToImmutable(),
            car,
            RaceInputMapping.UniformComponents(car.Reliability),
            new PitCrew(Math.Clamp(75d - (45d * tier) + Wobble(6, 8d), 5, 95)),
            (int)Math.Clamp(80d - (50d * tier) + Wobble(7, 10d), 5, 95),
            Math.Clamp(1d - (0.3d * tier) + Wobble(8, 0.04d), 0.5, 1d),
            TyreSupplierProfile.Neutral,
            PartnerTuned: false,
            engine);
    }

    // A driver as 1..20-style ratings turned into the 0..100 inputs by the same linear mapping the tool uses everywhere.
    private static DriverEntry Driver(int index, double weakness)
    {
        int Attribute(int salt, double centre) => (int)Math.Clamp(
            Math.Round(centre + (((((index * 5) + (salt * 17)) % 9) - 4) / 4d * 2.5d)),
            1,
            20);

        var strength = 16d - (weakness / 100d * 12d); // ESTIMATE: 16 for the best, about 6 for the weakest
        var attributes = new Paddock.Domain.People.DriverAttributes(
            cornering: Attribute(1, strength),
            braking: Attribute(2, strength),
            smoothness: Attribute(3, 11),
            overtaking: Attribute(4, 11),
            defending: Attribute(5, 11),
            consistency: Attribute(6, strength - 1),
            composure: Attribute(7, strength - 1),
            adaptability: 10,
            wetWeather: 10,
            fitness: 10,
            feedback: 10);
        return RaceInputMapping.DriverFrom(
            "driver-" + (index + 1).ToString("000", System.Globalization.CultureInfo.InvariantCulture),
            attributes,
            Attribute(8, 10));
    }
}
