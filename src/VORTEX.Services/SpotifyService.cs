using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using VORTEX.Core;

namespace VORTEX.Services;

public sealed class SpotifyService : ISpotifyService
{
    public const string RedirectUri = "http://127.0.0.1:43821/spotify/callback";
    private const string ListenerPrefix = "http://127.0.0.1:43821/spotify/";
    private static readonly HttpClient Http = new();
    private readonly IAuthorizationService _authorization;
    private string? _accessToken;
    private string? _refreshToken;
    private string? _clientId;

    public SpotifyState State { get; private set; } =
        new(false, "Não conectado", "Nenhuma música", string.Empty, false);

    public SpotifyService(IAuthorizationService authorization) =>
        _authorization = authorization;

    public async Task<SpotifyState> ConnectAsync(
        string clientId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(clientId))
            throw new ArgumentException("Informe o Client ID do Spotify.");
        if (!await _authorization.RequestAsync(new(
                "Spotify e navegador",
                "Conectar conta Spotify",
                "O VORTEX abrirá a autorização oficial do Spotify e receberá o retorno somente em 127.0.0.1.",
                ["accounts.spotify.com", RedirectUri]), cancellationToken))
            throw new OperationCanceledException("Conexão cancelada.");

        _clientId = clientId.Trim();
        var verifier = Base64Url(RandomNumberGenerator.GetBytes(64));
        var challenge = Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        var state = Base64Url(RandomNumberGenerator.GetBytes(18));
        var scopes = Uri.EscapeDataString(
            "user-read-private user-read-email user-read-playback-state user-read-currently-playing " +
            "user-modify-playback-state playlist-read-private user-library-read user-top-read user-read-recently-played");
        var url =
            $"https://accounts.spotify.com/authorize?client_id={Uri.EscapeDataString(_clientId)}" +
            $"&response_type=code&redirect_uri={Uri.EscapeDataString(RedirectUri)}" +
            $"&code_challenge_method=S256&code_challenge={challenge}&state={state}&scope={scopes}";

        using var listener = new HttpListener();
        listener.Prefixes.Add(ListenerPrefix);
        listener.Start();
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        var context = await listener.GetContextAsync().WaitAsync(
            TimeSpan.FromMinutes(3), cancellationToken);
        var returnedState = context.Request.QueryString["state"];
        var code = context.Request.QueryString["code"];
        var responseHtml = Encoding.UTF8.GetBytes(
            "<html><body style='background:#121212;color:white;font-family:sans-serif;text-align:center;padding:70px'><h1>Spotify conectado ao VORTEX</h1><p>Você pode fechar esta janela.</p></body></html>");
        context.Response.ContentType = "text/html; charset=utf-8";
        await context.Response.OutputStream.WriteAsync(responseHtml, cancellationToken);
        context.Response.Close();
        if (returnedState != state || string.IsNullOrWhiteSpace(code))
            throw new InvalidOperationException("O retorno do Spotify não pôde ser validado.");

        using var tokenResponse = await Http.PostAsync(
            "https://accounts.spotify.com/api/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = _clientId,
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["redirect_uri"] = RedirectUri,
                ["code_verifier"] = verifier
            }), cancellationToken);
        var tokenJson = await tokenResponse.Content.ReadAsStringAsync(cancellationToken);
        tokenResponse.EnsureSuccessStatusCode();
        using var tokenDocument = JsonDocument.Parse(tokenJson);
        _accessToken = tokenDocument.RootElement.GetProperty("access_token").GetString();
        _refreshToken = tokenDocument.RootElement.TryGetProperty("refresh_token", out var refresh)
            ? refresh.GetString()
            : null;
        return await RefreshAsync(cancellationToken);
    }

    public async Task<SpotifyState> RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_accessToken)) return State;
        var user = await GetJsonAsync("https://api.spotify.com/v1/me", cancellationToken);
        var userName = user.RootElement.TryGetProperty("display_name", out var displayName)
            ? displayName.GetString() ?? "Spotify"
            : "Spotify";
        using var request = Authorized(HttpMethod.Get, "https://api.spotify.com/v1/me/player");
        using var response = await Http.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NoContent)
            return State = new(true, userName, "Nenhum dispositivo ativo", string.Empty, false);
        var playback = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(cancellationToken));
        var root = playback.RootElement;
        var track = root.TryGetProperty("item", out var item)
            && item.ValueKind != JsonValueKind.Null
            && item.TryGetProperty("name", out var name)
                ? name.GetString() ?? "Sem faixa"
                : "Sem faixa";
        var artist = item.ValueKind != JsonValueKind.Undefined
                     && item.TryGetProperty("artists", out var artists)
                     && artists.GetArrayLength() > 0
            ? artists[0].GetProperty("name").GetString() ?? string.Empty
            : string.Empty;
        JsonElement albumObject = default;
        var album = item.ValueKind != JsonValueKind.Undefined
                    && item.TryGetProperty("album", out albumObject)
            ? albumObject.GetProperty("name").GetString() ?? string.Empty
            : string.Empty;
        var imageUrl = albumObject.ValueKind != JsonValueKind.Undefined
                       && albumObject.TryGetProperty("images", out var images)
                       && images.GetArrayLength() > 0
            ? images[0].GetProperty("url").GetString() ?? string.Empty
            : string.Empty;
        var playing = root.TryGetProperty("is_playing", out var isPlaying)
                      && isPlaying.GetBoolean();
        var progress = root.TryGetProperty("progress_ms", out var progressElement)
                       && progressElement.ValueKind == JsonValueKind.Number
            ? progressElement.GetInt32()
            : 0;
        var duration = item.ValueKind != JsonValueKind.Undefined
                       && item.TryGetProperty("duration_ms", out var durationElement)
            ? durationElement.GetInt32()
            : 0;
        var deviceName = root.TryGetProperty("device", out var device)
            ? device.GetProperty("name").GetString() ?? string.Empty
            : string.Empty;
        var volume = device.ValueKind != JsonValueKind.Undefined
                     && device.TryGetProperty("volume_percent", out var volumeElement)
                     && volumeElement.ValueKind == JsonValueKind.Number
            ? volumeElement.GetInt32()
            : 0;
        var shuffle = root.TryGetProperty("shuffle_state", out var shuffleElement)
                      && shuffleElement.GetBoolean();
        var repeat = root.TryGetProperty("repeat_state", out var repeatElement)
            ? repeatElement.GetString() ?? "off"
            : "off";
        return State = new(
            true, userName, track, artist, playing, album, imageUrl,
            progress, duration, volume, deviceName, shuffle, repeat);
    }

    public async Task PlaybackAsync(
        string action, CancellationToken cancellationToken = default)
    {
        var (method, endpoint) = action switch
        {
            "play" => (HttpMethod.Put, "play"),
            "pause" => (HttpMethod.Put, "pause"),
            "next" => (HttpMethod.Post, "next"),
            "previous" => (HttpMethod.Post, "previous"),
            _ => throw new ArgumentOutOfRangeException(nameof(action))
        };
        using var request = Authorized(
            method, $"https://api.spotify.com/v1/me/player/{endpoint}");
        request.Content = new StringContent(string.Empty);
        using var response = await Http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        await Task.Delay(350, cancellationToken);
        await RefreshAsync(cancellationToken);
    }

    public void Disconnect()
    {
        _accessToken = null;
        _refreshToken = null;
        State = new(false, "Não conectado", "Nenhuma música", string.Empty, false);
    }

    public Task SetVolumeAsync(int volume, CancellationToken cancellationToken = default) =>
        PutQueryAsync("volume", $"volume_percent={Math.Clamp(volume, 0, 100)}", cancellationToken);

    public Task SeekAsync(int positionMs, CancellationToken cancellationToken = default) =>
        PutQueryAsync("seek", $"position_ms={Math.Max(0, positionMs)}", cancellationToken);

    public Task SetShuffleAsync(bool enabled, CancellationToken cancellationToken = default) =>
        PutQueryAsync("shuffle", $"state={enabled.ToString().ToLowerInvariant()}", cancellationToken);

    public Task SetRepeatAsync(string mode, CancellationToken cancellationToken = default) =>
        PutQueryAsync("repeat", $"state={Uri.EscapeDataString(mode)}", cancellationToken);

    public async Task<IReadOnlyList<string>> GetLibraryAsync(
        string section, CancellationToken cancellationToken = default)
    {
        var url = section switch
        {
            "playlists" => "https://api.spotify.com/v1/me/playlists?limit=30",
            "liked" => "https://api.spotify.com/v1/me/tracks?limit=30",
            "albums" => "https://api.spotify.com/v1/me/albums?limit=30",
            "artists" => "https://api.spotify.com/v1/me/top/artists?limit=30",
            _ => "https://api.spotify.com/v1/me/player/recently-played?limit=30"
        };
        using var document = await GetJsonAsync(url, cancellationToken);
        if (!document.RootElement.TryGetProperty("items", out var items)) return [];
        return items.EnumerateArray().Select(item =>
        {
            var value = item;
            if (item.TryGetProperty("track", out var track)) value = track;
            else if (item.TryGetProperty("album", out var album)) value = album;
            return value.TryGetProperty("name", out var name)
                ? name.GetString() ?? "Item"
                : value.TryGetProperty("context", out var context)
                  && context.TryGetProperty("type", out var type)
                    ? type.GetString() ?? "Item"
                    : "Item";
        }).ToList();
    }

    public async Task<IReadOnlyList<string>> SearchAsync(
        string query, CancellationToken cancellationToken = default)
    {
        using var document = await GetJsonAsync(
            $"https://api.spotify.com/v1/search?q={Uri.EscapeDataString(query)}&type=track,artist,album,playlist&limit=8",
            cancellationToken);
        var results = new List<string>();
        foreach (var collection in new[] { "tracks", "artists", "albums", "playlists" })
            if (document.RootElement.TryGetProperty(collection, out var group)
                && group.TryGetProperty("items", out var items))
                results.AddRange(items.EnumerateArray()
                    .Where(item => item.ValueKind != JsonValueKind.Null
                                   && item.TryGetProperty("name", out _))
                    .Select(item => $"{collection}: {item.GetProperty("name").GetString()}"));
        return results;
    }

    private async Task PutQueryAsync(
        string endpoint, string query, CancellationToken cancellationToken)
    {
        using var request = Authorized(
            HttpMethod.Put, $"https://api.spotify.com/v1/me/player/{endpoint}?{query}");
        request.Content = new StringContent(string.Empty);
        using var response = await Http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        await RefreshAsync(cancellationToken);
    }

    private async Task<JsonDocument> GetJsonAsync(
        string url, CancellationToken cancellationToken)
    {
        using var request = Authorized(HttpMethod.Get, url);
        using var response = await Http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(cancellationToken));
    }

    private HttpRequestMessage Authorized(HttpMethod method, string url)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
        return request;
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
