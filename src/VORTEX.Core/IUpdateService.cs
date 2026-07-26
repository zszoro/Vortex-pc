namespace VORTEX.Core;

public interface IUpdateService
{
    Task<AppUpdateInfo> CheckAsync(CancellationToken cancellationToken = default);
    void OpenDownloadPage(string? url = null);
}
