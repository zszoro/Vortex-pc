using System.Collections.Generic;
using System.Threading.Tasks;

namespace VORTEX.Core
{
    public interface IMessageService
    {
        Task<string> SendMessageAsync(string message);
        // Future: Add methods for message history, context management
    }
}
