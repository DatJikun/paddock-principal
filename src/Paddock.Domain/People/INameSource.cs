using Paddock.Domain.Random;

namespace Paddock.Domain.People;

/// <summary>
/// Given and family names by nationality and birth era.
/// R12 will supply the real table. Until that data is merged, <see cref="FixtureNameSource"/>
/// is the stand-in behind this interface.
/// </summary>
public interface INameSource
{
    PersonName Pick(Xoshiro256StarStar rng, string nationality, int birthYear, bool female);
}

public readonly record struct PersonName(string Given, string Family)
{
    public string Full => Given + " " + Family;
}

/// <summary>
/// Rejects a generated full name. Empty until the R12 blocklist is merged.
/// </summary>
public interface INameBlocklist
{
    bool Blocks(string givenName, string familyName);
}

public sealed class EmptyNameBlocklist : INameBlocklist
{
    public static EmptyNameBlocklist Instance { get; } = new();

    public bool Blocks(string givenName, string familyName) => false;
}
