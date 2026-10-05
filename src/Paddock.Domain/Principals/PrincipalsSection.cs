using System.Globalization;
using Paddock.Domain.Time;
using Paddock.Domain.World;

namespace Paddock.Domain.Principals;

/// <summary>
/// What the world keeps about one AI team principal (T44, DESIGN section 8): the archetype assigned to the person who runs the
/// team, the day the AI looks at the team next, and the little it must remember between reviews.
/// </summary>
/// <param name="Organization">The team.</param>
/// <param name="Archetype">One of <c>Contender</c>, <c>Builder</c>, <c>Opportunist</c>, <c>Survivor</c> (stored as text so Domain needs no AI type).</param>
/// <param name="Person">The principal the archetype was assigned for, or null when the team had none. A new principal gets a new assignment.</param>
/// <param name="AssignedOn">The day the archetype was assigned.</param>
/// <param name="LastReview">The last day the AI reviewed the team, or null before the first one.</param>
/// <param name="NextReview">The next day the AI looks at the team. Event triggers pull it earlier.</param>
/// <param name="SacrificedSeason">The season the principal wrote off to build the next car (0 when none).</param>
/// <param name="ScoutSeason">The last season the principal pointed the scouts at the talent pool (0 when never).</param>
/// <param name="StaffRoles">The key staff roles the principal considers part of the team, sorted and joined by commas.</param>
public sealed record AiPrincipalRecord(
    OrganizationId Organization,
    string Archetype,
    PersonId? Person,
    GameDate AssignedOn,
    GameDate? LastReview,
    GameDate NextReview,
    int SacrificedSeason,
    int ScoutSeason,
    string StaffRoles);

/// <summary>
/// The <c>principals</c> world section (T44): one record per AI-run team. Immutable; every change returns a new section.
/// It holds the AI's own bookkeeping (archetype, review dates). The archetype is truth about a person's style and is never shown
/// to a manager; the player reads a team's visible reasons through the decision trace (INV-003).
/// <para>
/// Canonical text (<see cref="SchemaVersion"/> 1), after the section header written by the state hash:
/// <code>
/// principals &lt;count&gt;
/// principal &lt;len&gt;:&lt;organizationId&gt; &lt;len&gt;:&lt;archetype&gt; &lt;len&gt;:&lt;person or -&gt; &lt;len&gt;:&lt;assignedOn&gt;
/// review &lt;len&gt;:&lt;lastReview or -&gt; &lt;len&gt;:&lt;nextReview&gt; &lt;sacrificedSeason&gt; &lt;scoutSeason&gt; &lt;len&gt;:&lt;staffRoles&gt;
/// </code>
/// </para>
/// </summary>
public sealed class PrincipalsSection : IWorldSection
{
    public const string SectionName = "principals";

    private const string None = "-";

    private readonly SortedDictionary<string, AiPrincipalRecord> _records;

    private PrincipalsSection(SortedDictionary<string, AiPrincipalRecord> records)
    {
        _records = records;
    }

    public static PrincipalsSection Empty { get; } = new(new SortedDictionary<string, AiPrincipalRecord>(StringComparer.Ordinal));

    public string Name => SectionName;

    public int SchemaVersion => 1;

    public bool IsEmpty => _records.Count == 0;

    /// <summary>The records, in ordinal order of the organization id.</summary>
    public IReadOnlyList<AiPrincipalRecord> Records => _records.Values.ToArray();

    public static PrincipalsSection Restore(IEnumerable<AiPrincipalRecord> records)
    {
        ArgumentNullException.ThrowIfNull(records);
        var section = Empty;
        foreach (var record in records)
        {
            if (section.Of(record.Organization) is not null)
            {
                throw new InvalidOperationException($"Organization '{record.Organization}' has two AI principal records.");
            }

            section = section.With(record);
        }

        return section;
    }

    public AiPrincipalRecord? Of(OrganizationId organization) =>
        organization.IsAssigned && _records.TryGetValue(organization.Value, out var record) ? record : null;

    public PrincipalsSection With(AiPrincipalRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentException.ThrowIfNullOrWhiteSpace(record.Archetype);
        ArgumentNullException.ThrowIfNull(record.StaffRoles);
        if (!record.Organization.IsAssigned)
        {
            throw new ArgumentException("A principal record needs an organization.", nameof(record));
        }

        if (record.SacrificedSeason < 0 || record.ScoutSeason < 0)
        {
            throw new ArgumentException("A season in a principal record cannot be negative.", nameof(record));
        }

        if (record.LastReview is GameDate last && last > record.NextReview)
        {
            throw new ArgumentException("The next review cannot be before the last one.", nameof(record));
        }

        var copy = new SortedDictionary<string, AiPrincipalRecord>(_records, StringComparer.Ordinal)
        {
            [record.Organization.Value] = record,
        };
        return new PrincipalsSection(copy);
    }

    public PrincipalsSection Without(OrganizationId organization)
    {
        if (!organization.IsAssigned || !_records.ContainsKey(organization.Value))
        {
            return this;
        }

        var copy = new SortedDictionary<string, AiPrincipalRecord>(_records, StringComparer.Ordinal);
        copy.Remove(organization.Value);
        return new PrincipalsSection(copy);
    }

    public void WriteCanonical(CanonicalWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.Count("principals", _records.Count);
        foreach (var record in _records.Values)
        {
            writer.Begin("principal");
            writer.Field(record.Organization.Value);
            writer.Space();
            writer.Field(record.Archetype);
            writer.Space();
            writer.Field(record.Person?.Value ?? None);
            writer.Space();
            writer.Field(record.AssignedOn.ToString());
            writer.End();
            writer.Begin("review");
            writer.Field(record.LastReview?.ToString() ?? None);
            writer.Space();
            writer.Field(record.NextReview.ToString());
            writer.Raw(" " + record.SacrificedSeason.ToString(CultureInfo.InvariantCulture) + " " + record.ScoutSeason.ToString(CultureInfo.InvariantCulture) + " ");
            writer.Field(record.StaffRoles);
            writer.End();
        }
    }
}
