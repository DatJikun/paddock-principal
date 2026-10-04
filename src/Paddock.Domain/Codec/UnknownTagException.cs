namespace Paddock.Domain.Codec;

/// <summary>
/// A save holds a type tag this build has no codec for: a type it does not know, or a version it cannot read.
/// Loading stops instead of guessing, because a guessed event or command would change the future silently (INV-002).
/// </summary>
public sealed class UnknownTagException : FormatException
{
    public UnknownTagException(string kind, string tag)
        : base($"The save holds a {kind} tagged '{tag}', which this build cannot read.")
    {
        Kind = kind;
        Tag = tag;
    }

    /// <summary>What was being decoded, for example <c>event payload</c> or <c>command</c>.</summary>
    public string Kind { get; }

    public string Tag { get; }
}
