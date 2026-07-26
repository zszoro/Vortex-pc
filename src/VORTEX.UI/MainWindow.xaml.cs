using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using VORTEX.Core;
using VORTEX.ViewModels;
using System.Diagnostics;
using System.Windows.Threading;
using System.IO;
using Microsoft.Win32;
using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Media;
using System.Collections.Specialized;
using System.Globalization;
using System.Speech.Recognition;
using System.Speech.Synthesis;
using System.Text.RegularExpressions;
using System.Windows.Data;
using SymbolIcon = Wpf.Ui.Controls.SymbolIcon;
using SymbolRegular = Wpf.Ui.Controls.SymbolRegular;

namespace VORTEX.UI;

public partial class MainWindow
{
    [DllImport("user32.dll")]
    private static extern bool SetCursorPos(int x, int y);

    private readonly IUpdateService _updateService;
    private AppUpdateInfo? _updateInfo;
    private readonly DispatcherTimer _updateTimer;
    private Window? _embeddedWindow;
    private readonly SpeechSynthesizer _speech = new();
    private SpeechRecognitionEngine? _recognizer;
    private bool _voiceMuted;
    private bool _isListening;

    public MainWindow(MainViewModel viewModel, IUpdateService updateService)
    {
        DataContext = viewModel;
        _updateService = updateService;
        InitializeComponent();
        ConfigureAssistantVoice();
        viewModel.SpotifyPanelRequested += OpenSpotifyPanel;
        viewModel.PlanningPanelRequested += OpenPlanningPanel;
        Loaded += async (_, _) => await CheckForUpdatesAsync();
        Loaded += (_, _) =>
        {
            viewModel.Messages.CollectionChanged += Messages_CollectionChanged;
            ScrollToLatestMessage();
        };
        _updateTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(3) };
        _updateTimer.Tick += async (_, _) => await CheckForUpdatesAsync();
        _updateTimer.Start();
        Closed += (_, _) => _updateTimer.Stop();
        Closed += (_, _) =>
        {
            viewModel.Messages.CollectionChanged -= Messages_CollectionChanged;
            viewModel.SpotifyPanelRequested -= OpenSpotifyPanel;
            viewModel.PlanningPanelRequested -= OpenPlanningPanel;
            _recognizer?.Dispose();
            _speech.Dispose();
        };
    }

    private void Messages_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        ScrollToLatestMessage();
        if (_voiceMuted || e.NewItems is null) return;
        var reply = e.NewItems.OfType<ChatMessage>().LastOrDefault(item => item.Role == "VORTEX");
        if (reply == null) return;
        if (!ShouldSpeak(reply.Content)) return;
        var spokenText = PrepareSpeechText(reply.Content);
        if (string.IsNullOrWhiteSpace(spokenText)) return;
        _speech.SpeakAsyncCancelAll();
        _speech.SpeakAsync(spokenText);
    }

    private void ConfigureAssistantVoice()
    {
        _speech.Rate = -2;
        _speech.Volume = 88;
        var voices = _speech.GetInstalledVoices()
            .Where(voice => voice.Enabled)
            .Select(voice => voice.VoiceInfo)
            .ToList();
        var preferred = voices.FirstOrDefault(voice =>
                            voice.Gender == VoiceGender.Male
                            && voice.Culture.Name.StartsWith("pt", StringComparison.OrdinalIgnoreCase))
                        ?? voices.FirstOrDefault(voice => voice.Gender == VoiceGender.Male)
                        ?? voices.FirstOrDefault();
        if (preferred != null)
            _speech.SelectVoice(preferred.Name);
    }

    private static bool ShouldSpeak(string content)
    {
        if (string.IsNullOrWhiteSpace(content)) return false;
        var trimmed = content.TrimStart();
        if (trimmed.StartsWith("Não consegui", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("Nao consegui", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("Ocorreu um erro", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("Erro", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("Ação cancelada", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("Acao cancelada", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("Pasta não encontrada", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("Caminho não encontrado", StringComparison.OrdinalIgnoreCase)
            || trimmed.Contains("Unhandled exception", StringComparison.OrdinalIgnoreCase)
            || trimmed.Contains("stack trace", StringComparison.OrdinalIgnoreCase)
            || trimmed.Contains("OpenRouter recusou", StringComparison.OrdinalIgnoreCase)
            || trimmed.Contains("<vortex-file-actions>", StringComparison.OrdinalIgnoreCase)
            || trimmed.Contains("```", StringComparison.Ordinal))
        {
            return false;
        }

        var codeSignals = new[] { "using ", "namespace ", "public class ", "private ", "function ", "const ", "let ", "var ", "=>", "{", "};" };
        var signalCount = codeSignals.Count(signal => trimmed.Contains(signal, StringComparison.Ordinal));
        return signalCount < 3;
    }

    private static string PrepareSpeechText(string content)
    {
        var withoutCodeBlocks = Regex.Replace(content, "```[\\s\\S]*?```", " ", RegexOptions.Multiline);
        var withoutInlineCode = Regex.Replace(withoutCodeBlocks, "`[^`]+`", " ");
        var plain = Regex.Replace(withoutInlineCode, @"[*_#>\[\]{}()<>|\\/]", " ");
        plain = Regex.Replace(plain, @"https?://\S+", " link ");
        plain = Regex.Replace(plain, @"\s+", " ").Trim();
        const int maxSpeechCharacters = 900;
        return plain.Length > maxSpeechCharacters
            ? plain[..maxSpeechCharacters] + "."
            : plain;
    }

    private void ScrollToLatestMessage()
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (MessageList.Items.Count == 0) return;
            var last = MessageList.Items[^1];
            MessageList.ScrollIntoView(last);
        }, DispatcherPriority.ContextIdle);
    }

    private async Task CheckForUpdatesAsync()
    {
        _updateInfo = await _updateService.CheckAsync();
        if (!_updateInfo.IsUpdateAvailable) return;
        UpdateBannerTitle.Text = $"Atualização VORTEX {_updateInfo.LatestVersion} disponível";
        UpdateBannerNotes.Text = _updateInfo.Notes;
        UpdateBanner.Visibility = Visibility.Visible;
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        var settings = App.ServiceProvider.GetRequiredService<SettingsWindow>();
        ShowOverlay(settings);
    }

    private void Account_Click(object sender, MouseButtonEventArgs e)
    {
        var account = App.ServiceProvider.GetRequiredService<AccountWindow>();
        ShowOverlay(account);
    }

    private void DismissUpdate_Click(object sender, RoutedEventArgs e) =>
        UpdateBanner.Visibility = Visibility.Collapsed;

    private void OpenUpdate_Click(object sender, RoutedEventArgs e) =>
        _updateService.OpenDownloadPage(_updateInfo?.DownloadUrl);

    private void CopyMessage_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string content } && !string.IsNullOrWhiteSpace(content))
            Clipboard.SetText(content);
    }

    private MainViewModel ViewModel => (MainViewModel)DataContext;

    private void FocusChat_Click(object sender, RoutedEventArgs e)
    {
        ScrollToLatestMessage();
        ChatInput.Focus();
    }

    private void AddFile_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Adicionar arquivos à conversa",
            Multiselect = true,
            CheckFileExists = true
        };
        if (dialog.ShowDialog(this) != true) return;
        var references = string.Join(" ", dialog.FileNames.Select(path => $"\"{path}\""));
        ViewModel.UserInput = string.IsNullOrWhiteSpace(ViewModel.UserInput)
            ? $"Analise os arquivos: {references}"
            : $"{ViewModel.UserInput} {references}";
        ChatInput.Focus();
        ChatInput.CaretIndex = ChatInput.Text.Length;
    }

    private void SlashCommand_Click(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.UserInput.StartsWith('/'))
            ViewModel.UserInput = "/";
        CommandPopup.IsOpen = true;
        ChatInput.Focus();
        ChatInput.CaretIndex = ChatInput.Text.Length;
    }

    private void CommandOption_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string command })
            ViewModel.UserInput = command;
        CommandPopup.IsOpen = false;
        ChatInput.Focus();
        ChatInput.CaretIndex = ChatInput.Text.Length;
    }

    private async void Microphone_Click(object sender, RoutedEventArgs e)
    {
        if (_isListening)
        {
            _recognizer?.RecognizeAsyncStop();
            return;
        }

        try
        {
            _recognizer ??= CreateRecognizer();
            _isListening = true;
            MicrophoneButton.Content = CreateSymbolIcon(SymbolRegular.MicOff24);
            MicrophoneButton.Foreground = Brushes.Red;
            MicrophoneButton.ToolTip = "Parar gravação";
            _recognizer.RecognizeAsync(RecognizeMode.Single);
        }
        catch (Exception ex)
        {
            _isListening = false;
            MessageBox.Show(this,
                $"Não foi possível iniciar o microfone: {ex.Message}",
                "Voz do VORTEX", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        await Task.CompletedTask;
    }

    private SpeechRecognitionEngine CreateRecognizer()
    {
        var installed = SpeechRecognitionEngine.InstalledRecognizers();
        var recognizerInfo = installed.FirstOrDefault(item =>
                                 item.Culture.Name.Equals("pt-BR", StringComparison.OrdinalIgnoreCase))
                             ?? installed.FirstOrDefault()
                             ?? throw new InvalidOperationException(
                                 "Instale um pacote de reconhecimento de voz nas configurações de idioma do Windows.");
        var recognizer = new SpeechRecognitionEngine(recognizerInfo);
        recognizer.LoadGrammar(new DictationGrammar());
        recognizer.SetInputToDefaultAudioDevice();
        recognizer.SpeechRecognized += async (_, args) =>
        {
            if (args.Result.Confidence < 0.35 || string.IsNullOrWhiteSpace(args.Result.Text)) return;
            await Dispatcher.InvokeAsync(async () =>
            {
                ViewModel.UserInput = args.Result.Text;
                await ViewModel.SendMessageCommand.ExecuteAsync(null);
            });
        };
        recognizer.RecognizeCompleted += (_, _) => Dispatcher.Invoke(() =>
        {
            _isListening = false;
            MicrophoneButton.Content = CreateSymbolIcon(SymbolRegular.Mic24);
            MicrophoneButton.Foreground = Brushes.White;
            MicrophoneButton.ToolTip = "Falar com o VORTEX";
        });
        return recognizer;
    }

    private void MuteVoice_Click(object sender, RoutedEventArgs e)
    {
        _voiceMuted = !_voiceMuted;
        MuteVoiceButton.Content = CreateSymbolIcon(_voiceMuted ? SymbolRegular.MicOff24 : SymbolRegular.Mic24);
        MuteVoiceButton.Foreground = _voiceMuted ? Brushes.Gray : Brushes.White;
        MuteVoiceButton.ToolTip = _voiceMuted ? "Ativar voz da IA" : "Mutar voz da IA";
        if (_voiceMuted) _speech.SpeakAsyncCancelAll();
    }

    private static SymbolIcon CreateSymbolIcon(SymbolRegular symbol) =>
        new() { Symbol = symbol, FontSize = 21 };

    private void ConversationSearch_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        var query = ConversationSearch.Text.Trim();
        var view = CollectionViewSource.GetDefaultView(ViewModel.Messages);
        view.Filter = item => item is ChatMessage message
                              && (string.IsNullOrWhiteSpace(query)
                                  || message.Content.Contains(query, StringComparison.OrdinalIgnoreCase));
        view.Refresh();
        ScrollToLatestMessage();
    }

    private void Terminal_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.UserInput = "/terminal ";
        ChatInput.Focus();
        ChatInput.CaretIndex = ChatInput.Text.Length;
    }

    private async void TestComputerUse_Click(object sender, RoutedEventArgs e)
    {
        var authorization = App.ServiceProvider.GetRequiredService<IAuthorizationService>();
        if (!await authorization.RequestAsync(new(
                "Teste do Computer Use",
                "Mover o mouse e minimizar o VORTEX",
                "O cursor será trocado pelo pet VORTEX, moverá até o botão minimizar e o app será minimizado. Nenhum outro aplicativo será controlado.",
                ["Cursor do Windows", "Janela do VORTEX"])))
            return;

        var previousCursor = Mouse.OverrideCursor;
        try
        {
            var cursorUri = new Uri("pack://application:,,,/VORTEX.UI;component/Assets/vortex-pet.cur");
            using var stream = Application.GetResourceStream(cursorUri)?.Stream;
            if (stream != null)
                Mouse.OverrideCursor = new Cursor(stream);

            var start = PointToScreen(Mouse.GetPosition(this));
            var target = PointToScreen(new Point(Math.Max(0, ActualWidth - 132), 16));
            const int frames = 26;
            for (var frame = 1; frame <= frames; frame++)
            {
                var progress = frame / (double)frames;
                var eased = 1 - Math.Pow(1 - progress, 3);
                SetCursorPos(
                    (int)(start.X + ((target.X - start.X) * eased)),
                    (int)(start.Y + ((target.Y - start.Y) * eased)));
                await Task.Delay(18);
            }
            await Task.Delay(220);
            WindowState = WindowState.Minimized;
        }
        finally
        {
            Mouse.OverrideCursor = previousCursor;
        }
    }

    private void Memory_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.UserInput = "Lembre e organize os pontos mais importantes desta conversa: ";
        ChatInput.Focus();
    }

    private void Automations_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.UserInput = "Crie uma automação para: ";
        ChatInput.Focus();
    }

    private void Spotify_Click(object sender, RoutedEventArgs e) => OpenSpotifyPanel();
    private void Planning_Click(object sender, RoutedEventArgs e) => OpenPlanningPanel();

    private void OpenSpotifyPanel()
    {
        Dispatcher.Invoke(() =>
        {
            var window = App.ServiceProvider.GetRequiredService<SpotifyWindow>();
            ShowOverlay(window);
        });
    }

    private void OpenPlanningPanel()
    {
        Dispatcher.Invoke(() =>
        {
            var window = App.ServiceProvider.GetRequiredService<PlanningWindow>();
            ShowOverlay(window);
        });
    }

    private void ShowOverlay(Window window)
    {
        CloseCurrentOverlay();
        _embeddedWindow = window;
        var content = window.Content;
        window.Content = null;
        OverlayContent.Content = content;
        OverlayHost.Visibility = Visibility.Visible;
    }

    private void CloseOverlay_Click(object sender, RoutedEventArgs e)
    {
        CloseCurrentOverlay();
        ViewModel.RefreshSpotifyState();
        OverlayHost.Visibility = Visibility.Collapsed;
    }

    private void CloseCurrentOverlay()
    {
        OverlayContent.Content = null;
        _embeddedWindow?.Close();
        _embeddedWindow = null;
    }

    private void Summarize_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.UserInput = "Resuma nossa conversa e liste as próximas ações.";
        ChatInput.Focus();
    }

    private async void Files_Click(object sender, RoutedEventArgs e)
    {
        var downloads = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        var authorization = App.ServiceProvider.GetRequiredService<IAuthorizationService>();
        if (!await authorization.RequestAsync(new(
                "Acesso a pastas", "Abrir Downloads",
                "O Explorador de Arquivos será aberto na pasta Downloads.", [downloads])))
            return;
        Process.Start(new ProcessStartInfo(downloads) { UseShellExecute = true });
    }

    private void Backups_Click(object sender, RoutedEventArgs e)
    {
        var workspace = App.ServiceProvider.GetRequiredService<IWorkspaceService>();
        if (workspace.Current == null)
        {
            MessageBox.Show(this, "Abra uma Workspace antes de acessar backups.", "VORTEX",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        new BackupsWindow(workspace) { Owner = this }.ShowDialog();
    }

    private async void NewConversation_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new NewConversationWindow { Owner = this };
        if (dialog.ShowDialog() != true) return;
        try
        {
            if (dialog.Choice == "blank")
            {
                await ViewModel.StartBlankConversationAsync();
                return;
            }
            if (dialog.Choice == "open")
            {
                var folderDialog = new OpenFolderDialog
                {
                    Title = "Selecione a pasta do projeto",
                    Multiselect = false
                };
                if (folderDialog.ShowDialog(this) == true)
                    await ViewModel.OpenWorkspaceAsync(folderDialog.FolderName);
                return;
            }
            if (dialog.Choice == "create")
                await ViewModel.CreateWorkspaceAsync(dialog.ProjectName);
        }
        catch (OperationCanceledException)
        {
            // O modal de autorização já informou a decisão do usuário.
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Não foi possível abrir a Workspace",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
