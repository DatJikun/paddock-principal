using Paddock.Application.Access;
using Paddock.Application.Contracts;
using Paddock.Domain.Cars;
using Paddock.Domain.People;
using Paddock.Domain.Time;
using Paddock.Domain.World;
using ManagerId = Paddock.Application.Managers.ManagerId;

namespace Paddock.Application.Cars;

/// <summary>Inclusive band. Not the simulation's exact number.</summary>
public readonly record struct CarBandView(double Low, double High);

/// <summary>The team's own car, as its engineers read it. Axes are the decision they made. Levels and the ceiling are bands.</summary>
public sealed record OwnCarView(
    string CarId,
    int Season,
    int AeroMilli,
    int PhilosophyMilli,
    int WindowMilli,
    int CoolingMilli,
    int TyreMilli,
    int IntegrationMilli,
    CarBandView Power,
    CarBandView Downforce,
    CarBandView MechanicalGrip,
    CarBandView Braking,
    CarBandView Reliability,
    CarBandView Ceiling,
    CarBandView Understanding);

/// <summary>A rival car. No ceiling and no performance vector: those stay hidden until public results exist (not in this task).</summary>
public sealed record RivalCarView(string CarId, string OrganizationId);

/// <summary>
/// A car of the manager's team that is in the workshop after a crash (#270). <c>Kind</c> is <c>light</c>, <c>heavy</c> or <c>wrecked</c>
/// (a new chassis); <c>Source</c> is <c>race</c> or <c>test</c>. Dates are ISO text.
/// </summary>
public sealed record OwnCarDamageView(string CarId, string Kind, string Source, string DamagedOn, string ReadyOn, int DaysLeft, long CostCents);

/// <summary>The older chassis the manager's team keeps for a crash it cannot repair in time, and how much slower it runs, in percent.</summary>
public sealed record OwnSpareView(string OrganizationId, int Season, int SlowerPercent);

/// <summary>What one manager or AI may see. Queries do not change the world and do not draw RNG (INV-005).</summary>
public sealed record ManagerCarRoster(IReadOnlyList<OwnCarView> Own, IReadOnlyList<RivalCarView> Rivals)
{
    /// <summary>Own cars still being repaired (#270).</summary>
    public IReadOnlyList<OwnCarDamageView> Damage { get; init; } = [];

    /// <summary>Own spare chassis, if any (#270).</summary>
    public IReadOnlyList<OwnSpareView> Spares { get; init; } = [];
}

public sealed class CarQuery
{
    private readonly CarBook _book;
    private readonly IOrganizationControl _control;

    public CarQuery(CarBook book, IOrganizationControl control)
    {
        ArgumentNullException.ThrowIfNull(book);
        ArgumentNullException.ThrowIfNull(control);
        _book = book;
        _control = control;
    }

    public ManagerCarRoster View(AccessContext access)
    {
        ArgumentNullException.ThrowIfNull(access);
        if (access.Kind == AccessKind.Developer)
        {
            throw new InvalidOperationException("A developer reads Truth, not the manager roster.");
        }

        if (access.Manager is not { } actor)
        {
            throw new InvalidOperationException("A manager view needs a manager.");
        }

        var manager = new ManagerId(actor.Value);
        var own = new List<OwnCarView>();
        var rivals = new List<RivalCarView>();
        var today = new GameDate(_book.World.CurrentDate.Year, _book.World.CurrentDate.Month, _book.World.CurrentDate.Day);
        foreach (var car in _book.Section.Cars)
        {
            if (_control.Controls(manager, car.Organization))
            {
                own.Add(Own(car, _book.World, today));
            }
            else
            {
                rivals.Add(new RivalCarView(car.Id, car.Organization.Value));
            }
        }

        var damage = new List<OwnCarDamageView>();
        var spares = new List<OwnSpareView>();
        var section = _book.World.Section<CarDamageSection>(CarDamageSection.SectionName);
        if (section is not null)
        {
            foreach (var item in section.Damages)
            {
                if (item.ReadyOn > today && _control.Controls(manager, item.Organization))
                {
                    damage.Add(new OwnCarDamageView(
                        item.CarId,
                        item.Kind.ToString().ToLowerInvariant(),
                        item.Source.ToString().ToLowerInvariant(),
                        item.DamagedOn.ToString(),
                        item.ReadyOn.ToString(),
                        today.DaysUntil(item.ReadyOn),
                        item.CostCents));
                }
            }

            foreach (var spare in section.Spares)
            {
                if (_control.Controls(manager, spare.Organization))
                {
                    spares.Add(new OwnSpareView(
                        spare.Organization.Value,
                        spare.Season,
                        (int)Math.Round(CarDamageEstimates.SpareLevelShare * 100d)));
                }
            }
        }

        return new ManagerCarRoster(own, rivals) { Damage = damage, Spares = spares };
    }

    private static OwnCarView Own(TeamCar car, WorldState world, GameDate today)
    {
        var vision = TeamEngineers.Attribute(world, car.Organization, today, StaffRole.TechnicalDirector, "vision");
        var aero = TeamEngineers.Attribute(world, car.Organization, today, StaffRole.HeadOfAerodynamics, "aerodynamics");
        var key = car.Organization.Value + "|" + car.Id + "|" + car.Season.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return new OwnCarView(
            car.Id,
            car.Season,
            CarEstimates.Milli(car.Concept.Aero),
            CarEstimates.Milli(car.Concept.Philosophy),
            CarEstimates.Milli(car.Concept.Window),
            CarEstimates.Milli(car.Concept.Cooling),
            CarEstimates.Milli(car.Concept.TyreKindness),
            CarEstimates.Milli(car.Concept.Integration),
            Band(car.Levels.Power, vision, aero, key),
            Band(car.Levels.Downforce, vision, aero, key),
            Band(car.Levels.MechanicalGrip, vision, aero, key),
            Band(car.Levels.Braking, vision, aero, key),
            Band(car.Levels.Reliability, vision, aero, key),
            Band(car.ConceptCeiling, vision, aero, key),
            Band(car.Understanding, vision, aero, key));
    }

    private static CarBandView Band(double truth, int vision, int aero, string biasKey)
    {
        var band = CarKnowledgeBands.Around(truth, vision, aero, biasKey);
        return new CarBandView(band.Low, band.High);
    }
}
