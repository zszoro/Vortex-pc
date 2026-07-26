using System.Diagnostics;
using System.Text.Json;
using VORTEX.Core;

namespace VORTEX.Services;

public sealed class GitHubUpdateService : IUpdateService
{
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(8) };

    public async Task<AppUpdateInfo> CheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await Client.GetAsync(AppConstants.UpdateManifestUrl, cancellationToken);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var manifest = await JsonSerializer.DeserializeAsync<UpdateManifest>(
                stream,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
                cancellationToken);
            var latest = manifest?.Version ?? AppConstants.Version;
            var available = Version.TryParse(latest, out var latestVersion)
                && Version.TryParse(AppConstants.Version, out var currentVersion)
                && latestVersion > currentVersion;
            return new(
                AppConstants.Version,
                latest,
                manifest?.Title ?? $"VORTEX {latest}",
                manifest?.Notes ?? "Nenhuma nota disponível.",
                manifest?.DownloadUrl ?? AppConstants.ReleasesUrl,
                available);
        }
        catch
        {
            return new(
                AppConstants.Version,
                AppConstants.Version,
                $"VORTEX {AppConstants.Version}",
                "Não foi possível consultar atualizações agora.",
                AppConstants.ReleasesUrl,
                false);
        }
    }

    public void OpenDownloadPage(string? url = null) =>
        Process.Start(new ProcessStartInfo(url ?? AppConstants.ReleasesUrl) { UseShellExecute = true });

    private sealed class UpdateManifest
    {
        public string Version { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
        public string DownloadUrl { get; set; } = string.Empty;
    }
}
