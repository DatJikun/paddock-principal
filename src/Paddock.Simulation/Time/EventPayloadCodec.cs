using Paddock.Domain.Codec;
using Paddock.Simulation.Codec;

namespace Paddock.Simulation.Time;

/// <summary>
/// Save codec of <see cref="EventPayload"/>: a versioned tag (<c>name/version</c>) and a flat JSON body.
/// The registry is an explicit list, not a search by type name, so a renamed class keeps reading old saves and a new
/// payload type cannot be saved until someone writes its entry. An unknown tag or version throws
/// <see cref="UnknownTagException"/>. A change to a body means a new version of the tag, and the old version stays readable.
/// <see cref="WorldClockHash"/> is closed over the same set.
/// </summary>
public static class EventPayloadCodec
{
    public const string RaceSessionTag = "race-session/1";

    public const string MarkerTag = "marker/1";

    private const string Kind = "event payload";

    /// <summary>Every payload type this build can save, for tests that check no subtype is left without a codec.</summary>
    public static IReadOnlyList<Type> SavedTypes { get; } = Array.AsReadOnly([typeof(RaceSessionPayload), typeof(MarkerPayload)]);

    public static TaggedText Encode(EventPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        return payload switch
        {
            RaceSessionPayload race => TaggedText.Of(
                RaceSessionTag,
                FlatJson.Write(("season", race.Season), ("round", race.Round), ("layoutId", race.LayoutId))),
            MarkerPayload marker => TaggedText.Of(MarkerTag, FlatJson.Write(("marker", marker.Marker))),
            _ => throw new InvalidOperationException(
                "No save codec for payload type '" + payload.GetType().Name + "'. Add an entry to EventPayloadCodec."),
        };
    }

    public static EventPayload Decode(string tag, string text)
    {
        ArgumentNullException.ThrowIfNull(tag);
        ArgumentNullException.ThrowIfNull(text);
        try
        {
            switch (tag)
            {
                case RaceSessionTag:
                    var race = FlatJson.Read(text, "season", "round", "layoutId");
                    return new RaceSessionPayload(race.Int32("season"), race.Int32("round"), race.String("layoutId"));
                case MarkerTag:
                    return new MarkerPayload(FlatJson.Read(text, "marker").String("marker"));
                default:
                    throw new UnknownTagException(Kind, tag);
            }
        }
        catch (ArgumentException exception)
        {
            throw new InvalidDataException("A stored '" + tag + "' payload is not valid: " + exception.Message, exception);
        }
    }
}
