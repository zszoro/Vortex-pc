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

    public ObservableCollection<ChatMessage> Messages { get; } = [];
    public ObservableCollection<string> WorkspaceFiles { get; } = [];
    public ObservableCollection<string> FilteredWorkspaceFiles { get; } = [];
    public ObservableCollection<string> PendingChangePreviews { get; } = [];

    public MainViewModel(
        IAIProviderService aiService,
        IDatabaseService dbService,
        IDesktopCommandService desktopCommands,
        IWorkspaceService workspaceService)
    {
        _aiService = aiService;
        _dbService = dbService;
        _desktopCommands = desktopCommands;
        _workspaceService = workspaceService;
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
