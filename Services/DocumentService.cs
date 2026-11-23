using System.Text.Json;
using DocGen.Data;
using Microsoft.EntityFrameworkCore;

namespace DocGen.Services;

public record DocumentSummary(Guid Id, string Name, string TemplateName, DateTime CreatedAt);
public record DocumentFile(byte[] Content, string FileName, string ContentType);

public class DocumentService(IDbContextFactory<AppDbContext> dbFactory, IFileStore store, ILogger<DocumentService> logger)
{
    private const string DocxType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
    private const string PdfType = "application/pdf";

    public async Task<Guid> SaveAsync(int userId, string name, string templateName,
        byte[] docx, byte[] pdf, IReadOnlyDictionary<string, string> values, CancellationToken ct = default)
    {
        var id = Guid.NewGuid();
        var docxKey = $"{userId}/{id:N}.docx";
        var pdfKey = $"{userId}/{id:N}.pdf";

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
                Name = FileNames.Sanitize(name),
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

        return id;
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
}