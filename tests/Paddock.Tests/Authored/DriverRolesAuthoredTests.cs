using Paddock.Data.Authored;
using Paddock.Domain.World;
using Paddock.Tests.Career;

namespace Paddock.Tests.Authored;

/// <summary>The authored first and second drivers (#325): one editable file, read with the rest and checked by validate-authored.</summary>
public class DriverRolesAuthoredTests
{
    private static string File(params string[] entries) =>
        "{ \"notes\": \"fixture\", \"roles\": [" + string.Join(", ", entries) + "] }";

    private static string Entry(int year, string driver, string role) =>
        "{ \"year\": " + year + ", \"driver_id\": \"" + driver + "\", \"role\": \"" + role + "\" }";

    private static IReadOnlyList<AuthoredDataError> Validate(string roles)
    {
        using var fixture = new TempAuthoredData(driverRoles: roles);
        return AuthoredDataValidator.Validate(AuthoredDataLoader.Load(fixture.Root));
    }

    [Fact]
    public void AFixtureWithNoFileHasNoAuthoredRolesAndNothingToCheck()
    {
        using var fixture = new TempAuthoredData();
        var data = AuthoredDataLoader.Load(fixture.Root);

        Assert.Null(data.DriverRolesFile);
        Assert.Equal(0, data.DriverRoles.Count);
        Assert.Null(data.DriverRoles.RoleOf(2010, "alonso"));
        Assert.Empty(AuthoredDataValidator.Validate(data));
    }

    [Fact]
    public void TheRepositoryFileIsValidAndStartsEmpty()
    {
        var data = AuthoredDataLoader.Load(CareerKit.DataRoot);

        Assert.NotNull(data.DriverRolesFile);
        Assert.Empty(data.DriverRolesFile!.Roles);
        Assert.Equal(0, data.DriverRoles.Count);
        Assert.DoesNotContain(AuthoredDataValidator.Validate(data), error => error.Code.StartsWith("driver-role", StringComparison.Ordinal));
    }

    [Fact]
    public void AValidFileHasNoErrorsAndIsReadByYearAndDriver()
    {
        using var fixture = new TempAuthoredData(driverRoles: File(Entry(2010, "alpha", "first"), Entry(2010, "beta", "second"), Entry(2011, "alpha", "second")));
        var data = AuthoredDataLoader.Load(fixture.Root);

        Assert.Empty(AuthoredDataValidator.Validate(data));
        Assert.Equal(SeatStatus.NumberOne, data.DriverRoles.RoleOf(2010, "alpha"));
        Assert.Equal(SeatStatus.NumberTwo, data.DriverRoles.RoleOf(2010, "beta"));
        Assert.Equal(SeatStatus.NumberTwo, data.DriverRoles.RoleOf(2011, "alpha"));
        Assert.Null(data.DriverRoles.RoleOf(2012, "alpha"));
    }

    [Fact]
    public void AnUnknownRoleWordIsReported()
    {
        var error = Assert.Single(Validate(File(Entry(2010, "alpha", "captain"))));

        Assert.Equal(AuthoredDataValidator.DriverRoleUnknown, error.Code);
        Assert.Contains("'captain'", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AYearOutsideTheCoveredWindowIsReported()
    {
        var error = Assert.Single(Validate(File(Entry(1949, "alpha", "first"))));

        Assert.Equal(AuthoredDataValidator.DriverRoleYear, error.Code);
    }

    [Fact]
    public void AMissingDriverIdIsReported()
    {
        var error = Assert.Single(Validate(File(Entry(2010, " ", "first"))));

        Assert.Equal(AuthoredDataValidator.DriverRoleDriver, error.Code);
    }

    [Fact]
    public void ADriverWithTwoRolesInOneYearIsReported()
    {
        var error = Assert.Single(Validate(File(Entry(2010, "alpha", "first"), Entry(2010, "alpha", "second"))));

        Assert.Equal(AuthoredDataValidator.DriverRoleDuplicate, error.Code);
        Assert.Contains("'alpha'", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnUnknownPropertyOfTheFileFailsTheLoad()
    {
        using var fixture = new TempAuthoredData(driverRoles: "{ \"notes\": \"x\", \"roles\": [], \"extra\": 1 }");

        Assert.Throws<AuthoredDataLoadException>(() => AuthoredDataLoader.Load(fixture.Root));
    }
}
