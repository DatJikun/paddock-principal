using System.Text;

namespace Paddock.Domain.Random;

public static class RngStreams
{
    /// <summary>
    /// Derives an isolated stream from the career master seed.
    /// The payload is FNV-1a 64 over a little-endian canonical encoding, then expanded
    /// with SplitMix64 inside <see cref="Xoshiro256StarStar.FromSeed"/>. Never uses
    /// <see cref="string.GetHashCode"/>.
    /// Layout: u64 master | u32 utf8 byte length | utf8 name | i32 season | u8 hasRound | i32 round when present.
    /// A missing round is distinct from round 0.
    /// </summary>
    public static Xoshiro256StarStar Derive(ulong masterSeed, string streamName, int season, int? round = null)
    {
        ArgumentNullException.ThrowIfNull(streamName);
        if (streamName.Length == 0)
        {
            throw new ArgumentException("Stream name must not be empty.", nameof(streamName));
        }

        ulong hash = HashInputs(masterSeed, streamName, season, round);
        return Xoshiro256StarStar.FromSeed(hash);
    }

    private static ulong HashInputs(ulong masterSeed, string streamName, int season, int? round)
    {
        byte[] nameBytes = Encoding.UTF8.GetBytes(streamName);
        int length = 8 + 4 + nameBytes.Length + 4 + 1 + (round.HasValue ? 4 : 0);
        var buffer = new byte[length];
        var offset = 0;
        WriteUInt64(buffer, ref offset, masterSeed);
        WriteUInt32(buffer, ref offset, (uint)nameBytes.Length);
        nameBytes.CopyTo(buffer, offset);
        offset += nameBytes.Length;
        WriteInt32(buffer, ref offset, season);
        buffer[offset++] = round.HasValue ? (byte)1 : (byte)0;
        if (round is int roundValue)
        {
            WriteInt32(buffer, ref offset, roundValue);
        }

        return Fnv1a64.Hash(buffer);
    }

    private static void WriteUInt64(byte[] buffer, ref int offset, ulong value)
    {
        for (var i = 0; i < 8; i++)
        {
            buffer[offset++] = (byte)(value >> (8 * i));
        }
    }

    private static void WriteUInt32(byte[] buffer, ref int offset, uint value)
    {
        for (var i = 0; i < 4; i++)
        {
            buffer[offset++] = (byte)(value >> (8 * i));
        }
    }

    private static void WriteInt32(byte[] buffer, ref int offset, int value)
    {
        WriteUInt32(buffer, ref offset, unchecked((uint)value));
    }
}
