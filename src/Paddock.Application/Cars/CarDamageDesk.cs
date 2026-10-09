using System.Globalization;
using Paddock.Application.Contracts;
using Paddock.Application.Inbox;
using Paddock.Application.Managers;
using Paddock.Application.Racing;
using Paddock.Domain.Cars;
using Paddock.Domain.Finance;
using Paddock.Domain.Inbox;
using Paddock.Domain.Random;
using Paddock.Domain.Time;
using Paddock.Domain.World;

namespace Paddock.Application.Cars;

/// <summary>
/// Crash damage, repairs and spare chassis (#270). One place so a race crash, a private-test crash and an AI team follow the same
/// rules: a repair always costs money (ledger line, never a gate), a damaged car stays out until it is ready, and every
/// consequence (damage, spare run, missed race, lost spare) reaches the team managers' inbox. The desk reads and writes the live
/// world through the two delegates, so a caller that holds its own copy of the world must write it first.
/// </summary>
public sealed class CarDamageDesk
{
    private readonly Func<WorldState> _read;
    private readonly Action<WorldState> _write;
    private readonly IOrganizationControl? _control;
    private readonly InboxBook? _inbox;
    private readonly ManagerRegistry? _managers;

    public CarDamageDesk(
        Func<WorldState> read,
        Action<WorldState> write,
        IOrganizationControl? control,
        InboxBook? inbox,
        ManagerRegistry? managers)
    {
        ArgumentNullException.ThrowIfNull(read);
        ArgumentNullException.ThrowIfNull(write);
        _read = read;
        _write = write;
        _control = control;
        _inbox = inbox;
        _managers = managers;
    }

    private WorldState World => _read();

    public CarDamageSection Section => World.Section<CarDamageSection>(CarDamageSection.SectionName) ?? CarDamageSection.Empty;

    /// <summary>
    /// A crash damaged the car. Draws the severity and the extra days from <paramref name="rng"/>, posts the bill, stores the damage
    /// and tells the team. Null when the car is unknown.
    /// </summary>
    public CarDamage? Record(string carId, DamageSource source, GameDate day, bool driverHurt, Xoshiro256StarStar rng)
    {
        ArgumentNullException.ThrowIfNull(rng);
        var world = World;
        var cars = world.Section<CarsSection>(CarsSection.SectionName);
        if (cars?.Find(carId) is not { } car)
        {
            return null;
        }

        var kind = CarDamageEstimates.Kind(rng.NextDouble(), day.Year, driverHurt);
        var extra = rng.NextInt(0, CarDamageEstimates.MaxExtraDays + 1);
        var ready = day.AddDays(CarDamageEstimates.Days(kind, day.Year, extra));
        var finance = world.Section<FinanceSection>(FinanceSection.SectionName);
        var cost = 0L;
        if (finance is not null && finance.HasBook(car.Organization))
        {
            cost = CarDamageEstimates.CostCents(kind, finance.TypicalCents);
            if (cost > 0)
            {
                finance = finance.Post(
                    car.Organization,
                    day,
                    LedgerCategories.CarBuild,
                    kind == DamageKind.Wrecked ? "chassis" : "repair",
                    -cost,
                    kind == DamageKind.Wrecked ? CarDamageKeys.LedgerChassis : CarDamageKeys.LedgerRepair);
                world = world.WithSection(finance);
            }
        }

        var damage = new CarDamage(carId, car.Organization, kind, source, day, ready, cost);
        world = world.WithSection(Section.WithDamage(damage));
        _write(world);
        Tell(
            car.Organization,
            DamageSubject(source, kind),
            day,
            ("n", CarNumber(cars, car).ToString(CultureInfo.InvariantCulture)),
            ("ready", ready.ToString()),
            ("cost", Dollars(cost)),
            ("days", CarDamageEstimates.Days(kind, day.Year, extra).ToString(CultureInfo.InvariantCulture)));
        return damage;
    }

    /// <summary>The race went with these cars on a spare or left out. Each is told to the team on the race day.</summary>
    public void NoteRaceField(RaceField field, GameDate day)
    {
        ArgumentNullException.ThrowIfNull(field);
        var cars = World.Section<CarsSection>(CarsSection.SectionName);
        if (cars is null)
        {
            return;
        }

        if (!field.SpareRuns.IsDefault)
        {
            foreach (var run in field.SpareRuns)
            {
                Tell(
                    run.Organization,
                    CarDamageKeys.SpareRunSubject,
                    day,
                    ("n", NumberOf(cars, run.CarId)),
                    ("ready", run.ReadyOn.ToString()),
                    ("slower", Math.Round(CarDamageEstimates.SpareLevelShare * 100d).ToString("0", CultureInfo.InvariantCulture)));
            }
        }

        if (!field.Absences.IsDefault)
        {
            foreach (var absence in field.Absences)
            {
                Tell(
                    absence.Organization,
                    absence.HasSpare ? CarDamageKeys.MissedSpareBusySubject : CarDamageKeys.MissedNoSpareSubject,
                    day,
                    ("n", NumberOf(cars, absence.CarId)),
                    ("ready", absence.ReadyOn.ToString()));
            }
        }
    }

    /// <summary>The spare crashed in the race: it is gone (no repair is offered for an old chassis).</summary>
    public void WriteOffSpare(OrganizationId organization, string carId, GameDate day)
    {
        var section = Section;
        if (section.SpareOf(organization) is null)
        {
            return;
        }

        _write(World.WithSection(section.WithoutSpare(organization)));
        var cars = World.Section<CarsSection>(CarsSection.SectionName);
        Tell(organization, CarDamageKeys.SpareLostSubject, day, ("n", cars is null ? "?" : NumberOf(cars, carId)));
    }

    /// <summary>
    /// End of season: each team keeps its last chassis as next year's spare, and repairs that are long finished are forgotten.
    /// </summary>
    public void CloseSeason(GameDate today, IEnumerable<OrganizationId> teams)
    {
        ArgumentNullException.ThrowIfNull(teams);
        var world = World;
        var cars = world.Section<CarsSection>(CarsSection.SectionName);
        if (cars is null)
        {
            return;
        }

        var section = Section;
        foreach (var damage in section.Damages)
        {
            if (damage.ReadyOn <= today)
            {
                section = section.WithoutDamage(damage.CarId);
            }
        }

        foreach (var team in teams)
        {
            var last = cars.Of(team)
                .Where(car => car.Season == today.Year)
                .OrderBy(car => car.Id, StringComparer.Ordinal)
                .FirstOrDefault();
            if (last is not null)
            {
                section = section.WithSpare(new SpareChassis(team, today.Year, last.Concept, last.Levels));
            }
        }

        if (!section.IsEmpty || world.Section<CarDamageSection>(CarDamageSection.SectionName) is not null)
        {
            _write(world.WithSection(section));
        }
    }

    /// <summary>The car as it races on the spare: the older concept and levels, lower by a share, and little understanding of it.</summary>
    public static TeamCar OnSpare(TeamCar car, SpareChassis spare)
    {
        ArgumentNullException.ThrowIfNull(car);
        ArgumentNullException.ThrowIfNull(spare);
        return car.WithDesign(
            car.Season,
            spare.Concept,
            spare.Levels.Scale(1d - CarDamageEstimates.SpareLevelShare),
            car.ConceptCeiling,
            car.Understanding * CarDamageEstimates.SpareUnderstandingShare,
            car.TyreWearMultiplier,
            car.SupplierChangeCost);
    }

    /// <summary>The 1-based place of the car among its team's cars (the number the player sees).</summary>
    public static int CarNumber(CarsSection cars, TeamCar car)
    {
        var index = 0;
        foreach (var other in cars.Of(car.Organization).Where(c => c.Season == car.Season).OrderBy(c => c.Id, StringComparer.Ordinal))
        {
            index++;
            if (other.Id == car.Id)
            {
                return index;
            }
        }

        return 1;
    }

    private static string NumberOf(CarsSection cars, string carId) =>
        cars.Find(carId) is { } car ? CarNumber(cars, car).ToString(CultureInfo.InvariantCulture) : "?";

    private static string DamageSubject(DamageSource source, DamageKind kind) => (source, kind == DamageKind.Wrecked) switch
    {
        (DamageSource.Race, false) => CarDamageKeys.RaceRepairSubject,
        (DamageSource.Race, true) => CarDamageKeys.RaceChassisSubject,
        (_, false) => CarDamageKeys.TestRepairSubject,
        _ => CarDamageKeys.TestChassisSubject,
    };

    private static string Dollars(long cents) =>
        "$" + new Money(cents).WholeDollars.ToString("N0", CultureInfo.InvariantCulture);

    private void Tell(OrganizationId organization, string subject, GameDate day, params (string Key, string Value)[] arguments)
    {
        if (_inbox is null || _managers is null || _control is null)
        {
            return;
        }

        foreach (var manager in _control.ManagersOf(organization))
        {
            if (!_managers.Contains(manager))
            {
                continue;
            }

            var args = new List<KeyValuePair<string, string>>(arguments.Length + 1);
            foreach (var (key, value) in arguments)
            {
                args.Add(new KeyValuePair<string, string>(key, value));
            }

            args.Add(new KeyValuePair<string, string>("organization", organization.Value));
            _inbox.Post(
                _managers,
                manager,
                new InboxItemDraft(CarDamageKeys.NoticeKind, subject, args, options: null, validUntil: null, defaultOptionId: null),
                day);
        }
    }
}
