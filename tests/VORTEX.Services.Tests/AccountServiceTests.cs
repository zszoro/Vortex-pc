using VORTEX.Database;
using VORTEX.Services;
using Xunit;

namespace VORTEX.Services.Tests;

public sealed class AccountServiceTests
{
    [Fact]
    public async Task RegistersLogsOutAndLogsBackInWithoutStoringPlainPassword()
    {
        var directory = Path.Combine(
            Path.GetTempPath(), "vortex-account-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var database = new DatabaseService(directory);
            await database.InitializeAsync();
            var accounts = new AccountService(database);

            var created = await accounts.RegisterAsync("zs", "zs@example.com", "senha-forte-123");
            Assert.True(created.Id > 0);
            Assert.StartsWith("pbkdf2-sha256:", created.PasswordHash);
            Assert.DoesNotContain("senha-forte-123", created.PasswordHash);

            await accounts.LogoutAsync();
            Assert.Null(await accounts.GetCurrentAsync());

            var loggedIn = await accounts.LoginAsync("ZS@EXAMPLE.COM", "senha-forte-123");
            Assert.Equal(created.Id, loggedIn.Id);
            Assert.True(loggedIn.IsActive);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }
}
