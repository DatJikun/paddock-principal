namespace Paddock.Domain.People;

public readonly record struct NamedAttribute
{
    public NamedAttribute(string key, int value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        if (value < GenerationEstimates.AttributeMin || value > GenerationEstimates.AttributeMax)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "Staff attributes must be from 1 to 20.");
        }

        Key = key;
        Value = value;
    }

    public string Key { get; }

    public int Value { get; }
}

public sealed record GeneratedStaff
{
    public GeneratedStaff(
        string id,
        StaffRole role,
        string givenName,
        string familyName,
        string nationality,
        DateOnly birthDate,
        bool isFemale,
        QualityBand quality,
        int eraYear,
        IReadOnlyList<NamedAttribute> attributes,
        int innovation,
        int ambition,
        int loyalty,
        CareerCurve curve)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(givenName);
        ArgumentException.ThrowIfNullOrWhiteSpace(familyName);
        ArgumentException.ThrowIfNullOrWhiteSpace(nationality);
        if (!Enum.IsDefined(role))
        {
            throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown staff role.");
        }

        if (!Enum.IsDefined(quality))
        {
            throw new ArgumentOutOfRangeException(nameof(quality), quality, "Unknown quality band.");
        }

        ArgumentNullException.ThrowIfNull(attributes);
        IReadOnlyList<string> expected = StaffCatalogue.AttributeKeys(role);
        if (attributes.Count != expected.Count)
        {
            throw new ArgumentException("Staff attribute count does not match the role.", nameof(attributes));
        }

        for (var i = 0; i < expected.Count; i++)
        {
            if (!string.Equals(attributes[i].Key, expected[i], StringComparison.Ordinal))
            {
                throw new ArgumentException("Staff attribute keys do not match the role.", nameof(attributes));
            }
        }

        CheckTrait(innovation, nameof(innovation));
        CheckTrait(ambition, nameof(ambition));
        CheckTrait(loyalty, nameof(loyalty));
        foreach (NamedAttribute attribute in attributes)
        {
            if (string.Equals(attribute.Key, StaffCatalogue.InnovationKey, StringComparison.Ordinal)
                && attribute.Value != innovation)
            {
                throw new ArgumentException("Innovation must match the role's innovation attribute.", nameof(innovation));
            }
        }

        Id = id;
        Role = role;
        GivenName = givenName;
        FamilyName = familyName;
        Nationality = nationality;
        BirthDate = birthDate;
        IsFemale = isFemale;
        Quality = quality;
        EraYear = eraYear;
        Attributes = attributes.ToArray();
        Innovation = innovation;
        Ambition = ambition;
        Loyalty = loyalty;
        Curve = curve;
    }

    public string Id { get; }

    public StaffRole Role { get; }

    public string GivenName { get; }

    public string FamilyName { get; }

    public string Name => GivenName + " " + FamilyName;

    public string Nationality { get; }

    public DateOnly BirthDate { get; }

    public bool IsFemale { get; }

    public QualityBand Quality { get; }

    public int EraYear { get; }

    public IReadOnlyList<NamedAttribute> Attributes { get; }

    public int Innovation { get; }

    public int Ambition { get; }

    public int Loyalty { get; }

    public CareerCurve Curve { get; }

    private static void CheckTrait(int value, string name)
    {
        if (value < GenerationEstimates.AttributeMin || value > GenerationEstimates.AttributeMax)
        {
            throw new ArgumentOutOfRangeException(name, value, "Staff traits must be from 1 to 20.");
        }
    }
}
