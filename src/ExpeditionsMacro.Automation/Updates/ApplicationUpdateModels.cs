namespace ExpeditionsMacro.Automation.Updates;

public enum ApplicationUpdatePhase
{
    Idle,
    Checking,
    Current,
    Available,
    Downloading,
    Ready,
    Error,
}

public sealed record ApplicationUpdateAsset(
    string Name,
    long Size,
    string Sha256,
    Uri DownloadUri);

public sealed record ApplicationUpdateRelease(
    ApplicationSemanticVersion Version,
    bool IsPrerelease,
    string DisplayName,
    Uri ReleaseUri,
    ApplicationUpdateAsset Installer,
    ApplicationUpdateAsset Checksums);

public sealed record StagedApplicationUpdate(
    string Version,
    string InstallerFileName,
    long InstallerSize,
    string InstallerSha256,
    string ReleaseUri);

public sealed record ApplicationUpdateState(
    ApplicationUpdatePhase Phase,
    string Message,
    ApplicationUpdateRelease? Release = null,
    string? InstallerPath = null,
    double? Progress = null,
    ApplicationSemanticVersion? Version = null,
    Uri? ReleaseUri = null)
{
    public static ApplicationUpdateState Idle { get; } =
        new(
            ApplicationUpdatePhase.Idle,
            "Updates have not been checked yet.");
}
