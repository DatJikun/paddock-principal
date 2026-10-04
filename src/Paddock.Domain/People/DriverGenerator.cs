using Paddock.Domain.Random;

namespace Paddock.Domain.People;

/// <summary>
/// Builds fictional drivers, and re-rolls skills for a real identity, from the People stream only.
/// Person N depends on earlier people only through <see cref="StableIdAllocator"/>.
/// </summary>
public sealed class DriverGenerator
{
    private readonly StableIdAllocator _ids;
    private readonly INameSource _names;
    private readonly INameBlocklist _blocklist;

    public DriverGenerator(StableIdAllocator ids, INameSource names, INameBlocklist? blocklist = null)
    {
        ArgumentNullException.ThrowIfNull(ids);
        ArgumentNullException.ThrowIfNull(names);
        _ids = ids;
        _names = names;
        _blocklist = blocklist ?? EmptyNameBlocklist.Instance;
    }

    public GeneratedDriver Generate(RngStream people, int season, GenerationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        PeopleSampling.EnsureSeason(season);
        PeopleSampling.EnsurePeopleStream(people);
        if (request.KnownPerson is KnownPersonIdentity known)
        {
            return Randomize(people, known, request.Strength!.Value, request.Quality, request.EraYear);
        }

        StablePersonId id = _ids.Allocate();
        var rng = PeopleSampling.Child(people, "driver", id.Value);
        string nationality = PeopleSampling.PickNationality(rng, request.NationalityWeights);
        bool female = PeopleSampling.PickFemale(rng, season);
        (int ageMin, int ageMax) = request.AgeRange ?? GenerationEstimates.AgeRange(request.Quality);
        int age = PeopleSampling.PickAge(rng, ageMin, ageMax);
        DateOnly birth = PeopleSampling.PickBirthDate(rng, season, age);
        PersonName name = PeopleSampling.PickName(rng, _names, _blocklist, nationality, birth.Year, female);
        return Finish(
            rng,
            id.Value,
            name.Given,
            name.Family,
            nationality,
            birth,
            female,
            request.Quality,
            request.EraYear,
            sdFactor: 1,
            strength: GenerationEstimates.StrengthMax);
    }

    public GeneratedDriver RandomizeKnownPerson(
        RngStream people,
        int season,
        KnownPersonIdentity person,
        int strength,
        QualityBand quality)
    {
        GenerationRequest request = GenerationRequest.RandomizeKnownPerson(person, strength, quality, season);
        return Generate(people, season, request);
    }

    private GeneratedDriver Randomize(
        RngStream people,
        KnownPersonIdentity person,
        int strength,
        QualityBand quality,
        int eraYear)
    {
        var rng = PeopleSampling.Child(people, "known", person.Id);
        return Finish(
            rng,
            person.Id,
            person.GivenName,
            person.FamilyName,
            person.Nationality,
            person.BirthDate,
            isFemale: null,
            quality,
            eraYear,
            GenerationEstimates.StrengthSdFactor(strength),
            strength);
    }

    private static GeneratedDriver Finish(
        Xoshiro256StarStar rng,
        string id,
        string givenName,
        string familyName,
        string nationality,
        DateOnly birthDate,
        bool? isFemale,
        QualityBand quality,
        int eraYear,
        double sdFactor,
        int strength)
    {
        DriverAttributes attributes = PeopleSampling.PickAttributes(rng, quality, eraYear, sdFactor);
        DriverAttributes potentialAttributes = PeopleSampling.PickPotential(rng, attributes, quality, strength);
        CareerCurve curve = PeopleSampling.PickDriverCurve(rng);
        PersonalityTraits personality = PeopleSampling.PickPersonality(rng);
        return new GeneratedDriver(
            id,
            givenName,
            familyName,
            nationality,
            birthDate,
            isFemale,
            quality,
            eraYear,
            attributes,
            potentialAttributes,
            GenerationEstimates.Overall(attributes, eraYear),
            GenerationEstimates.Overall(potentialAttributes, eraYear),
            curve,
            personality);
    }
}
