using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Paddock.Domain.Sponsors;
using Paddock.Domain.World;

namespace Paddock.Data.Authored;

public sealed record SponsorsFile(
    [property: JsonPropertyName("notes")] string Notes,
    [property: JsonPropertyName("sponsors")] IReadOnlyList<SponsorEntry> Sponsors);

public sealed record SponsorEntry(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("industry")] string Industry,
    [property: JsonPropertyName("nationality")] string Nationality,
    [property: JsonPropertyName("prestige_need")] double PrestigeNeed,
    [property: JsonPropertyName("budget_level")] double BudgetLevel,
    [property: JsonPropertyName("from")] int From,
    [property: JsonPropertyName("to")] int? To,
    [property: JsonPropertyName("slots")] IReadOnlyList<string> Slots,
    [property: JsonPropertyName("objective")] SponsorObjectiveEntry? Objective,
    [property: JsonPropertyName("confidence")] string Confidence,
    [property: JsonPropertyName("notes")] string Notes);

public sealed record SponsorObjectiveEntry(
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("value")] string Value,
    [property: JsonPropertyName("within_days")] int WithinDays);

/// <summary>
/// Loads <c>data/authored/commercial/sponsors_estimates.json</c>. Strict like the other authored files: unknown properties
/// are errors. The sponsors are fictional and every number in the file is an ESTIMATE (see <see cref="SponsorsValidator"/>).
/// </summary>
public static class SponsorsLoader
{
    public const string RelativePath = "authored/commercial/sponsors_estimates.json";

    /// <param name="dataRoot">The <c>data</c> directory.</param>
    public static SponsorsFile Load(string dataRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataRoot);
        var path = Path.Combine(Path.GetFullPath(dataRoot), RelativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(path))
        {
            throw new AuthoredDataLoadException("Missing file: " + path);
        }

        try
        {
            using var stream = File.OpenRead(path);
            return JsonSerializer.Deserialize<SponsorsFile>(stream, AuthoredJson.Options)
                ?? throw new AuthoredDataLoadException(path + ": JSON value is null.");
        }
        catch (JsonException exception)
        {
            throw new AuthoredDataLoadException(path + ": " + exception.Message);
        }
    }

    /// <summary>Builds the domain catalog. The file must have passed <see cref="SponsorsValidator"/>.</summary>
    public static SponsorCatalog ToCatalog(SponsorsFile file)
    {
        ArgumentNullException.ThrowIfNull(file);
        return new SponsorCatalog(file.Sponsors.Select(entry => new SponsorDefinition(
            entry.Id,
            entry.Name,
            entry.Industry,
            entry.Nationality,
            entry.PrestigeNeed,
            entry.BudgetLevel,
            entry.From,
            entry.To,
            entry.Slots.Select(slot => SlotKinds.TryParse(slot, out var kind) ? kind : throw new ArgumentException("Unknown slot '" + slot + "'.")).ToArray(),
            entry.Objective is null ? null : new SponsorObjectiveSpec(entry.Objective.Kind, entry.Objective.Value, entry.Objective.WithinDays))));
    }
}

/// <summary>
/// Checks the sponsors file against the era data. Every violation is returned. Developer-facing codes, not player text.
/// Rules: unique ids; known industry, slot kinds and objective kind; livery industries only in livery slots; numbers in range;
/// a window inside 1950 to 2026; and the owner's rule from PP-050: in every year of the window, for every slot kind the era
/// offers, at least <see cref="SponsorEstimates.MinCandidatesPerSlot"/> sponsors can fill it.
/// </summary>
public static class SponsorsValidator
{
    public const int FirstSeason = 1950;
    public const int LastSeason = 2026;

    public const string DuplicateId = "sponsor-duplicate-id";
    public const string BadIndustry = "sponsor-industry";
    public const string BadSlots = "sponsor-slots";
    public const string BadNumber = "sponsor-number";
    public const string BadWindow = "sponsor-window";
    public const string BadObjective = "sponsor-objective";
    public const string NotEstimate = "sponsor-not-estimate";
    public const string TooFewCandidates = "sponsor-too-few-candidates";

    public static IReadOnlyList<AuthoredDataError> Validate(SponsorsFile file, IReadOnlyList<RulePeriod> eraPeriods)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(eraPeriods);
        var errors = new List<AuthoredDataError>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        if (!file.Notes.Contains("ESTIMATE", StringComparison.Ordinal))
        {
            errors.Add(new AuthoredDataError(NotEstimate, "the file notes must say that every number is an ESTIMATE"));
        }

        foreach (var entry in file.Sponsors)
        {
            var label = "sponsor '" + entry.Id + "'";
            if (string.IsNullOrWhiteSpace(entry.Id) || !ids.Add(entry.Id))
            {
                errors.Add(new AuthoredDataError(DuplicateId, label + " has a blank or repeated id"));
            }

            if (!SponsorIndustries.IsKnown(entry.Industry))
            {
                errors.Add(new AuthoredDataError(BadIndustry, label + " has unknown industry '" + entry.Industry + "'"));
            }

            ValidateSlots(entry, label, errors);
            if (entry.PrestigeNeed is < 0.0 or > 1.0 || entry.BudgetLevel is <= 0.0 or > 1.0 || double.IsNaN(entry.PrestigeNeed) || double.IsNaN(entry.BudgetLevel))
            {
                errors.Add(new AuthoredDataError(BadNumber, label + " needs prestige_need in 0..1 and budget_level in (0..1]"));
            }

            if (entry.From < FirstSeason || entry.From > LastSeason || (entry.To is int to && (to < entry.From || to > LastSeason)))
            {
                errors.Add(new AuthoredDataError(BadWindow, label + " has a year window outside " + FirstSeason.ToString(CultureInfo.InvariantCulture) + " to " + LastSeason.ToString(CultureInfo.InvariantCulture)));
            }

            if (string.IsNullOrWhiteSpace(entry.Name) || entry.Nationality.Length != 3 || entry.Nationality.Any(character => character is < 'A' or > 'Z'))
            {
                errors.Add(new AuthoredDataError(BadNumber, label + " needs a name and a three letter upper case nationality"));
            }

            if (entry.Objective is { } objective)
            {
                ValidateObjective(objective, label, errors);
            }
        }

        if (errors.Count == 0)
        {
            CheckCoverage(file, eraPeriods, errors);
        }

        return errors;
    }

    private static void ValidateSlots(SponsorEntry entry, string label, List<AuthoredDataError> errors)
    {
        var kinds = new List<SlotKind>();
        foreach (var slot in entry.Slots)
        {
            if (!SlotKinds.TryParse(slot, out var kind) || kinds.Contains(kind))
            {
                errors.Add(new AuthoredDataError(BadSlots, label + " has an unknown or repeated slot '" + slot + "'"));
                return;
            }

            kinds.Add(kind);
        }

        if (kinds.Count == 0)
        {
            errors.Add(new AuthoredDataError(BadSlots, label + " fits no slot"));
        }

        if (SponsorIndustries.IsKnown(entry.Industry) && !SponsorIndustries.IsTechnical(entry.Industry) && kinds.Any(kind => !SlotKinds.IsLivery(kind)))
        {
            errors.Add(new AuthoredDataError(BadSlots, label + " is a livery industry and cannot take a technical slot"));
        }
    }

    private static void ValidateObjective(SponsorObjectiveEntry objective, string label, List<AuthoredDataError> errors)
    {
        var valid = SponsorObjectiveSpec.Kinds.Contains(objective.Kind) && objective.WithinDays is >= 30 and <= SponsorEstimates.DealDays - 1;
        if (valid)
        {
            valid = objective.Kind switch
            {
                SponsorObjectiveSpec.PodiumsAtLeast => int.TryParse(objective.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var podiums) && podiums >= 1,
                SponsorObjectiveSpec.ChampionshipPositionAtMost => int.TryParse(objective.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var position) && position >= 1,
                SponsorObjectiveSpec.PointsAtLeast => decimal.TryParse(objective.Value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var points) && points > 0,
                _ => objective.Value.Length == 3 && objective.Value.All(character => character is >= 'A' and <= 'Z'),
            };
        }

        if (!valid)
        {
            errors.Add(new AuthoredDataError(BadObjective, label + " has an invalid objective"));
        }
    }

    private static void CheckCoverage(SponsorsFile file, IReadOnlyList<RulePeriod> eraPeriods, List<AuthoredDataError> errors)
    {
        var catalog = SponsorsLoader.ToCatalog(file);
        for (var year = FirstSeason; year <= LastSeason; year++)
        {
            var era = SponsorEra.ForYear(eraPeriods, year);
            foreach (var kind in era.Slots.Distinct())
            {
                var count = catalog.Candidates(year, kind, era).Count;
                if (count < SponsorEstimates.MinCandidatesPerSlot)
                {
                    errors.Add(new AuthoredDataError(
                        TooFewCandidates,
                        year.ToString(CultureInfo.InvariantCulture) + " has " + count.ToString(CultureInfo.InvariantCulture) + " sponsors for a "
                        + SlotKinds.KeyOf(kind) + " slot, the minimum is " + SponsorEstimates.MinCandidatesPerSlot.ToString(CultureInfo.InvariantCulture)));
                }
            }
        }
    }
}
