using System.Collections.Generic;
using System.Threading.Tasks;

namespace VORTEX.Core
{
    public interface IAIProviderService
    {
        IEnumerable<IAIProvider> GetAvailableProviders();
        Task<bool> TestConnectionAsync(string providerName, string apiKey);
        Task<string> AskAsync(string prompt);
        string ActiveModel { get; }
        string ConnectionStatus { get; }
        int LastContextTokens { get; }
        long LastResponseMilliseconds { get; }
    }
}
