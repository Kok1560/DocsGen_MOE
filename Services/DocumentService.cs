using System.Text.Json;
using DocGen.Data;
using Microsoft.EntityFrameworkCore;

namespace DocGen.Services;

public record DocumentSummary(Guid Id, string Name, string TemplateName, DateTime CreatedAt);
public record DocumentFile(byte[] Content, string FileName, string ContentType);
public record DuplicateMatch(int Count, string LatestDocumentName, DateTime LatestCreatedAt);

public class DocumentService(IDbContextFactory<AppDbContext> dbFactory, IFileStore store, ILogger<DocumentService> logger)
{
    private const string DocxType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
    private const string PdfType = "application/pdf";

    /// <summary>The template field that holds the person's name. Change this if your templates use another name.</summary>
    public const string PersonNameField = "الاسم";

    /// <summary>Creates a document named "{baseName} {next number}" and returns its id and final name.</summary>
    public async Task<(Guid Id, string Name)> CreateAsync(int userId, string baseName, string templateName,
        byte[] docx, byte[] pdf, IReadOnlyDictionary<string, string> values, CancellationToken ct = default)
    {
        var id = Guid.NewGuid();
        var docxKey = $"{userId}/{id:N}.docx";
        var pdfKey = $"{userId}/{id:N}.pdf";
        var name = await NextNameAsync(userId, FileNames.Sanitize(baseName), ct);

        // Files first, then the database row. If the database fails, remove the files again.
        await store.SaveAsync(docxKey, docx, ct);
        await store.SaveAsync(pdfKey, pdf, ct);

        try
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);
            db.Documents.Add(new GeneratedDocument
            {
                Id = id,
                UserId = userId,
                Name = name,
                TemplateName = templateName,
                DocxKey = docxKey,
                PdfKey = pdfKey,
                ValuesJson = JsonSerializer.Serialize(values)
            });
            await db.SaveChangesAsync(ct);
        }
        catch
        {
            await store.DeleteAsync(docxKey);
            await store.DeleteAsync(pdfKey);
            throw;
        }

        return (id, name);
    }

    /// <summary>Replaces the files and values of an existing document. Returns false if it doesn't exist or isn't yours.</summary>
    public async Task<bool> UpdateContentAsync(int userId, Guid id, string name,
        byte[] docx, byte[] pdf, IReadOnlyDictionary<string, string> values, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var doc = await db.Documents.FirstOrDefaultAsync(d => d.Id == id && d.UserId == userId, ct);   // ownership
        if (doc is null) return false;

        await store.SaveAsync(doc.DocxKey, docx, ct);   // same keys: the old files are overwritten
        await store.SaveAsync(doc.PdfKey, pdf, ct);

        doc.Name = FileNames.Sanitize(name);
        doc.ValuesJson = JsonSerializer.Serialize(values);
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> RenameAsync(int userId, Guid id, string name)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var doc = await db.Documents.FirstOrDefaultAsync(d => d.Id == id && d.UserId == userId);   // ownership
        if (doc is null) return false;

        doc.Name = FileNames.Sanitize(name);
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<int> CountAsync(int userId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Documents.CountAsync(d => d.UserId == userId);   // ownership
    }

    /// <summary>
    /// Looks for other saved documents of this user with the same person name (any template).
    /// excludeIds = documents to ignore, such as the one currently open in the editor.
    /// </summary>
    public async Task<DuplicateMatch?> FindPersonDuplicateAsync(
        int userId, string? personName, IReadOnlyCollection<Guid> excludeIds)
    {
        var wanted = NormalizeName(personName);
        if (wanted.Length == 0) return null;

        await using var db = await dbFactory.CreateDbContextAsync();
        var rows = await db.Documents.AsNoTracking()
            .Where(d => d.UserId == userId)                       // ownership
            .OrderByDescending(d => d.CreatedAt)
            .Select(d => new { d.Id, d.Name, d.CreatedAt, d.ValuesJson })
            .ToListAsync();

        var count = 0;
        string? latestName = null;
        DateTime latestDate = default;

        foreach (var row in rows)
        {
            if (excludeIds.Contains(row.Id)) continue;
            if (NormalizeName(ReadPersonName(row.ValuesJson)) != wanted) continue;

            if (count == 0) { latestName = row.Name; latestDate = row.CreatedAt; }   // rows are newest first
            count++;
        }

        return count == 0 ? null : new DuplicateMatch(count, latestName!, latestDate);
    }

    public async Task<List<DocumentSummary>> ListAsync(int userId, string? search = null)
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        var query = db.Documents.AsNoTracking().Where(d => d.UserId == userId);   // ownership
        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            query = query.Where(d => EF.Functions.Like(d.Name, pattern));
        }

        return await query
            .OrderByDescending(d => d.CreatedAt)
            .Select(d => new DocumentSummary(d.Id, d.Name, d.TemplateName, d.CreatedAt))
            .ToListAsync();
    }

    /// <summary>Returns the file, or null if it doesn't exist OR belongs to someone else.</summary>
    public async Task<DocumentFile?> GetFileAsync(int userId, Guid id, bool pdf)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var doc = await db.Documents.AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == id && d.UserId == userId);      // ownership
        if (doc is null) return null;

        var bytes = await store.ReadAsync(pdf ? doc.PdfKey : doc.DocxKey);
        if (bytes is null)
        {
            logger.LogError("File missing in storage for document {Id}", id);
            return null;
        }

        var ext = pdf ? ".pdf" : ".docx";
        return new DocumentFile(bytes, FileNames.Sanitize(doc.Name) + ext, pdf ? PdfType : DocxType);
    }

    public async Task<bool> DeleteAsync(int userId, Guid id)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var doc = await db.Documents.FirstOrDefaultAsync(d => d.Id == id && d.UserId == userId);   // ownership
        if (doc is null) return false;

        db.Documents.Remove(doc);
        await db.SaveChangesAsync();

        await store.DeleteAsync(doc.DocxKey);
        await store.DeleteAsync(doc.PdfKey);
        return true;
    }

    // ---------- helpers ----------

    /// <summary>"Contract" becomes "Contract 1", then "Contract 2", and so on (per user).</summary>
    private async Task<string> NextNameAsync(int userId, string baseName, CancellationToken ct)
    {
        var prefix = baseName + " ";

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var names = await db.Documents.AsNoTracking()
            .Where(d => d.UserId == userId && d.Name.StartsWith(prefix))
            .Select(d => d.Name)
            .ToListAsync(ct);

        var max = 0;
        foreach (var existing in names)
            if (int.TryParse(existing.AsSpan(prefix.Length), out var number) && number > max)
                max = number;

        return $"{baseName} {max + 1}";
    }

    private static string? ReadPersonName(string json)
    {
        try
        {
            var values = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            if (values is null) return null;

            foreach (var (key, value) in values)
                if (key.Equals(PersonNameField, StringComparison.OrdinalIgnoreCase)) return value;
        }
        catch (JsonException) { }

        return null;
    }

    // "  Ahmed   ALI " and "ahmed ali" count as the same name
    private static string NormalizeName(string? name) =>
        string.Join(' ', (name ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant();
}