using System.Diagnostics;
using System.IO;

namespace kparser2.Services;

public sealed class PacketViewerImportService
{
    public string ConvertToNdjson(
        string outputPath,
        string? fullLog = null,
        string? incomingLog = null,
        string? outgoingLog = null,
        string? sessionId = null)
    {
        var scriptPath = ResolveScriptPath();

        if (!File.Exists(scriptPath))
        {
            throw new FileNotFoundException("PacketViewer converter script not found.", scriptPath);
        }

        var args = new List<string>
        {
            "-NoProfile",
            "-ExecutionPolicy", "Bypass",
            "-File", Quote(scriptPath),
            "-OutputNdjson", Quote(outputPath)
        };

        if (!string.IsNullOrWhiteSpace(fullLog))
        {
            args.Add("-FullLog");
            args.Add(Quote(fullLog));
        }

        if (!string.IsNullOrWhiteSpace(incomingLog))
        {
            args.Add("-IncomingLog");
            args.Add(Quote(incomingLog));
        }

        if (!string.IsNullOrWhiteSpace(outgoingLog))
        {
            args.Add("-OutgoingLog");
            args.Add(Quote(outgoingLog));
        }

        if (!string.IsNullOrWhiteSpace(sessionId))
        {
            args.Add("-SessionId");
            args.Add(Quote(sessionId));
        }

        var psi = new ProcessStartInfo
        {
            FileName = "powershell",
            Arguments = string.Join(' ', args),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var proc = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start PacketViewer converter.");

        var stdout = proc.StandardOutput.ReadToEnd();
        var stderr = proc.StandardError.ReadToEnd();
        proc.WaitForExit();

        if (proc.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"PacketViewer conversion failed (exit {proc.ExitCode}): {stderr}{stdout}");
        }

        if (!File.Exists(outputPath))
        {
            throw new FileNotFoundException("Converter did not produce output.", outputPath);
        }

        return outputPath;
    }

    private static string ResolveScriptPath()
    {
        var candidates = new[]
        {
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "scripts", "convert-packetviewer-to-ndjson.ps1")),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "scripts", "convert-packetviewer-to-ndjson.ps1"))
        };

        return candidates.FirstOrDefault(File.Exists)
            ?? candidates[0];
    }

    private static string Quote(string value) => $"\"{value.Replace("\"", "\\\"")}\"";
}
