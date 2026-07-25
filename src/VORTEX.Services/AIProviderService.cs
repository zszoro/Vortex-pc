using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VORTEX.Core;

namespace VORTEX.Services
{
    public class AIProviderService : IAIProviderService
    {
        private readonly IEnumerable<IAIProvider> _providers;
        private readonly IDatabaseService _databaseService;

        public AIProviderService(IEnumerable<IAIProvider> providers, IDatabaseService databaseService)
        {
            _providers = providers;
            _databaseService = databaseService;
        }

        public IEnumerable<IAIProvider> GetAvailableProviders() => _providers;

        public async Task<bool> TestConnectionAsync(string providerName, string apiKey)
        {
            var provider = _providers.FirstOrDefault(p => p.Name == providerName);
            if (provider == null) return false;
            return await provider.ValidateApiKeyAsync(apiKey);
        }

        public async Task<string> AskAsync(string prompt)
        {
            var configs = await _databaseService.GetAIProvidersAsync();
            var primary = configs.FirstOrDefault(c => c.IsPrimary) ?? configs.FirstOrDefault();
            
            if (primary == null) return "Nenhum provedor de IA configurado.";

            var provider = _providers.FirstOrDefault(p => p.Name == primary.ProviderName);
            if (provider == null) return "Provedor configurado não encontrado.";

            return await provider.GetResponseAsync(primary.ApiKey, primary.Model, prompt);
        }
    }
}
