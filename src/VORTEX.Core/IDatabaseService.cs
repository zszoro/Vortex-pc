using System.Collections.Generic;
using System.Threading.Tasks;

namespace VORTEX.Core
{
    public interface IDatabaseService
    {
        Task InitializeAsync();
        Task SaveUserProfileAsync(UserProfile profile);
        Task<UserProfile?> GetUserProfileAsync();
        Task SaveAIProviderAsync(AIProviderConfig config);
        Task<List<AIProviderConfig>> GetAIProvidersAsync();
        Task DeleteAIProviderAsync(string providerName);
        Task<List<ChatMessage>> GetChatMessagesAsync(int limit = 100);
        Task SaveChatMessageAsync(ChatMessage message);
        Task ClearChatMessagesAsync();
        Task<LocalAccount?> GetActiveAccountAsync();
        Task<LocalAccount?> GetAccountByEmailAsync(string email);
        Task<long> CreateAccountAsync(LocalAccount account);
        Task SetActiveAccountAsync(long? accountId);
        Task UpdateAccountProfileAsync(long accountId, string name, string avatarPath);
    }
}
