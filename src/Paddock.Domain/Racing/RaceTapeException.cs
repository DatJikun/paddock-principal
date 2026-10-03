namespace Paddock.Domain.Racing;

public sealed class RaceTapeException : Exception
{
    public RaceTapeException(string message)
        : base(message)
    {
    }
}
