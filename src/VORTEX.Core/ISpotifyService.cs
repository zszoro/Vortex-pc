namespace VORTEX.Core;

public record SpotifyState(
    bool IsConnected,
    string UserName,
    string Track,
    string Artist,
    bool IsPlaying,
    string Album = "",
    string ImageUrl = "",
    int ProgressMs = 0,
    int DurationMs = 0,
    int Volume = 0,
    string Device = "",
    bool Shuffle = false,
    string Repeat = "off");

public interface ISpotifyService
{
    SpotifyState State { get; }
    Task<SpotifyState> ConnectAsync(string clientId, CancellationToken cancellationToken = default);
    Task<SpotifyState> RefreshAsync(CancellationToken cancellationToken = default);
    Task PlaybackAsync(string action, CancellationToken cancellationToken = default);
    Task SetVolumeAsync(int volume, CancellationToken cancellationToken = default);
    Task SeekAsync(int positionMs, CancellationToken cancellationToken = default);
    Task SetShuffleAsync(bool enabled, CancellationToken cancellationToken = default);
    Task SetRepeatAsync(string mode, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetLibraryAsync(string section, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> SearchAsync(string query, CancellationToken cancellationToken = default);
    void Disconnect();
}
