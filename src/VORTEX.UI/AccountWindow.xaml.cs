using Microsoft.Win32;
using System.IO;
using System.Windows;
using VORTEX.Core;
using VORTEX.ViewModels;

namespace VORTEX.UI;

public partial class AccountWindow : Window
{
    private readonly IAccountService _accounts;
    private readonly MainViewModel _main;
    private string _avatarSource = string.Empty;

    public AccountWindow(IAccountService accounts, MainViewModel main)
    {
        _accounts = accounts;
        _main = main;
        InitializeComponent();
        Loaded += async (_, _) => await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        var account = await _accounts.GetCurrentAsync();
        SessionText.Text = account == null
            ? "Nenhuma conta conectada. Os dados permanecem somente neste computador."
            : $"Conectado como {account.Name} · {account.Email}";
        if (account == null) return;
        ProfileName.Text = account.Name;
        RegisterEmail.Text = account.Email;
        AvatarText.Text = string.IsNullOrWhiteSpace(account.AvatarPath)
            ? "Sem foto"
            : Path.GetFileName(account.AvatarPath);
        _main.UserName = account.Name;
    }

    private async void Register_Click(object sender, RoutedEventArgs e) =>
        await RunAsync(async () =>
        {
            var account = await _accounts.RegisterAsync(
                ProfileName.Text, RegisterEmail.Text, RegisterPassword.Password);
            _main.UserName = account.Name;
            await RefreshAsync();
        });

    private async void Login_Click(object sender, RoutedEventArgs e) =>
        await RunAsync(async () =>
        {
            var account = await _accounts.LoginAsync(LoginEmail.Text, LoginPassword.Password);
            _main.UserName = account.Name;
            await RefreshAsync();
        });

    private async void SaveProfile_Click(object sender, RoutedEventArgs e) =>
        await RunAsync(async () =>
        {
            var account = await _accounts.UpdateProfileAsync(ProfileName.Text, _avatarSource);
            _main.UserName = account.Name;
            await RefreshAsync();
        });

    private async void Logout_Click(object sender, RoutedEventArgs e) =>
        await RunAsync(async () =>
        {
            await _accounts.LogoutAsync();
            _main.UserName = "Você";
            ProfileName.Clear();
            RegisterEmail.Clear();
            await RefreshAsync();
        });

    private void ChooseAvatar_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Escolha sua foto",
            Filter = "Imagens|*.png;*.jpg;*.jpeg;*.webp;*.bmp",
            CheckFileExists = true
        };
        if (dialog.ShowDialog(this) != true) return;
        _avatarSource = dialog.FileName;
        AvatarText.Text = Path.GetFileName(_avatarSource);
    }

    private async Task RunAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Conta VORTEX",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
