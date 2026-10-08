using System.Text.Json;
using Paddock.Domain.Career;

namespace Paddock.Persistence;

/// <summary>
/// Reads the canonical career-config JSON produced by <see cref="CareerConfig.ToCanonicalJson"/>.
/// Any other property order, whitespace, or preset name is rejected so the stored bytes stay the hash input.
/// </summary>
internal static class CareerConfigCodec
{
    private static readonly string[] Properties =
    [
        "peopleSource",
        "rulesSource",
        "aiBehavior",
        "historyStrength",
        "randomnessLevel",
        "fatalityLevel",
        "startYear",
        "playerTeam",
        "noNumbers",
        "presetName",
    ];

    /// <summary>Written only when it is not the default (see <see cref="CareerConfig.ToCanonicalJson"/>).</summary>
    private const string OptionalVoteMode = "voteMode";

    public static CareerConfig Read(string payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        CareerConfig config;
        try
        {
            config = Parse(payload);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("Career config JSON is malformed.", exception);
        }

        if (!string.Equals(payload, config.ToCanonicalJson(), StringComparison.Ordinal))
        {
            throw new InvalidDataException("Career config JSON is not in canonical form.");
        }

        // Old saves store history strength on 0–100. The canonical bytes stay as written; the loaded value is the 0–10 scale.
        return config.WithHistoryStrength(CareerConfig.ScaleLegacyHistoryStrength(config.HistoryStrength));
    }

    private static CareerConfig Parse(string payload)
    {
        using var document = JsonDocument.Parse(payload);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("Career config JSON is not an object.");
        }

        var count = 0;
        foreach (var property in document.RootElement.EnumerateObject())
        {
            count++;
            if (Array.IndexOf(Properties, property.Name) < 0 && property.Name != OptionalVoteMode)
            {
                throw new InvalidDataException($"Career config JSON has unknown property '{property.Name}'.");
            }
        }

        var hasVoteMode = document.RootElement.TryGetProperty(OptionalVoteMode, out _);
        if (count != Properties.Length + (hasVoteMode ? 1 : 0))
        {
            throw new InvalidDataException("Career config JSON is missing a property.");
        }

        if (RequireString(document.RootElement, "presetName").Length == 0)
        {
            throw new InvalidDataException("Career config property 'presetName' must not be empty.");
        }

        return new CareerConfig(
            RequireEnum<PeopleSource>(document.RootElement, "peopleSource"),
            RequireEnum<RulesSource>(document.RootElement, "rulesSource"),
            RequireEnum<AiBehavior>(document.RootElement, "aiBehavior"),
            RequireInt(document.RootElement, "historyStrength"),
            RequireInt(document.RootElement, "randomnessLevel"),
            RequireEnum<FatalityLevel>(document.RootElement, "fatalityLevel"),
            RequireInt(document.RootElement, "startYear"),
            RequireString(document.RootElement, "playerTeam"),
            RequireBool(document.RootElement, "noNumbers"),
            hasVoteMode ? RequireEnum<VoteMode>(document.RootElement, OptionalVoteMode) : VoteMode.OneVoteEach);
    }

    private static string RequireString(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var property) || property.ValueKind != JsonValueKind.String)
        {
            throw new InvalidDataException($"Career config property '{name}' must be a string.");
        }

        return property.GetString() ?? throw new InvalidDataException($"Career config property '{name}' must be a string.");
    }

    private static int RequireInt(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var property)
            || property.ValueKind != JsonValueKind.Number
            || !property.TryGetInt32(out var value))
        {
            throw new InvalidDataException($"Career config property '{name}' must be an integer.");
        }

        return value;
    }

    private static bool RequireBool(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var property)
            || property.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            throw new InvalidDataException($"Career config property '{name}' must be a boolean.");
        }

        return property.GetBoolean();
    }

    private static TEnum RequireEnum<TEnum>(JsonElement root, string name)
        where TEnum : struct, Enum
    {
        var text = RequireString(root, name);
        if (!Enum.TryParse<TEnum>(text, ignoreCase: false, out var value) || !Enum.IsDefined(value))
        {
            throw new InvalidDataException($"Career config property '{name}' is not a known value.");
        }

        return value;
    }
}
