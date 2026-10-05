using Paddock.Application.Career;
using Paddock.Data.Authored;
using Paddock.Data.World;
using Paddock.Domain.Career;
using Paddock.Domain.Contracts;
using Paddock.Domain.Supply;
using Paddock.Domain.World;

namespace Paddock.Career;

/// <summary>
/// Loads the data a career run reads from <c>data/authored</c>: the era periods, the fictional sponsors, and the ESTIMATE of the
/// previous season's constructors' order (the Jolpica standings stay local, PP-041). The same inputs for <c>run</c>, for
/// <c>run --resume</c> and for the tests, so they all see one economy.
/// </summary>
public static class CareerInputsLoader
{
    public static CareerInputs Load(string dataRoot, AuthoredData data, IReadOnlyList<EngineSupplyLink>? supplies = null, CareerConfig? career = null, RaceDateBook? raceDates = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataRoot);
        ArgumentNullException.ThrowIfNull(data);
        var sponsors = SponsorsLoader.ToCatalog(SponsorsLoader.Load(dataRoot));
        var tiers = TeamTiersLoader.ToSource(TeamTiersLoader.Load(dataRoot));
        var inputs = CareerInputs.From(data.EraPeriods, sponsors, new EraPayBenchmark(data.EraSetFor), tiers);
        return new CareerInputs
        {
            Eras = inputs.Eras,
            SponsorEras = inputs.SponsorEras,
            Sponsors = inputs.Sponsors,
            Pay = inputs.Pay,
            Tiers = inputs.Tiers,
            CarStrength = data.CarStrength,
            EraPeriods = inputs.EraPeriods,
            RulePeriods = data.Periods,
            SupplyLinks = supplies?.Select(link => new SupplyLink(link.Constructor, link.Supplier, link.EngineName, link.SupplyType)).ToArray(),
            Layouts = data.Layouts,
            RaceAssignments = data.RaceAssignments,
            RaceDates = raceDates,
            RegulationDimensionIds = data.DimensionIds,
            EraDimensionIds = data.EraDimensionIds,
            RegulationCatalog = RuleCatalog.ToSpecs(data.Catalog),
            Rules = career?.RulesSource ?? RulesSource.Historical,
            Fatality = career?.FatalityLevel ?? FatalityLevel.Off,
            Facilities = data.Facilities,
            TeamCountries = TeamCountriesOf(data.Founders),
        };
    }

    private static IReadOnlyDictionary<string, string> TeamCountriesOf(FoundersFile founders)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var organization in founders.Organizations)
        {
            if (string.IsNullOrWhiteSpace(organization.Country))
            {
                continue;
            }

            var country = organization.Country.Trim().ToUpperInvariant();
            map[organization.OrganizationId] = country;
            foreach (var entry in organization.ConstructorEntries)
            {
                map[entry.ConstructorId] = country;
            }
        }

        return map;
    }
}
