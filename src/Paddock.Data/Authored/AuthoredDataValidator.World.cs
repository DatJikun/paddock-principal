using System.Globalization;

namespace Paddock.Data.Authored;

public static partial class AuthoredDataValidator
{
    public const string DuplicateTechnology = "duplicate-technology";
    public const string TechnologyArea = "technology-area";
    public const string UnknownPrerequisite = "unknown-prerequisite";
    public const string PrerequisiteCycle = "prerequisite-cycle";
    public const string TechnologySeasonOrder = "technology-season-order";
    public const string InvertedBan = "inverted-ban";
    public const string UnknownConstructor = "unknown-constructor";
    public const string EngineYear = "engine-year";
    public const string EngineType = "engine-type";
    public const string EmptyText = "empty-text";
    public const string DuplicateLineage = "duplicate-lineage";
    public const string LineageOverlap = "lineage-overlap";
    public const string ConstructorYear = "constructor-year";
    public const string DuplicateOrganization = "duplicate-organization";
    public const string InvertedSpan = "inverted-span";
    public const string FoundedAfterEntry = "founded-after-entry";
    public const string DuplicateStaff = "duplicate-staff";
    public const string StaffBorn = "staff-born";
    public const string InvertedCareer = "inverted-career";
    public const string StaffRole = "staff-role";
    public const string SourceUrl = "source-url";

    private static readonly string[] TechnologyAreas =
    [
        "aero",
        "chassis",
        "engine",
        "electronics",
        "safety",
        "tyres",
        "other",
    ];

    private static readonly string[] StaffRoles =
    [
        "technical_director",
        "team_principal",
        "chief_designer",
        "owner",
        "designer",
        "head_of_aero",
        "race_engineer",
        "engine_designer",
    ];

    private static void AppendTechnologiesTeamsAndStaff(List<AuthoredDataError> errors, AuthoredData data)
    {
        var constructors = data.ConstructorIds;
        AppendTechnologies(errors, data.Technologies, constructors);
        AppendEngines(errors, data.Engines);
        AppendLineage(errors, data.Lineage, constructors);
        AppendFounders(errors, data.Founders, constructors);
        AppendStaff(errors, data.Staff, constructors);
        AppendLegacySources(errors, data);
    }

    private static void AppendTechnologies(
        List<AuthoredDataError> errors,
        IReadOnlyList<Technology> technologies,
        IReadOnlySet<string> constructors)
    {
        AppendDuplicates(errors, technologies.Select(technology => technology.Id), DuplicateTechnology, "technology id");

        var ids = new HashSet<string>(technologies.Count, StringComparer.Ordinal);
        foreach (var technology in technologies)
        {
            ids.Add(technology.Id);
        }

        var areas = new HashSet<string>(TechnologyAreas, StringComparer.Ordinal);
        foreach (var technology in technologies)
        {
            if (!areas.Contains(technology.Area))
            {
                errors.Add(new AuthoredDataError(
                    TechnologyArea,
                    $"technology '{technology.Id}' area '{technology.Area}' is not one of {string.Join(", ", TechnologyAreas)}"));
            }

            AppendSeasonOrder(errors, technology);
            AppendBans(errors, technology);
            AppendTeam(errors, technology, constructors);
            AppendTechnologySources(errors, technology);

            foreach (var prerequisite in technology.Prerequisites)
            {
                if (ids.Contains(prerequisite))
                {
                    continue;
                }

                errors.Add(new AuthoredDataError(
                    UnknownPrerequisite,
                    $"technology '{technology.Id}' prerequisite '{prerequisite}' does not exist"));
            }
        }

        AppendPrerequisiteCycles(errors, technologies, ids);
    }

    private static void AppendSeasonOrder(List<AuthoredDataError> errors, Technology technology)
    {
        // A null season means the year was not established. Compare only the years that are present.
        var earliest = technology.EarliestPlausible.Season;
        var first = technology.FirstUsed.Season;
        var widespread = technology.WidespreadBy;
        if (earliest is int earliestYear && first is int firstYear && earliestYear > firstYear)
        {
            errors.Add(new AuthoredDataError(
                TechnologySeasonOrder,
                $"technology '{technology.Id}' has earliest_plausible.season {Year(earliestYear)} after first_used.season {Year(firstYear)}"));
        }

        if (first is int firstUsed && widespread is int widespreadYear && firstUsed > widespreadYear)
        {
            errors.Add(new AuthoredDataError(
                TechnologySeasonOrder,
                $"technology '{technology.Id}' has first_used.season {Year(firstUsed)} after widespread_by {Year(widespreadYear)}"));
        }

        if (first is null && earliest is int early && widespread is int wide && early > wide)
        {
            errors.Add(new AuthoredDataError(
                TechnologySeasonOrder,
                $"technology '{technology.Id}' has earliest_plausible.season {Year(early)} after widespread_by {Year(wide)}"));
        }
    }

    private static void AppendBans(List<AuthoredDataError> errors, Technology technology)
    {
        foreach (var ban in technology.Banned)
        {
            // The catalogue omits `to` when a ban has no recorded end year.
            if (ban.To is int toYear && ban.From > toYear)
            {
                errors.Add(new AuthoredDataError(
                    InvertedBan,
                    $"technology '{technology.Id}' ban {Span(ban.From, toYear)} ends before it starts"));
            }
        }
    }

    private static void AppendTeam(
        List<AuthoredDataError> errors,
        Technology technology,
        IReadOnlySet<string> constructors)
    {
        var team = technology.FirstUsed.Team;
        if (team is null || constructors.Contains(team))
        {
            return;
        }

        errors.Add(new AuthoredDataError(
            UnknownConstructor,
            $"technology '{technology.Id}' first_used.team '{team}' is not a known constructor"));
    }

    private static void AppendTechnologySources(List<AuthoredDataError> errors, Technology technology)
    {
        AppendSource(errors, technology.FirstUsed.Source, $"technology '{technology.Id}' first_used");
        AppendSource(errors, technology.EarliestPlausible.Source, $"technology '{technology.Id}' earliest_plausible");
        foreach (var ban in technology.Banned)
        {
            AppendSource(errors, ban.Source, $"technology '{technology.Id}' ban from {Year(ban.From)}");
        }
    }

    private static void AppendPrerequisiteCycles(
        List<AuthoredDataError> errors,
        IReadOnlyList<Technology> technologies,
        HashSet<string> ids)
    {
        var prerequisites = new Dictionary<string, IReadOnlyList<string>>(ids.Count, StringComparer.Ordinal);
        foreach (var technology in technologies)
        {
            prerequisites.TryAdd(technology.Id, technology.Prerequisites);
        }

        var color = new Dictionary<string, int>(ids.Count, StringComparer.Ordinal);
        var stack = new List<string>();
        var reported = new HashSet<string>(StringComparer.Ordinal);
        var started = new HashSet<string>(StringComparer.Ordinal);
        foreach (var technology in technologies)
        {
            if (!started.Add(technology.Id))
            {
                continue;
            }

            if (color.TryGetValue(technology.Id, out var state) && state != 0)
            {
                continue;
            }

            VisitPrerequisite(technology.Id, prerequisites, color, stack, reported, errors);
        }
    }

    private static void VisitPrerequisite(
        string id,
        Dictionary<string, IReadOnlyList<string>> prerequisites,
        Dictionary<string, int> color,
        List<string> stack,
        HashSet<string> reported,
        List<AuthoredDataError> errors)
    {
        color[id] = 1;
        stack.Add(id);
        if (prerequisites.TryGetValue(id, out var next))
        {
            foreach (var prerequisite in next)
            {
                if (!prerequisites.ContainsKey(prerequisite))
                {
                    continue;
                }

                color.TryGetValue(prerequisite, out var state);
                if (state == 1)
                {
                    ReportCycle(errors, stack, prerequisite, reported);
                }
                else if (state == 0)
                {
                    VisitPrerequisite(prerequisite, prerequisites, color, stack, reported, errors);
                }
            }
        }

        stack.RemoveAt(stack.Count - 1);
        color[id] = 2;
    }

    private static void ReportCycle(
        List<AuthoredDataError> errors,
        List<string> stack,
        string repeated,
        HashSet<string> reported)
    {
        var start = stack.IndexOf(repeated);
        if (start < 0)
        {
            return;
        }

        var body = new string[stack.Count - start];
        for (var i = 0; i < body.Length; i++)
        {
            body[i] = stack[start + i];
        }

        var minIndex = 0;
        for (var i = 1; i < body.Length; i++)
        {
            if (string.CompareOrdinal(body[i], body[minIndex]) < 0)
            {
                minIndex = i;
            }
        }

        var rotated = new string[body.Length];
        for (var i = 0; i < body.Length; i++)
        {
            rotated[i] = body[(minIndex + i) % body.Length];
        }

        var key = string.Join("\u001f", rotated);
        if (!reported.Add(key))
        {
            return;
        }

        var path = string.Join(" -> ", rotated.Append(rotated[0]).Select(id => $"'{id}'"));
        errors.Add(new AuthoredDataError(
            PrerequisiteCycle,
            $"technologies {path} form a prerequisite cycle"));
    }

    private static void AppendEngines(List<AuthoredDataError> errors, EnginesFile engines)
    {
        foreach (var supplyType in engines.SupplyTypes)
        {
            AppendEmpty(errors, supplyType.Id, $"supply type '{supplyType.Id}' id");
            AppendEmpty(errors, supplyType.Meaning, $"supply type '{supplyType.Id}' meaning");
        }

        var types = new HashSet<string>(StringComparer.Ordinal);
        var typeNames = new List<string>();
        foreach (var supplyType in engines.SupplyTypes)
        {
            if (string.IsNullOrWhiteSpace(supplyType.Id) || !types.Add(supplyType.Id))
            {
                continue;
            }

            typeNames.Add(supplyType.Id);
        }

        var allowed = string.Join(", ", typeNames);
        foreach (var entry in engines.Entries)
        {
            var where = $"engine '{entry.ConstructorId}' {Year(entry.Year)}";
            AppendEmpty(errors, entry.ConstructorId, $"{where} constructorId");
            AppendEmpty(errors, entry.Supplier, $"{where} supplier");
            AppendEmpty(errors, entry.EngineName, $"{where} engine_name");
            AppendEmpty(errors, entry.Type, $"{where} type");
            AppendEmpty(errors, entry.Confidence, $"{where} confidence");
            AppendEmpty(errors, entry.Source, $"{where} source");

            if (entry.Year < FirstSeason || entry.Year > LastSeason)
            {
                errors.Add(new AuthoredDataError(
                    EngineYear,
                    $"{where} is outside {Year(FirstSeason)}-{Year(LastSeason)}"));
            }

            if (!string.IsNullOrWhiteSpace(entry.Type) && !types.Contains(entry.Type))
            {
                errors.Add(new AuthoredDataError(
                    EngineType,
                    $"{where} type '{entry.Type}' is not one of {allowed}"));
            }

            AppendSource(errors, entry.Source, where);
        }
    }

    private static void AppendLineage(
        List<AuthoredDataError> errors,
        LineageFile lineage,
        IReadOnlySet<string> constructors)
    {
        AppendDuplicates(
            errors,
            lineage.Lineages.Select(group => group.LineageId),
            DuplicateLineage,
            "lineage id");

        foreach (var group in lineage.Lineages)
        {
            var entries = group.Entries;
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (!constructors.Contains(entry.ConstructorId))
                {
                    errors.Add(new AuthoredDataError(
                        UnknownConstructor,
                        $"lineage '{group.LineageId}' constructor '{entry.ConstructorId}' is not a known constructor"));
                }

                AppendSource(
                    errors,
                    entry.Source,
                    $"lineage '{group.LineageId}' constructor '{entry.ConstructorId}'");

                for (var j = i + 1; j < entries.Count; j++)
                {
                    var other = entries[j];
                    if (!YearsOverlap(entry.From, entry.To, other.From, other.To))
                    {
                        continue;
                    }

                    errors.Add(new AuthoredDataError(
                        LineageOverlap,
                        $"lineage '{group.LineageId}' entries '{entry.ConstructorId}' ({Span(entry.From, entry.To)}) and '{other.ConstructorId}' ({Span(other.From, other.To)}) overlap"));
                }
            }
        }

        AppendConstructorYears(errors, lineage);
    }

    private static void AppendConstructorYears(List<AuthoredDataError> errors, LineageFile lineage)
    {
        var owner = new Dictionary<string, Dictionary<int, string>>(StringComparer.Ordinal);
        var reported = new HashSet<string>(StringComparer.Ordinal);
        foreach (var group in lineage.Lineages)
        {
            foreach (var entry in group.Entries)
            {
                var end = entry.To ?? LastSeason;
                if (entry.From > end)
                {
                    continue;
                }

                if (!owner.TryGetValue(entry.ConstructorId, out var years))
                {
                    years = [];
                    owner.Add(entry.ConstructorId, years);
                }

                for (var year = entry.From; year <= end; year++)
                {
                    if (!years.TryGetValue(year, out var existing))
                    {
                        years.Add(year, group.LineageId);
                        continue;
                    }

                    if (string.Equals(existing, group.LineageId, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var key = string.Join("\u001f", entry.ConstructorId, existing, group.LineageId);
                    if (!reported.Add(key))
                    {
                        continue;
                    }

                    errors.Add(new AuthoredDataError(
                        ConstructorYear,
                        $"constructor '{entry.ConstructorId}' in {Year(year)} belongs to lineages '{existing}' and '{group.LineageId}'"));
                }
            }
        }
    }

    private static void AppendFounders(
        List<AuthoredDataError> errors,
        FoundersFile founders,
        IReadOnlySet<string> constructors)
    {
        AppendDuplicates(
            errors,
            founders.Organizations.Select(organization => organization.OrganizationId),
            DuplicateOrganization,
            "organization id");

        foreach (var organization in founders.Organizations)
        {
            AppendSource(errors, organization.Source, $"organization '{organization.OrganizationId}'");
            foreach (var entry in organization.ConstructorEntries)
            {
                if (!constructors.Contains(entry.ConstructorId))
                {
                    errors.Add(new AuthoredDataError(
                        UnknownConstructor,
                        $"organization '{organization.OrganizationId}' constructor '{entry.ConstructorId}' is not a known constructor"));
                }

                if (entry.From > entry.To)
                {
                    errors.Add(new AuthoredDataError(
                        InvertedSpan,
                        $"organization '{organization.OrganizationId}' constructor '{entry.ConstructorId}' period {Span(entry.From, entry.To)} ends before it starts"));
                }

                if (organization.Founded is int founded && founded > entry.From)
                {
                    errors.Add(new AuthoredDataError(
                        FoundedAfterEntry,
                        $"organization '{organization.OrganizationId}' founded {Year(founded)} is after constructor '{entry.ConstructorId}' entry {Span(entry.From, entry.To)}"));
                }
            }
        }
    }

    private static void AppendStaff(
        List<AuthoredDataError> errors,
        IReadOnlyList<StaffMember> staff,
        IReadOnlySet<string> constructors)
    {
        AppendDuplicates(errors, staff.Select(person => person.Id), DuplicateStaff, "staff id");

        var roles = new HashSet<string>(StaffRoles, StringComparer.Ordinal);
        foreach (var person in staff)
        {
            if (person.Born is not null && !IsIsoDate(person.Born))
            {
                errors.Add(new AuthoredDataError(
                    StaffBorn,
                    $"staff '{person.Id}' born '{person.Born}' is not an ISO date"));
            }

            foreach (var stint in person.Career)
            {
                var where = $"staff '{person.Id}'";
                if (stint.From > stint.To)
                {
                    errors.Add(new AuthoredDataError(
                        InvertedCareer,
                        $"{where} career at '{stint.Org}' {Span(stint.From, stint.To)} ends before it starts"));
                }

                if (!roles.Contains(stint.Role))
                {
                    errors.Add(new AuthoredDataError(
                        StaffRole,
                        $"{where} role '{stint.Role}' is not one of {string.Join(", ", StaffRoles)}"));
                }

                if (stint.Series is null && !constructors.Contains(stint.Org))
                {
                    errors.Add(new AuthoredDataError(
                        UnknownConstructor,
                        $"{where} F1 stint at '{stint.Org}' is not a known constructor"));
                }

                AppendSource(errors, stint.Source, $"{where} stint at '{stint.Org}'");
            }
        }
    }

    private static void AppendLegacySources(List<AuthoredDataError> errors, AuthoredData data)
    {
        foreach (var period in data.Timeline)
        {
            AppendSource(
                errors,
                period.Source,
                $"timeline period '{period.Dimension}' ({Span(period.From, period.To)})");
        }

        foreach (var circuit in data.Circuits.Circuits)
        {
            foreach (var layout in circuit.Layouts)
            {
                AppendSource(errors, layout.Source, $"layout '{layout.LayoutId}'");
            }
        }
    }

    private static void AppendDuplicates(
        List<AuthoredDataError> errors,
        IEnumerable<string> ids,
        string code,
        string noun)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        var order = new List<string>();
        foreach (var id in ids)
        {
            if (counts.TryGetValue(id, out var count))
            {
                counts[id] = count + 1;
                continue;
            }

            counts.Add(id, 1);
            order.Add(id);
        }

        foreach (var id in order)
        {
            var count = counts[id];
            if (count < 2)
            {
                continue;
            }

            errors.Add(new AuthoredDataError(
                code,
                $"{noun} '{id}' appears {count.ToString(CultureInfo.InvariantCulture)} times"));
        }
    }

    private static void AppendEmpty(List<AuthoredDataError> errors, string value, string where)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        errors.Add(new AuthoredDataError(EmptyText, $"{where} is empty"));
    }

    private static void AppendSource(List<AuthoredDataError> errors, string source, string where)
    {
        if (IsAbsoluteHttps(source))
        {
            return;
        }

        errors.Add(new AuthoredDataError(
            SourceUrl,
            $"source '{source}' on {where} is not an absolute https URL"));
    }

    private static bool IsAbsoluteHttps(string source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return false;
        }

        if (!Uri.TryCreate(source, UriKind.Absolute, out var uri))
        {
            return false;
        }

        return string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal)
            && !string.IsNullOrEmpty(uri.IdnHost);
    }

    private static bool IsIsoDate(string value) =>
        DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);

    private static bool YearsOverlap(int fromA, int? toA, int fromB, int? toB)
    {
        var endA = toA ?? LastSeason;
        var endB = toB ?? LastSeason;
        if (fromA > endA || fromB > endB)
        {
            return false;
        }

        return fromA <= endB && fromB <= endA;
    }

    private static string Year(int year) => year.ToString(CultureInfo.InvariantCulture);
}
