using Aimon.Application.Abstractions;
using Aimon.Domain.Agents;

namespace Aimon.Application.Agents;

/// <summary>
/// Default <see cref="IChatBackendRegistry"/>: exposes the built-in model provider plus every
/// registered <see cref="IAgentBackend"/>. The model provider is the default backend, so turns that
/// don't name one keep the existing behavior.
/// </summary>
internal sealed class ChatBackendRegistry : IChatBackendRegistry
{
    private readonly Dictionary<BackendId, IAgentBackend> _agents;
    private readonly List<BackendDescriptor> _descriptors;

    public BackendId DefaultBackendId { get; }

    public ChatBackendRegistry(IAiProvider modelProvider, IEnumerable<IAgentBackend> agents)
    {
        ArgumentNullException.ThrowIfNull(modelProvider);
        ArgumentNullException.ThrowIfNull(agents);

        DefaultBackendId = BackendId.From(modelProvider.Name);

        var modelDescriptor = new BackendDescriptor
        {
            Id = DefaultBackendId,
            DisplayName = DisplayNameFor(modelProvider.Name),
            Kind = BackendKind.ModelProvider,
            SupportsWorkspace = false,
            SupportsSessions = false,
            SupportsImages = true,
            AuthKind = BackendAuthKind.ApiKey,
        };

        var agentList = agents.ToList();
        _agents = agentList.ToDictionary(agent => agent.Descriptor.Id);
        _descriptors = [modelDescriptor, .. agentList.Select(agent => agent.Descriptor)];
    }

    public IReadOnlyList<BackendDescriptor> Descriptors => _descriptors;

    public BackendDescriptor? Find(BackendId id) => _descriptors.FirstOrDefault(descriptor => descriptor.Id == id);

    public bool TryGetAgent(BackendId id, out IAgentBackend backend) => _agents.TryGetValue(id, out backend!);

    private static string DisplayNameFor(string providerName) => providerName switch
    {
        "openai" => "OpenAI",
        _ => providerName,
    };
}
