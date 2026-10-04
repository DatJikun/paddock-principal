using System.Globalization;
using Paddock.Domain.World;

namespace Paddock.Domain.Cars;

/// <summary>Stable id <c>car:{n}</c> from the cars section counter. Numbers are never reused (INV-009).</summary>
public static class CarIds
{
    public const string Prefix = "car:";

    public static string Format(long number)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(number, 1);
        return Prefix + number.ToString(CultureInfo.InvariantCulture);
    }

    public static long Require(string id)
    {
        if (!TryParse(id, out var number))
        {
            throw new ArgumentException("A car id is car:{n}.", nameof(id));
        }

        return number;
    }

    public static bool TryParse(string? id, out long number)
    {
        number = 0;
        if (id is null || !id.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return false;
        }

        var tail = id.AsSpan(Prefix.Length);
        if (tail.Length == 0 || (tail.Length > 1 && tail[0] == '0'))
        {
            return false;
        }

        return long.TryParse(tail, NumberStyles.None, CultureInfo.InvariantCulture, out number) && number >= 1;
    }
}

/// <summary>
/// One car of one organization for one season. The ceiling and the exact levels are simulation truth.
/// A manager reads bands, not this type (INV-003). The driver stays for the season (PP-050): nothing here swaps seats.
/// </summary>
public sealed class TeamCar
{
    public TeamCar(
        string id,
        OrganizationId organization,
        int season,
        CarConcept concept,
        PerformanceLevels levels,
        double conceptCeiling,
        double understanding,
        double tyreWearMultiplier,
        int supplierChangeCost,
        string? engineKey,
        PersonId? driver)
    {
        _ = CarIds.Require(id);
        if (!organization.IsAssigned)
        {
            throw new ArgumentException("Organization id is unassigned.", nameof(organization));
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(season, 1950);
        if (conceptCeiling is < 0d or > 100d || double.IsNaN(conceptCeiling))
        {
            throw new ArgumentOutOfRangeException(nameof(conceptCeiling));
        }

        if (understanding is < 0d or > 100d || double.IsNaN(understanding))
        {
            throw new ArgumentOutOfRangeException(nameof(understanding));
        }

        if (tyreWearMultiplier <= 0d || double.IsNaN(tyreWearMultiplier))
        {
            throw new ArgumentOutOfRangeException(nameof(tyreWearMultiplier));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(supplierChangeCost);
        if (driver is PersonId assigned && !assigned.IsAssigned)
        {
            throw new ArgumentException("Driver id is unassigned.", nameof(driver));
        }

        Id = id;
        Organization = organization;
        Season = season;
        Concept = concept;
        Levels = levels;
        ConceptCeiling = CarEstimates.Quantize(conceptCeiling);
        Understanding = CarEstimates.Quantize(understanding);
        TyreWearMultiplier = CarEstimates.Quantize(tyreWearMultiplier);
        SupplierChangeCost = supplierChangeCost;
        EngineKey = string.IsNullOrEmpty(engineKey) ? null : engineKey;
        Driver = driver is PersonId person && person.IsAssigned ? person : null;
    }

    public string Id { get; }

    public OrganizationId Organization { get; }

    public int Season { get; }

    public CarConcept Concept { get; }

    public PerformanceLevels Levels { get; }

    /// <summary>Hidden potential of the concept. Not a manager or AI field.</summary>
    public double ConceptCeiling { get; }

    /// <summary>0–100. In-season gains are T42; this task only stores the number.</summary>
    public double Understanding { get; }

    /// <summary>Wear multiplier for the tyre model (T30). ESTIMATE.</summary>
    public double TyreWearMultiplier { get; }

    /// <summary>ESTIMATE cost of changing an engine supplier. Universal integration is cheaper.</summary>
    public int SupplierChangeCost { get; }

    /// <summary>Engine deal is T43. Null until a supplier is named.</summary>
    public string? EngineKey { get; }

    public PersonId? Driver { get; }

    /// <summary>Same car and same driver, with a newly approved design.</summary>
    public TeamCar WithDesign(
        int season,
        CarConcept concept,
        PerformanceLevels levels,
        double conceptCeiling,
        double understanding,
        double tyreWearMultiplier,
        int supplierChangeCost) =>
        new(
            Id,
            Organization,
            season,
            concept,
            levels,
            conceptCeiling,
            understanding,
            tyreWearMultiplier,
            supplierChangeCost,
            EngineKey,
            Driver);
}
