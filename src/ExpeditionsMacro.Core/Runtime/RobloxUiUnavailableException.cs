namespace ExpeditionsMacro.Core.Runtime;

/// <summary>
/// Indicates that Roblox remained available, but an owned, verified UI
/// transition could not be completed safely in the current session.
/// </summary>
public sealed class RobloxUiUnavailableException : InvalidOperationException
{
    public RobloxUiUnavailableException(string message)
        : base(message)
    {
    }

    public RobloxUiUnavailableException(
        string message,
        Exception innerException)
        : base(message, innerException)
    {
    }
}
