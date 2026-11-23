namespace DocGen.Services;

public record TemplateInfo(string FileName, string Name, IReadOnlyList<string> Fields, string? Error);

public class TemplateService(IWebHostEnvironment env, DocxEngine engine, ILogger<TemplateService> logger)
{
    private string Folder => Path.Combine(env.ContentRootPath, "Templates");

    public IReadOnlyList<TemplateInfo> GetAll()
    {
        if (!Directory.Exists(Folder)) return [];

        return Directory.GetFiles(Folder, "*.docx")
            .Where(f => !Path.GetFileName(f).StartsWith("~$"))   // skip Word's temporary lock files
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .Select(Describe)
            .ToList();
    }

    /// <summary>Turns a file name from the browser into a safe full path, or null if invalid.</summary>
    public string? GetPath(string fileName)
    {
        var safeName = Path.GetFileName(fileName);   // strips any ..\ tricks
        if (!safeName.EndsWith(".docx", StringComparison.OrdinalIgnoreCase)) return null;

        var path = Path.Combine(Folder, safeName);
        return File.Exists(path) ? path : null;
    }

    private TemplateInfo Describe(string path)
    {
        var fileName = Path.GetFileName(path);
        var name = Path.GetFileNameWithoutExtension(path);
        try
        {
            return new TemplateInfo(fileName, name, engine.DetectFields(path), null);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not read template {File}", fileName);
            return new TemplateInfo(fileName, name, [], "This file could not be read as a Word document.");
        }
    }
}