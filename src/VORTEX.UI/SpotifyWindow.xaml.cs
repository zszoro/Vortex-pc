using System.Windows;
using VORTEX.Core;

namespace VORTEX.UI;

public partial class SpotifyWindow
{
    private const string ClientId = "6db687a4a5b64bf99025f05ad07109ed";
    private readonly ISpotifyService _spotify;

    public SpotifyWindow(ISpotifyService spotify)
    {
        _spotify = spotify;
        InitializeComponent();
        Render(_spotify.State);
    }

    private async void Connect_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            ConnectButton.IsEnabled = false;
            ConnectionText.Text = "Aguardando autorização no navegador…";
            Render(await _spotify.ConnectAsync(ClientId));
        }
        catch (OperationCanceledException)
        {
            ConnectionText.Text = "Conexão cancelada.";
        }
        catch (Exception exception)
        {
            ConnectionText.Text = $"Não foi possível conectar: {exception.Message}";
        }
        finally { ConnectButton.IsEnabled = true; }
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e) =>
        await RunAsync(() => _spotify.RefreshAsync());
    private async void Previous_Click(object sender, RoutedEventArgs e) =>
        await RunAsync(async () => { await _spotify.PlaybackAsync("previous"); return _spotify.State; });
    private async void Next_Click(object sender, RoutedEventArgs e) =>
        await RunAsync(async () => { await _spotify.PlaybackAsync("next"); return _spotify.State; });
    private async void PlayPause_Click(object sender, RoutedEventArgs e) =>
        await RunAsync(async () =>
        {
            await _spotify.PlaybackAsync(_spotify.State.IsPlaying ? "pause" : "play");
            return _spotify.State;
        });

    private async Task RunAsync(Func<Task<SpotifyState>> operation)
    {
        try { Render(await operation()); }
        catch (Exception exception) { ConnectionText.Text = exception.Message; }
    }

    private void Render(SpotifyState state)
    {
        ConnectionText.Text = state.IsConnected
            ? $"Conectado como {state.UserName}"
            : "Conecte sua conta quando quiser usar música.";
        TrackText.Text = state.Track;
        ArtistText.Text = string.IsNullOrWhiteSpace(state.Artist) ? "—" : state.Artist;
        PlayButton.Content = state.IsPlaying ? "⏸" : "▶";
        ConnectButton.Content = state.IsConnected ? "Reconectar" : "Conectar Spotify";
    }
}
