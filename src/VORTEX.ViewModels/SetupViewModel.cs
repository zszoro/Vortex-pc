using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VORTEX.Core;

namespace VORTEX.ViewModels
{
    public partial class SetupViewModel : ObservableObject
    {
        private readonly IAIProviderService _aiService;
        private readonly IDatabaseService _dbService;

        [ObservableProperty] private string _name = string.Empty;
        [ObservableProperty] private string _apiKey = string.Empty;
        [ObservableProperty] private string _selectedProvider = "OpenRouter";
        [ObservableProperty] private string _selectedModel = "openrouter/free";
        [ObservableProperty] private bool _autoFallback = true;
        [ObservableProperty] private string _connectionStatus = "Aguardando teste...";
        [ObservableProperty] private bool _isBusy;

        public List<string> Providers => _aiService.GetAvailableProviders().Select(p => p.Name).ToList();
        public List<string> OpenRouterModels { get; } =
        [
            "openrouter/free",
            "nvidia/nemotron-3-ultra-550b-a55b:free",
            "cohere/north-mini-code:free",
            "poolside/laguna-s-2.1:free",
            "inclusionai/ling-3.0-flash:free"
        ];

        public event Action? OnSetupComplete;

        public SetupViewModel(IAIProviderService aiService, IDatabaseService dbService)
        {
            _aiService = aiService;
            _dbService = dbService;
            LoadExistingConfig();
        }

        partial void OnSelectedProviderChanged(string value)
        {
            SelectedModel = value switch
            {
                "OpenRouter" => "openrouter/free",
                "Groq" => "openai/gpt-oss-120b",
                "OpenAI" => "gpt-4.1-mini",
                _ => SelectedModel
            };
        }

        private async void LoadExistingConfig()
        {
            var profile = await _dbService.GetUserProfileAsync();
            if (profile != null) Name = profile.Name;

            var providers = await _dbService.GetAIProvidersAsync();
            var primary = providers.FirstOrDefault(p => p.IsPrimary) ?? providers.FirstOrDefault();
            if (primary != null)
            {
                SelectedProvider = primary.ProviderName;
                ApiKey = primary.ApiKey;
                SelectedModel = primary.Model;
                AutoFallback = primary.AutoFallback;
            }
        }

        [RelayCommand]
        public async Task TestConnectionAsync()
        {
            if (string.IsNullOrWhiteSpace(ApiKey))
            {
                ConnectionStatus = "Insira uma API Key para testar.";
                return;
            }

            IsBusy = true;
            ConnectionStatus = "Validando conexão...";
            bool success = await _aiService.TestConnectionAsync(SelectedProvider, ApiKey);
            ConnectionStatus = success ? "✓ Conexão estabelecida!" : "✗ Falha na conexão. Verifique os dados.";
            IsBusy = false;
        }

        [RelayCommand]
        public async Task FinishSetupAsync()
        {
            if (string.IsNullOrWhiteSpace(Name))
            {
                ConnectionStatus = "Por favor, insira seu nome.";
                return;
            }

            IsBusy = true;
            try
            {
                ConnectionStatus = "Validando conexão final...";
                bool success = await _aiService.TestConnectionAsync(SelectedProvider, ApiKey);
                if (!success)
                {
                    ConnectionStatus = "Erro: a chave não foi aceita pelo OpenRouter.";
                    return;
                }

                await _dbService.SaveUserProfileAsync(new UserProfile
                {
                    Name = Name,
                    IsSetupComplete = true
                });

                await _dbService.SaveAIProviderAsync(new AIProviderConfig
                {
                    ProviderName = SelectedProvider,
                    ApiKey = ApiKey,
                    Model = string.IsNullOrWhiteSpace(SelectedModel)
                        ? "openrouter/free"
                        : SelectedModel.Trim(),
                    IsPrimary = true,
                    AutoFallback = AutoFallback
                });

                ConnectionStatus = "✓ Configuração salva com segurança.";
                OnSetupComplete?.Invoke();
            }
            catch (Exception exception)
            {
                ConnectionStatus =
                    $"Não foi possível salvar: {exception.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
