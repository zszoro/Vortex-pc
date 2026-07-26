using System.Security.Cryptography;
using VORTEX.Core;

namespace VORTEX.Services;

public sealed class AccountService(IDatabaseService database) : IAccountService
{
    public Task<LocalAccount?> GetCurrentAsync() => database.GetActiveAccountAsync();

    public async Task<LocalAccount> RegisterAsync(string name, string email, string password)
    {
        name = name.Trim();
        email = email.Trim().ToLowerInvariant();
        Validate(name, email, password);
        if (await database.GetAccountByEmailAsync(email) != null)
            throw new InvalidOperationException("Já existe uma conta com este e-mail.");
        var account = new LocalAccount
        {
            Name = name,
            Email = email,
            PasswordHash = HashPassword(password),
            IsActive = true
        };
        account.Id = await database.CreateAccountAsync(account);
        return account;
    }

    public async Task<LocalAccount> LoginAsync(string email, string password)
    {
        var account = await database.GetAccountByEmailAsync(email.Trim());
        if (account == null || !VerifyPassword(password, account.PasswordHash))
            throw new InvalidOperationException("E-mail ou senha incorretos.");
        await database.SetActiveAccountAsync(account.Id);
        account.IsActive = true;
        return account;
    }

    public async Task<LocalAccount> UpdateProfileAsync(string name, string avatarSourcePath)
    {
        var account = await database.GetActiveAccountAsync()
                      ?? throw new InvalidOperationException("Entre em uma conta primeiro.");
        name = name.Trim();
        if (name.Length < 2) throw new InvalidOperationException("Informe um nome válido.");
        var avatarPath = account.AvatarPath;
        if (!string.IsNullOrWhiteSpace(avatarSourcePath))
        {
            var directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "VORTEX", "avatars");
            Directory.CreateDirectory(directory);
            avatarPath = Path.Combine(directory, $"{account.Id}{Path.GetExtension(avatarSourcePath)}");
            File.Copy(avatarSourcePath, avatarPath, true);
        }
        await database.UpdateAccountProfileAsync(account.Id, name, avatarPath);
        account.Name = name;
        account.AvatarPath = avatarPath;
        return account;
    }

    public Task LogoutAsync() => database.SetActiveAccountAsync(null);

    private static void Validate(string name, string email, string password)
    {
        if (name.Length < 2) throw new InvalidOperationException("O nome precisa ter ao menos 2 caracteres.");
        if (!email.Contains('@') || !email.Contains('.'))
            throw new InvalidOperationException("Informe um e-mail válido.");
        if (password.Length < 8)
            throw new InvalidOperationException("A senha precisa ter ao menos 8 caracteres.");
    }

    private static string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, 210_000, HashAlgorithmName.SHA256, 32);
        return $"pbkdf2-sha256:210000:{Convert.ToBase64String(salt)}:{Convert.ToBase64String(hash)}";
    }

    private static bool VerifyPassword(string password, string stored)
    {
        var parts = stored.Split(':');
        if (parts.Length != 4 || !int.TryParse(parts[1], out var iterations)) return false;
        var salt = Convert.FromBase64String(parts[2]);
        var expected = Convert.FromBase64String(parts[3]);
        var actual = Rfc2898DeriveBytes.Pbkdf2(
            password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
