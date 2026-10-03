namespace Paddock.Domain.People;

public enum PrimaryPersonality
{
    SeeksSecurity = 0,
    Mercenary = 1,
    Loyal = 2,
    Prestige = 3,
    ShortTerm = 4,
    Ambitious = 5,
    Mentor = 6,
    TeamPlayer = 7,
}

/// <summary>
/// One primary personality plus the hidden 1–20 traits from DESIGN §6.1.
/// </summary>
public readonly record struct PersonalityTraits
{
    public PersonalityTraits(
        PrimaryPersonality primary,
        int loyalty,
        int ambition,
        int temperament,
        int professionalism,
        int ego)
    {
        if (!Enum.IsDefined(primary))
        {
            throw new ArgumentOutOfRangeException(nameof(primary), primary, "Unknown personality.");
        }

        Loyalty = Check(loyalty);
        Ambition = Check(ambition);
        Temperament = Check(temperament);
        Professionalism = Check(professionalism);
        Ego = Check(ego);
        Primary = primary;
    }

    public PrimaryPersonality Primary { get; }

    public int Loyalty { get; }

    public int Ambition { get; }

    public int Temperament { get; }

    public int Professionalism { get; }

    public int Ego { get; }

    private static int Check(int value)
    {
        if (value < GenerationEstimates.AttributeMin || value > GenerationEstimates.AttributeMax)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "Personality traits must be from 1 to 20.");
        }

        return value;
    }
}
