using System.Text.Json.Serialization;
using Paddock.Domain.Racing;

namespace Paddock.Data.Authored;

/// <summary>
/// <c>data/authored/regulations/banned_rules.json</c> (#275, owner decision of round 2): the rules and mechanics that can never be
/// proposed or voted on, so that a championship keeps a stable core. <see cref="Default"/> applies to every series that has no entry
/// of its own in <see cref="Series"/>; a series entry is that series' whole list, it does not add to the default.
/// </summary>
public sealed record BannedRulesFile(
    [property: JsonPropertyName("default")] IReadOnlyList<string> Default,
    [property: JsonPropertyName("series")] IReadOnlyDictionary<string, IReadOnlyList<string>> Series);

/// <summary>Reads the banned list file into the domain type the regulations module uses.</summary>
public static class BannedRulesLoader
{
    public const string RelativePath = "authored/regulations/banned_rules.json";

    public static BannedRules ToBannedRules(BannedRulesFile file)
    {
        ArgumentNullException.ThrowIfNull(file);
        return new BannedRules(file.Default, file.Series);
    }
}
