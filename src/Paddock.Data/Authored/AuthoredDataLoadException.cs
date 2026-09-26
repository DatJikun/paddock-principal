namespace Paddock.Data.Authored;

public sealed class AuthoredDataLoadException : Exception
{
    public AuthoredDataLoadException(string message)
        : base(message)
    {
    }
}
