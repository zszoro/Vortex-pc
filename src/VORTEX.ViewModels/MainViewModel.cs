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

    [ObservableProperty] private string _userName = "Você";
    [ObservableProperty] private string _status = "Online";
    [ObservableProperty] private string _userInput = string.Empty;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _lastAssistantMessage = "Olá! Clique com o botão direito para falar comigo.";

    public ObservableCollection<ChatMessage> Messages { get; } = [];

    public MainViewModel(
        IAIProviderService aiService,
        IDatabaseService dbService,
        IDesktopCommandService desktopCommands)
    {
        _aiService = aiService;
        _dbService = dbService;
        _desktopCommands = desktopCommands;
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

    [RelayCommand]
    private async Task NewConversationAsync()
    {
        await _dbService.ClearChatMessagesAsync();
        Messages.Clear();
        Status = "Online";
    }
}
