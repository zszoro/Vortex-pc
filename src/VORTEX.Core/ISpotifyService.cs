namespace VORTEX.Core;

public record SpotifyState(
    bool IsConnected,
    string UserName,
    string Track,
    string Artist,
    bool IsPlaying);

public interface ISpotifyService
{
    SpotifyState State { get; }
    Task<SpotifyState> ConnectAsync(string clientId, CancellationToken cancellationToken = default);
    Task<SpotifyState> RefreshAsync(CancellationToken cancellationToken = default);
    Task PlaybackAsync(string action, CancellationToken cancellationToken = default);
    void Disconnect();
}
