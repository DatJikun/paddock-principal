namespace Paddock.Domain.Codec;

/// <summary>
/// A value stored as a versioned type tag plus text, the shape of every save codec (events, commands, managers).
/// The tag is <c>name/version</c> (for example <c>marker/1</c>), written by the owner of the type and never derived
/// from a CLR type name, so renaming a class cannot change what a save means.
/// </summary>
public readonly record struct TaggedText(string Tag, string Text)
{
    public static TaggedText Of(string tag, string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tag);
        ArgumentNullException.ThrowIfNull(text);
        return new TaggedText(tag, text);
    }
}
