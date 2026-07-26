using VORTEX.Core;
using VORTEX.Database;
using Xunit;

namespace VORTEX.Services.Tests;

public sealed class DatabaseServiceTests
{
    [Fact]
    public async Task SavesProfileAndProviderInsideOpenTransactions()
    {
        var directory = Path.Combine(
            Path.GetTempPath(), "vortex-database-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var database = new DatabaseService(directory);
            await database.InitializeAsync();
            await database.SaveUserProfileAsync(new UserProfile
            {
                Name = "zs",
                IsSetupComplete = true
            });
            await database.SaveAIProviderAsync(new AIProviderConfig
            {
                ProviderName = "OpenRouter",
                ApiKey = "test-only-key",
                Model = "openrouter/free",
                IsPrimary = true,
                AutoFallback = true
            });

            Assert.Equal("zs", (await database.GetUserProfileAsync())?.Name);
            var provider = Assert.Single(await database.GetAIProvidersAsync());
            Assert.Equal("OpenRouter", provider.ProviderName);
            Assert.Equal("test-only-key", provider.ApiKey);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }
}
