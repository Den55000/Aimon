using Aimon.Domain.Pipeline;

namespace Aimon.Infrastructure.Agents;

/// <summary>
/// Maps a CLI's per-line output (one JSON event for a stream-json CLI) to zero or more
/// <see cref="ProviderStreamItem"/>s. Implementations are stateful across a turn (e.g. tracking the
/// session id) and must never throw on malformed input — an unparseable line yields nothing. Lifecycle
/// <see cref="AgentState"/> transitions are owned by <see cref="CliAgentProcessHost"/>, not the mapper.
/// </summary>
internal interface ICliStreamMapper
{
    IEnumerable<ProviderStreamItem> Map(string line);
}
