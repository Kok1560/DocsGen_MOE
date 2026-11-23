namespace DocGen.Services;

public static class FileNames
{
    /// <summary>Makes a user-typed name safe to use as a download file name.</summary>
    public static string Sanitize(string? name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var clean = string.Concat((name ?? "").Where(c => !invalid.Contains(c))).Trim().TrimEnd('.');
        if (clean.Length > 100) clean = clean[..100].TrimEnd();
        return clean.Length == 0 ? "document" : clean;
    }
}