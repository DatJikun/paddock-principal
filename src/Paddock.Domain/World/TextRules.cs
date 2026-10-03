using System.Globalization;

namespace Paddock.Domain.World;

internal static class DisplayText
{
    public static string Require(string value, string paramName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var trimmed = value.Trim();
        if (trimmed.Length == 0)
        {
            throw new ArgumentException("Text is empty.", paramName);
        }

        foreach (var ch in trimmed)
        {
            if (char.IsControl(ch))
            {
                throw new ArgumentException("Text contains a control character.", paramName);
            }
        }

        return trimmed;
    }
}

internal static class AttributeKeys
{
    public static string Require(string key, string paramName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        if (key[0] is < 'a' or > 'z')
        {
            throw new ArgumentException("Attribute keys are lowercase identifiers.", paramName);
        }

        for (var i = 1; i < key.Length; i++)
        {
            var ch = key[i];
            var ok = ch is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '_';
            if (!ok)
            {
                throw new ArgumentException("Attribute keys are lowercase identifiers.", paramName);
            }
        }

        return key;
    }
}

internal static class IdText
{
    public const string PersonPrefix = "gen:";

    public const string OrganizationPrefix = "org:";

    public const string ContractPrefix = "con:";

    public static string RequireReal(string authoredId, string paramName)
    {
        var id = RequireToken(authoredId, paramName);
        if (id.StartsWith(PersonPrefix, StringComparison.Ordinal)
            || id.StartsWith(OrganizationPrefix, StringComparison.Ordinal)
            || id.StartsWith(ContractPrefix, StringComparison.Ordinal))
        {
            throw new ArgumentException("A real id cannot use a generated prefix (gen:, org:, or con:).", paramName);
        }

        return id;
    }

    public static string Generated(string prefix, long sequence)
    {
        if (sequence < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(sequence), sequence, "Generated ids start at 1.");
        }

        return prefix + sequence.ToString(CultureInfo.InvariantCulture);
    }

    public static bool TryReadSequence(string value, string prefix, out long sequence)
    {
        sequence = 0;
        if (!value.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }

        var tail = value.AsSpan(prefix.Length);
        if (tail.Length == 0 || (tail.Length > 1 && tail[0] == '0'))
        {
            return false;
        }

        return long.TryParse(tail, NumberStyles.None, CultureInfo.InvariantCulture, out sequence) && sequence >= 1;
    }

    public static bool IsGenerated(string value) =>
        TryReadSequence(value, PersonPrefix, out _)
        || TryReadSequence(value, OrganizationPrefix, out _)
        || TryReadSequence(value, ContractPrefix, out _);

    private static string RequireToken(string value, string paramName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var id = value.Trim();
        if (id.Length == 0)
        {
            throw new ArgumentException("Id is empty.", paramName);
        }

        foreach (var ch in id)
        {
            if (char.IsWhiteSpace(ch) || char.IsControl(ch))
            {
                throw new ArgumentException("Id cannot contain whitespace or control characters.", paramName);
            }
        }

        return id;
    }
}
