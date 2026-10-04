namespace Paddock.Domain.People;

public readonly record struct NationalityWeight
{
    public NationalityWeight(string code, int weight)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        if (weight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(weight), weight, "Nationality weight must be positive.");
        }

        Code = code.Trim().ToUpperInvariant();
        Weight = weight;
    }

    public string Code { get; }

    public int Weight { get; }
}

/// <summary>
/// Identity kept by <see cref="GenerationRequest.RandomizeKnownPerson"/>.
/// Skills are not part of identity and are rolled again.
/// </summary>
public sealed record KnownPersonIdentity
{
    public KnownPersonIdentity(string id, string givenName, string familyName, DateOnly birthDate, string nationality)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(givenName);
        ArgumentException.ThrowIfNullOrWhiteSpace(familyName);
        ArgumentException.ThrowIfNullOrWhiteSpace(nationality);
        if (birthDate.Year < GenerationEstimates.MinBirthYear || birthDate.Year > GenerationEstimates.MaxBirthYear)
        {
            throw new ArgumentOutOfRangeException(nameof(birthDate), birthDate, "Birth year is outside the supported range.");
        }

        Id = id.Trim();
        GivenName = givenName.Trim();
        FamilyName = familyName.Trim();
        BirthDate = birthDate;
        Nationality = nationality.Trim().ToUpperInvariant();
    }

    public string Id { get; }

    public string GivenName { get; }

    public string FamilyName { get; }

    public DateOnly BirthDate { get; }

    public string Nationality { get; }

    public string Name => GivenName + " " + FamilyName;
}

/// <summary>
/// What to generate: quality band, nationality weights, and the era used for attribute weights.
/// <see cref="RandomizeKnownPerson"/> keeps a real person's identity and re-rolls potential and skills.
/// </summary>
public sealed record GenerationRequest
{
    private GenerationRequest(
        QualityBand quality,
        IReadOnlyList<NationalityWeight> nationalityWeights,
        int eraYear,
        KnownPersonIdentity? knownPerson,
        int? strength,
        (int MinInclusive, int MaxExclusive)? ageRange = null)
    {
        AgeRange = ageRange;
        Quality = quality;
        NationalityWeights = nationalityWeights;
        EraYear = eraYear;
        KnownPerson = knownPerson;
        Strength = strength;
    }

    public QualityBand Quality { get; }

    public IReadOnlyList<NationalityWeight> NationalityWeights { get; }

    public int EraYear { get; }

    public KnownPersonIdentity? KnownPerson { get; }

    /// <summary>0–100 for <see cref="RandomizeKnownPerson"/>. Null for a new fictional person.</summary>
    public int? Strength { get; }

    /// <summary>
    /// Age at the season (inclusive minimum, exclusive maximum) that replaces the band's own range for a new person.
    /// Null keeps the band's range, so a request without it draws exactly as before.
    /// </summary>
    public (int MinInclusive, int MaxExclusive)? AgeRange { get; }

    /// <summary>A new person who enters at an age chosen by the caller, for example a young talent-pool entrant.</summary>
    public static GenerationRequest ForNewAtAge(
        QualityBand quality,
        IReadOnlyList<NationalityWeight> nationalityWeights,
        int eraYear,
        int ageMinInclusive,
        int ageMaxExclusive)
    {
        if (ageMinInclusive < 1 || ageMaxExclusive <= ageMinInclusive)
        {
            throw new ArgumentOutOfRangeException(nameof(ageMaxExclusive), ageMaxExclusive, "The age range must be a non-empty range of positive ages.");
        }

        var basic = ForNew(quality, nationalityWeights, eraYear);
        return new GenerationRequest(basic.Quality, basic.NationalityWeights, basic.EraYear, null, null, (ageMinInclusive, ageMaxExclusive));
    }

    public static GenerationRequest ForNew(QualityBand quality, IReadOnlyList<NationalityWeight> nationalityWeights, int eraYear)
    {
        EnsureBand(quality);
        EnsureEra(eraYear);
        ArgumentNullException.ThrowIfNull(nationalityWeights);
        if (nationalityWeights.Count == 0)
        {
            throw new ArgumentException("At least one nationality weight is required.", nameof(nationalityWeights));
        }

        return new GenerationRequest(quality, nationalityWeights.ToArray(), eraYear, null, null);
    }

    public static GenerationRequest RandomizeKnownPerson(
        KnownPersonIdentity person,
        int strength,
        QualityBand quality,
        int eraYear)
    {
        ArgumentNullException.ThrowIfNull(person);
        EnsureBand(quality);
        EnsureEra(eraYear);
        if (strength < GenerationEstimates.StrengthMin || strength > GenerationEstimates.StrengthMax)
        {
            throw new ArgumentOutOfRangeException(nameof(strength), strength, "Strength must be from 0 to 100.");
        }

        return new GenerationRequest(quality, [], eraYear, person, strength);
    }

    public static GenerationRequest ForReview(int season, IReadOnlyList<string> nationalities)
    {
        ArgumentNullException.ThrowIfNull(nationalities);
        if (nationalities.Count == 0)
        {
            throw new ArgumentException("At least one nationality is required.", nameof(nationalities));
        }

        NationalityWeight[] weights = nationalities
            .Select(code => new NationalityWeight(code, GenerationEstimates.ReviewNationalityWeight))
            .ToArray();
        return ForNew(GenerationEstimates.ReviewCommandQuality, weights, season);
    }

    private static void EnsureBand(QualityBand quality)
    {
        if (!Enum.IsDefined(quality))
        {
            throw new ArgumentOutOfRangeException(nameof(quality), quality, "Unknown quality band.");
        }
    }

    private static void EnsureEra(int eraYear)
    {
        if (eraYear < GenerationEstimates.MinSeason || eraYear > GenerationEstimates.MaxSeason)
        {
            throw new ArgumentOutOfRangeException(nameof(eraYear), eraYear, "Era year is outside the supported range.");
        }
    }
}
