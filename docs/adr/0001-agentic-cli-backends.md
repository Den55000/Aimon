# ADR 0001 — Agentic CLI backends

**Status:** Accepted (Phase 9) · **Date:** 2026-08-10

## Context

Aimon personalizes the AI experience over any model or task. Beyond commercial model
providers (OpenAI), users want Aimon to drive **external agentic coding CLIs** — Claude Code
first, then Codex, Cursor, and GitHub Copilot — as a backend, so the same personalized chat +
avatar surface can operate on a real project folder.

These CLIs differ from an HTTP model provider: they are **subprocesses** that run in a working
directory, use tools, edit files, carry their own multi-turn **session**, and authenticate
themselves. The pipeline already has one unifying seam — every response is an
`IAsyncEnumerable<ProviderStreamItem>` consumed identically by the chat UI and the Graphics
Engine — and the goal was to reuse it rather than fork the UI.

Product decisions taken for this phase:

- **A1 — Backend + workspace.** A CLI agent is a *selectable backend* behind the existing chat,
  gaining a working directory. Persona, hard settings, and the avatar's state animations still apply.
- **A2 — CLI-native auth.** Aimon relies on each CLI's own login (e.g. `claude login`). A BYO
  `ANTHROPIC_API_KEY` present in the environment is inherited by the child automatically; Aimon
  stores no agent key.
- **A3 — Full-auto edits.** The agent may edit files in the chosen folder from the start
  (Claude Code `--permission-mode bypassPermissions`). A confirm/plan mode is reserved.
- **A4 — Session resume.** Aimon maps each conversation to the CLI's own `session_id` and replays
  it with `--resume`, so the agent's memory carries context. Aimon's Conversation State Reducer is
  bypassed on the agent path.

## Decision

Introduce a **backend abstraction** with a registry, a reusable **CLI process host**, and per-CLI
**adapters** — keeping model providers and CLI agents behind one selection mechanism while giving
agents their own request type. Everything still emits `ProviderStreamItem`.

### Layers

- **Domain (`Aimon.Domain.Agents`)** — `BackendId`, `WorkspaceContext`, `AgentSessionId`,
  `AgentPermissionMode`, and `AgentTurnRequest` (the agent analogue of `ProviderRequest`).
- **Application** — the `IAgentBackend` port, `BackendDescriptor`/`BackendAvailability`/`BackendKind`/
  `BackendAuthKind`, and `IChatBackendRegistry` (with `ChatBackendRegistry`, which wraps the model
  provider as the default backend). `IRouter.RouteAgent` enforces hard settings and builds the
  persona system prompt; `ChatService` dispatches model vs. agent turns. Backend/workspace/session
  are threaded through `SendMessageRequest`.
- **Infrastructure (`Aimon.Infrastructure.Agents`)** — `IProcessRunner`/`ProcessRunner`
  (process-tree kill, stdin prompt, stderr drain), `CliAgentProcessHost` (Working → mapped items →
  Idle / Error+`agent-error`), `CliExecutableResolver` (PATH + Windows `.cmd` shim wrapping), and
  the Claude Code adapter under `Agents/ClaudeCode`.

### Event mapping convention

Agent activity rides the existing `ProviderStreamItem.ProviderEvent` with these `Kind`s:

| Kind | Payload | Meaning |
|------|---------|---------|
| `session` | session id (string) | The CLI's session, persisted by Aimon to resume next turn. |
| `tool` | tool name | The agent invoked a tool. |
| `file-edit` | file path | The agent edited a file. |
| `usage` | summary string | Tokens/cost for the turn. |
| `agent-error` | message | The process failed or exited non-zero. |

Lifecycle `AgentState` transitions (`Working`/`Idle`/`Error`) are owned by `CliAgentProcessHost`,
**not** the mapper. Typed `ProviderStreamItem` variants and a dedicated `UsingTool` `AgentState`
are a possible future refinement.

## Adding a new CLI agent backend

Adding Codex / Cursor / Copilot is **four small pieces** — no pipeline or UI changes:

1. **Descriptor** — a `BackendDescriptor` (id, display name, `Kind = CliAgent`, capability flags,
   `AuthKind`).
2. **Argument builder** — a method turning an `AgentTurnRequest` into the CLI's non-interactive,
   streaming argv (prompt preferably via **stdin** to dodge shell quoting).
3. **Event mapper** — an `ICliStreamMapper` translating the CLI's per-line output into
   `ProviderStreamItem`s using the convention above. Must never throw; pin the shape with fixture tests.
4. **Registration** — `services.AddSingleton<IAgentBackend, MyCliAgentBackend>();` in
   `Aimon.Infrastructure.DependencyInjection`.

Reuse `CliExecutableResolver`, `CliAgentProcessHost`, and `ProcessRunner` unchanged.

### Per-CLI specifics to pin when writing each adapter

These CLIs evolve quickly; confirm the exact flags against the installed version.

| CLI | Executable | Non-interactive streaming | Session resume | Notes |
|-----|-----------|---------------------------|----------------|-------|
| **Claude Code** | `claude` | `-p --output-format stream-json --verbose` | `--resume <id>` | Full-auto via `--permission-mode bypassPermissions`; persona via `--append-system-prompt`. Implemented. |
| **Codex** | `codex` | `exec` with a JSON/streaming output flag | session/rollout flag | Confirm the exec subcommand's machine-readable output. |
| **Cursor** | `cursor-agent` | non-interactive/print flag with a stream format | resume/continue flag | Confirm headless output schema. |
| **Copilot** | `copilot` (GitHub Copilot CLI) | programmatic/print mode | continue/resume flag | Confirm the agent CLI's streaming contract. |

## Consequences

- The model-provider path is unchanged; agent support is additive and behind a registry.
- The process host, resolver, and runner are shared, so new CLIs are mostly a descriptor + mapper.
- **Known limitations (this phase):** full-auto only (no approval round-trip UI); Windows `.cmd`
  shims are launched via `cmd.exe`, so a persona system prompt with exotic shell metacharacters could
  be mis-parsed (the native installer's `claude.exe` avoids this; the prompt itself goes via stdin);
  MCP-config passthrough is out of scope. The Codex/Cursor/Copilot adapters themselves are future work.
