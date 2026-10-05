using System.Text.Json;
using Paddock.Domain.People;
using Paddock.Domain.Random;

namespace Paddock.Tests.People;

public class DriverGeneratorTests
{
    private const int DistributionCount = 10_000;

    [Fact]
    public void SameSeedAndRequestsAreByteIdenticalJson()
    {
        string first = BatchJson(42, 1955);
        string second = BatchJson(42, 1955);

        Assert.Equal(first, second);
        using JsonDocument parsed = JsonDocument.Parse("[" + first + "]");
        Assert.Equal(JsonValueKind.Array, parsed.RootElement.ValueKind);
    }

    [Fact]
    public void TwoSeedsDiffer()
    {
        Assert.NotEqual(BatchJson(1, 1960), BatchJson(2, 1960));
    }

    [Fact]
    public void PersonDependsOnEarlierPeopleOnlyThroughTheIdCounter()
    {
        RngStream stream = People(7, 1955);
        GenerationRequest request = SampleRequest(QualityBand.Solid, 1955);
        var batch = Generator();
        GeneratedDriver first = batch.Generate(stream, 1955, request);
        GeneratedDriver second = batch.Generate(stream, 1955, request);
        GeneratedDriver third = batch.Generate(stream, 1955, request);

        GeneratedDriver onlyThird = Generator(3).Generate(stream, 1955, request);

        Assert.Equal("gen:1", first.Id);
        Assert.Equal("gen:2", second.Id);
        Assert.Equal("gen:3", third.Id);
        Assert.Equal(PeopleCanonical.Driver(third), PeopleCanonical.Driver(onlyThird));
        Assert.NotEqual(PeopleCanonical.Driver(first), PeopleCanonical.Driver(third));
    }

    [Fact]
    public void GeneratingDoesNotAdvanceThePeopleStream()
    {
        RngStream stream = People(4, 1950);
        RngState before = stream.State;
        Generator().Generate(stream, 1950, SampleRequest(QualityBand.Filler, 1950));
        Assert.Equal(before, stream.State);
    }

    [Fact]
    public void OnlyThePeopleStreamIsAcceptedAndARejectedCallDoesNotSpendAnId()
    {
        var ids = new StableIdAllocator();
        var generator = new DriverGenerator(ids, new FixtureNameSource());
        RngStream weather = RngStream.Derive(1, RngStreamName.Weather, 1950);

        Assert.Throws<ArgumentException>(() => generator.Generate(weather, 1950, SampleRequest(QualityBand.Filler, 1950)));
        Assert.Equal(1, ids.NextSequence);
    }

    [Fact]
    public void AttributesStayInRangeAndTheCareerCurvePeaksAfterGrowth()
    {
        RngStream stream = People(11, 1975);
        GenerationRequest request = SampleRequest(QualityBand.Contender, 1975);
        var generator = Generator();
        for (var i = 0; i < 200; i++)
        {
            GeneratedDriver driver = generator.Generate(stream, 1975, request);
            AssertDriverShape(driver, 1975, QualityBand.Contender);
        }
    }

    [Fact]
    public void RandomizeKnownPersonKeepsIdentityAndRerollsSkills()
    {
        var ids = new StableIdAllocator();
        var generator = new DriverGenerator(ids, new FixtureNameSource());
        RngStream stream = People(19, 1955);
        var fangio = new KnownPersonIdentity("fangio", "Juan Manuel", "Fangio", new DateOnly(1911, 6, 24), "ARG");

        generator.Generate(stream, 1955, SampleRequest(QualityBand.Filler, 1955));
        generator.Generate(stream, 1955, SampleRequest(QualityBand.Filler, 1955));
        GeneratedDriver rolled = generator.RandomizeKnownPerson(stream, 1955, fangio, 80, QualityBand.Contender);
        GeneratedDriver again = Generator().RandomizeKnownPerson(stream, 1955, fangio, 80, QualityBand.Contender);

        Assert.Equal(PeopleCanonical.Driver(rolled), PeopleCanonical.Driver(again));
        Assert.Equal("fangio", rolled.Id);
        Assert.Equal("Juan Manuel", rolled.GivenName);
        Assert.Equal("Fangio", rolled.FamilyName);
        Assert.Equal(new DateOnly(1911, 6, 24), rolled.BirthDate);
        Assert.Equal("ARG", rolled.Nationality);
        Assert.Null(rolled.IsFemale);
        Assert.Equal(3, ids.NextSequence);

        RngStream otherSeed = People(20, 1955);
        GeneratedDriver other = Generator().RandomizeKnownPerson(otherSeed, 1955, fangio, 80, QualityBand.Contender);
        Assert.Equal("fangio", other.Id);
        Assert.Equal(rolled.Name, other.Name);
        Assert.NotEqual(PeopleCanonical.Driver(rolled), PeopleCanonical.Driver(other));

        GeneratedDriver tight = Generator().RandomizeKnownPerson(stream, 1955, fangio, 0, QualityBand.Contender);
        GeneratedDriver wide = Generator().RandomizeKnownPerson(stream, 1955, fangio, 100, QualityBand.Contender);
        Assert.NotEqual(PeopleCanonical.Driver(tight), PeopleCanonical.Driver(wide));
        Assert.Equal(fangio.Name, tight.Name);
        Assert.True(tight.Potential >= tight.Overall);
        Assert.True(wide.Potential >= wide.Overall);
    }

    [Fact]
    public void FillerDistributionOverTenThousandSamplesStaysInsideEstimates()
    {
        (double mean, double starShare) = SampleBand(QualityBand.Filler, 1980, 12345);
        Assert.InRange(mean, GenerationEstimates.FillerSampleMeanLower, GenerationEstimates.FillerSampleMeanUpper);
        Assert.InRange(starShare, 0, GenerationEstimates.FillerStarShareUpper);
    }

    [Fact]
    public void FutureStarDistributionOverTenThousandSamplesStaysInsideEstimates()
    {
        (double mean, double starShare) = SampleBand(QualityBand.FutureStar, 1980, 12345);
        Assert.InRange(mean, GenerationEstimates.FutureStarSampleMeanLower, GenerationEstimates.FutureStarSampleMeanUpper);
        Assert.InRange(starShare, GenerationEstimates.FutureStarShareLower, GenerationEstimates.FutureStarShareUpper);
        Assert.True(starShare > GenerationEstimates.FillerStarShareUpper);
    }

    [Fact]
    public void EraWeightsShiftSmoothnessFitnessAndFeedback()
    {
        Assert.Equal(120, Weight(1950, "smoothness"));
        Assert.Equal(180, Weight(1950, "cornering"));
        Assert.True(Weight(1950, "smoothness") > Weight(2024, "smoothness"));
        Assert.True(Weight(1950, "fitness") > Weight(2024, "fitness"));
        Assert.True(Weight(2024, "feedback") > Weight(1950, "feedback"));
        Assert.Equal(Weight(1950, "smoothness"), Weight(GenerationEstimates.EarlyEraLastYear, "smoothness"));
        Assert.NotEqual(Weight(GenerationEstimates.EarlyEraLastYear, "smoothness"), Weight(GenerationEstimates.EarlyEraLastYear + 1, "smoothness"));
        Assert.Equal(Weight(GenerationEstimates.ModernEraFirstYear, "feedback"), Weight(2026, "feedback"));
        Assert.NotEqual(Weight(GenerationEstimates.ModernEraFirstYear - 1, "feedback"), Weight(GenerationEstimates.ModernEraFirstYear, "feedback"));
    }

    [Fact]
    public void EqualAttributesProduceTheSameOverallInEveryEra()
    {
        var ten = new DriverAttributes(10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10);
        var twenty = new DriverAttributes(20, 20, 20, 20, 20, 20, 20, 20, 20, 20, 20);
        var one = new DriverAttributes(1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1);
        foreach (int year in new[] { 1950, 1980, 2024 })
        {
            Assert.Equal(50, GenerationEstimates.Overall(ten, year));
            Assert.Equal(100, GenerationEstimates.Overall(twenty, year));
            Assert.Equal(5, GenerationEstimates.Overall(one, year));
            AttributeWeight[] weights = GenerationEstimates.OverallWeights(year);
            Assert.Equal(GenerationEstimates.DriverAttributeKeys, weights.Select(weight => weight.Key).ToArray());
            Assert.Equal(GenerationEstimates.WeightTotal, weights.Sum(weight => weight.Weight));
        }
    }

    [Fact]
    public void NamesComeFromTheFixtureForTheBirthEra()
    {
        var names = new FixtureNameSource();
        RngStream stream = People(3, 1950);
        var generator = new DriverGenerator(new StableIdAllocator(), names);
        GenerationRequest request = GenerationRequest.ForNew(
            QualityBand.Filler,
            names.Nationalities.Select(code => new NationalityWeight(code, 1)).ToArray(),
            1950);
        for (var i = 0; i < 40; i++)
        {
            GeneratedDriver driver = generator.Generate(stream, 1950, request);
            Assert.NotNull(driver.IsFemale);
            Assert.Contains(driver.GivenName, names.GivenNames(driver.Nationality, driver.BirthDate.Year, driver.IsFemale.Value));
            Assert.Contains(driver.FamilyName, names.FamilyNames(driver.Nationality, driver.BirthDate.Year));
        }

        var rng = Xoshiro256StarStar.FromSeed(5);
        PersonName fallback = names.Pick(rng, "ZZZ", 1930, false);
        Assert.Contains(fallback.Given, names.GivenNames(FixtureNameSource.OtherNationality, 1930, false));
    }

    [Fact]
    public void ABlockedNameIsSkippedAndABlockedPoolFailsWithoutReuse()
    {
        RngStream stream = People(9, 1966);
        GenerationRequest request = SampleRequest(QualityBand.Solid, 1966);
        GeneratedDriver open = Generator().Generate(stream, 1966, request);
        GeneratedDriver skipped = new DriverGenerator(new StableIdAllocator(), new FixtureNameSource(), new BlockFirst(1))
            .Generate(stream, 1966, request);
        GeneratedDriver skippedAgain = new DriverGenerator(new StableIdAllocator(), new FixtureNameSource(), new BlockFirst(1))
            .Generate(stream, 1966, request);

        Assert.Equal(PeopleCanonical.Driver(skipped), PeopleCanonical.Driver(skippedAgain));
        Assert.NotEqual(PeopleCanonical.Driver(open), PeopleCanonical.Driver(skipped));

        var ids = new StableIdAllocator();
        var blocked = new DriverGenerator(ids, new FixtureNameSource(), new BlockAll());
        Assert.Throws<InvalidOperationException>(() => blocked.Generate(stream, 1966, request));
        Assert.Equal(2, ids.NextSequence);
        Assert.Equal("gen:2", ids.Allocate().Value);
    }

    [Fact]
    public void OutOfRangeRequestsAreRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SampleRequest(QualityBand.Filler, 1949));
        Assert.Throws<ArgumentException>(() => GenerationRequest.ForNew(QualityBand.Filler, [], 1950));
        var person = new KnownPersonIdentity("moss", "Stirling", "Moss", new DateOnly(1929, 9, 17), "GBR");
        Assert.Throws<ArgumentOutOfRangeException>(() => GenerationRequest.RandomizeKnownPerson(person, -1, QualityBand.Solid, 1955));
        Assert.Throws<ArgumentOutOfRangeException>(() => GenerationRequest.RandomizeKnownPerson(person, 101, QualityBand.Solid, 1955));
        Assert.Throws<ArgumentOutOfRangeException>(() => Generator().Generate(People(1, 1950), 1949, SampleRequest(QualityBand.Filler, 1950)));
        GeneratedDriver future = Generator().Generate(People(1, 2027), 2027, SampleRequest(QualityBand.Filler, 2027));
        Assert.Equal(2027, future.EraYear);
    }

    [Fact]
    public void CareerCurveRejectsAPeakThatIsNotAfterGrowth()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new CareerCurve(24, 24, 2, 30));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CareerCurve(20, 30, -1, 30));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CareerCurve(20, 30, 2, 0));
    }

    private static (double Mean, double StarShare) SampleBand(QualityBand band, int season, ulong seed)
    {
        var generator = Generator();
        RngStream stream = People(seed, season);
        GenerationRequest request = SampleRequest(band, season);
        long sum = 0;
        var stars = 0;
        for (var i = 0; i < DistributionCount; i++)
        {
            GeneratedDriver driver = generator.Generate(stream, season, request);
            AssertDriverShape(driver, season, band);
            foreach (string key in GenerationEstimates.DriverAttributeKeys)
            {
                sum += driver.Attributes.Get(key);
            }

            if (driver.IsStar)
            {
                stars++;
            }
        }

        double mean = sum / (double)(DistributionCount * GenerationEstimates.DriverAttributeKeys.Length);
        return (mean, stars / (double)DistributionCount);
    }

    private static void AssertDriverShape(GeneratedDriver driver, int season, QualityBand band)
    {
        Assert.Equal(band, driver.Quality);
        foreach (string key in GenerationEstimates.DriverAttributeKeys)
        {
            Assert.InRange(driver.Attributes.Get(key), GenerationEstimates.AttributeMin, GenerationEstimates.AttributeMax);
            Assert.InRange(driver.PotentialAttributes.Get(key), driver.Attributes.Get(key), GenerationEstimates.AttributeMax);
        }

        Assert.Equal(GenerationEstimates.Overall(driver.Attributes, driver.EraYear), driver.Overall);
        Assert.Equal(GenerationEstimates.Overall(driver.PotentialAttributes, driver.EraYear), driver.Potential);
        Assert.True(driver.Potential >= driver.Overall);
        Assert.True(driver.Curve.PeakAge > driver.Curve.GrowthStartAge);
        Assert.InRange(driver.Curve.GrowthStartAge, GenerationEstimates.DriverGrowthStartMin, GenerationEstimates.DriverGrowthStartMaxExclusive - 1);
        Assert.InRange(
            driver.Curve.PeakAge - driver.Curve.GrowthStartAge,
            GenerationEstimates.DriverYearsToPeakMin,
            GenerationEstimates.DriverYearsToPeakMaxExclusive - 1);
        Assert.InRange(driver.Curve.PlateauYears, GenerationEstimates.DriverPlateauMin, GenerationEstimates.DriverPlateauMaxExclusive - 1);
        Assert.InRange(driver.Curve.DeclineMilliPerYear, GenerationEstimates.DriverDeclineMilliMin, GenerationEstimates.DriverDeclineMilliMaxExclusive - 1);
        Assert.InRange(driver.Personality.Loyalty, GenerationEstimates.AttributeMin, GenerationEstimates.AttributeMax);
        (int ageMin, int ageMax) = GenerationEstimates.AgeRange(band);
        Assert.InRange(season - driver.BirthDate.Year, ageMin, ageMax - 1);
        Assert.InRange(driver.BirthDate.Month, 1, 12);
        Assert.InRange(driver.BirthDate.Day, 1, GenerationEstimates.BirthDayMaxExclusive - 1);
        Assert.NotNull(driver.IsFemale);
    }

    private static string BatchJson(ulong seed, int season)
    {
        var generator = Generator();
        RngStream stream = People(seed, season);
        GenerationRequest request = SampleRequest(QualityBand.Solid, season);
        var parts = new string[4];
        for (var i = 0; i < parts.Length; i++)
        {
            parts[i] = PeopleCanonical.Driver(generator.Generate(stream, season, request));
        }

        var fangio = new KnownPersonIdentity("fangio", "Juan Manuel", "Fangio", new DateOnly(1911, 6, 24), "ARG");
        string known = PeopleCanonical.Driver(generator.RandomizeKnownPerson(stream, season, fangio, 60, QualityBand.Solid));
        return string.Join(",", parts) + "," + known;
    }

    private static int Weight(int year, string key)
    {
        return GenerationEstimates.OverallWeights(year).Single(weight => weight.Key == key).Weight;
    }

    private static GenerationRequest SampleRequest(QualityBand band, int era)
    {
        return GenerationRequest.ForNew(
            band,
            [new NationalityWeight("GBR", 3), new NationalityWeight("ITA", 1)],
            era);
    }

    private static DriverGenerator Generator(long nextId = 1)
    {
        return new DriverGenerator(new StableIdAllocator(nextId), new FixtureNameSource());
    }

    private static RngStream People(ulong seed, int season)
    {
        return RngStream.Derive(seed, RngStreamName.People, season);
    }

    private sealed class BlockFirst : INameBlocklist
    {
        private int _left;

        public BlockFirst(int count) => _left = count;

        public bool Blocks(string givenName, string familyName)
        {
            if (_left <= 0)
            {
                return false;
            }

            _left--;
            return true;
        }
    }

    private sealed class BlockAll : INameBlocklist
    {
        public bool Blocks(string givenName, string familyName) => true;
    }
}
