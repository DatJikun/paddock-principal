namespace Paddock.Domain.World;

/// <summary>
/// One version of a circuit: identity, length, lap-profile weights, and character tags.
/// </summary>
public sealed class TrackLayout
{
    public TrackLayout(
        string id,
        string circuitId,
        double lengthKm,
        IReadOnlyDictionary<string, double> profileWeights,
        IReadOnlyList<string> characterTags,
        string country = "")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(circuitId);
        ArgumentNullException.ThrowIfNull(profileWeights);
        ArgumentNullException.ThrowIfNull(characterTags);

        Id = id;
        CircuitId = circuitId;
        LengthKm = lengthKm;
        ProfileWeights = new Dictionary<string, double>(profileWeights, StringComparer.Ordinal);
        CharacterTags = characterTags.ToArray();
        Country = string.IsNullOrWhiteSpace(country) ? "" : country.Trim().ToUpperInvariant();
    }

    public string Id { get; }

    public string CircuitId { get; }

    public double LengthKm { get; }

    public IReadOnlyDictionary<string, double> ProfileWeights { get; }

    public IReadOnlyList<string> CharacterTags { get; }

    /// <summary>ISO-3 country of the circuit, or empty when the catalog did not name one.</summary>
    public string Country { get; }
}
