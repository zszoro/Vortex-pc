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

            var db = ServiceProvider.GetRequiredService<IDatabaseService>();
            await db.InitializeAsync();

            var profile = await db.GetUserProfileAsync();
            if (profile == null || !profile.IsSetupComplete)
            {
                var setupWindow = ServiceProvider.GetRequiredService<SetupWindow>();
                setupWindow.Show();
            }
            else
            {
                var mainWindow = ServiceProvider.GetRequiredService<MainWindow>();
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

            // ViewModels
            services.AddTransient<SetupViewModel>();
            services.AddSingleton<MainViewModel>();
            services.AddTransient<CompanionViewModel>();

            // Windows
            services.AddTransient<SetupWindow>();
            services.AddSingleton<MainWindow>();
            services.AddSingleton<CompanionWindow>();
            services.AddTransient<QuickChatWindow>();
            services.AddTransient<SettingsWindow>();
            services.AddTransient<SettingsIAPage>();
        }
    }
}
