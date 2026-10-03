using Paddock.Application.Objectives;
using Paddock.Domain.Objectives;
using Paddock.Domain.Time;
using Paddock.Domain.World;
using AccessContext = Paddock.Application.Access.AccessContext;
using AccessManagerId = Paddock.Application.Access.ManagerId;

namespace Paddock.Tests.Objectives;

/// <summary>The STATE, WHY and FORECAST of an objective, as its owner reads it. Fixture numbers are SYNTHETIC.</summary>
public class ObjectiveViewTests
{
    private static readonly OrganizationId Alpha = OrganizationId.Real("team_alpha");

    private static readonly OrganizationId Bravo = OrganizationId.Real("team_bravo");

    private static readonly OrganizationId Board = OrganizationId.Real("board_alpha");

    private static readonly GameDate Start = new(1955, 1, 1);

    private static readonly GameDate Deadline = new(1955, 12, 31);

    [Fact]
    public void AnOpenObjectiveShowsStateWhyAndForecast()
    {
        var section = Section((Alpha, new PodiumsAtLeast(4), 0m));
        var query = new ObjectiveQuery(Facts(alphaPodiums: 2), Managers());

        var view = query.View(Manager("mgr-anna"), section, Start.AddDays(182));

        var item = Assert.Single(view.Items);
        Assert.Equal("obj:1", item.Id);
        Assert.Equal(ObjectiveStatus.Open, item.State.Status);
        Assert.Equal(2m, item.State.Current);
        Assert.Equal(4m, item.State.Target);
        Assert.Equal(182, item.State.DaysLeft);
        Assert.Null(item.State.Holds);
        Assert.Equal(Board.Value, item.Why.GrantorId);
        Assert.Equal("objective.test.reason", item.Why.Reason.Key);
        Assert.NotNull(item.Forecast);
        Assert.Equal(ForecastKind.OnTrack, item.Forecast!.Kind);
        Assert.Equal(4m, item.Forecast.Projected);
        Assert.Equal(ObjectiveKeys.ForecastOnTrack, item.Forecast.Message.Key);
        Assert.Equal(ObjectiveKeys.PredicatePodiumsAtLeast, item.Requirement.Key);
        Assert.Equal("4", item.Requirement.Parameters["target"]);
        Assert.Equal(new DateOnly(1955, 12, 31), item.Deadline);
    }

    [Fact]
    public void TheConsequencesAreShownUpFront()
    {
        var section = Section((Alpha, new PodiumsAtLeast(4), 0m));

        var item = Assert.Single(new ObjectiveQuery(Facts(), Managers()).View(Manager("mgr-anna"), section, Start).Items);

        Assert.Equal("objective.test.met", item.OnMet.Key);
        Assert.Equal("objective.test.failed", item.OnFailed.Key);
        Assert.Equal("2", item.OnFailed.Parameters["size"]);
    }

    [Fact]
    public void ALineupObjectiveShowsWhetherItHoldsAndHasNothingToProject()
    {
        var section = Section((Alpha, new DriverNationalityInLineup("GBR"), null));
        var query = new ObjectiveQuery(Facts(alphaNationalities: ["GBR"]), Managers());

        var item = Assert.Single(query.View(Manager("mgr-anna"), section, Start.AddDays(10)).Items);

        Assert.True(item.State.Holds);
        Assert.Null(item.State.Current);
        Assert.Equal(ForecastKind.NotProjectable, item.Forecast!.Kind);
        Assert.Equal(ObjectiveKeys.PredicateDriverNationalityInLineup, item.Requirement.Key);
        Assert.Equal("GBR", item.Requirement.Parameters["nationality"]);
    }

    [Fact]
    public void AFigureTheOwningSystemCannotGiveShowsAsUnknown()
    {
        var section = Section((Alpha, new CashAtLeast(500), 100m));

        var item = Assert.Single(new ObjectiveQuery(new ObjectiveFactRegistry(), Managers()).View(Manager("mgr-anna"), section, Start.AddDays(10)).Items);

        Assert.Null(item.State.Current);
        Assert.Equal(ForecastKind.Unknown, item.Forecast!.Kind);
    }

    [Fact]
    public void ASettledObjectiveHasNoForecast()
    {
        var section = Section((Alpha, new PodiumsAtLeast(2), 0m)).Settle("obj:1", met: false, Deadline);

        var item = Assert.Single(new ObjectiveQuery(Facts(alphaPodiums: 1), Managers()).View(Manager("mgr-anna"), section, Deadline).Items);

        Assert.Equal(ObjectiveStatus.Failed, item.State.Status);
        Assert.Null(item.Forecast);
        Assert.Equal(0, item.State.DaysLeft);
    }

    [Fact]
    public void AManagerSeesOnlyTheObjectivesOfTheirOwnOrganization()
    {
        var section = Section(
            (Alpha, new PodiumsAtLeast(2), 0m),
            (Bravo, new PodiumsAtLeast(3), 0m),
            (Alpha, new PointsAtLeast(10m), 0m));
        var query = new ObjectiveQuery(Facts(), Managers());

        var anna = query.View(Manager("mgr-anna"), section, Start);
        var bram = query.View(Manager("mgr-bram"), section, Start);
        var drifter = query.View(Manager("mgr-nobody"), section, Start);
        var developer = query.View(AccessContext.Developer, section, Start);

        Assert.Equal(["obj:1", "obj:3"], anna.Items.Select(item => item.Id));
        Assert.Equal(["obj:2"], bram.Items.Select(item => item.Id));
        Assert.Empty(drifter.Items);
        Assert.Equal(["obj:1", "obj:2", "obj:3"], developer.Items.Select(item => item.Id));
    }

    [Fact]
    public void TheQueryChangesNothing()
    {
        var section = Section((Alpha, new PodiumsAtLeast(2), 0m));
        var world = WorldState.At(Start).WithSection(section);
        var before = world.StateHash();

        new ObjectiveQuery(Facts(alphaPodiums: 1), Managers()).View(AccessContext.Developer, section, Start.AddDays(40));

        Assert.Equal(before, world.StateHash());
    }

    private static AccessContext Manager(string id) => AccessContext.ForManager(new AccessManagerId(id));

    private static ObjectivesSection Section(params (OrganizationId Owner, ObjectivePredicate Predicate, decimal? Baseline)[] objectives)
    {
        var section = ObjectivesSection.Empty;
        foreach (var (owner, predicate, baseline) in objectives)
        {
            (section, _) = section.Add(
                new ObjectiveDraft(
                    owner,
                    Board,
                    "objective.test.kind",
                    "objective.test.reason",
                    predicate,
                    baseline,
                    Deadline,
                    new ObjectiveEffect("objective.test.met"),
                    new ObjectiveEffect("objective.test.failed", [new("size", "2")])),
                Start);
        }

        return section;
    }

    private static ObjectiveFactRegistry Facts(decimal? alphaPodiums = null, string[]? alphaNationalities = null)
    {
        var registry = new ObjectiveFactRegistry();
        registry.RegisterNumber(ObjectiveFactKeys.SeasonPodiums, owner => owner == Alpha ? alphaPodiums : null);
        registry.RegisterFlag(
            ObjectiveFactKeys.LineupDriverNationality,
            (owner, value) => owner == Alpha ? alphaNationalities?.Contains(value) ?? false : null);
        return registry;
    }

    private static Organizations Managers() => new();

    private sealed class Organizations : IManagerOrganizations
    {
        public OrganizationId? OrganizationOf(string managerId) => managerId switch
        {
            "mgr-anna" => Alpha,
            "mgr-bram" => Bravo,
            _ => null,
        };
    }
}
