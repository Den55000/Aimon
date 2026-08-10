namespace Aimon.Infrastructure.Agents;

/// <summary>
/// Locates a CLI executable on <c>PATH</c> and, on Windows, wraps npm-style <c>.cmd</c>/<c>.bat</c>
/// shims through <c>cmd.exe</c> so they launch under <c>UseShellExecute=false</c>. Returns the launch
/// target (process file name + any prefix arguments) or null when the command can't be found.
/// </summary>
internal static class CliExecutableResolver
{
    /// <summary>The resolved launch target: the process file name plus prefix args (for shim wrapping).</summary>
    internal sealed record LaunchTarget(string FileName, IReadOnlyList<string> PrefixArguments);

    public static LaunchTarget? Resolve(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
            return null;

        var path = FindOnPath(command);
        if (path is null)
            return null;

        if (OperatingSystem.IsWindows())
        {
            var extension = Path.GetExtension(path);
            if (extension.Equals(".cmd", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".bat", StringComparison.OrdinalIgnoreCase))
            {
                return new LaunchTarget("cmd.exe", ["/c", path]);
            }
        }

        return new LaunchTarget(path, []);
    }

    private static string? FindOnPath(string command)
    {
        if (command.Contains(Path.DirectorySeparatorChar) || command.Contains(Path.AltDirectorySeparatorChar))
            return ProbeCandidates(command);

        var pathVariable = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(pathVariable))
            return null;

        foreach (var directory in pathVariable.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var candidate = ProbeCandidates(Path.Combine(directory, command));
            if (candidate is not null)
                return candidate;
        }

        return null;
    }

    private static string? ProbeCandidates(string basePath)
    {
        if (File.Exists(basePath))
            return basePath;

        if (OperatingSystem.IsWindows())
        {
            foreach (var extension in WindowsExecutableExtensions())
            {
                var candidate = basePath + extension;
                if (File.Exists(candidate))
                    return candidate;
            }
        }

        return null;
    }

    private static IEnumerable<string> WindowsExecutableExtensions()
    {
        var pathext = Environment.GetEnvironmentVariable("PATHEXT");
        return string.IsNullOrEmpty(pathext)
            ? [".COM", ".EXE", ".BAT", ".CMD"]
            : pathext.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}
