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
        private List<AIProviderConfig> _savedProviders = [];
        private bool _loadingProvider;

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

        async partial void OnSelectedProviderChanged(string value)
        {
            if (_loadingProvider) return;
            SelectedModel = value switch
            {
                "OpenRouter" => "openrouter/free",
                "Groq" => "openai/gpt-oss-120b",
                "OpenAI" => "gpt-4.1-mini",
                _ => SelectedModel
            };
            if (_savedProviders.Count == 0)
                _savedProviders = await _dbService.GetAIProvidersAsync();
            var saved = _savedProviders.FirstOrDefault(provider =>
                provider.ProviderName.Equals(value, StringComparison.OrdinalIgnoreCase));
            ApiKey = saved != null && IsCredentialCompatible(value, saved.ApiKey)
                ? saved.ApiKey
                : string.Empty;
            if (saved != null)
            {
                SelectedModel = saved.Model;
                AutoFallback = saved.AutoFallback;
            }
        }

        private async void LoadExistingConfig()
        {
            var profile = await _dbService.GetUserProfileAsync();
            if (profile != null) Name = profile.Name;

            _savedProviders = await _dbService.GetAIProvidersAsync();
            var primary = _savedProviders.FirstOrDefault(p => p.IsPrimary)
                          ?? _savedProviders.FirstOrDefault();
            if (primary != null)
            {
                _loadingProvider = true;
                SelectedProvider = primary.ProviderName;
                ApiKey = IsCredentialCompatible(primary.ProviderName, primary.ApiKey)
                    ? primary.ApiKey
                    : string.Empty;
                if (string.IsNullOrWhiteSpace(ApiKey))
                    ConnectionStatus =
                        $"A credencial salva não pertence ao {primary.ProviderName}. Cole uma chave nova.";
                SelectedModel = primary.Model;
                AutoFallback = primary.AutoFallback;
                _loadingProvider = false;
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
            if (!IsCredentialCompatible(SelectedProvider, ApiKey))
            {
                ConnectionStatus = CredentialMessage(SelectedProvider);
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
            if (!IsCredentialCompatible(SelectedProvider, ApiKey))
            {
                ConnectionStatus = CredentialMessage(SelectedProvider);
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
                    Name = string.IsNullOrWhiteSpace(Name) ? "zs" : Name.Trim(),
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
                _savedProviders.RemoveAll(provider =>
                    provider.ProviderName.Equals(SelectedProvider, StringComparison.OrdinalIgnoreCase));
                _savedProviders.Add(new AIProviderConfig
                {
                    ProviderName = SelectedProvider,
                    ApiKey = ApiKey,
                    Model = SelectedModel,
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

        private static bool IsCredentialCompatible(string provider, string key) =>
            provider switch
            {
                "OpenRouter" => key.StartsWith("sk-or-", StringComparison.Ordinal),
                "Groq" => key.StartsWith("gsk_", StringComparison.Ordinal),
                "OpenAI" => key.StartsWith("sk-", StringComparison.Ordinal)
                            && !key.StartsWith("sk-or-", StringComparison.Ordinal),
                _ => !string.IsNullOrWhiteSpace(key)
            };

        private static string CredentialMessage(string provider) =>
            provider switch
            {
                "OpenRouter" => "A chave do OpenRouter deve começar com sk-or-. Cole uma chave nova.",
                "Groq" => "A chave da Groq deve começar com gsk_.",
                "OpenAI" => "Informe uma chave válida da OpenAI.",
                _ => $"Informe uma chave válida para {provider}."
            };
    }
}
