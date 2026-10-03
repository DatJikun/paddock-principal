namespace Paddock.Domain.People;

public sealed record GeneratedDriver
{
    public GeneratedDriver(
        string id,
        string givenName,
        string familyName,
        string nationality,
        DateOnly birthDate,
        bool? isFemale,
        QualityBand quality,
        int eraYear,
        DriverAttributes attributes,
        DriverAttributes potentialAttributes,
        int overall,
        int potential,
        CareerCurve curve,
        PersonalityTraits personality)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(givenName);
        ArgumentException.ThrowIfNullOrWhiteSpace(familyName);
        ArgumentException.ThrowIfNullOrWhiteSpace(nationality);
        if (!Enum.IsDefined(quality))
        {
            throw new ArgumentOutOfRangeException(nameof(quality), quality, "Unknown quality band.");
        }

        attributes.EnsureValid();
        potentialAttributes.EnsureValid();
        foreach (string key in GenerationEstimates.DriverAttributeKeys)
        {
            if (potentialAttributes.Get(key) < attributes.Get(key))
            {
                throw new ArgumentException("Potential is below the current attribute for " + key + ".", nameof(potentialAttributes));
            }
        }

        int expectedOverall = GenerationEstimates.Overall(attributes, eraYear);
        int expectedPotential = GenerationEstimates.Overall(potentialAttributes, eraYear);
        if (overall != expectedOverall)
        {
            throw new ArgumentException("Overall does not match the era weights.", nameof(overall));
        }

        if (potential != expectedPotential || potential < overall)
        {
            throw new ArgumentException("Potential must be the ceiling overall and at least the current overall.", nameof(potential));
        }

        Id = id;
        GivenName = givenName;
        FamilyName = familyName;
        Nationality = nationality;
        BirthDate = birthDate;
        IsFemale = isFemale;
        Quality = quality;
        EraYear = eraYear;
        Attributes = attributes;
        PotentialAttributes = potentialAttributes;
        Overall = overall;
        Potential = potential;
        Curve = curve;
        Personality = personality;
    }

    public string Id { get; }

    public string GivenName { get; }

    public string FamilyName { get; }

    public string Name => GivenName + " " + FamilyName;

    public string Nationality { get; }

    public DateOnly BirthDate { get; }

    public bool? IsFemale { get; }

    public QualityBand Quality { get; }

    public int EraYear { get; }

    public DriverAttributes Attributes { get; }

    public DriverAttributes PotentialAttributes { get; }

    public int Overall { get; }

    public int Potential { get; }

    public CareerCurve Curve { get; }

    public PersonalityTraits Personality { get; }

    public bool IsStar => Potential >= GenerationEstimates.StarPotential;
}
