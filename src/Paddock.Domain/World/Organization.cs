using Paddock.Domain.Time;

namespace Paddock.Domain.World;

/// <summary>
/// Team, engine supplier, or a placeholder for a sponsor or a series body.
/// </summary>
public enum OrganizationKind
{
    Team = 0,
    EngineSupplier = 1,
    Sponsor = 2,
    SeriesBody = 3,
}

/// <summary>
/// One inclusive span of a name. A null <see cref="To"/> means the name is still current.
/// </summary>
public readonly record struct OrganizationNameSpan
{
    public OrganizationNameSpan(string name, GameDate from, GameDate? to)
    {
        Name = DisplayText.Require(name, nameof(name));
        if (to is GameDate end && end < from)
        {
            throw new ArgumentException("A name span ends before it starts.", nameof(to));
        }

        From = from;
        To = to;
    }

    public string Name { get; }

    public GameDate From { get; }

    public GameDate? To { get; }
}

public enum LineageDirection
{
    Predecessor = 0,
    Successor = 1,
}

/// <summary>
/// A dated successor or predecessor link. A lineage here is a single chain, not a branch.
/// </summary>
public readonly record struct LineageLink
{
    public LineageLink(OrganizationId otherId, LineageDirection direction, GameDate from, GameDate? to)
    {
        if (!otherId.IsAssigned)
        {
            throw new ArgumentException("Lineage link id is unassigned.", nameof(otherId));
        }

        if (!Enum.IsDefined(direction))
        {
            throw new ArgumentOutOfRangeException(nameof(direction), direction, "Unknown lineage direction.");
        }

        if (to is GameDate end && end < from)
        {
            throw new ArgumentException("A lineage link ends before it starts.", nameof(to));
        }

        OtherId = otherId;
        Direction = direction;
        From = from;
        To = to;
    }

    public OrganizationId OtherId { get; }

    public LineageDirection Direction { get; }

    public GameDate From { get; }

    public GameDate? To { get; }
}

/// <summary>
/// Input for <see cref="WorldState.AddOrganization"/>. The world assigns the id.
/// Lineage links are added afterwards with <see cref="WorldState.LinkLineage"/>.
/// </summary>
public sealed class OrganizationSpec
{
    public OrganizationSpec(
        OrganizationKind kind,
        bool isReal,
        string? realId,
        GameDate founded,
        GameDate? dissolved,
        long budget,
        IReadOnlyList<OrganizationNameSpan> names)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown organization kind.");
        }

        if (dissolved is GameDate end && end < founded)
        {
            throw new ArgumentException("An organization dissolves before it is founded.", nameof(dissolved));
        }

        if (isReal == string.IsNullOrWhiteSpace(realId))
        {
            throw new ArgumentException(
                "A real organization needs an authored id, and a generated organization must not have one.",
                nameof(realId));
        }

        Kind = kind;
        IsReal = isReal;
        RealId = isReal ? IdText.RequireReal(realId!, nameof(realId)) : null;
        Founded = founded;
        Dissolved = dissolved;
        Budget = budget;
        ArgumentNullException.ThrowIfNull(names);
        Names = names;
    }

    public OrganizationKind Kind { get; }

    public bool IsReal { get; }

    public string? RealId { get; }

    public GameDate Founded { get; }

    public GameDate? Dissolved { get; }

    /// <summary>Nominal opening budget in dollars: the era budget of the tier of a team at career start (an ESTIMATE), a placeholder where the data has none. Not a calibrated season budget.</summary>
    public long Budget { get; }

    public IReadOnlyList<OrganizationNameSpan> Names { get; }
}

/// <summary>
/// An organization: kind, name history, lineage links, life dates, and a budget placeholder.
/// </summary>
public sealed class Organization
{
    public Organization(
        OrganizationId id,
        OrganizationKind kind,
        bool isReal,
        GameDate founded,
        GameDate? dissolved,
        long budget,
        IReadOnlyList<OrganizationNameSpan> names,
        IReadOnlyList<LineageLink> lineage)
    {
        if (!id.IsAssigned)
        {
            throw new ArgumentException("Organization id is unassigned.", nameof(id));
        }

        if (id.IsReal != isReal)
        {
            throw new ArgumentException("IsReal does not match the id.", nameof(isReal));
        }

        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown organization kind.");
        }

        if (dissolved is GameDate end && end < founded)
        {
            throw new ArgumentException("An organization dissolves before it is founded.", nameof(dissolved));
        }

        Id = id;
        Kind = kind;
        IsReal = isReal;
        Founded = founded;
        Dissolved = dissolved;
        Budget = budget;
        Names = CanonicalNames(names);
        Lineage = CanonicalLinks(lineage, id);
    }

    public OrganizationId Id { get; }

    public OrganizationKind Kind { get; }

    public bool IsReal { get; }

    public GameDate Founded { get; }

    public GameDate? Dissolved { get; }

    /// <summary>Nominal opening budget in dollars: the era budget of the tier of a team at career start (an ESTIMATE), a placeholder where the data has none. Not a calibrated season budget. See <see cref="WorldText.BudgetExplanation"/>.</summary>
    public long Budget { get; }

    public IReadOnlyList<OrganizationNameSpan> Names { get; }

    public IReadOnlyList<LineageLink> Lineage { get; }

    public string NameOn(GameDate date)
    {
        foreach (var span in Names)
        {
            if (span.From <= date && (span.To is null || date <= span.To.Value))
            {
                return span.Name;
            }
        }

        throw new InvalidOperationException($"Organization '{Id}' has no name on {date}.");
    }

    internal Organization WithLineage(IReadOnlyList<LineageLink> lineage) =>
        new(Id, Kind, IsReal, Founded, Dissolved, Budget, Names, lineage);

    private static OrganizationNameSpan[] CanonicalNames(IReadOnlyList<OrganizationNameSpan> names)
    {
        ArgumentNullException.ThrowIfNull(names);
        if (names.Count == 0)
        {
            throw new ArgumentException("An organization needs a name.", nameof(names));
        }

        var copy = names.ToArray();
        for (var i = 0; i < copy.Length; i++)
        {
            if (copy[i].Name is null)
            {
                throw new ArgumentException("A name span is unassigned.", nameof(names));
            }
        }

        Array.Sort(copy, static (left, right) =>
        {
            var from = left.From.CompareTo(right.From);
            return from != 0 ? from : string.CompareOrdinal(left.Name, right.Name);
        });

        for (var i = 1; i < copy.Length; i++)
        {
            var previous = copy[i - 1];
            if (previous.To is null || previous.To.Value >= copy[i].From)
            {
                throw new ArgumentException("Name spans overlap.", nameof(names));
            }
        }

        return copy;
    }

    private static LineageLink[] CanonicalLinks(IReadOnlyList<LineageLink> links, OrganizationId self)
    {
        ArgumentNullException.ThrowIfNull(links);
        var copy = links.ToArray();
        var predecessors = 0;
        var successors = 0;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var link in copy)
        {
            if (!link.OtherId.IsAssigned)
            {
                throw new ArgumentException("A lineage link is unassigned.", nameof(links));
            }

            if (link.OtherId == self)
            {
                throw new ArgumentException("An organization cannot link to itself.", nameof(links));
            }

            if (!seen.Add(link.Direction + ":" + link.OtherId.Value))
            {
                throw new ArgumentException("Duplicate lineage link.", nameof(links));
            }

            if (link.Direction == LineageDirection.Predecessor)
            {
                predecessors++;
            }
            else
            {
                successors++;
            }
        }

        if (predecessors > 1 || successors > 1)
        {
            throw new ArgumentException("Lineage is a single chain, with at most one predecessor and one successor.", nameof(links));
        }

        Array.Sort(copy, static (left, right) =>
        {
            var direction = left.Direction.CompareTo(right.Direction);
            if (direction != 0)
            {
                return direction;
            }

            var id = string.CompareOrdinal(left.OtherId.Value, right.OtherId.Value);
            return id != 0 ? id : left.From.CompareTo(right.From);
        });
        return copy;
    }
}
