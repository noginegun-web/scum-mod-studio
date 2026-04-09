namespace ScumPakWizard;

internal sealed record AppUpdateStatusDto(
    bool Enabled,
    bool Configured,
    bool IsInstalled,
    bool IsPortable,
    bool IsChecking,
    bool IsDownloading,
    bool IsInstalling,
    bool PendingRestart,
    bool UpdateAvailable,
    bool CanCheck,
    bool CanDownload,
    bool CanInstall,
    string? CurrentVersion,
    string? AvailableVersion,
    string? PendingVersion,
    int DownloadProgress,
    string? RepoUrl,
    string? StatusTitle,
    string? StatusMessage,
    string? ReleaseNotesMarkdown,
    string? LastError,
    string? LastCheckedUtc);

internal sealed record AppUpdateCommandResultDto(
    bool Ok,
    string? Message,
    string? Error,
    bool ShouldShutdown = false);
