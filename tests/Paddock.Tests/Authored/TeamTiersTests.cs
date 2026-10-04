using Paddock.Data.Authored;
using Paddock.Domain.Finance;
using Paddock.Domain.World;

namespace Paddock.Tests.Authored;

/// <summary>
/// #160: the authored ESTIMATE of the previous season's order, the stand-in for the Jolpica standings that stay local (PP-041).
/// </summary>
public sealed class TeamTiersTests
{
    private static readonly string DataRoot = Path.Combine(RepoPaths.Root(), "data");

    private static TeamTiersFile File() => TeamTiersLoader.Load(DataRoot);

    [Fact]
    public void TheFileIsAnEstimateAndEveryConstructorIsOneTheTeamsDataKnows()
    {
        var data = AuthoredDataLoader.Load(DataRoot);

        var errors = TeamTiersValidator.Validate(File(), data.ConstructorIds);

        Assert.Empty(errors);
        Assert.Contains("ESTIMATE", File().Notes);
        Assert.All(File().Standings, entry => Assert.Equal("estimate", entry.Confidence));
    }

    [Fact]
    public void A1955StartReadsThe1954OrderAndAnUnlistedTeamOrAStartWithNoPreviousSeasonIsTypical()
    {
        var source = TeamTiersLoader.ToSource(File());

        Assert.Equal(TeamTier.Top, source.TierOf(OrganizationId.Real("mercedes"), 1955));
        Assert.Equal(TeamTier.Typical, source.TierOf(OrganizationId.Real("ferrari"), 1955));
        Assert.Equal(TeamTier.Low, source.TierOf(OrganizationId.Real("gordini"), 1955));
        Assert.Equal(TeamTier.Typical, source.TierOf(OrganizationId.Real("pawl"), 1955));
        Assert.Equal(TeamTier.Typical, source.TierOf(OrganizationId.Real("mercedes"), 1950));
    }

    [Fact]
    public void TheValidatorRefusesAnUnknownConstructorARepeatedPositionAndAFileThatDoesNotSayEstimate()
    {
        var known = new HashSet<string>(["a", "b"], StringComparer.Ordinal);
        var bad = new TeamTiersFile(
            "no hedge",
            [
                new TeamTierEntry(1954, "a", 1, "estimate", "x"),
                new TeamTierEntry(1954, "b", 1, "high", "x"),
                new TeamTierEntry(1954, "ghost", 2, "estimate", "x"),
                new TeamTierEntry(1949, "a", 0, "estimate", "x"),
            ]);

        var codes = TeamTiersValidator.Validate(bad, known).Select(error => error.Code).ToHashSet();

        Assert.Contains(TeamTiersValidator.NotEstimate, codes);
        Assert.Contains(TeamTiersValidator.UnknownConstructor, codes);
        Assert.Contains(TeamTiersValidator.Duplicate, codes);
        Assert.Contains(TeamTiersValidator.BadNumber, codes);
    }
}
