using Paddock.Application.Career;
using Paddock.Data.Authored;
using Paddock.Domain.Contracts;

namespace Paddock.SimRunner;

/// <summary>
/// Loads the data a career run reads from <c>data/authored</c>: the era periods, the fictional sponsors, and the ESTIMATE of the
/// previous season's constructors' order (the Jolpica standings stay local, PP-041). The same inputs for <c>run</c>, for
/// <c>run --resume</c> and for the tests, so they all see one economy.
/// </summary>
public static class CareerInputsLoader
{
    public static CareerInputs Load(string dataRoot, AuthoredData data)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataRoot);
        ArgumentNullException.ThrowIfNull(data);
        var sponsors = SponsorsLoader.ToCatalog(SponsorsLoader.Load(dataRoot));
        var tiers = TeamTiersLoader.ToSource(TeamTiersLoader.Load(dataRoot));
        return CareerInputs.From(data.EraPeriods, sponsors, new EraPayBenchmark(data.EraSetFor), tiers);
    }
}
