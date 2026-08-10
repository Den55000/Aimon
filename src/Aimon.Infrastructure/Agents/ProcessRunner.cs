using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;

namespace Aimon.Infrastructure.Agents;

/// <summary>
/// Real <see cref="IProcessRunner"/> over <see cref="Process"/>. Reads standard output line by line as
/// it arrives, drains standard error in the background (surfaced only on failure), and — on
/// cancellation — kills the whole process tree so no orphaned agent keeps editing files.
/// </summary>
internal sealed class ProcessRunner : IProcessRunner
{
    public async IAsyncEnumerable<ProcessOutput> RunAsync(
        CliProcessSpec spec,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var process = new Process { StartInfo = BuildStartInfo(spec) };

        string? startError = null;
        try
        {
            process.Start();
        }
        catch (Win32Exception ex)
        {
            startError = $"Could not start '{spec.FileName}': {ex.Message}";
        }
        catch (InvalidOperationException ex)
        {
            startError = $"Could not start '{spec.FileName}': {ex.Message}";
        }

        if (startError is not null)
        {
            process.Dispose();
            yield return new ProcessOutput.Failed(startError);
            yield break;
        }

        // Kill the whole tree if the caller cancels (the agent may have spawned child processes).
        await using var registration = cancellationToken.Register(() => TryKill(process)).ConfigureAwait(false);

        try
        {
            var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

            if (spec.StandardInput is not null)
            {
                await process.StandardInput.WriteAsync(spec.StandardInput.AsMemory(), cancellationToken).ConfigureAwait(false);
                process.StandardInput.Close();
            }

            while (true)
            {
                var line = await process.StandardOutput.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                if (line is null)
                    break;

                yield return new ProcessOutput.StdOut(line);
            }

            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

            var stderr = await stderrTask.ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(stderr))
                yield return new ProcessOutput.StdErr(stderr.Trim());

            yield return new ProcessOutput.Exited(process.ExitCode);
        }
        finally
        {
            process.Dispose();
        }
    }

    private static ProcessStartInfo BuildStartInfo(CliProcessSpec spec)
    {
        var info = new ProcessStartInfo
        {
            FileName = spec.FileName,
            WorkingDirectory = spec.WorkingDirectory ?? string.Empty,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = spec.StandardInput is not null,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        foreach (var argument in spec.Arguments)
            info.ArgumentList.Add(argument);

        foreach (var (key, value) in spec.Environment)
            info.Environment[key] = value;

        return info;
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            // The process already exited between the check and the kill — nothing to do.
        }
        catch (Win32Exception)
        {
            // The OS refused the kill (already terminating); best-effort only.
        }
    }
}
