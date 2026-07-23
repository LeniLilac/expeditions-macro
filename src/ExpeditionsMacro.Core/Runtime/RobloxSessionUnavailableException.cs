namespace ExpeditionsMacro.Core.Runtime;

public sealed class RobloxSessionUnavailableException : InvalidOperationException
{
    public RobloxSessionUnavailableException(string message)
        : base(message)
    {
    }

    public RobloxSessionUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
