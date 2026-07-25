using System.Threading.Tasks;
using VORTEX.Core;

namespace VORTEX.Services
{
    public class MessageService : IMessageService
    {
        private readonly IAIProviderService _aiProviderService;

        public MessageService(IAIProviderService aiProviderService)
        {
            _aiProviderService = aiProviderService;
        }

        public async Task<string> SendMessageAsync(string message)
        {
            return await _aiProviderService.AskAsync(message);
        }
    }
}
