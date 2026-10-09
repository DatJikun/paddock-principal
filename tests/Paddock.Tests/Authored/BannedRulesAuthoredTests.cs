using Paddock.Data.Authored;
using Paddock.Domain.Racing;
using Paddock.Tests.Career;

namespace Paddock.Tests.Authored;

/// <summary>The banned list (#275, owner decision of round 2): authored per series with a default, loaded with the rest and checked by validate-authored.</summary>
public class BannedRulesAuthoredTests
{
    private const string Dormant = """["cash_bonus_last_place", "cash_bonus_promoted_team", "ers_charge_by_position"]""";

    private static string File(string defaultList, string series = "{}") =>
        "{ \"default\": " + defaultList + ", \"series\": " + series + " }";

    private static IReadOnlyList<AuthoredDataError> Validate(string banned)
    {
        using var fixture = new TempAuthoredData(bannedRules: banned);
        return AuthoredDataValidator.Validate(AuthoredDataLoader.Load(fixture.Root));
    }

    [Fact]
    public void AFixtureWithNoFileBansNothingAndHasNothingToCheck()
    {
        using var fixture = new TempAuthoredData();
        var data = AuthoredDataLoader.Load(fixture.Root);

        Assert.Null(data.BannedRulesFile);
        Assert.Empty(data.BannedRules.For(SeriesIds.WorldChampionship));
        Assert.False(data.BannedRules.IsBanned(SeriesIds.WorldChampionship, DormantRules.ErsChargeByPosition));
        Assert.Empty(AuthoredDataValidator.Validate(data));
    }

    [Fact]
    public void AValidFileWithRulesCalendarEntriesAndDormantMechanicsHasNoErrors()
    {
        var errors = Validate(File("""["cash_bonus_last_place", "cash_bonus_promoted_team", "ers_charge_by_position", "points_scale", "calendar.circuit.test"]"""));

        Assert.Empty(errors);
    }

    [Fact]
    public void ARuleTheGameDoesNotKnowIsReported()
    {
        var errors = Validate(File("""["cash_bonus_last_place", "cash_bonus_promoted_team", "ers_charge_by_position", "not_a_rule"]"""));

        var error = Assert.Single(errors);
        Assert.Equal(AuthoredDataValidator.BannedUnknownRule, error.Code);
        Assert.Contains("'not_a_rule'", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ACalendarEntryOfAnUnknownCircuitIsReported()
    {
        var errors = Validate(File("""["cash_bonus_last_place", "cash_bonus_promoted_team", "ers_charge_by_position", "calendar.circuit.nowhere"]"""));

        Assert.Equal(AuthoredDataValidator.BannedUnknownRule, Assert.Single(errors).Code);
    }

    [Fact]
    public void ARuleListedTwiceInOneListIsReported()
    {
        var errors = Validate(File("""["cash_bonus_last_place", "cash_bonus_promoted_team", "ers_charge_by_position", "points_scale", "points_scale"]"""));

        var error = Assert.Single(errors);
        Assert.Equal(AuthoredDataValidator.BannedDuplicate, error.Code);
        Assert.Contains("'points_scale'", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ASeriesTheGameDoesNotKnowIsReported()
    {
        var errors = Validate(File(Dormant, "{ \"f9\": " + Dormant + " }"));

        var error = Assert.Single(errors);
        Assert.Equal(AuthoredDataValidator.BannedUnknownSeries, error.Code);
        Assert.Contains("'f9'", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ADormantMechanicThatIsNotBannedInEverySeriesIsReported()
    {
        // The default misses one mechanic, and the series list of f1 replaces the default and misses two.
        var errors = Validate(File("""["cash_bonus_last_place", "ers_charge_by_position"]""", "{ \"f1\": [\"ers_charge_by_position\"] }"));

        Assert.All(errors, error => Assert.Equal(AuthoredDataValidator.BannedMissingDormant, error.Code));
        Assert.Equal(2, errors.Count);
        Assert.Contains(errors, error => error.Message.Contains("'cash_bonus_last_place'", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Message.Contains("'cash_bonus_promoted_team'", StringComparison.Ordinal));
    }

    [Fact]
    public void AnUnknownPropertyOfTheFileIsALoadError()
    {
        using var fixture = new TempAuthoredData(bannedRules: """{ "default": [], "series": {}, "extra": 1 }""");

        Assert.Throws<AuthoredDataLoadException>(() => AuthoredDataLoader.Load(fixture.Root));
    }

    [Fact]
    public void TheShippedFileBansTheCatchUpMechanicAndBothCashBonusesInEverySeriesAndPassesValidation()
    {
        var data = CareerKit.Data;

        Assert.Empty(AuthoredDataValidator.Validate(data));
        foreach (var series in SeriesIds.All)
        {
            Assert.True(data.BannedRules.IsBanned(series, DormantRules.ErsChargeByPosition), series);
            Assert.True(data.BannedRules.IsBanned(series, DormantRules.CashBonusLastPlace), series);
            Assert.True(data.BannedRules.IsBanned(series, DormantRules.CashBonusPromotedTeam), series);
        }

        // Nothing else is banned yet, and the world championship uses the default list.
        Assert.Equal(DormantRules.All, data.BannedRules.For(SeriesIds.WorldChampionship));
        Assert.False(data.BannedRules.HasOwnList(SeriesIds.WorldChampionship));
    }
}
