namespace Paddock.Domain.World;

/// <summary>
/// Looks up who held a role at an organization in one season.
/// </summary>
public static class StaffBook
{
    public static IReadOnlyList<StaffAssignment> StaffAt(
        string org,
        int season,
        IReadOnlyList<StaffAssignment> assignments)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(org);
        ArgumentNullException.ThrowIfNull(assignments);

        var matches = new List<StaffAssignment>();
        foreach (var assignment in assignments)
        {
            if (!string.Equals(assignment.Org, org, StringComparison.Ordinal) || !Covers(assignment, season))
            {
                continue;
            }

            matches.Add(assignment);
        }

        return matches;
    }

    private static bool Covers(StaffAssignment assignment, int season)
    {
        if (assignment.FromYear > assignment.ToYear)
        {
            return false;
        }

        return assignment.FromYear <= season && season <= assignment.ToYear;
    }
}
