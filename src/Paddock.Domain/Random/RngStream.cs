using System.Text;

namespace Paddock.Domain.Random;

/// <summary>
/// Immutable snapshot of one named RNG stream (TECH §4).
/// Child streams are hashed from this state plus a tag, so drawing a child does not
/// advance the parent and later people do not depend on how many draws an earlier
/// person used.
/// </summary>
public sealed class RngStream
{
    public RngStream(string name, RngState state)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (state.IsZero)
        {
            throw new ArgumentException("Stream state must not be all zeroes.", nameof(state));
        }

        Name = name;
        State = state;
    }

    public string Name { get; }

    public RngState State { get; }

    public static RngStream Derive(ulong masterSeed, string streamName, int season, int? round = null)
    {
        var rng = RngStreams.Derive(masterSeed, streamName, season, round);
        return new RngStream(streamName, rng.State);
    }

    /// <summary>
    /// Deterministic child generator. Layout of the hashed bytes: four little-endian
    /// state words, then the UTF-8 tag length as u32, then the tag. The parent state
    /// is not modified.
    /// </summary>
    public Xoshiro256StarStar DeriveChild(string tag)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tag);
        return Xoshiro256StarStar.FromSeed(HashStateAndTag(State, tag));
    }

    private static ulong HashStateAndTag(RngState state, string tag)
    {
        byte[] tagBytes = Encoding.UTF8.GetBytes(tag);
        var buffer = new byte[32 + 4 + tagBytes.Length];
        var offset = 0;
        WriteUInt64(buffer, ref offset, state.S0);
        WriteUInt64(buffer, ref offset, state.S1);
        WriteUInt64(buffer, ref offset, state.S2);
        WriteUInt64(buffer, ref offset, state.S3);
        WriteUInt32(buffer, ref offset, (uint)tagBytes.Length);
        tagBytes.CopyTo(buffer, offset);
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
}
