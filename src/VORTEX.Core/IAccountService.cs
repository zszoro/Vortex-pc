namespace VORTEX.Core;

public interface IAccountService
{
    Task<LocalAccount?> GetCurrentAsync();
    Task<LocalAccount> RegisterAsync(string name, string email, string password);
    Task<LocalAccount> LoginAsync(string email, string password);
    Task<LocalAccount> UpdateProfileAsync(string name, string avatarSourcePath);
    Task LogoutAsync();
}
