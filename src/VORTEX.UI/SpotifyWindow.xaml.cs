using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
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
    private async void Shuffle_Click(object sender, RoutedEventArgs e) =>
        await RunAsync(async () =>
        {
            await _spotify.SetShuffleAsync(!_spotify.State.Shuffle);
            return _spotify.State;
        });
    private async void Repeat_Click(object sender, RoutedEventArgs e) =>
        await RunAsync(async () =>
        {
            var next = _spotify.State.Repeat switch
            {
                "off" => "context",
                "context" => "track",
                _ => "off"
            };
            await _spotify.SetRepeatAsync(next);
            return _spotify.State;
        });
    private async void VolumeSlider_Commit(object sender, MouseButtonEventArgs e) =>
        await RunAsync(async () =>
        {
            await _spotify.SetVolumeAsync((int)VolumeSlider.Value);
            return _spotify.State;
        });
    private async void ProgressSlider_Commit(object sender, MouseButtonEventArgs e) =>
        await RunAsync(async () =>
        {
            await _spotify.SeekAsync((int)ProgressSlider.Value);
            return _spotify.State;
        });
    private async void Search_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(SearchInput.Text)) return;
        LibraryList.ItemsSource = await _spotify.SearchAsync(SearchInput.Text);
    }
    private async void Playlists_Click(object sender, RoutedEventArgs e) =>
        LibraryList.ItemsSource = await _spotify.GetLibraryAsync("playlists");
    private async void Liked_Click(object sender, RoutedEventArgs e) =>
        LibraryList.ItemsSource = await _spotify.GetLibraryAsync("liked");
    private async void Recent_Click(object sender, RoutedEventArgs e) =>
        LibraryList.ItemsSource = await _spotify.GetLibraryAsync("recent");

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
        AlbumText.Text = state.Album;
        ProgressSlider.Maximum = Math.Max(1, state.DurationMs);
        ProgressSlider.Value = state.ProgressMs;
        ProgressText.Text = FormatTime(state.ProgressMs);
        DurationText.Text = FormatTime(state.DurationMs);
        VolumeSlider.Value = state.Volume;
        DeviceText.Text = string.IsNullOrWhiteSpace(state.Device)
            ? "Sem dispositivo ativo"
            : $"Dispositivo: {state.Device}";
        ShuffleButton.Foreground = state.Shuffle
            ? System.Windows.Media.Brushes.LimeGreen
            : System.Windows.Media.Brushes.White;
        RepeatButton.Foreground = state.Repeat != "off"
            ? System.Windows.Media.Brushes.LimeGreen
            : System.Windows.Media.Brushes.White;
        if (Uri.TryCreate(state.ImageUrl, UriKind.Absolute, out var imageUri))
        {
            AlbumArt.Source = new BitmapImage(imageUri);
            FallbackNote.Visibility = Visibility.Collapsed;
        }
        else
        {
            AlbumArt.Source = null;
            FallbackNote.Visibility = Visibility.Visible;
        }
        PlayButton.Content = state.IsPlaying ? "⏸" : "▶";
        ConnectButton.Content = state.IsConnected ? "Reconectar" : "Conectar Spotify";
    }

    private static string FormatTime(int milliseconds) =>
        TimeSpan.FromMilliseconds(Math.Max(0, milliseconds)).ToString(@"m\:ss");
}
