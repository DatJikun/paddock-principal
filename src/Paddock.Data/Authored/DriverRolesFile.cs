using System.Text.Json.Serialization;
using Paddock.Domain.World;

namespace Paddock.Data.Authored;

/// <summary>One authored seat role: in the career that starts in <see cref="Year"/>, the driver sits as <see cref="Role"/>.</summary>
public sealed record DriverRoleEntry(
    [property: JsonPropertyName("year")] int Year,
    [property: JsonPropertyName("driver_id")] string DriverId,
    [property: JsonPropertyName("role")] string Role);

/// <summary>
/// <c>data/authored/people/driver_roles.json</c> (#325): the first and second driver of a team where history disagrees with the
/// default (the better-rated driver is first). It is the one file the owner edits by hand, so it starts empty. An entry names the start
/// year of the career, the driver id as the people schedule spells it and the role, <c>first</c> or <c>second</c>.
/// </summary>
public sealed record DriverRolesFile(
    [property: JsonPropertyName("notes")] string Notes,
    [property: JsonPropertyName("roles")] IReadOnlyList<DriverRoleEntry> Roles);

/// <summary>Where the file lives under the data directory.</summary>
public static class DriverRolesLoader
{
    public const string RelativePath = "authored/people/driver_roles.json";
}

/// <summary>The role words of <see cref="DriverRoleEntry.Role"/>.</summary>
public static class DriverRoleWords
{
    public const string First = "first";

    public const string Second = "second";

    /// <summary>The seat a role word stands for, or null when the word is neither.</summary>
    public static SeatStatus? SeatOf(string? word) => word switch
    {
        First => SeatStatus.NumberOne,
        Second => SeatStatus.NumberTwo,
        _ => null,
    };
}

/// <summary>
/// The authored seat roles by start year and driver id. A pure lookup; the world initializer lets an entry win over the default.
/// An entry the validator rejects (an unknown word) is left out here, so loading never fails on a half-edited file.
/// </summary>
public sealed class DriverRoleOverrides
{
    private readonly Dictionary<(int Year, string DriverId), SeatStatus> _roles;

    public DriverRoleOverrides(IEnumerable<DriverRoleEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        _roles = [];
        foreach (var entry in entries)
        {
            if (DriverRoleWords.SeatOf(entry.Role) is SeatStatus seat && !string.IsNullOrWhiteSpace(entry.DriverId))
            {
                _roles.TryAdd((entry.Year, entry.DriverId), seat);
            }
        }
    }

    public static DriverRoleOverrides None { get; } = new([]);

    public int Count => _roles.Count;

    /// <summary>The authored seat of <paramref name="driverId"/> in the career that starts in <paramref name="year"/>, or null.</summary>
    public SeatStatus? RoleOf(int year, string driverId) =>
        _roles.TryGetValue((year, driverId), out var seat) ? seat : null;
}
