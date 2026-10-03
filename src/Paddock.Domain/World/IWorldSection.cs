namespace Paddock.Domain.World;

/// <summary>
/// A part of the world that a later system owns (finance, sponsors, an inbox, and so on) and that
/// <see cref="WorldState"/> holds, hashes, and hands to the save without knowing what is inside.
/// <para>
/// Rules for an implementation. It is immutable: every change returns a new value, which the caller puts back with
/// <see cref="WorldState.WithSection"/>. <see cref="Name"/> is stable for the life of the save and unique in the world.
/// <see cref="WriteCanonical"/> is a pure function of the contents. It never depends on insertion order, culture,
/// the clock, or object identity, and a changed field must change it, because it feeds <see cref="WorldState.StateHash"/>.
/// <see cref="SchemaVersion"/> is the version of that text and of the stored rows; raise it when either changes.
/// A section holds truth like the rest of the world: what a manager may see goes through a query with an access context.
/// </para>
/// </summary>
public interface IWorldSection
{
    /// <summary>Lowercase name, letters, digits, dots and hyphens, starting with a letter (<see cref="SectionNames"/>).</summary>
    string Name { get; }

    /// <summary>Version of the canonical text and the stored rows, at least 1.</summary>
    int SchemaVersion { get; }

    /// <summary>Writes the canonical contents. Lines and fields follow the rules of <see cref="CanonicalWriter"/>.</summary>
    void WriteCanonical(CanonicalWriter writer);
}

/// <summary>Validation of section names.</summary>
public static class SectionNames
{
    public static bool IsValid(string? name)
    {
        if (string.IsNullOrEmpty(name) || name.Length > 64 || name[0] < 'a' || name[0] > 'z')
        {
            return false;
        }

        foreach (var character in name)
        {
            var allowed = character is >= 'a' and <= 'z' or >= '0' and <= '9' or '.' or '-';
            if (!allowed)
            {
                return false;
            }
        }

        return true;
    }

    public static string Require(string? name, string paramName)
    {
        if (!IsValid(name))
        {
            throw new ArgumentException(
                "A section name is lowercase letters, digits, dots and hyphens, starts with a letter, and has at most 64 characters.",
                paramName);
        }

        return name!;
    }
}
