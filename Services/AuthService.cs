using DocGen.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DocGen.Services;

public class AuthService(IDbContextFactory<AppDbContext> dbFactory)
{
    private readonly PasswordHasher<User> _hasher = new();

    public async Task<(User? User, string? Error)> RegisterAsync(string username, string password)
    {
        username = Normalize(username);
        await using var db = await dbFactory.CreateDbContextAsync();

        if (await db.Users.AnyAsync(u => u.Username == username))
            return (null, "This username is already taken.");

        var user = new User { Username = username };
        user.PasswordHash = _hasher.HashPassword(user, password);
        db.Users.Add(user);

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return (null, "This username is already taken.");
        }

        return (user, null);
    }

    public async Task<User?> ValidateAsync(string username, string password)
    {
        username = Normalize(username);
        await using var db = await dbFactory.CreateDbContextAsync();

        var user = await db.Users.FirstOrDefaultAsync(u => u.Username == username);
        if (user is null) return null;

        var result = _hasher.VerifyHashedPassword(user, user.PasswordHash, password);
        return result == PasswordVerificationResult.Failed ? null : user;
    }

    private static string Normalize(string username) => username.Trim().ToLowerInvariant();
}