namespace DocGen.Services;

/// <summary>Where generated files are kept. Disk now; a cloud version can replace it later.</summary>
public interface IFileStore
{
    Task SaveAsync(string key, byte[] content, CancellationToken ct = default);
    Task<byte[]?> ReadAsync(string key, CancellationToken ct = default);
    Task DeleteAsync(string key);
}

public class LocalFileStore : IFileStore
{
    private readonly string _root;

    public LocalFileStore(IWebHostEnvironment env)
    {
        _root = Path.GetFullPath(Path.Combine(env.ContentRootPath, "Storage", "files"));
        Directory.CreateDirectory(_root);
    }

    public async Task SaveAsync(string key, byte[] content, CancellationToken ct = default)
    {
        var path = Resolve(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllBytesAsync(path, content, ct);
    }

    public async Task<byte[]?> ReadAsync(string key, CancellationToken ct = default)
    {
        var path = Resolve(key);
        return File.Exists(path) ? await File.ReadAllBytesAsync(path, ct) : null;
    }

    public Task DeleteAsync(string key)
    {
        var path = Resolve(key);
        try { if (File.Exists(path)) File.Delete(path); } catch { /* an orphan file is harmless */ }
        return Task.CompletedTask;
    }

    // Turns a key into a full path and refuses anything that escapes the storage folder
    private string Resolve(string key)
    {
        var full = Path.GetFullPath(Path.Combine(_root, key));
        if (!full.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Invalid storage key.");
        return full;
    }
}