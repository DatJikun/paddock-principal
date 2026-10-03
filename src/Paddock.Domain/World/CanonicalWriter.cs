using System.Globalization;
using System.Text;

namespace Paddock.Domain.World;

/// <summary>
/// Builds the canonical text that the state hash is taken over (see <see cref="WorldState.StateHash"/>).
/// Every line ends with LF. A string is written as <c>length:text</c>, where length is <see cref="string.Length"/>
/// (UTF-16 code units), so no value can be mistaken for a separator. Integers use the invariant culture.
/// A world section writes its own text through this type, so all sections share one encoding.
/// </summary>
public sealed class CanonicalWriter
{
    private readonly StringBuilder _builder = new();

    public void Line(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        _builder.Append(text);
        _builder.Append('\n');
    }

    public void Count(string label, int count) =>
        Line(label + " " + count.ToString(CultureInfo.InvariantCulture));

    public void Number(string label, long value) =>
        Line(label + " " + value.ToString(CultureInfo.InvariantCulture));

    public void Flag(string label, bool value) => Line(label + " " + (value ? "1" : "0"));

    public void TextLine(string label, string value)
    {
        Begin(label);
        Field(value);
        End();
    }

    public void TextNumber(string label, string value, int number)
    {
        Begin(label);
        Field(value);
        Space();
        Raw(number.ToString(CultureInfo.InvariantCulture));
        End();
    }

    public void Begin(string label)
    {
        ArgumentNullException.ThrowIfNull(label);
        _builder.Append(label);
        _builder.Append(' ');
    }

    public void Field(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        _builder.Append(value.Length.ToString(CultureInfo.InvariantCulture));
        _builder.Append(':');
        _builder.Append(value);
    }

    public void Space() => _builder.Append(' ');

    public void Raw(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        _builder.Append(text);
    }

    public void End() => _builder.Append('\n');

    public override string ToString() => _builder.ToString();
}
