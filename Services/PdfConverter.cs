using System.Diagnostics;
using System.Threading.Channels;
using Microsoft.Extensions.Options;

namespace DocGen.Services;

public class PdfConverterOptions
{
    public string SofficePath { get; set; } = "";
    public int TimeoutSeconds { get; set; } = 60;
    public int MaxConcurrent { get; set; } = 2;
}

/// <summary>A conversion problem whose message is safe to show to the user.</summary>
public class PdfConversionException(string message) : Exception(message);

public class PdfConverter
{
    private readonly PdfConverterOptions _options;
    private readonly ILogger<PdfConverter> _logger;
    private readonly string _workRoot;
    private readonly Channel<string> _profiles;   // free LibreOffice profile folders

    public PdfConverter(IOptions<PdfConverterOptions> options, IWebHostEnvironment env, ILogger<PdfConverter> logger)
    {
        _options = options.Value;
        _logger = logger;

        // Remove leftovers from a previous run that crashed
        _workRoot = Path.Combine(Path.GetTempPath(), "docgen-work");
        try { if (Directory.Exists(_workRoot)) Directory.Delete(_workRoot, true); } catch { }
        Directory.CreateDirectory(_workRoot);

        var profileRoot = Path.Combine(env.ContentRootPath, "Storage", "lo-profiles");
        Directory.CreateDirectory(profileRoot);

        var slots = Math.Max(1, _options.MaxConcurrent);
        _profiles = Channel.CreateBounded<string>(slots);
        for (var i = 1; i <= slots; i++)
            _profiles.Writer.TryWrite(Path.Combine(profileRoot, $"profile-{i}"));
    }

    public async Task<byte[]> ConvertAsync(byte[] docx, CancellationToken ct = default)
    {
        if (!File.Exists(_options.SofficePath))
        {
            _logger.LogError("LibreOffice not found at {Path}", _options.SofficePath);
            throw new PdfConversionException("LibreOffice was not found. Check the path in appsettings.json.");
        }

        // Wait here until one of the profile folders is free
        var profile = await _profiles.Reader.ReadAsync(ct);
        var workDir = Path.Combine(_workRoot, Guid.NewGuid().ToString("N"));
        var broken = false;

        try
        {
            Directory.CreateDirectory(workDir);
            var input = Path.Combine(workDir, "document.docx");
            await File.WriteAllBytesAsync(input, docx, ct);

            ct.ThrowIfCancellationRequested();
            await RunLibreOfficeAsync(profile, workDir, input);

            var output = Path.Combine(workDir, "document.pdf");
            if (!File.Exists(output))
                throw new PdfConversionException("LibreOffice finished but did not produce a PDF.");

            return await File.ReadAllBytesAsync(output, ct);
        }
        catch (PdfConversionException)
        {
            broken = true;   // a crashed or killed run can leave the profile locked
            throw;
        }
        finally
        {
            if (broken) TryDelete(profile);   // it is recreated automatically on the next run
            TryDelete(workDir);
            _profiles.Writer.TryWrite(profile);
        }
    }

    private async Task RunLibreOfficeAsync(string profileDir, string workDir, string inputPath)
    {
        var psi = new ProcessStartInfo
        {
            FileName = _options.SofficePath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        psi.ArgumentList.Add($"-env:UserInstallation={new Uri(profileDir).AbsoluteUri}");
        psi.ArgumentList.Add("--headless");
        psi.ArgumentList.Add("--norestore");
        psi.ArgumentList.Add("--convert-to");
        psi.ArgumentList.Add("pdf");
        psi.ArgumentList.Add("--outdir");
        psi.ArgumentList.Add(workDir);
        psi.ArgumentList.Add(inputPath);

        using var process = Process.Start(psi)
            ?? throw new PdfConversionException("Could not start LibreOffice.");

        // Read the output so LibreOffice can never block on a full pipe
        _ = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();

        // Once started, a conversion is not cancelled when the user types again:
        // the result is simply discarded. Only the timeout can stop it.
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(_options.TimeoutSeconds));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch { }
            throw new PdfConversionException("The PDF conversion took too long.");
        }

        if (process.ExitCode != 0)
        {
            _logger.LogError("LibreOffice exited with code {Code}: {Error}", process.ExitCode, await stderr);
            throw new PdfConversionException("The PDF conversion failed.");
        }
    }

    private static void TryDelete(string directory)
    {
        try { if (Directory.Exists(directory)) Directory.Delete(directory, true); } catch { }
    }
}