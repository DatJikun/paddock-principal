using Paddock.Application.Supply;
using Paddock.Domain.Supply;
using Paddock.Domain.Time;

namespace Paddock.Tests.Supply;

/// <summary>
/// PP-064: each supplier's engine moves a little at random every season, from a child stream of the master seed. The steps are
/// ESTIMATES (<see cref="SupplyEstimates.PowerDriftStep"/>, <see cref="SupplyEstimates.ReliabilityDriftStep"/>). Tests do not read data/cache.
/// </summary>
public class EngineDriftTests
{
    private const int Anchor = 1955;

    private static readonly GameDate Opening = new(Anchor, 1, 1);

    [Fact]
    public void TheSameSeedGivesTheSameEngines()
    {
        var left = new EstimateSupplierProfiles(Anchor, null, 99);
        var right = new EstimateSupplierProfiles(Anchor, null, 99);

        for (var season = Anchor; season <= Anchor + 15; season++)
        {
            Assert.Equal(left.EngineOf(SupplyKit.Acme, season), right.EngineOf(SupplyKit.Acme, season));
            Assert.Equal(left.EngineOf(SupplyKit.Bolt, season), right.EngineOf(SupplyKit.Bolt, season));
        }
    }

    [Fact]
    public void DifferentSeedsGiveDifferentEngines()
    {
        var left = new EstimateSupplierProfiles(Anchor, null, 1);
        var right = new EstimateSupplierProfiles(Anchor, null, 2);

        Assert.Equal(left.EngineOf(SupplyKit.Acme, Anchor), right.EngineOf(SupplyKit.Acme, Anchor));
        Assert.NotEqual(left.EngineOf(SupplyKit.Acme, Anchor + 5).Power, right.EngineOf(SupplyKit.Acme, Anchor + 5).Power);
    }

    [Fact]
    public void SuppliersDriftApart()
    {
        var profiles = new EstimateSupplierProfiles(Anchor, null, 5);

        var steps = new[] { SupplyKit.Acme, SupplyKit.Bolt }.Select(supplier => Enumerable.Range(Anchor + 1, 8).Select(season => profiles.PowerStepOf(supplier, season)!.Value).ToArray()).ToArray();

        Assert.NotEqual(steps[0], steps[1]);
    }

    [Fact]
    public void NoSeedMeansNoDrift()
    {
        var profiles = new EstimateSupplierProfiles(Anchor);

        Assert.Null(profiles.PowerStepOf(SupplyKit.Acme, Anchor + 3));
        var gain = profiles.EngineOf(SupplyKit.Acme, Anchor + 3).Power - profiles.EngineOf(SupplyKit.Acme, Anchor).Power;
        Assert.Equal(SupplyEstimates.ProgressPerSeason * 3, gain, 0.01);
    }

    [Fact]
    public void TheAnchorSeasonIsTheBaselineAndHasNoStep()
    {
        var drifting = new EstimateSupplierProfiles(Anchor, null, 77);
        var plain = new EstimateSupplierProfiles(Anchor);

        Assert.Equal(plain.EngineOf(SupplyKit.Acme, Anchor), drifting.EngineOf(SupplyKit.Acme, Anchor));
        Assert.Null(drifting.PowerStepOf(SupplyKit.Acme, Anchor));
        Assert.Null(drifting.PowerStepOf(SupplyKit.Acme, Anchor - 2));
    }

    [Theory]
    [InlineData(1UL)]
    [InlineData(42UL)]
    [InlineData(123456789UL)]
    public void NoYearlyStepIsLargerThanTheConstant(ulong seed)
    {
        var profiles = new EstimateSupplierProfiles(Anchor, null, seed);
        var plain = new EstimateSupplierProfiles(Anchor);

        foreach (var supplier in new[] { SupplyKit.Acme, SupplyKit.Bolt })
        {
            for (var season = Anchor + 1; season <= Anchor + 10; season++)
            {
                var step = profiles.PowerStepOf(supplier, season)!.Value;
                Assert.InRange(step, -SupplyEstimates.PowerDriftStep, SupplyEstimates.PowerDriftStep);

                // The change of the engine since last season is the general progress plus this season's step (ratings stay inside 0 to 100 here).
                var now = profiles.EngineOf(supplier, season);
                var before = profiles.EngineOf(supplier, season - 1);
                Assert.Equal(SupplyEstimates.ProgressPerSeason + step, now.Power - before.Power, 0.01);
                var reliabilityStep = now.Reliability - before.Reliability - SupplyEstimates.ProgressPerSeason;
                Assert.InRange(reliabilityStep, -SupplyEstimates.ReliabilityDriftStep - 0.01, SupplyEstimates.ReliabilityDriftStep + 0.01);
                Assert.Equal(plain.EngineOf(supplier, season).Efficiency, now.Efficiency);
            }
        }
    }

    [Fact]
    public void StepsAddUpFromTheAnchor()
    {
        var profiles = new EstimateSupplierProfiles(Anchor, null, 11);
        var plain = new EstimateSupplierProfiles(Anchor);

        var total = Enumerable.Range(Anchor + 1, 6).Sum(season => profiles.PowerStepOf(SupplyKit.Bolt, season)!.Value);

        Assert.Equal(total, profiles.EngineOf(SupplyKit.Bolt, Anchor + 6).Power - plain.EngineOf(SupplyKit.Bolt, Anchor + 6).Power, 0.01);
    }

    [Fact]
    public void AQueryDoesNotDependOnWhatWasAskedBefore()
    {
        var asked = new EstimateSupplierProfiles(Anchor, null, 31);
        for (var season = Anchor + 9; season >= Anchor; season--)
        {
            asked.EngineOf(SupplyKit.Acme, season);
        }

        var fresh = new EstimateSupplierProfiles(Anchor, null, 31);

        Assert.Equal(fresh.EngineOf(SupplyKit.Acme, Anchor + 4), asked.EngineOf(SupplyKit.Acme, Anchor + 4));
        Assert.Equal(fresh.EngineOf(SupplyKit.Bolt, Anchor + 4), new EstimateSupplierProfiles(Anchor, null, 31).EngineOf(SupplyKit.Bolt, Anchor + 4));
    }

    [Fact]
    public void AResumedSaveGivesTheSameEngineNextSeason()
    {
        // A save keeps the master seed and the opening year; the profiles are rebuilt from them on load (SupplyModule.Attach).
        const ulong seed = 2024;
        var before = new EstimateSupplierProfiles(Anchor, null, seed);
        var expectedNextSeason = before.EngineOf(SupplyKit.Acme, Anchor + 2);

        var resumed = new EstimateSupplierProfiles(Anchor, null, seed);

        Assert.Equal(expectedNextSeason, resumed.EngineOf(SupplyKit.Acme, Anchor + 2));
    }

    [Fact]
    public void AHumanTeamGetsOneEngineNoticePerSeasonOnTheFirstDay()
    {
        var kit = new SupplyKit(Opening, seed: 42);
        var price = kit.Floor(SupplyItem.Engine, SupplyKind.Customer, seasons: 4);
        kit.Propose(SupplyKit.Anna, SupplyKit.Alfa, SupplyKit.Acme, price, Opening, seasons: 4);

        kit.Live(Opening, 365 + 366 + 200);
        var notices = EngineNotices(kit, SupplyKit.Anna);

        // A customer gets last season's version: 1956 delivers the 1955 version (the baseline, no step), so the first notice is 1957.
        Assert.Single(notices);
        Assert.Empty(EngineNotices(kit, SupplyKit.Bram));

        kit.Live(Opening.AddDays(365 + 366 + 200), 365);
        Assert.Equal(2, EngineNotices(kit, SupplyKit.Anna).Count);
    }

    [Fact]
    public void TheNoticeNamesAFixedBandNotTheStep()
    {
        var kit = new SupplyKit(Opening, seed: 42);
        var price = kit.Floor(SupplyItem.Engine, SupplyKind.Customer, seasons: 4);
        kit.Propose(SupplyKit.Anna, SupplyKit.Alfa, SupplyKit.Acme, price, Opening, seasons: 4);

        kit.Live(Opening, 365 + 366 + 200);

        var step = kit.Environment.Profiles.PowerStepOf(SupplyKit.Acme, Anchor + 1)!.Value;
        var expected = step >= SupplyEstimates.DriftNoticeBand
            ? SupplyKeys.InboxEngineStrongerSubject
            : step <= -SupplyEstimates.DriftNoticeBand ? SupplyKeys.InboxEngineWeakerSubject : SupplyKeys.InboxEngineSameSubject;
        var notice = Assert.Single(EngineNotices(kit, SupplyKit.Anna));
        Assert.Equal(expected, notice);
    }

    private static IReadOnlyList<string> EngineNotices(SupplyKit kit, Paddock.Application.Managers.ManagerId manager) =>
        kit.InboxSubjects(manager).Where(IsEngineNotice).ToArray();

    private static bool IsEngineNotice(string key) =>
        key is SupplyKeys.InboxEngineStrongerSubject or SupplyKeys.InboxEngineWeakerSubject or SupplyKeys.InboxEngineSameSubject;
}
