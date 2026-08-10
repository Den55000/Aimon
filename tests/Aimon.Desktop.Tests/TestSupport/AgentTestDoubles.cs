using Aimon.Application.Abstractions;
using Aimon.Application.Agents;
using Aimon.Application.Pipeline;
using Aimon.Desktop.Services;
using Aimon.Domain.Agents;
using Aimon.Domain.Pipeline;

namespace Aimon.Desktop.Tests.TestSupport;

/// <summary>An <see cref="IChatService"/> that yields nothing — the backend-wiring tests never send.</summary>
internal sealed class StubChatService : IChatService
{
    public IAsyncEnumerable<ProviderStreamItem> SendAsync(SendMessageRequest request, CancellationToken cancellationToken)
        => AsyncEnumerable.Empty<ProviderStreamItem>();
}

internal sealed class StubImagePicker : IImagePicker
{
    public Task<IReadOnlyList<PickedImage>> PickImagesAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<PickedImage>>([]);

    public Task<bool> SaveImageAsync(byte[] data, string suggestedFileName, CancellationToken cancellationToken = default)
        => Task.FromResult(false);
}

/// <summary>An <see cref="IWorkspacePicker"/> that always returns a preset folder path.</summary>
internal sealed class StubWorkspacePicker : IWorkspacePicker
{
    private readonly string? _path;

    public StubWorkspacePicker(string? path) => _path = path;

    public Task<string?> PickWorkspaceAsync(CancellationToken cancellationToken = default) => Task.FromResult(_path);
}

/// <summary>An <see cref="IAgentBackend"/> that advertises a CLI-agent descriptor and streams nothing.</summary>
internal sealed class FakeAgentBackend : IAgentBackend
{
    public FakeAgentBackend(string id = "claude-code", bool available = true)
    {
        Descriptor = new BackendDescriptor
        {
            Id = BackendId.From(id),
            DisplayName = "Claude Code",
            Kind = BackendKind.CliAgent,
            SupportsWorkspace = true,
            SupportsSessions = true,
            AuthKind = BackendAuthKind.CliNativeLogin,
        };
        _available = available;
    }

    private readonly bool _available;

    public BackendDescriptor Descriptor { get; }

    public ValueTask<BackendAvailability> CheckAvailabilityAsync(CancellationToken cancellationToken)
        => ValueTask.FromResult(_available ? BackendAvailability.Available("1.0.0") : BackendAvailability.Unavailable("not found"));

    public IAsyncEnumerable<ProviderStreamItem> StreamAsync(AgentTurnRequest request, CancellationToken cancellationToken)
        => AsyncEnumerable.Empty<ProviderStreamItem>();
}

/// <summary>A self-contained <see cref="IChatBackendRegistry"/> for the Desktop tests.</summary>
internal sealed class FakeChatBackendRegistry : IChatBackendRegistry
{
    private readonly Dictionary<BackendId, IAgentBackend> _agents;

    public FakeChatBackendRegistry(BackendDescriptor modelProvider, params IAgentBackend[] agents)
    {
        DefaultBackendId = modelProvider.Id;
        _agents = agents.ToDictionary(agent => agent.Descriptor.Id);
        Descriptors = [modelProvider, .. agents.Select(agent => agent.Descriptor)];
    }

    public IReadOnlyList<BackendDescriptor> Descriptors { get; }

    public BackendId DefaultBackendId { get; }

    public BackendDescriptor? Find(BackendId id) => Descriptors.FirstOrDefault(descriptor => descriptor.Id == id);

    public bool TryGetAgent(BackendId id, out IAgentBackend backend) => _agents.TryGetValue(id, out backend!);

    public static BackendDescriptor ModelProvider() => new()
    {
        Id = BackendId.From("openai"),
        DisplayName = "OpenAI",
        Kind = BackendKind.ModelProvider,
        SupportsImages = true,
        AuthKind = BackendAuthKind.ApiKey,
    };
}
