using Paddock.Data.Authored;
using Paddock.Domain.Time;
using Paddock.Domain.World;

namespace Paddock.Tests.World;

public class LineageChainTests
{
    [Fact]
    public void AuthoredTyrrellLineageRunsThroughToMercedes()
    {
        var data = AuthoredDataLoader.Load(Path.Combine(RepoPaths.Root(), "data"));
        var chain = TeamLineage.Chain("tyrrell-mercedes", data.LineageSpans);

        Assert.Equal(
            ["tyrrell", "bar", "honda", "brawn", "mercedes"],
            chain.Select(step => step.ConstructorId).ToArray());
        Assert.Equal("tyrrell-mercedes", data.LineageOf("tyrrell", 1975));
        Assert.Equal("tyrrell-mercedes", data.LineageOf("mercedes", 2015));
    }

    [Fact]
    public void ChainKeepsInputOrderWhenSeasonsTie()
    {
        IReadOnlyList<LineageSpan> spans =
        [
            new("jordan-aston-martin", "spyker_mf1", 2006, 2006),
            new("jordan-aston-martin", "mf1", 2006, 2006),
            new("other", "alfa", 1950, 1951),
            new("jordan-aston-martin", "spyker", 2007, 2007),
        ];

        var chain = TeamLineage.Chain("jordan-aston-martin", spans);

        Assert.Equal(["spyker_mf1", "mf1", "spyker"], chain.Select(step => step.ConstructorId).ToArray());
        Assert.Empty(TeamLineage.Chain("missing", spans));
    }

    [Fact]
    public void OrganizationLinksWalkTheTyrrellChainFromEitherEnd()
    {
        var world = WorldState.At(GameDate.SeasonStart(1970));
        (world, var tyrrell) = world.AddOrganization(Team("tyrrell", "Tyrrell", 1970));
        (world, var bar) = world.AddOrganization(Team("bar", "BAR", 1999));
        (world, var honda) = world.AddOrganization(Team("honda", "Honda", 2006));
        (world, var brawn) = world.AddOrganization(Team("brawn", "Brawn", 2009));
        (world, var mercedes) = world.AddOrganization(Team("mercedes", "Mercedes", 2010));

        world = world.LinkLineage(tyrrell, bar, new GameDate(1999, 1, 1), new GameDate(2005, 12, 31));
        world = world.LinkLineage(bar, honda, new GameDate(2006, 1, 1), new GameDate(2008, 12, 31));
        world = world.LinkLineage(honda, brawn, new GameDate(2009, 1, 1), new GameDate(2009, 12, 31));
        world = world.LinkLineage(brawn, mercedes, new GameDate(2010, 1, 1), null);

        var expected = new[] { tyrrell, bar, honda, brawn, mercedes };
        Assert.Equal(expected, world.LineageChain(tyrrell));
        Assert.Equal(expected, world.LineageChain(mercedes));
        Assert.Equal(expected, world.LineageChain(bar));
        Assert.Equal("Mercedes", world.GetOrganization(mercedes).NameOn(new GameDate(2015, 6, 1)));
    }

    private static OrganizationSpec Team(string id, string name, int year) => new(
        OrganizationKind.Team,
        true,
        id,
        new GameDate(year, 1, 1),
        null,
        0,
        [new OrganizationNameSpan(name, new GameDate(year, 1, 1), null)]);
}
