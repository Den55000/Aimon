using System.Text.Json;
using Aimon.Application.Abstractions;
using Aimon.Domain;
using Aimon.Domain.Graphics;

namespace Aimon.Infrastructure.Graphics;

/// <summary>
/// <see cref="IGraphicsCompiler"/> that packages a folder of user-authored frames into an
/// <see cref="AnimationLibrary"/> (implementation_plan.md, Phase 6). If the folder already has a
/// <c>manifest.json</c> it is authoritative; otherwise the compiler discovers frames by convention —
/// one subfolder per <see cref="AgentState"/> (<c>idle</c>, <c>listening</c>, <c>working</c>,
/// <c>error</c>) — and writes a manifest so the library becomes self-describing. Either way, every
/// referenced frame file must exist, or compilation fails.
/// </summary>
internal sealed class FileGraphicsCompiler : IGraphicsCompiler
{
    private const string ManifestFileName = "manifest.json";

    private static readonly AgentState[] AllStates =
        [AgentState.Idle, AgentState.Listening, AgentState.Working, AgentState.Error];

    private static readonly HashSet<string> FrameExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".gif", ".webp", ".bmp", ".svg",
    };

    public async Task<AnimationLibrary> CompileAsync(string sourceDirectory, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceDirectory);
        if (!Directory.Exists(sourceDirectory))
            throw new DirectoryNotFoundException($"Graphics source directory not found: '{sourceDirectory}'.");

        var manifestPath = Path.Combine(sourceDirectory, ManifestFileName);
        var hasManifest = File.Exists(manifestPath);

        var manifest = hasManifest
            ? await ReadManifestAsync(manifestPath, cancellationToken).ConfigureAwait(false)
            : ScanByConvention(sourceDirectory);

        if (manifest.FrameSets.Count == 0)
            throw new InvalidDataException("The animation library defines no frame sets.");

        ValidateFramesExist(sourceDirectory, manifest);

        // Persist a generated manifest so a convention-authored folder becomes a packaged library.
        if (!hasManifest)
            await WriteManifestAsync(manifestPath, manifest, cancellationToken).ConfigureAwait(false);

        return ToDomain(manifest);
    }

    private static GraphicsManifest ScanByConvention(string sourceDirectory)
    {
        var frameSets = new List<FrameSetDocument>();

        foreach (var state in AllStates)
        {
            var folderName = state.ToString().ToLowerInvariant();
            var stateDirectory = Path.Combine(sourceDirectory, folderName);
            if (!Directory.Exists(stateDirectory))
                continue;

            var frames = Directory.EnumerateFiles(stateDirectory)
                .Where(path => FrameExtensions.Contains(Path.GetExtension(path)))
                .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
                .Select(path => $"{folderName}/{Path.GetFileName(path)}")
                .ToList();

            if (frames.Count > 0)
                frameSets.Add(new FrameSetDocument { State = state, Frames = frames });
        }

        if (frameSets.Count == 0)
        {
            throw new InvalidDataException(
                $"No frames found under '{sourceDirectory}'. Add a {ManifestFileName}, or per-state frame " +
                "folders named idle, listening, working, or error.");
        }

        return new GraphicsManifest
        {
            Name = new DirectoryInfo(sourceDirectory).Name,
            FrameSets = frameSets,
        };
    }

    private static void ValidateFramesExist(string sourceDirectory, GraphicsManifest manifest)
    {
        var missing = new List<string>();

        foreach (var frameSet in manifest.FrameSets)
        {
            if (frameSet.Frames.Count == 0)
                missing.Add($"(no frames for {frameSet.State})");

            foreach (var frame in frameSet.Frames)
            {
                var relative = frame.Replace('/', Path.DirectorySeparatorChar);
                if (!File.Exists(Path.Combine(sourceDirectory, relative)))
                    missing.Add(frame);
            }
        }

        if (missing.Count > 0)
            throw new InvalidDataException($"The animation library references frames that do not exist: {string.Join(", ", missing)}.");
    }

    private static async Task<GraphicsManifest> ReadManifestAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        try
        {
            var manifest = await JsonSerializer
                .DeserializeAsync(stream, GraphicsJsonContext.Default.GraphicsManifest, cancellationToken)
                .ConfigureAwait(false);

            return manifest ?? throw new InvalidDataException($"Graphics manifest was empty: '{path}'.");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"Graphics manifest was malformed: '{path}'.", ex);
        }
    }

    private static async Task WriteManifestAsync(string path, GraphicsManifest manifest, CancellationToken cancellationToken)
    {
        await using var stream = File.Create(path);
        await JsonSerializer
            .SerializeAsync(stream, manifest, GraphicsJsonContext.Default.GraphicsManifest, cancellationToken)
            .ConfigureAwait(false);
    }

    private static AnimationLibrary ToDomain(GraphicsManifest manifest) => new()
    {
        Name = string.IsNullOrWhiteSpace(manifest.Name) ? "animation-library" : manifest.Name,
        Version = string.IsNullOrWhiteSpace(manifest.Version) ? "1.0.0" : manifest.Version,
        FrameSets = [.. manifest.FrameSets.Select(frameSet => new FrameSet
        {
            State = frameSet.State,
            Frames = [.. frameSet.Frames],
            FramesPerSecond = frameSet.FramesPerSecond,
            Loop = frameSet.Loop,
        })],
    };
}
