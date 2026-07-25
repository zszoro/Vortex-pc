using System.Threading.Tasks;

namespace VORTEX.Core
{
    public interface IAIProvider
    {
        string Name { get; }
        Task<bool> ValidateApiKeyAsync(string apiKey);
        Task<string> GetResponseAsync(string apiKey, string model, string prompt);
    }
}
