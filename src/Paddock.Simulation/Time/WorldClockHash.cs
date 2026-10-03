using System.Text;
using Paddock.Domain.Random;
using Paddock.Domain.Time;

namespace Paddock.Simulation.Time;

/// <summary>
/// FNV-1a 64 over a little-endian canonical encoding of <see cref="WorldClockState"/>.
/// The same layout as domain stream hashing: no <see cref="string.GetHashCode"/> and no wall-clock input.
/// </summary>
public static class WorldClockHash
{
    private const ulong Offset = 14695981039346656037UL;
    private const ulong Prime = 1099511628211UL;
    private const byte Version = 1;

    public static ulong Compute(WorldClockState state)
    {
        ArgumentNullException.ThrowIfNull(state.Queue);
        ArgumentNullException.ThrowIfNull(state.RngStates);

        var buffer = new List<byte>(256);
        buffer.Add(Version);
        WriteDate(buffer, state.Date);
        WriteUInt64(buffer, state.MasterSeed);
        WriteUInt64(buffer, state.NextEventId);
        WriteUInt64(buffer, state.Queue.NextSequence);
        WriteInt32(buffer, state.Queue.Count);
        foreach (var scheduled in state.Queue.Events)
        {
            WriteUtf8(buffer, scheduled.Id.Value);
            WriteDate(buffer, scheduled.Date);
            WriteUInt64(buffer, scheduled.Sequence);
            WriteUtf8(buffer, scheduled.TypeId);
            WritePayload(buffer, scheduled.Payload);
        }

        var slots = new List<RngStreamSlot>(state.RngStates.Count);
        foreach (var pair in state.RngStates)
        {
            slots.Add(pair.Key);
        }

        slots.Sort();
        WriteInt32(buffer, slots.Count);
        foreach (var slot in slots)
        {
            var rng = state.RngStates[slot];
            WriteUtf8(buffer, slot.Name);
            WriteInt32(buffer, slot.Season);
            WriteUInt64(buffer, rng.S0);
            WriteUInt64(buffer, rng.S1);
            WriteUInt64(buffer, rng.S2);
            WriteUInt64(buffer, rng.S3);
        }

        return Hash(buffer);
    }

    private static void WritePayload(List<byte> buffer, EventPayload payload)
    {
        switch (payload)
        {
            case RaceSessionPayload race:
                buffer.Add(1);
                WriteInt32(buffer, race.Season);
                WriteInt32(buffer, race.Round);
                WriteUtf8(buffer, race.LayoutId);
                break;
            case MarkerPayload marker:
                buffer.Add(2);
                WriteUtf8(buffer, marker.Marker);
                break;
            default:
                throw new InvalidOperationException($"No canonical form for payload type '{payload.GetType().Name}'.");
        }
    }

    private static void WriteDate(List<byte> buffer, GameDate date)
    {
        WriteInt32(buffer, date.Year);
        buffer.Add((byte)date.Month);
        buffer.Add((byte)date.Day);
    }

    private static void WriteUtf8(List<byte> buffer, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        WriteInt32(buffer, bytes.Length);
        buffer.AddRange(bytes);
    }

    private static void WriteInt32(List<byte> buffer, int value) =>
        WriteUInt32(buffer, unchecked((uint)value));

    private static void WriteUInt32(List<byte> buffer, uint value)
    {
        buffer.Add((byte)value);
        buffer.Add((byte)(value >> 8));
        buffer.Add((byte)(value >> 16));
        buffer.Add((byte)(value >> 24));
    }

    private static void WriteUInt64(List<byte> buffer, ulong value)
    {
        for (var i = 0; i < 8; i++)
        {
            buffer.Add((byte)(value >> (8 * i)));
        }
    }

    private static ulong Hash(List<byte> data)
    {
        unchecked
        {
            ulong hash = Offset;
            for (var i = 0; i < data.Count; i++)
            {
                hash ^= data[i];
                hash *= Prime;
            }

            return hash;
        }
    }
}
