namespace Paddock.Domain.World;

/// <summary>One dimension moving to a new value in the next season.</summary>
public readonly record struct RuleChange(string DimensionId, string NewValue);

public enum RuleChangeError
{
    UnknownDimension,
    ValueNotAllowed,
    NotANumber,
    OutOfRange,
    DuplicateDimension,
}

public sealed class RuleChangeException : Exception
{
    public RuleChangeException(RuleChangeError code, string message)
        : base(message)
    {
        Code = code;
    }

    public RuleChangeError Code { get; }
}
