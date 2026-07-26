using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using VORTEX.AIProviders;
using VORTEX.Core;
using VORTEX.Database;
using VORTEX.Services;
using VORTEX.ViewModels;

namespace VORTEX.UI
{
    public partial class App : Application
    {
        public static ServiceProvider ServiceProvider { get; private set; } = null!;

        protected override async void OnStartup(StartupEventArgs e)
        {
            var services = new ServiceCollection();
            ConfigureServices(services);
            ServiceProvider = services.BuildServiceProvider();
            ShutdownMode = ShutdownMode.OnMainWindowClose;

            var db = ServiceProvider.GetRequiredService<IDatabaseService>();
            await db.InitializeAsync();

            var profile = await db.GetUserProfileAsync();
            if (profile == null || !profile.IsSetupComplete)
            {
                var setupWindow = ServiceProvider.GetRequiredService<SetupWindow>();
                MainWindow = setupWindow;
                setupWindow.Show();
            }
            else
            {
                var mainWindow = ServiceProvider.GetRequiredService<MainWindow>();
                MainWindow = mainWindow;
                mainWindow.Show();
                
                var companion = ServiceProvider.GetRequiredService<CompanionWindow>();
                companion.Show();
            }

            base.OnStartup(e);
        }

        private void ConfigureServices(IServiceCollection services)
        {
            // Database
            services.AddSingleton<IDatabaseService, DatabaseService>();

            // AI Providers
            services.AddSingleton<IAIProvider, OpenAIProvider>();
            services.AddSingleton<IAIProvider, GroqProvider>();
            services.AddSingleton<IAIProviderService, AIProviderService>();

            // Services
            services.AddSingleton<IMessageService, MessageService>();
            services.AddSingleton<IDesktopCommandService, DesktopCommandService>();
            services.AddSingleton<IUpdateService, GitHubUpdateService>();

            // ViewModels
            services.AddTransient<SetupViewModel>();
            services.AddSingleton<MainViewModel>();

            // Windows
            services.AddTransient<SetupWindow>();
            services.AddSingleton<MainWindow>();
            services.AddSingleton<CompanionWindow>();
            services.AddTransient<QuickChatWindow>();
            services.AddTransient<SettingsWindow>();
            services.AddTransient<SettingsIAPage>();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            ServiceProvider?.Dispose();
            base.OnExit(e);
        }
    }
}
