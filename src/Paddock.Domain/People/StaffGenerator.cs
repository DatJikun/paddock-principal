using System.Globalization;
using Paddock.Domain.Random;

namespace Paddock.Domain.People;

/// <summary>
/// Builds fictional staff for the roles in DESIGN §6.2 from the People stream only.
/// Person N depends on earlier people only through <see cref="StableIdAllocator"/>.
/// </summary>
public sealed class StaffGenerator
{
    private readonly StableIdAllocator _ids;
    private readonly INameSource _names;
    private readonly INameBlocklist _blocklist;

    public StaffGenerator(StableIdAllocator ids, INameSource names, INameBlocklist? blocklist = null)
    {
        ArgumentNullException.ThrowIfNull(ids);
        ArgumentNullException.ThrowIfNull(names);
        _ids = ids;
        _names = names;
        _blocklist = blocklist ?? EmptyNameBlocklist.Instance;
    }

    public GeneratedStaff Generate(RngStream people, int season, StaffRole role, GenerationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        PeopleSampling.EnsureSeason(season);
        if (!Enum.IsDefined(role))
        {
            throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown staff role.");
        }

        if (request.KnownPerson is not null)
        {
            throw new ArgumentException("RandomizeKnownPerson applies to drivers.", nameof(request));
        }

        int availableFrom = StaffCatalogue.AvailableFrom(role);
        if (season < availableFrom)
        {
            throw new ArgumentException(
                role + " is not available until " + availableFrom.ToString(CultureInfo.InvariantCulture) + ".",
                nameof(role));
        }

        PeopleSampling.EnsurePeopleStream(people);
        StablePersonId id = _ids.Allocate();
        var rng = PeopleSampling.Child(people, "staff", id.Value);
        string nationality = PeopleSampling.PickNationality(rng, request.NationalityWeights);
        bool female = PeopleSampling.PickFemale(rng, season);
        int age = PeopleSampling.PickAge(rng, GenerationEstimates.StaffAgeMin, GenerationEstimates.StaffAgeMaxExclusive);
        DateOnly birth = PeopleSampling.PickBirthDate(rng, season, age);
        PersonName name = PeopleSampling.PickName(rng, _names, _blocklist, nationality, birth.Year, female);

        IReadOnlyList<string> keys = StaffCatalogue.AttributeKeys(role);
        var attributes = new NamedAttribute[keys.Count];
        int? listedInnovation = null;
        for (var i = 0; i < keys.Count; i++)
        {
            int value = PeopleSampling.PickStaffAttribute(rng, request.Quality);
            attributes[i] = new NamedAttribute(keys[i], value);
            if (string.Equals(keys[i], StaffCatalogue.InnovationKey, StringComparison.Ordinal))
            {
                listedInnovation = value;
            }
        }

        int innovation = listedInnovation ?? PeopleSampling.PickStaffAttribute(rng, request.Quality);
        int ambition = PeopleSampling.PickHiddenTrait(rng);
        int loyalty = PeopleSampling.PickHiddenTrait(rng);
        CareerCurve curve = PeopleSampling.PickStaffCurve(rng);
        return new GeneratedStaff(
            id.Value,
            role,
            name.Given,
            name.Family,
            nationality,
            birth,
            female,
            request.Quality,
            request.EraYear,
            attributes,
            innovation,
            ambition,
            loyalty,
            curve);
    }
}
