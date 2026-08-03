using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using VORTEX.Core;

namespace VORTEX.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IAIProviderService _aiService;
    private readonly IDatabaseService _dbService;
    private readonly IDesktopCommandService _desktopCommands;
    private readonly IWorkspaceService _workspaceService;
    private readonly IPlanningService _planningService;
    private readonly ISpotifyService _spotifyService;
    private readonly IAuthorizationService _authorizationService;
    private readonly IGuiAutomationService _guiAutomationService;

    [ObservableProperty] private string _userName = "Você";
    [ObservableProperty] private string _status = "Online";
    [ObservableProperty] private string _userInput = string.Empty;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _lastAssistantMessage = "Olá! Clique com o botão direito para falar comigo.";
    [ObservableProperty] private string _petAppearance = "Orb";
    [ObservableProperty] private string _workspaceName = "Sem Workspace";
    [ObservableProperty] private string _workspacePath = "Conversa livre";
    [ObservableProperty] private bool _hasWorkspace;
    [ObservableProperty] private string _activeModel = "Não configurado";
    [ObservableProperty] private string _connectionStatus = "Desconectado";
    [ObservableProperty] private string _contextUsage = "0 tokens";
    [ObservableProperty] private string _responseTime = "—";
    [ObservableProperty] private bool _hasPendingChanges;
    [ObservableProperty] private string _globalSearch = string.Empty;
    [ObservableProperty] private string _spotifyTrack = "Spotify desconectado";
    [ObservableProperty] private string _spotifyArtist = "Conecte pelo painel";
    [ObservableProperty] private bool _spotifyIsPlaying;

    public ObservableCollection<ChatMessage> Messages { get; } = [];
    public ObservableCollection<string> WorkspaceFiles { get; } = [];
    public ObservableCollection<string> FilteredWorkspaceFiles { get; } = [];
    public ObservableCollection<string> PendingChangePreviews { get; } = [];
    public event Action? SpotifyPanelRequested;
    public event Action? PlanningPanelRequested;
    public event Action? LibraryPanelRequested;
    public event Action? LibraryImportRequested;

    public MainViewModel(
        IAIProviderService aiService,
        IDatabaseService dbService,
        IDesktopCommandService desktopCommands,
        IWorkspaceService workspaceService,
        IPlanningService planningService,
        ISpotifyService spotifyService,
        IAuthorizationService authorizationService,
        IGuiAutomationService guiAutomationService)
    {
        _aiService = aiService;
        _dbService = dbService;
        _desktopCommands = desktopCommands;
        _workspaceService = workspaceService;
        _planningService = planningService;
        _spotifyService = spotifyService;
        _authorizationService = authorizationService;
        _guiAutomationService = guiAutomationService;
        ApplyWorkspace(_workspaceService.Current);
        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        var profile = await _dbService.GetUserProfileAsync();
        if (!string.IsNullOrWhiteSpace(profile?.Name)) UserName = profile.Name;
        foreach (var message in await _dbService.GetChatMessagesAsync())
        {
            Messages.Add(message);
            if (message.Role == "VORTEX") LastAssistantMessage = message.Content;
        }
    }

    [RelayCommand]
    private async Task SendMessageAsync()
    {
        if (string.IsNullOrWhiteSpace(UserInput) || IsBusy) return;

        var prompt = UserInput.Trim();
        UserInput = string.Empty;
        var userMessage = new ChatMessage { Role = "Você", Content = prompt };
        Messages.Add(userMessage);
        await _dbService.SaveChatMessageAsync(userMessage);

        Status = "Thinking";
        IsBusy = true;
        try
        {
            if (System.Text.RegularExpressions.Regex.IsMatch(
                    prompt, @"\b(?:abra|abrir|mostre|mostrar)\b.*\bspotify\b",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            {
                SpotifyPanelRequested?.Invoke();
                await AddAssistantReplyAsync("Painel do Spotify aberto.");
                Status = "Online";
                return;
            }
            if (System.Text.RegularExpressions.Regex.IsMatch(
                    prompt, @"\b(?:pause|pausar|pare a música)\b",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            {
                await _spotifyService.PlaybackAsync("pause");
                RefreshSpotifyState();
                await AddAssistantReplyAsync("Spotify pausado.");
                Status = "Online";
                return;
            }
            if (System.Text.RegularExpressions.Regex.IsMatch(
                    prompt, @"\b(?:pule|próxima música|proxima musica)\b",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            {
                await _spotifyService.PlaybackAsync("next");
                RefreshSpotifyState();
                await AddAssistantReplyAsync("Avancei para a próxima música.");
                Status = "Online";
                return;
            }
            var volumeMatch = System.Text.RegularExpressions.Regex.Match(
                prompt, @"volume\s+(?:para\s+)?(?<volume>\d{1,3})",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (volumeMatch.Success)
            {
                var volume = Math.Clamp(int.Parse(volumeMatch.Groups["volume"].Value), 0, 100);
                await _spotifyService.SetVolumeAsync(volume);
                RefreshSpotifyState();
                await AddAssistantReplyAsync($"Volume do Spotify ajustado para {volume}%.");
                Status = "Online";
                return;
            }
            if (System.Text.RegularExpressions.Regex.IsMatch(
                    prompt, @"\b(?:continue a reprodução|continue a reproducao|retome|tocar música)\b",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            {
                await _spotifyService.PlaybackAsync("play");
                RefreshSpotifyState();
                await AddAssistantReplyAsync("Reprodução do Spotify retomada.");
                Status = "Online";
                return;
            }
            var objectiveMatch = System.Text.RegularExpressions.Regex.Match(
                prompt,
                @"(?:adicione|adicionar|crie|criar)\s+(?:um\s+)?(?:novo\s+)?objetivo\s*:?\s*(?<goal>.+)",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (objectiveMatch.Success)
            {
                var goal = objectiveMatch.Groups["goal"].Value.Trim();
                await _planningService.AddObjectiveAsync(goal);
                await AddAssistantReplyAsync(
                    $"Objetivo adicionado ao Planejamento VORTEX: **{goal}**");
                Status = "Online";
                return;
            }
            if (System.Text.RegularExpressions.Regex.IsMatch(
                    prompt, @"\b(?:abra|abrir|mostre|mostrar)\b.*\b(?:planejamento|objetivos|notas)\b",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            {
                PlanningPanelRequested?.Invoke();
                await AddAssistantReplyAsync("Planejamento VORTEX aberto.");
                Status = "Online";
                return;
            }
            if (IsLibraryOpenRequest(prompt))
            {
                LibraryPanelRequested?.Invoke();
                await AddAssistantReplyAsync("Vortex Library aberta. Pesquise, importe ou reutilize recursos pelo catálogo.");
                Status = "Online";
                return;
            }
            if (IsLibrarySaveRequest(prompt))
            {
                LibraryImportRequested?.Invoke();
                await AddAssistantReplyAsync("Abri o cadastro da Vortex Library. Selecione o arquivo, pasta ou projeto e confirme os metadados para torná-lo reutilizável.");
                Status = "Online";
                return;
            }
            if (IsDiscordMessageRequest(prompt))
            {
                await HandleDiscordGuiRequestAsync(prompt);
                return;
            }
            var localResult = await _desktopCommands.TryExecuteAsync(prompt);
            var content = localResult.Handled
                ? localResult.Output
                : await _aiService.AskAsync(prompt);
            if (!localResult.Handled)
                content = await _workspaceService.ProcessAgentResponseAsync(content);
            RefreshAgentStatus();
            RefreshPendingChanges();
            var reply = new ChatMessage { Role = "VORTEX", Content = content };
            Messages.Add(reply);
            LastAssistantMessage = content;
            await _dbService.SaveChatMessageAsync(reply);
            Status = localResult.Handled && localResult.IsError
                ? "Error"
                : content.StartsWith("Erro", StringComparison.OrdinalIgnoreCase)
                ? "Error"
                : "Online";
        }
        catch (Exception ex)
        {
            Messages.Add(new ChatMessage
            {
                Role = "VORTEX",
                Content = $"Não consegui concluir a solicitação: {ex.Message}"
            });
            LastAssistantMessage = $"Ocorreu um erro: {ex.Message}";
            Status = "Error";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task AddAssistantReplyAsync(string content)
    {
        var reply = new ChatMessage { Role = "VORTEX", Content = content };
        Messages.Add(reply);
        LastAssistantMessage = content;
        await _dbService.SaveChatMessageAsync(reply);
    }

    private async Task HandleDiscordGuiRequestAsync(string prompt)
    {
        var target = ExtractDiscordTarget(prompt);
        var message = ExtractDiscordMessage(prompt, target);
        if (string.IsNullOrWhiteSpace(target))
        {
            await AddAssistantReplyAsync("Não identifiquei para quem devo enviar no Discord. Use algo como: mande mensagem para Math no Discord: oi");
            Status = "Error";
            return;
        }
        if (string.IsNullOrWhiteSpace(message))
        {
            await AddAssistantReplyAsync("Não identifiquei o texto da mensagem. Use algo como: mande mensagem para Math no Discord: oi");
            Status = "Error";
            return;
        }
        LastAssistantMessage = string.IsNullOrWhiteSpace(target)
            ? "Solicitando autorização para controlar o Discord."
            : $"Solicitando autorização para controlar o Discord e enviar mensagem para {target}.";

        var targets = new List<string>
        {
            "Aplicativo: Discord",
            "Ação: controlar a interface do Discord",
            "Permissão: clicar, digitar e navegar somente para esta tarefa"
        };
        if (!string.IsNullOrWhiteSpace(target))
            targets.Add($"Destinatário: {target}");
        if (!string.IsNullOrWhiteSpace(message))
            targets.Add($"Mensagem: {message}");

        var allowed = await _authorizationService.RequestAsync(new AuthorizationRequest(
            "Controle do computador",
            "Usar Computer Use no Discord",
            "O VORTEX precisa controlar a interface gráfica para encontrar a conversa no Discord, digitar a mensagem e aguardar sua confirmação antes de enviar qualquer conteúdo.",
            targets,
            IsHighImpact: true));

        if (!allowed)
        {
            await AddAssistantReplyAsync("Ação cancelada: autorização para controlar o Discord negada.");
            Status = "Error";
            return;
        }

        var openDiscord = await _desktopCommands.TryExecuteAsync("/confirmar abrir discord");
        if (!openDiscord.Handled || openDiscord.IsError)
        {
            await AddAssistantReplyAsync("Autorização concedida, mas não consegui abrir o Discord automaticamente. Abra o Discord e peça novamente.");
            Status = "Error";
            return;
        }

        try
        {
            await _guiAutomationService.PrepareDiscordMessageAsync(target, message);
        }
        catch (Exception exception)
        {
            await AddAssistantReplyAsync($"Não consegui preparar a mensagem no Discord: {exception.Message}");
            Status = "Error";
            return;
        }

        var sendAllowed = await _authorizationService.RequestAsync(new AuthorizationRequest(
            "Enviar mensagem",
            "Confirmar envio no Discord",
            "O VORTEX preparou a conversa e digitou a mensagem. Autorize somente se a conversa e o texto estiverem corretos na tela.",
            [
                $"Destinatário: {target}",
                $"Mensagem: {message}",
                "Ação final: pressionar Enter no Discord"
            ],
            IsHighImpact: true));

        if (!sendAllowed)
        {
            await AddAssistantReplyAsync("Mensagem deixada como rascunho no Discord. Envio cancelado.");
            Status = "Online";
            return;
        }

        try
        {
            await _guiAutomationService.ConfirmDiscordSendAsync();
            await AddAssistantReplyAsync($"Mensagem enviada para {target} no Discord.");
            Status = "Online";
        }
        catch (Exception exception)
        {
            await AddAssistantReplyAsync($"Não consegui enviar a mensagem no Discord: {exception.Message}");
            Status = "Error";
        }
    }

    private static string ExtractDiscordTarget(string prompt)
    {
        var match = System.Text.RegularExpressions.Regex.Match(
            prompt,
            @"(?:para|pro|pra|ao|a)\s+(?<target>[\p{L}\p{N}_\-. ]{2,40})(?:\s+(?:no|na|pelo)\s+discord|\s*:|$)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return match.Success
            ? match.Groups["target"].Value.Trim().Trim('.', ',', ':', ';', '!')
            : string.Empty;
    }

    internal static bool IsDiscordMessageRequest(string prompt) =>
        System.Text.RegularExpressions.Regex.IsMatch(
            prompt,
            @"\bdiscord\b",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase)
        && System.Text.RegularExpressions.Regex.IsMatch(
            prompt,
            @"\b(?:mande|mandar|envie|enviar|escreva|escrever|digite|digitar|mensagem|msg|fale)\b",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    internal static bool IsLibraryOpenRequest(string prompt) =>
        System.Text.RegularExpressions.Regex.IsMatch(
            prompt, @"\b(?:abra|abrir|mostre|mostrar|acesse|acessar)\b.*\b(?:biblioteca|library)\b",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    internal static bool IsLibrarySaveRequest(string prompt) =>
        System.Text.RegularExpressions.Regex.IsMatch(
            prompt,
            @"\b(?:adicione|adicionar|salve|salvar|registre|registrar|guarde|guardar)\b.*\b(?:biblioteca|library|componente|projeto|template|prompt|api|asset|script|layout|tema)\b",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    internal static string ExtractDiscordMessage(string prompt, string target)
    {
        var quoted = System.Text.RegularExpressions.Regex.Match(
            prompt,
            @"[""“'](?<message>[^""”']+)[""”']",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (quoted.Success) return quoted.Groups["message"].Value.Trim();

        var colon = System.Text.RegularExpressions.Regex.Match(
            prompt,
            @":\s*(?<message>.+)$",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (colon.Success) return colon.Groups["message"].Value.Trim();

        var saying = System.Text.RegularExpressions.Regex.Match(
            prompt,
            @"(?:dizendo|falando|com\s+(?:a\s+)?mensagem|texto)\s+(?<message>.+)$",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (saying.Success) return saying.Groups["message"].Value.Trim().Trim('"', '\'', '“', '”');

        var lower = prompt.ToLowerInvariant();
        var targetIndex = string.IsNullOrWhiteSpace(target)
            ? -1
            : lower.IndexOf(target.ToLowerInvariant(), StringComparison.Ordinal);
        if (targetIndex >= 0)
        {
            var afterTarget = prompt[(targetIndex + target.Length)..].Trim();
            afterTarget = System.Text.RegularExpressions.Regex.Replace(
                afterTarget,
                @"^(?:no|na|pelo)\s+discord\s*",
                string.Empty,
                System.Text.RegularExpressions.RegexOptions.IgnoreCase).Trim();
            if (!string.IsNullOrWhiteSpace(afterTarget))
                return afterTarget.Trim(':', '-', ' ');
        }

        return string.Empty;
    }

    public void RefreshSpotifyState()
    {
        var state = _spotifyService.State;
        SpotifyTrack = state.Track;
        SpotifyArtist = string.IsNullOrWhiteSpace(state.Artist)
            ? (state.IsConnected ? state.UserName : "Conecte pelo painel")
            : state.Artist;
        SpotifyIsPlaying = state.IsPlaying;
    }

    [RelayCommand]
    private async Task SpotifyPlayPauseAsync()
    {
        await _spotifyService.PlaybackAsync(
            _spotifyService.State.IsPlaying ? "pause" : "play");
        RefreshSpotifyState();
    }

    [RelayCommand]
    private async Task SpotifyNextAsync()
    {
        await _spotifyService.PlaybackAsync("next");
        RefreshSpotifyState();
    }

    partial void OnUserInputChanged(string value)
    {
        if (IsBusy) return;
        Status = string.IsNullOrWhiteSpace(value) ? "Online" : "Typing";
    }

    partial void OnGlobalSearchChanged(string value) => FilterWorkspaceFiles();

    [RelayCommand]
    private async Task ApplyAllChangesAsync()
    {
        var result = await _workspaceService.ApplyProposalAsync();
        RefreshPendingChanges();
        ApplyWorkspace(_workspaceService.Current);
        var message = new ChatMessage { Role = "VORTEX", Content = result };
        Messages.Add(message);
        await _dbService.SaveChatMessageAsync(message);
    }

    [RelayCommand]
    private void CancelChanges()
    {
        _workspaceService.CancelProposal();
        RefreshPendingChanges();
    }

    [RelayCommand]
    private async Task NewConversationAsync()
    {
        await StartBlankConversationAsync();
    }

    public async Task StartBlankConversationAsync()
    {
        await _dbService.ClearChatMessagesAsync();
        await _workspaceService.ClearAsync();
        Messages.Clear();
        Status = "Online";
        ApplyWorkspace(null);
    }

    public async Task OpenWorkspaceAsync(string path)
    {
        var context = await _workspaceService.OpenAsync(path);
        await BeginWorkspaceConversationAsync(context);
    }

    public async Task CreateWorkspaceAsync(string name)
    {
        var context = await _workspaceService.CreateAsync(name);
        await BeginWorkspaceConversationAsync(context);
    }

    private async Task BeginWorkspaceConversationAsync(WorkspaceContext context)
    {
        await _dbService.ClearChatMessagesAsync();
        Messages.Clear();
        ApplyWorkspace(context);
        var message = new ChatMessage
        {
            Role = "VORTEX",
            Content = $"Workspace **{context.Name}** indexada.\n\n{context.ArchitectureSummary}\n\n" +
                      "Agora posso relacionar os arquivos deste projeto durante toda a conversa."
        };
        Messages.Add(message);
        await _dbService.SaveChatMessageAsync(message);
        LastAssistantMessage = $"Workspace {context.Name} pronta.";
    }

    private void ApplyWorkspace(WorkspaceContext? context)
    {
        HasWorkspace = context != null;
        WorkspaceName = context?.Name ?? "Sem Workspace";
        WorkspacePath = context?.RootPath ?? "Conversa livre";
        WorkspaceFiles.Clear();
        if (context != null)
            foreach (var file in context.Files) WorkspaceFiles.Add(file);
        FilterWorkspaceFiles();
    }

    private void FilterWorkspaceFiles()
    {
        FilteredWorkspaceFiles.Clear();
        foreach (var file in WorkspaceFiles.Where(file =>
                     string.IsNullOrWhiteSpace(GlobalSearch)
                     || file.Contains(GlobalSearch, StringComparison.OrdinalIgnoreCase)).Take(1000))
            FilteredWorkspaceFiles.Add(file);
    }

    private void RefreshAgentStatus()
    {
        ActiveModel = _aiService.ActiveModel;
        ConnectionStatus = _aiService.ConnectionStatus;
        ContextUsage = $"~{_aiService.LastContextTokens:N0} tokens";
        ResponseTime = $"{_aiService.LastResponseMilliseconds / 1000d:0.0}s";
    }

    private void RefreshPendingChanges()
    {
        PendingChangePreviews.Clear();
        if (_workspaceService.PendingProposal != null)
            foreach (var preview in _workspaceService.PendingProposal.Previews)
                PendingChangePreviews.Add(preview);
        HasPendingChanges = PendingChangePreviews.Count > 0;
    }

    [RelayCommand]
    private void MentionMessage(ChatMessage? message)
    {
        if (message == null) return;
        var excerpt = message.Content.Length > 220 ? message.Content[..220] + "…" : message.Content;
        UserInput = $"@{message.Role}: “{excerpt}”\n";
    }

    [RelayCommand]
    private async Task RegenerateResponseAsync(ChatMessage? message)
    {
        if (IsBusy || message == null) return;
        var index = Messages.IndexOf(message);
        if (index < 0) index = Messages.Count;
        var previousPrompt = Messages.Take(index).LastOrDefault(item => item.Role == "Você");
        if (previousPrompt == null) return;
        UserInput = previousPrompt.Content;
        await SendMessageAsync();
    }
}
