using System.Diagnostics;

namespace Argus.Herald.Collectors;

public static class CommandRunner
{
    /// <summary>
    /// Runs a command and returns stdout, or null if it can't be started / times out.
    /// Used to shell out to journalctl (Linux) and log show (macOS).
    /// </summary>
    public static async Task<string?> RunAsync(string fileName, IEnumerable<string> args, CancellationToken ct, int timeoutSeconds = 30)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var a in args) psi.ArgumentList.Add(a);

        try
        {
            using var proc = Process.Start(psi);
            if (proc is null) return null;

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

            var stdout = await proc.StandardOutput.ReadToEndAsync(timeout.Token);
            await proc.WaitForExitAsync(timeout.Token);
            return stdout;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (Exception)
        {
            // Command not found or not permitted on this host.
            return null;
        }
    }
}
