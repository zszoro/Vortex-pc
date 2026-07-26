namespace VORTEX.Core;

public sealed record AppUpdateInfo(
    string CurrentVersion,
    string LatestVersion,
    string Title,
    string Notes,
    string DownloadUrl,
    bool IsUpdateAvailable);
