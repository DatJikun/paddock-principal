using Paddock.Application.Career;
using Paddock.Data.Authored;
using Paddock.Data.Historical;
using Paddock.Data.World;
using Paddock.Domain.Career;
using Paddock.Domain.Contracts;
using Paddock.Domain.Supply;
using Paddock.Domain.World;

namespace Paddock.Career;

/// <summary>
/// Loads the data a career run reads from <c>data/authored</c>: the era periods, the fictional sponsors, and the ESTIMATE of the
/// previous season's constructors' order (the Jolpica standings stay local, PP-041). The same inputs for <c>run</c>, for
/// <c>run --resume</c> and for the tests, so they all see one economy. With <c>starting</c> (#271) the car strengths and the tiers
/// are the ones the world was built with: the authored files layered over the local starting data of the start year.
/// </summary>
public static class CareerInputsLoader
{
    public static CareerInputs Load(string dataRoot, AuthoredData data, IReadOnlyList<EngineSupplyLink>? supplies = null, CareerConfig? career = null, RaceDateBook? raceDates = null, StartingSources? starting = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataRoot);
        ArgumentNullException.ThrowIfNull(data);
        var sponsors = SponsorsLoader.ToCatalog(SponsorsLoader.Load(dataRoot), localMarket: true);
        var tiers = starting?.Tiers ?? TeamTiersLoader.ToSource(TeamTiersLoader.Load(dataRoot));
        var inputs = CareerInputs.From(data.EraPeriods, sponsors, new EraPayBenchmark(data.EraSetFor), tiers);
        return new CareerInputs
        {
            Eras = inputs.Eras,
            SponsorEras = inputs.SponsorEras,
            Sponsors = inputs.Sponsors,
            Pay = inputs.Pay,
            Tiers = inputs.Tiers,
            CarStrength = starting is null ? data.CarStrength : starting.CarStrength,
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
            VoteMode = career?.VoteMode ?? VoteMode.OneVoteEach,
            BannedRules = data.BannedRules,
            Fatality = career?.FatalityLevel ?? FatalityLevel.Off,
            Facilities = data.Facilities,
            TeamCountries = TeamCountriesOf(data.Founders),
            TrackGeometry = TrackGeometryCatalog.Create(data),
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
