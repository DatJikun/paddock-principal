using System.Globalization;
using Paddock.Domain.World;

namespace Paddock.Domain.Contracts;

/// <summary>
/// One driver and one team: whether he already asked this season, the salary he asked for, trust, and the extra
/// chance he leaves after a refusal. Trust and leave bias are 0–100. An empty section is left out of the world.
/// </summary>
public sealed record RaiseRecord(PersonId Person, OrganizationId Team, int Season, long AskedSalary, int Trust, int LeaveBias);

/// <summary>The <c>raises</c> section (PP-057). Schema 1.</summary>
public sealed class RaisesSection : IWorldSection
{
    public const string SectionName = "raises";

    private readonly SortedDictionary<string, RaiseRecord> _rows;

    private RaisesSection(SortedDictionary<string, RaiseRecord> rows) => _rows = rows;

    public static RaisesSection Empty { get; } = new(new SortedDictionary<string, RaiseRecord>(StringComparer.Ordinal));

    public string Name => SectionName;

    public int SchemaVersion => 1;

    public bool IsEmpty => _rows.Count == 0;

    public IReadOnlyList<RaiseRecord> Records => _rows.Values.ToArray();

    public bool Considered(PersonId person, OrganizationId team, int season) =>
        _rows.TryGetValue(Key(person, team), out var row) && row.Season == season;

    public int Trust(PersonId person, OrganizationId team) =>
        _rows.TryGetValue(Key(person, team), out var row) ? row.Trust : NegotiationEstimates.RaiseTrustStart;

    public int LeaveBias(PersonId person, OrganizationId team) =>
        _rows.TryGetValue(Key(person, team), out var row) ? row.LeaveBias : 0;

    /// <summary>Marks the season as checked. <paramref name="askedSalary"/> is 0 when he did not ask.</summary>
    public RaisesSection Consider(PersonId person, OrganizationId team, int season, long askedSalary)
    {
        var next = Record(person, team, season, askedSalary, Trust(person, team), LeaveBias(person, team));
        var rows = Copy();
        rows[Key(person, team)] = next;
        return new RaisesSection(rows);
    }

    public RaisesSection Refuse(PersonId person, OrganizationId team)
    {
        var row = Require(person, team);
        var rows = Copy();
        rows[Key(person, team)] = Record(
            person,
            team,
            row.Season,
            row.AskedSalary,
            Math.Max(0, row.Trust - NegotiationEstimates.RaiseTrustHit),
            Math.Min(100, row.LeaveBias + NegotiationEstimates.RaiseLeaveHit));
        return new RaisesSection(rows);
    }

    public RaisesSection Accept(PersonId person, OrganizationId team)
    {
        var row = Require(person, team);
        var rows = Copy();
        rows[Key(person, team)] = Record(person, team, row.Season, row.AskedSalary, row.Trust, 0);
        return new RaisesSection(rows);
    }

    public static RaisesSection From(IEnumerable<RaiseRecord> records)
    {
        var rows = new SortedDictionary<string, RaiseRecord>(StringComparer.Ordinal);
        foreach (var record in records)
        {
            rows[Key(record.Person, record.Team)] = Record(
                record.Person, record.Team, record.Season, record.AskedSalary, record.Trust, record.LeaveBias);
        }

        return new RaisesSection(rows);
    }

    public void WriteCanonical(CanonicalWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.Count("raises", _rows.Count);
        foreach (var row in _rows.Values)
        {
            writer.Begin("raise");
            writer.Field(row.Person.Value);
            writer.Field(row.Team.Value);
            writer.Raw(
                row.Season.ToString(CultureInfo.InvariantCulture)
                + " " + row.AskedSalary.ToString(CultureInfo.InvariantCulture)
                + " " + row.Trust.ToString(CultureInfo.InvariantCulture)
                + " " + row.LeaveBias.ToString(CultureInfo.InvariantCulture));
            writer.End();
        }
    }

    private static RaiseRecord Record(PersonId person, OrganizationId team, int season, long askedSalary, int trust, int leaveBias)
    {
        if (!person.IsAssigned || !team.IsAssigned)
        {
            throw new ArgumentException("A raise needs a person and a team.");
        }

        if (season < 1950)
        {
            throw new ArgumentOutOfRangeException(nameof(season), season, "A season is 1950 or later.");
        }

        if (askedSalary < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(askedSalary), askedSalary, "An asked salary is not negative.");
        }

        if (trust is < 0 or > 100 || leaveBias is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(trust), "Trust and leave bias run from 0 to 100.");
        }

        return new RaiseRecord(person, team, season, askedSalary, trust, leaveBias);
    }

    private RaiseRecord Require(PersonId person, OrganizationId team) =>
        _rows.TryGetValue(Key(person, team), out var row)
            ? row
            : throw new InvalidOperationException("No raise record for " + person.Value + " at " + team.Value + ".");

    private SortedDictionary<string, RaiseRecord> Copy() => new(_rows, StringComparer.Ordinal);

    private static string Key(PersonId person, OrganizationId team) => person.Value + "|" + team.Value;
}
