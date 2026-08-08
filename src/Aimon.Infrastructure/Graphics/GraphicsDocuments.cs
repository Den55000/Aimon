using System.Text.Json.Serialization;
using Aimon.Domain;

namespace Aimon.Infrastructure.Graphics;

// On-disk manifest for a packaged animation library (implementation_plan.md, §3.5): graphics/manifest.json
// keyed by AgentState. Decoupled from the Domain AnimationLibrary so the file schema can evolve.

internal sealed class GraphicsManifest
{
    public int SchemaVersion { get; set; } = 1;

    public string Name { get; set; } = "";

    public string Version { get; set; } = "1.0.0";

    public List<FrameSetDocument> FrameSets { get; set; } = [];
}

internal sealed class FrameSetDocument
{
    public AgentState State { get; set; }

    /// <summary>Frame references relative to the library root, in playback order.</summary>
    public List<string> Frames { get; set; } = [];

    public int FramesPerSecond { get; set; } = 12;

    public bool Loop { get; set; } = true;
}

/// <summary>
/// Source-generated context for the graphics manifest: camelCase names, string enums (so states read
/// as "Idle"/"Working"), indented output, nulls omitted.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true,
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(GraphicsManifest))]
internal sealed partial class GraphicsJsonContext : JsonSerializerContext;
