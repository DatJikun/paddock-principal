using System.Globalization;
using Paddock.Domain.World;

namespace Paddock.Domain.People;

/// <summary>
/// Which race engineer works with which driver, and how long they have been a pair (PP-059).
/// The weekend does not read this yet. Truth stays here; a query decides what a manager sees.
/// </summary>
public sealed record RaceEngineerLink
{
    public RaceEngineerLink(OrganizationId team, PersonId engineer, PersonId driver, int relationship, int season)
    {
        if (!team.IsAssigned || !engineer.IsAssigned || !driver.IsAssigned)
        {
            throw new ArgumentException("A race-engineer link needs a team, an engineer and a driver.");
        }

        if (relationship < 1 || relationship > StaffEstimates.RelationshipMax)
        {
            throw new ArgumentOutOfRangeException(nameof(relationship), relationship, "Relationship is from 1 to 100.");
        }

        if (season < GenerationEstimates.MinSeason)
        {
            throw new ArgumentOutOfRangeException(nameof(season), season, "Season is out of range.");
        }

        Team = team;
        Engineer = engineer;
        Driver = driver;
        Relationship = relationship;
        Season = season;
    }

    public OrganizationId Team { get; init; }

    public PersonId Engineer { get; init; }

    public PersonId Driver { get; init; }

    public int Relationship { get; init; }

    public int Season { get; init; }
}

/// <summary>
/// The <c>staff</c> world section: one link per race engineer. Immutable.
/// <para>
/// Canonical text (schema 1), after the section header:
/// <code>
/// links &lt;count&gt;
/// link &lt;len&gt;:&lt;team&gt; &lt;len&gt;:&lt;engineer&gt; &lt;len&gt;:&lt;driver&gt; &lt;relationship&gt; &lt;season&gt;
/// </code>
/// </para>
/// </summary>
public sealed class StaffSection : IWorldSection
{
    public const string SectionName = "staff";

    private readonly SortedDictionary<string, RaceEngineerLink> _links;

    private StaffSection(SortedDictionary<string, RaceEngineerLink> links) => _links = links;

    public static StaffSection Empty { get; } = new(new SortedDictionary<string, RaceEngineerLink>(StringComparer.Ordinal));

    public string Name => SectionName;

    public int SchemaVersion => 1;

    public bool IsEmpty => _links.Count == 0;

    /// <summary>Links in ordinal order of the engineer's id.</summary>
    public IReadOnlyList<RaceEngineerLink> Links => _links.Values.ToArray();

    public RaceEngineerLink? OfEngineer(PersonId engineer) =>
        engineer.IsAssigned && _links.TryGetValue(engineer.Value, out var link) ? link : null;

    public static StaffSection Restore(IEnumerable<RaceEngineerLink> links)
    {
        ArgumentNullException.ThrowIfNull(links);
        var section = Empty;
        foreach (var link in links)
        {
            if (section.OfEngineer(link.Engineer) is not null)
            {
                throw new InvalidOperationException($"Engineer '{link.Engineer}' is paired twice.");
            }

            section = section.With(link);
        }

        return section;
    }

    public StaffSection With(RaceEngineerLink link)
    {
        ArgumentNullException.ThrowIfNull(link);
        var copy = new SortedDictionary<string, RaceEngineerLink>(_links, StringComparer.Ordinal);
        copy[link.Engineer.Value] = link;
        return new StaffSection(copy);
    }

    public void WriteCanonical(CanonicalWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.Count("links", _links.Count);
        foreach (var link in _links.Values)
        {
            writer.Begin("link");
            writer.Field(link.Team.Value);
            writer.Space();
            writer.Field(link.Engineer.Value);
            writer.Space();
            writer.Field(link.Driver.Value);
            writer.Raw(" " + link.Relationship.ToString(CultureInfo.InvariantCulture) + " " + link.Season.ToString(CultureInfo.InvariantCulture));
            writer.End();
        }
    }
}
