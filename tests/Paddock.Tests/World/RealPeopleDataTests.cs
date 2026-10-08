using Paddock.Data.Authored;
using Paddock.Data.Historical;

namespace Paddock.Tests.World;

/// <summary>#265: the authored full names and genders of real people, and the plain career counts read from local data.</summary>
public sealed class RealPeopleDataTests
{
    private static string DataRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "data", "authored")))
        {
            directory = directory.Parent;
        }

        return Path.Combine(directory!.FullName, "data");
    }

    [Fact]
    public void TheAuthoredFileGivesFangioHisFullNameAndMarksTheWomen()
    {
        var overrides = RealPersonOverridesLoader.Load(DataRoot());

        Assert.Equal("Juan Manuel", overrides["fangio"].GivenName);
        Assert.Equal("Fangio", overrides["fangio"].FamilyName);
        Assert.False(overrides["fangio"].Female);
        Assert.True(overrides["filippis"].Female);
        Assert.Null(overrides["lombardi"].GivenName);
    }

    [Fact]
    public void ADataFolderWithoutTheFileChangesNothing()
    {
        var empty = Directory.CreateTempSubdirectory("paddock-noover-").FullName;
        try
        {
            Assert.Empty(RealPersonOverridesLoader.Load(empty));
        }
        finally
        {
            Directory.Delete(empty, recursive: true);
        }
    }

    [Fact]
    public void PlainSeasonCountsComeFromTheResultsAndSkipTheIndianapolis500()
    {
        const string races = """
            {"schemaVersion":1,"races":[
              {"season":1953,"round":1,"name":"A","circuitId":"a","date":"1953-01-01","time":null,"url":"","isIndianapolis500":false},
              {"season":1953,"round":2,"name":"B","circuitId":"indianapolis","date":"1953-05-30","time":null,"url":"","isIndianapolis500":true},
              {"season":1953,"round":3,"name":"C","circuitId":"c","date":"1953-07-01","time":null,"url":"","isIndianapolis500":false},
              {"season":1955,"round":1,"name":"D","circuitId":"d","date":"1955-01-01","time":null,"url":"","isIndianapolis500":false}]}
            """;
        const string results = """
            {"schemaVersion":1,"results":[
              {"season":1953,"round":1,"driverId":"x","constructorId":"alfa","carNumber":"1","position":1,"positionText":"1","status":"Finished","points":9,"grid":1,"laps":10,"time":null,"timeMillis":null,"isClassified":true,"isDisqualified":false,"isSharedDrive":false},
              {"season":1953,"round":2,"driverId":"x","constructorId":"alfa","carNumber":"1","position":1,"positionText":"1","status":"Finished","points":9,"grid":1,"laps":10,"time":null,"timeMillis":null,"isClassified":true,"isDisqualified":false,"isSharedDrive":false},
              {"season":1953,"round":3,"driverId":"x","constructorId":"alfa","carNumber":"1","position":3,"positionText":"3","status":"Finished","points":4,"grid":1,"laps":10,"time":null,"timeMillis":null,"isClassified":true,"isDisqualified":false,"isSharedDrive":false},
              {"season":1955,"round":1,"driverId":"x","constructorId":"alfa","carNumber":"1","position":2,"positionText":"2","status":"Finished","points":6,"grid":1,"laps":10,"time":null,"timeMillis":null,"isClassified":true,"isDisqualified":false,"isSharedDrive":false}]}
            """;
        const string constructors = """{"schemaVersion":1,"constructors":[{"constructorId":"alfa","name":"Alfa Romeo","nationality":"Italian","url":""}]}""";

        var history = DriverCareerHistory.Parse(results, races, constructors);

        var line = Assert.Single(history.Before("x", 1955));
        Assert.Equal(1953, line.Season);
        Assert.Equal("Alfa Romeo", line.ConstructorName);
        Assert.Equal((2, 1, 2, 0, 1), (line.Starts, line.Wins, line.Podiums, line.Retirements, line.Best ?? 0));
        Assert.Empty(history.Before("nobody", 1955));
        Assert.Empty(DriverCareerHistory.Empty.Before("x", 1955));
    }
}
