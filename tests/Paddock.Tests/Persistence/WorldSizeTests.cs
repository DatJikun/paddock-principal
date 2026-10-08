using System.Diagnostics;
using System.Globalization;
using Paddock.Domain.People;
using Paddock.Domain.Time;
using Paddock.Domain.World;
using Paddock.Persistence;
using Xunit.Abstractions;

namespace Paddock.Tests.Persistence;

/// <summary>
/// Size and time sanity for the world tables (T19). The bounds are ESTIMATES: loose ceilings picked to catch an
/// order-of-magnitude regression (a blob per row, a missing transaction, a quadratic load), not calibrated targets.
/// They must be re-measured on the CI machines before anyone treats them as a budget. The TECH §6.2 goal for a whole
/// 76-season save (under about 50 MB) is verified in SimRunner, not here.
/// </summary>
public class WorldSizeTests : IDisposable
{
    public const int Persons = 3_000;
    public const int Organizations = 200;
    public const int Contracts = 6_000;
    public const int Beliefs = 1_000;

    /// <summary>ESTIMATE: file size ceiling for the world above, in bytes (5 MiB). One local run measured 2.46 MiB.</summary>
    public const long MaxFileBytes = 5L * 1024 * 1024;

    /// <summary>
    /// Timed rounds of (StateHash, SaveWorld, LoadWorld) on the same world. Each round gives one ratio per term; the check keeps the smallest.
    /// </summary>
    public const int Rounds = 3;

    /// <summary>
    /// ESTIMATE: ceiling for SaveWorld as a multiple of one StateHash of the same world, in the same round. Local runs on a
    /// 4-core machine: 6 runs with the machine already busy gave a smallest save-to-hash ratio of 2.7 to 3.9 (no round above 9.1);
    /// 6 runs with six busy-loop processes added gave 2.2 to 5.2, with one round at 13.1. The check therefore keeps the smallest
    /// round, and the 10x ceiling leaves room for that noise. An order-of-magnitude slowdown lifts the ratio to about 30 (ESTIMATE).
    /// </summary>
    public const double MaxSaveToHashRatio = 10;

    /// <summary>
    /// ESTIMATE: ceiling for LoadWorld as a multiple of one StateHash of the same world, in the same round. Local runs gave a
    /// smallest load-to-hash ratio of 1.2 to 2.3 (no round above 6.6) when the machine was busy, and 0.6 to 3.2 with six busy-loop
    /// processes added. The 10x ceiling leaves room for that noise; an order-of-magnitude slowdown lifts the ratio to about 20 (ESTIMATE).
    /// </summary>
    public const double MaxLoadToHashRatio = 10;

    private readonly ITestOutputHelper _output;
    private readonly string _directory = Directory.CreateTempSubdirectory("paddock-size-").FullName;

    public WorldSizeTests(ITestOutputHelper output)
    {
        _output = output;
    }

    public void Dispose()
    {
        Directory.Delete(_directory, recursive: true);
    }

    [Fact]
    public void ABigWorldStaysUnderTheDocumentedSizeAndTimeBounds()
    {
        var world = BigWorld();
        Assert.Equal(Persons, world.Persons.Count);
        Assert.Equal(Organizations, world.Organizations.Count);
        Assert.Equal(Contracts, world.Contracts.Count);

        var path = Path.Combine(_directory, "big.paddock");
        WorldState? loaded = null;
        var saveRatios = new List<double>(Rounds);
        var loadRatios = new List<double>(Rounds);
        using (var save = SaveFile.Create(path, WorldFixtures.Meta()))
        {
            var repository = new WorldRepository(save);
            // One untimed save and load first, so that first-use costs (JIT, SQLite statement setup) stay out of the rounds.
            repository.SaveWorld(world, world.CurrentDate);
            loaded = repository.LoadWorld();
            for (var round = 0; round < Rounds; round++)
            {
                var hash = Milliseconds(() => world.StateHash());
                var saved = Milliseconds(() => repository.SaveWorld(world, world.CurrentDate));
                var read = Milliseconds(() => loaded = repository.LoadWorld());
                saveRatios.Add(saved / hash);
                loadRatios.Add(read / hash);
            }
        }

        var saveRatio = saveRatios.Min();
        var loadRatio = loadRatios.Min();
        var bytes = new FileInfo(path).Length;
        _output.WriteLine(string.Create(
            CultureInfo.InvariantCulture,
            $"{Persons} persons, {Organizations} organizations, {Contracts} contracts, {Beliefs} beliefs: {bytes / 1024.0 / 1024.0:F2} MiB, save/hash {FormatRatios(saveRatios)} (min {saveRatio:F2}), load/hash {FormatRatios(loadRatios)} (min {loadRatio:F2})"));

        Assert.Equal(world.StateHash(), loaded!.StateHash());
        Assert.True(bytes < MaxFileBytes, $"The save is {bytes} bytes, above the {MaxFileBytes} byte estimate.");
        Assert.True(saveRatio < MaxSaveToHashRatio, $"Saving took {saveRatio:F1}x a state hash, above the {MaxSaveToHashRatio} estimate.");
        Assert.True(loadRatio < MaxLoadToHashRatio, $"Loading took {loadRatio:F1}x a state hash, above the {MaxLoadToHashRatio} estimate.");
    }

    private static double Milliseconds(Action action)
    {
        var start = Stopwatch.GetTimestamp();
        action();
        return Stopwatch.GetElapsedTime(start).TotalMilliseconds;
    }

    private static string FormatRatios(IEnumerable<double> ratios) =>
        string.Join(" ", ratios.Select(ratio => ratio.ToString("F2", CultureInfo.InvariantCulture)));

    /// <summary>
    /// SYNTHETIC: ids, names, dates, salaries and attribute values are made up to fill the tables. Built through
    /// <see cref="WorldState.Restore"/> because adding 9 000 entities one at a time copies the state each time.
    /// </summary>
    private static WorldState BigWorld()
    {
        var people = new List<Person>(Persons);
        var issued = new List<string>();
        for (var i = 1; i <= Persons; i++)
        {
            var real = i % 5 == 0;
            var id = real ? PersonId.Real("synthetic_person_" + i.ToString(CultureInfo.InvariantCulture)) : PersonId.Generated(i);
            issued.Add(id.Value);
            people.Add(new Person(
                id,
                "Given" + i.ToString(CultureInfo.InvariantCulture),
                "Family" + i.ToString(CultureInfo.InvariantCulture),
                new GameDate(1930 + (i % 60), 1 + (i % 12), 1 + (i % 28)),
                i % 2 == 0 ? "GBR" : "ITA",
                real,
                [PersonRole.Driver],
                WorldFixtures.DriverTruth(5 + (i % 8), 14 + (i % 6))));
        }

        var teams = new List<Organization>(Organizations);
        for (var i = 1; i <= Organizations; i++)
        {
            var real = i % 4 == 0;
            var id = real ? OrganizationId.Real("synthetic_org_" + i.ToString(CultureInfo.InvariantCulture)) : OrganizationId.Generated(i);
            issued.Add(id.Value);
            teams.Add(new Organization(
                id,
                OrganizationKind.Team,
                real,
                new GameDate(1950, 1, 1),
                null,
                1_000_000 + i,
                [
                    new OrganizationNameSpan("Synthetic Racing " + i.ToString(CultureInfo.InvariantCulture), new GameDate(1950, 1, 1), new GameDate(1979, 12, 31)),
                    new OrganizationNameSpan("Synthetic GP " + i.ToString(CultureInfo.InvariantCulture), new GameDate(1980, 1, 1), null),
                ],
                []));
        }

        var contracts = new List<Contract>(Contracts);
        for (var i = 1; i <= Contracts; i++)
        {
            issued.Add(ContractId.Generated(i).Value);
            var person = people[(i - 1) % Persons];
            var team = teams[(i * 7) % Organizations];
            var second = i > Persons;
            contracts.Add(new Contract(
                ContractId.Generated(i),
                person.Id,
                team.Id,
                ContractRole.Driver((SeatStatus)(i % 4)),
                new GameDate(second ? 1956 : 1955, 1, 1),
                new GameDate(second ? 1957 : 1955, 12, 31),
                50_000 + i,
                true,
                i % 3 == 0 ? new ContractOption(new GameDate(second ? 1956 : 1955, 9, 1), 1) : null,
                i % 5 == 0 ? new ReleaseClause(250_000) : null));
        }

        var beliefs = new List<PersonKnowledge>(Beliefs);
        for (var i = 0; i < Beliefs; i++)
        {
            beliefs.Add(new PersonKnowledge(
                teams[i % Organizations].Id,
                people[i].Id,
                [
                    new KnownAttribute("cornering", new AttributeBand(6, 10)),
                    new KnownAttribute("braking", new AttributeBand(7, 12)),
                    new KnownAttribute("smoothness", new AttributeBand(5, 9)),
                ],
                new AttributeBand(10, 17)));
        }

        var ids = new IdAllocator(Persons + 1, Organizations + 1, Contracts + 1, issued);
        return WorldState.Restore(new GameDate(1955, 1, 1), ids, people, teams, contracts, beliefs);
    }
}
