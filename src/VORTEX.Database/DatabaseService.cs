using System.Security.Cryptography;
using System.Text;
using Dapper;
using Microsoft.Data.Sqlite;
using VORTEX.Core;

namespace VORTEX.Database;

public sealed class DatabaseService : IDatabaseService
{
    private readonly string _connectionString;

    public DatabaseService() : this(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "VORTEX"))
    {
    }

    public DatabaseService(string dataDirectory)
    {
        var dbPath = Path.Combine(dataDirectory, "vortex.db");
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
        _connectionString = $"Data Source={dbPath};Pooling=False";
    }

    public async Task InitializeAsync()
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.ExecuteAsync("""
            CREATE TABLE IF NOT EXISTS UserProfile (
                Name TEXT NOT NULL,
                Preferences TEXT,
                Treatment TEXT,
                IsSetupComplete INTEGER DEFAULT 0
            );
            CREATE TABLE IF NOT EXISTS AIProviders (
                ProviderName TEXT PRIMARY KEY,
                ApiKey TEXT NOT NULL,
                Model TEXT NOT NULL,
                IsPrimary INTEGER DEFAULT 0,
                AutoFallback INTEGER DEFAULT 1
            );
            CREATE TABLE IF NOT EXISTS ChatMessages (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Role TEXT NOT NULL,
                Content TEXT NOT NULL,
                CreatedAt TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS Accounts (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Email TEXT NOT NULL UNIQUE COLLATE NOCASE,
                Name TEXT NOT NULL,
                PasswordHash TEXT NOT NULL,
                AvatarPath TEXT NOT NULL DEFAULT '',
                IsActive INTEGER NOT NULL DEFAULT 0,
                CreatedAt TEXT NOT NULL
            );
            """);
        var columns = (await connection.QueryAsync<string>(
            "SELECT name FROM pragma_table_info('AIProviders')"))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!columns.Contains("AutoFallback"))
        {
            await connection.ExecuteAsync(
                "ALTER TABLE AIProviders ADD COLUMN AutoFallback INTEGER DEFAULT 1");
        }

        var profileExists = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM UserProfile");
        if (profileExists == 0)
        {
            await connection.ExecuteAsync(
                "INSERT INTO UserProfile (Name, IsSetupComplete) VALUES ('', 0)");
        }
    }

    public async Task SaveUserProfileAsync(UserProfile profile)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await connection.ExecuteAsync("DELETE FROM UserProfile", transaction: transaction);
        await connection.ExecuteAsync(
            "INSERT INTO UserProfile (Name, Preferences, Treatment, IsSetupComplete) VALUES (@Name, @Preferences, @Treatment, @IsSetupComplete)",
            profile, transaction);
        await transaction.CommitAsync();
    }

    public async Task<UserProfile?> GetUserProfileAsync()
    {
        await using var connection = new SqliteConnection(_connectionString);
        return await connection.QueryFirstOrDefaultAsync<UserProfile>("SELECT * FROM UserProfile");
    }

    public async Task SaveAIProviderAsync(AIProviderConfig config)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        if (config.IsPrimary)
        {
            await connection.ExecuteAsync(
                "UPDATE AIProviders SET IsPrimary = 0", transaction: transaction);
        }

        var stored = new AIProviderConfig
        {
            ProviderName = config.ProviderName,
            ApiKey = Protect(config.ApiKey),
            Model = config.Model,
            IsPrimary = config.IsPrimary,
            AutoFallback = config.AutoFallback
        };
        await connection.ExecuteAsync("""
            INSERT INTO AIProviders (ProviderName, ApiKey, Model, IsPrimary, AutoFallback)
            VALUES (@ProviderName, @ApiKey, @Model, @IsPrimary, @AutoFallback)
            ON CONFLICT(ProviderName) DO UPDATE SET
            ApiKey = @ApiKey, Model = @Model, IsPrimary = @IsPrimary,
            AutoFallback = @AutoFallback
            """, stored, transaction);
        await transaction.CommitAsync();
    }

    public async Task<List<AIProviderConfig>> GetAIProvidersAsync()
    {
        await using var connection = new SqliteConnection(_connectionString);
        var providers = await connection.QueryAsync<AIProviderConfig>("SELECT * FROM AIProviders");
        return providers.Select(provider =>
        {
            provider.ApiKey = Unprotect(provider.ApiKey);
            return provider;
        }).ToList();
    }

    public async Task DeleteAIProviderAsync(string providerName)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.ExecuteAsync(
            "DELETE FROM AIProviders WHERE ProviderName = @ProviderName",
            new { ProviderName = providerName });
    }

    public async Task<List<ChatMessage>> GetChatMessagesAsync(int limit = 100)
    {
        await using var connection = new SqliteConnection(_connectionString);
        var messages = await connection.QueryAsync<ChatMessage>("""
            SELECT Id, Role, Content, CreatedAt
            FROM (
                SELECT Id, Role, Content, CreatedAt
                FROM ChatMessages
                ORDER BY Id DESC
                LIMIT @Limit
            )
            ORDER BY Id
            """, new { Limit = Math.Clamp(limit, 1, 500) });
        return messages.ToList();
    }

    public async Task SaveChatMessageAsync(ChatMessage message)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.ExecuteAsync(
            "INSERT INTO ChatMessages (Role, Content, CreatedAt) VALUES (@Role, @Content, @CreatedAt)",
            message);
    }

    public async Task ClearChatMessagesAsync()
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.ExecuteAsync("DELETE FROM ChatMessages");
    }

    public async Task<LocalAccount?> GetActiveAccountAsync()
    {
        await using var connection = new SqliteConnection(_connectionString);
        return await connection.QueryFirstOrDefaultAsync<LocalAccount>(
            "SELECT * FROM Accounts WHERE IsActive = 1 LIMIT 1");
    }

    public async Task<LocalAccount?> GetAccountByEmailAsync(string email)
    {
        await using var connection = new SqliteConnection(_connectionString);
        return await connection.QueryFirstOrDefaultAsync<LocalAccount>(
            "SELECT * FROM Accounts WHERE Email = @Email COLLATE NOCASE LIMIT 1",
            new { Email = email.Trim() });
    }

    public async Task<long> CreateAccountAsync(LocalAccount account)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await connection.ExecuteAsync("UPDATE Accounts SET IsActive = 0", transaction: transaction);
        var id = await connection.ExecuteScalarAsync<long>("""
            INSERT INTO Accounts (Email, Name, PasswordHash, AvatarPath, IsActive, CreatedAt)
            VALUES (@Email, @Name, @PasswordHash, @AvatarPath, 1, @CreatedAt);
            SELECT last_insert_rowid();
            """, account, transaction);
        await transaction.CommitAsync();
        return id;
    }

    public async Task SetActiveAccountAsync(long? accountId)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await connection.ExecuteAsync("UPDATE Accounts SET IsActive = 0", transaction: transaction);
        if (accountId.HasValue)
            await connection.ExecuteAsync(
                "UPDATE Accounts SET IsActive = 1 WHERE Id = @Id",
                new { Id = accountId.Value }, transaction);
        await transaction.CommitAsync();
    }

    public async Task UpdateAccountProfileAsync(long accountId, string name, string avatarPath)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.ExecuteAsync(
            "UPDATE Accounts SET Name = @Name, AvatarPath = @AvatarPath WHERE Id = @Id",
            new { Id = accountId, Name = name, AvatarPath = avatarPath });
    }

    private static string Protect(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var protectedBytes = ProtectedData.Protect(
            Encoding.UTF8.GetBytes(value), null, DataProtectionScope.CurrentUser);
        return "dpapi:" + Convert.ToBase64String(protectedBytes);
    }

    private static string Unprotect(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        if (!value.StartsWith("dpapi:", StringComparison.Ordinal)) return value;
        try
        {
            var bytes = Convert.FromBase64String(value[6..]);
            return Encoding.UTF8.GetString(
                ProtectedData.Unprotect(bytes, null, DataProtectionScope.CurrentUser));
        }
        catch (CryptographicException)
        {
            return string.Empty;
        }
    }
}
