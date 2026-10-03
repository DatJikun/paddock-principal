using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Paddock.Application.World;

/// <summary>
/// Minimal world used until T16's clock exists. Holds the date, manager display names, and team notes
/// so the sample commands and the ready gate can prove determinism. Not the career state.
/// </summary>
public sealed class StubWorldState : IWorldState
{
    private readonly SortedDictionary<string, string> _managerNames = new(StringComparer.Ordinal);
    private readonly SortedDictionary<string, string> _notes = new(StringComparer.Ordinal);

    public StubWorldState(DateOnly currentDate)
    {
        CurrentDate = currentDate;
    }

    public DateOnly CurrentDate { get; private set; }

    public void AdvanceDate()
    {
        CurrentDate = CurrentDate.AddDays(1);
    }

    public void SetManagerName(string managerId, string displayName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(managerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        _managerNames[managerId] = displayName;
    }

    public string? ManagerName(string managerId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(managerId);
        return _managerNames.TryGetValue(managerId, out var name) ? name : null;
    }

    public void SetTeamNote(string teamId, string note)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(teamId);
        ArgumentNullException.ThrowIfNull(note);
        _notes[teamId] = note;
    }

    public string? TeamNote(string teamId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(teamId);
        return _notes.TryGetValue(teamId, out var note) ? note : null;
    }

    public string ContentHash()
    {
        var builder = new StringBuilder();
        Append(builder, "date");
        Append(builder, CurrentDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        builder.Append("managers ").Append(_managerNames.Count.ToString(CultureInfo.InvariantCulture)).Append('\n');
        foreach (var pair in _managerNames)
        {
            Append(builder, pair.Key);
            Append(builder, pair.Value);
        }

        builder.Append("notes ").Append(_notes.Count.ToString(CultureInfo.InvariantCulture)).Append('\n');
        foreach (var pair in _notes)
        {
            Append(builder, pair.Key);
            Append(builder, pair.Value);
        }

        var bytes = Encoding.UTF8.GetBytes(builder.ToString());
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private static void Append(StringBuilder builder, string value)
    {
        builder.Append(value.Length.ToString(CultureInfo.InvariantCulture));
        builder.Append(':');
        builder.Append(value);
        builder.Append('\n');
    }
}
