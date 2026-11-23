using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using DocGen.Data;
using Microsoft.EntityFrameworkCore;

namespace DocGen.Services;

public record ShareInfo(Guid Id, DateTime CreatedAt, DateTime? ExpiresAt, bool IsRevoked)
{
    public string Status =>
        IsRevoked ? "Revoked"
        : ExpiresAt is not null && ExpiresAt <= DateTime.UtcNow ? "Expired"
        : "Active";
}

public record SharedDocumentInfo(string Name);

public class ShareService(IDbContextFactory<AppDbContext> dbFactory, IFileStore store, ILogger<ShareService> logger)
{
    // ---------- For the owner (logged in) ----------

    /// <summary>Creates a link and returns the secret, or null if the document isn't yours. The secret is returned only here.</summary>
    public async Task<string?> CreateAsync(int userId, Guid documentId, int expiresInDays)
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        var owns = await db.Documents.AnyAsync(d => d.Id == documentId && d.UserId == userId);   // ownership
        if (!owns) return null;

        var token = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));

        db.Shares.Add(new DocumentShare
        {
            DocumentId = documentId,
            TokenHash = Hash(token),
            ExpiresAt = expiresInDays > 0 ? DateTime.UtcNow.AddDays(Math.Min(expiresInDays, 365)) : null
        });
        await db.SaveChangesAsync();

        return token;
    }

    public async Task<List<ShareInfo>> ListAsync(int userId, Guid documentId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        return await db.Shares.AsNoTracking()
            .Where(s => s.DocumentId == documentId && s.Document.UserId == userId)   // ownership
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new ShareInfo(s.Id, s.CreatedAt, s.ExpiresAt, s.IsRevoked))
            .ToListAsync();
    }

    public async Task<bool> RevokeAsync(int userId, Guid shareId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        var share = await db.Shares.FirstOrDefaultAsync(s => s.Id == shareId && s.Document.UserId == userId);   // ownership
        if (share is null) return false;

        share.IsRevoked = true;
        await db.SaveChangesAsync();
        return true;
    }

    // ---------- For anyone with the link (not logged in) ----------

    public async Task<SharedDocumentInfo?> ResolveAsync(string token)
    {
        var doc = await FindActiveDocumentAsync(token);
        return doc is null ? null : new SharedDocumentInfo(doc.Name);
    }

    public async Task<DocumentFile?> GetPdfAsync(string token)
    {
        var doc = await FindActiveDocumentAsync(token);
        if (doc is null) return null;

        var bytes = await store.ReadAsync(doc.PdfKey);
        if (bytes is null)
        {
            logger.LogError("File missing in storage for shared document {Id}", doc.Id);
            return null;
        }

        return new DocumentFile(bytes, FileNames.Sanitize(doc.Name) + ".pdf", "application/pdf");
    }

    /// <summary>The one place that decides whether a link works. Same answer (null) for wrong, revoked and expired links.</summary>
    private async Task<GeneratedDocument?> FindActiveDocumentAsync(string token)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 100) return null;

        var hash = Hash(token);
        await using var db = await dbFactory.CreateDbContextAsync();

        var share = await db.Shares.AsNoTracking()
            .Include(s => s.Document)
            .FirstOrDefaultAsync(s => s.TokenHash == hash);

        if (share is null || share.IsRevoked) return null;
        if (share.ExpiresAt is { } expires && expires <= DateTime.UtcNow) return null;

        return share.Document;
    }

    private static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}