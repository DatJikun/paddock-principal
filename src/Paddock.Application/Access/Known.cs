namespace Paddock.Application.Access;

/// <summary>Closed range [Min, Max] a value is known to lie in.</summary>
public readonly record struct Band<T> where T : struct, IComparable<T>
{
    public Band(T min, T max)
    {
        if (min.CompareTo(max) > 0)
        {
            throw new ArgumentException("Band minimum must not exceed maximum.");
        }

        Min = min;
        Max = max;
    }

    public T Min { get; }

    public T Max { get; }

    public bool Contains(T value) => Min.CompareTo(value) <= 0 && value.CompareTo(Max) <= 0;

    public bool Contains(Band<T> inner) => Contains(inner.Min) && Contains(inner.Max);
}

public enum KnownKind
{
    Unknown,
    Banded,
    Exact,
}

/// <summary>
/// What an actor knows about a value: exact, a band (min-max) or nothing. There is deliberately no
/// implicit or explicit conversion from a band to an exact value; knowledge only grows through
/// <see cref="Narrow"/> and <see cref="Reveal"/>, which are explicit and never widen.
/// A default instance is Unknown.
/// </summary>
public readonly struct Known<T> : IEquatable<Known<T>> where T : struct, IComparable<T>
{
    private readonly T _min;
    private readonly T _max;

    private Known(KnownKind kind, T min, T max)
    {
        Kind = kind;
        _min = min;
        _max = max;
    }

    public KnownKind Kind { get; }

    public static Known<T> Unknown => default;

    public static Known<T> Exact(T value) => new(KnownKind.Exact, value, value);

    public static Known<T> Banded(T min, T max)
    {
        var band = new Band<T>(min, max);
        return new Known<T>(KnownKind.Banded, band.Min, band.Max);
    }

    public bool TryGetExact(out T value)
    {
        value = Kind == KnownKind.Exact ? _min : default;
        return Kind == KnownKind.Exact;
    }

    public bool TryGetBand(out Band<T> band)
    {
        band = Kind == KnownKind.Banded ? new Band<T>(_min, _max) : default;
        return Kind == KnownKind.Banded;
    }

    /// <summary>Narrows an unknown or banded value to a tighter band. A wider or disjoint band is rejected.</summary>
    public Known<T> Narrow(T min, T max)
    {
        var next = new Band<T>(min, max);
        switch (Kind)
        {
            case KnownKind.Exact:
                throw new InvalidOperationException("An exact value cannot be narrowed.");
            case KnownKind.Banded when !new Band<T>(_min, _max).Contains(next):
                throw new ArgumentException("Narrowing must stay inside the current band.");
            default:
                return Banded(next.Min, next.Max);
        }
    }

    /// <summary>Reveals the exact value. For a banded value it must lie inside the band.</summary>
    public Known<T> Reveal(T value)
    {
        if (Kind == KnownKind.Exact)
        {
            throw new InvalidOperationException("Value is already exact.");
        }

        if (Kind == KnownKind.Banded && !new Band<T>(_min, _max).Contains(value))
        {
            throw new ArgumentException("Revealed value lies outside the known band.");
        }

        return Exact(value);
    }

    public bool Equals(Known<T> other) =>
        Kind == other.Kind && _min.Equals(other._min) && _max.Equals(other._max);

    public override bool Equals(object? obj) => obj is Known<T> other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Kind, _min, _max);

    public static bool operator ==(Known<T> left, Known<T> right) => left.Equals(right);

    public static bool operator !=(Known<T> left, Known<T> right) => !left.Equals(right);
}
