# Aimon — Implementation Plan

> **Purpose of this document.** A phased, reviewable, and testable roadmap for building the
> initial release of Aimon. Each milestone has explicit deliverables and acceptance criteria so
> it can be demoed, reviewed, and signed off before the next one starts.
>
> Source material: `planning/Aimon.docx` (project description), `planning/Aimon Pipeline
> Flowchart.png` (pipeline), and the existing Avalonia template in `src/Aimon.Desktop`.

---

## 1. Product summary

Aimon is a **desktop app that personalizes the AI experience** independently of the underlying
model or the task at hand:

- **Visually** — unique, character-driven animations that react to what the AI is doing.
- **Stylistically** — per-task personalities and interaction rules.
- **Practically** — use any combination of commercial AI models the way the user wants.

Personalization lives in **portable, clearly structured configuration profiles** that can be
exported, tweaked, and imported. Profiles can be authored by hand or built with AI assistance.

**Tech baseline (from the description):** .NET 10 + Avalonia — chosen for ease of development and
future migration. The repo already contains an Avalonia template at `src/Aimon.Desktop`
(Avalonia 12.1.1, `CommunityToolkit.Mvvm`, `net10.0`).

---

## 2. Decisions locked in for this plan

These were confirmed with the product owner and shape the plan below.

| # | Decision | Rationale / notes |
|---|----------|-------------------|
| D1 | **Graphics = staged "reactions first."** A **semantic-state-driven frame system** is the initial target: the AI's lifecycle state (`Idle`, `Listening`, `Working`, `Error`, …) selects which set of frames/animation to load and play. | Lowest risk, fastest to a testable product. The `IGraphicsEngine` is a seam; a richer avatar engine can replace the frame player later. Future: state can also be derived from conversation state, not just the request lifecycle. |
| D2 | **Scope = local desktop app + *stub* billing.** Ship the desktop app with **BYO API keys** stored locally; include the client-side account / licensing / quota **abstractions** with a mock backend so a real subscription/proxy server is a drop-in later. **No server is built in this plan.** | Keeps the plan focused on a usable product while leaving clean seams for the pricing tiers. |
| D3 | **Providers = OpenAI first.** Implement the OpenAI adapter behind an `IAiProvider` port; Anthropic, Google, and local/OpenAI-compatible endpoints are added later without touching Application/Domain. | Single well-supported streaming provider to validate the whole pipeline. The rest of the fleet (Claude, Gemini, DeepSeek, Grok) lands in **Phase 10**; the remaining coding CLIs in **Phase 11**. |

---

## 3. Architecture

### 3.1 Solution structure (Clean Architecture)

Follows the dependency structure in the project description:

```
Aimon.Domain            (no project dependencies)
Aimon.Application       -> Aimon.Domain
Aimon.Infrastructure    -> Aimon.Application, Aimon.Domain
Aimon.Desktop           -> Aimon.Application, Aimon.Infrastructure
```

- **Aimon.Domain** — entities, value objects, enums, domain rules. No framework, no I/O.
- **Aimon.Application** — use cases + **ports** (interfaces): `IProfileCompiler`, `IRouter`,
  `IInterpreter`, `IAiProvider`, `IConversationStateReducer`, `IGraphicsEngine`,
  `IGraphicsCompiler`, `IProfileStore`, `ISecretStore`, `ILicenseService`, `IQuotaService`.
  Orchestrates the pipeline; depends only on Domain.
- **Aimon.Infrastructure** — adapters that implement the ports: OpenAI provider, on-disk profile
  store, OS secret storage, graphics library loader, mock licensing backend, etc.
- **Aimon.Desktop** — Avalonia UI (Views + ViewModels, MVVM), composition root / DI wiring.
  This is the existing template, extended.

**Dependency rule:** dependencies point inward only. Infrastructure and Desktop are swappable;
Domain and Application never reference a UI framework, an HTTP client, or a specific provider.

### 3.2 Component → layer mapping

| Pipeline component (description / flowchart) | Layer | Port / type |
|---|---|---|
| Profile Compiler (NL → structured, review/confirm) | Application use case + Infra adapter | `IProfileCompiler` |
| Hard Settings (safety blocks) | Domain (rules) + Application (enforcement) | `HardSettings`, enforced by Router/Interpreter |
| Router (build context + execution plan) | Application | `IRouter` |
| Interpreter (build provider request) | Application | `IInterpreter` |
| Commercial AI Provider (stream text + events) | Infrastructure | `IAiProvider` (OpenAI; +Claude/Gemini/DeepSeek/Grok in Phase 10) |
| Agentic CLI backend (subprocess in a workspace) | Infrastructure | `IAgentBackend` (Claude Code; +Codex/Cursor/Copilot in Phase 11) |
| Backend catalog & selection (providers + agents) | Application | `IChatBackendRegistry`, `BackendDescriptor` |
| Graphics Compiler (sketches → animation library) | Infrastructure + tooling | `IGraphicsCompiler` |
| Graphics Engine (state/stream → visuals) | Application port + Desktop renderer | `IGraphicsEngine` |
| Conversation State Reducer | Application | `IConversationStateReducer` |
| Profile storage (natural + structured) | Infrastructure | `IProfileStore` |

### 3.3 Runtime data flow (per request)

```
User message ─┐
Hard settings ─┤
Conv. state ───┼─► Router ─► ExecutionPlan ─► Interpreter ─► ProviderRequest
Profile fields ┘                                                    │
Graphics config                                                     ▼
                                                        IAiProvider (OpenAI)
                                                                    │  stream of:
                                                                    │   • TextDelta
                                                                    │   • StateChange(AgentState)
                                                                    │   • Event (tool/finish/…)
                                          ┌─────────────────────────┴──────────────┐
                                          ▼                                         ▼
                                   Graphics Engine                            Chat interface
                             (state → frame set, reactions)                 (streamed text)
                                          │
                        Completed response ▼
                                Conversation State Reducer ─► updated lightweight state ─► (next Router)
```

### 3.4 The streaming contract (key design point)

Every provider adapter exposes a single async stream of typed items so the UI and the graphics
engine can consume the same source:

```csharp
public interface IAiProvider
{
    IAsyncEnumerable<ProviderStreamItem> StreamAsync(
        ProviderRequest request, CancellationToken ct);
}

// ProviderStreamItem is a discriminated set:
//   TextDelta(string text)
//   StateChange(AgentState state)
//   ProviderEvent(string kind, object? payload)   // tool call, finish reason, usage, error…
```

**Semantic states (`AgentState`).** Per D1, these drive which frames the Graphics Engine loads.
Canonical starter set (extensible):

| State | When | Typical visual |
|---|---|---|
| `Idle` | No active request; awaiting input | Resting / breathing loop |
| `Listening` | User is composing / request being prepared | Attentive pose |
| `Working` | Request in flight, before/around token generation | Thinking / typing |
| `Error` | Request failed or was blocked by hard settings | Distressed / apologetic |

Where a provider emits its own semantic states, the adapter maps them onto `AgentState`;
where it does not, **Aimon derives them from the request lifecycle** (idle → prepare → in‑flight →
complete/error). The `AgentState` channel is therefore first-class and provider-independent, and
can later also be influenced by conversation state.

### 3.5 Portable profile format

A profile is a self-contained, human-readable folder (per the "portable, clearly structured
configuration" requirement), e.g.:

```
<profile>/
  profile.json          # natural-language source + compiled structured profile + metadata/version
  hard-settings.json    # optional safety blocks
  graphics/
    manifest.json       # maps AgentState -> frame set (fps, loop, transitions)
    frames/…            # images / vector clips authored or compiled by the Graphics Compiler
```

Design rules: versioned schema, `System.Text.Json` (source-generated), forward-compatible, and
export/import as a single portable bundle (zip). **API keys are never stored in a profile** — they
live in the OS secret store (§3.6).

### 3.6 Cross-cutting concerns

- **DI / composition:** `Microsoft.Extensions.DependencyInjection` + `Configuration` + `Logging`;
  Desktop is the composition root.
- **Secrets:** Windows DPAPI (`ProtectedData`) first; cross-platform seam (`ISecretStore`) for
  macOS/Linux keychains or an encrypted-file fallback later. BYO keys only in this plan.
- **Async streaming:** `IAsyncEnumerable<T>` end-to-end; UI marshals to the Avalonia dispatcher.
- **Resilience:** `IHttpClientFactory` + ret/timeout policy for provider calls.
- **Errors:** typed results for expected failures (blocked action, quota exceeded, provider error)
  surfaced as `AgentState.Error` + a chat message; exceptions only for the unexpected.
- **Telemetry:** off by default; local structured logs only.

### 3.7 Testing strategy (applies to every phase)

- **Unit** — Domain rules and Application use cases with mocked ports (xUnit + NSubstitute).
- **Contract/replay** — provider adapters tested against recorded SSE fixtures (no network); from
  Phase 10 every `IAiProvider` must pass one **shared contract suite**, and every CLI agent mapper is
  driven by recorded `stream-json` fixtures, so adding a backend never means re-writing its tests.
- **Live integration** — a small OpenAI smoke test gated behind an env flag / API key.
- **UI/VM** — `Avalonia.Headless` tests for ViewModels and key views.
- **Demo script** — every phase ends with a manual demo that exercises its acceptance criteria.

---

## 4. Milestone roadmap

Each phase is independently reviewable and ends in a testable state. Phases 0–3 deliver a
**walking skeleton** (real streaming chat) as early as possible; later phases add richness.

### Phase 0 — Solution scaffolding & foundations
**Goal:** a building, testable 4-project solution with the existing template slotted in.

- Restructure `src/` into `Aimon.Domain`, `Aimon.Application`, `Aimon.Infrastructure`, and the
  existing `Aimon.Desktop`; add project references per §3.1.
- Add `Aimon.sln`, `Directory.Build.props` (shared `net10.0`, nullable, analyzers, warnings-as-errors),
  and test projects under `tests/` (`*.Domain.Tests`, `*.Application.Tests`, `*.Infrastructure.Tests`).
- Wire DI/config/logging in the Desktop composition root; keep the template window running.
- Minimal CI build + test script in `build/`.

**Acceptance / test:** `dotnet build` and `dotnet test` succeed; app still launches; a trivial
Domain unit test passes; dependency directions verified (a test asserting Domain has no outward refs).

**Status (2026-08-06): ✅ Done.** Solution `Aimon.slnx` created with the four projects wired per
§3.1 plus three xUnit test projects. `Directory.Build.props` centralizes shared settings
(nullable, implicit usings, analyzers, warnings-as-errors). DI composition root lives in
`src/Aimon.Desktop/App.axaml.cs` and resolves the `MainViewModel` through a placeholder
`IGreetingService` (Application port → Infrastructure adapter → Domain `AgentState`), proving the
dependency chain end-to-end. Build scripts in `build/`. Verified: `dotnet build -c Release` → 0
warnings / 0 errors (all 7 projects); `dotnet test` → 5/5 passing, including the architecture test
that asserts Domain has no outward/UI references. (GUI launch not auto-run to avoid opening a
window; verify with `dotnet run --project src/Aimon.Desktop`.)

### Phase 1 — Domain model & Application ports
**Goal:** the vocabulary of the whole system exists and compiles.

- Domain: `Profile` (natural + structured), structured-profile fields (personality, interaction
  rules, model preferences, per-task style), `HardSettings`, `ConversationState`, `Message`,
  `Exchange`, `ExecutionPlan`, `ProviderRequest`, `AgentState`, `GraphicsConfiguration`,
  `AnimationLibrary`/`FrameSet` descriptors, `ProviderStreamItem` variants.
- Application: all port interfaces (§3.2) and use-case signatures (no implementations yet).

**Acceptance / test:** everything compiles; unit tests cover value-object invariants and any
domain rules (e.g., hard-settings validation).

**Status (2026-08-07): ✅ Done.** Domain vocabulary added across five areas —
`Models` (`ModelRef`, `GenerationParameters`), `Profiles` (`ProfileId`, `Profile`,
`StructuredProfile`, `Personality`, `InteractionRules`, `Verbosity`, `ModelPreferences`,
`TaskStyle`), `Safety` (`HardSettings` with normalization + blocked-lookup rules), `Conversations`
(`Message`, `MessageRole`, `Exchange`, `ConversationState`), `Graphics` (`GraphicsConfiguration`,
`FrameSet`, `AnimationLibrary`), and `Pipeline` (`ExecutionPlan`, `ProviderRequest`, and the closed
`ProviderStreamItem` union: `TextDelta` / `StateChange` / `ProviderEvent`). All Application ports
(§3.2) are declared in `Aimon.Application/Abstractions` — `IProfileCompiler`, `IRouter`,
`IInterpreter`, `IAiProvider`, `IConversationStateReducer`, `IGraphicsEngine`, `IGraphicsCompiler`,
`IProfileStore`, `ISecretStore`, `ILicenseService`, `IQuotaService` — plus the `IChatService`
orchestration use-case signature and its supporting DTOs (`RoutingContext`, `RoutingResult`,
`SendMessageRequest`, `ProfileSummary`, licensing types). No implementations yet, per the phase
goal. Verified: `dotnet build -c Release` → 0 warnings / 0 errors; `dotnet test` → 28/28 passing
(value-object invariants for `ProfileId`, `GenerationParameters`, `Message`; the `HardSettings`
normalization/lookup rule; the `ProviderStreamItem` union; `AnimationLibrary` lookup; and an
Application-layer dependency-rule test). The Phase 0 `IGreetingService` shim remains only to keep
the window rendering until the chat use case is wired into the UI (Phase 3).

### Phase 2 — Configuration, storage & portability
**Goal:** profiles are real, on-disk, portable artifacts.

- Implement the profile format (§3.5) with versioned `System.Text.Json` serialization.
- `IProfileStore` (Infra): create / read / update / list profiles under an app data folder.
- `ISecretStore` (Infra): DPAPI-backed API-key storage, provider-keyed.
- Export/import a profile as a single portable bundle (zip); JSON Schema in `config/`; sample
  profile(s) in `samples/`.

**Acceptance / test:** round-trip a profile (save → load → equal); export then import into a clean
store reproduces it; a key stored via `ISecretStore` is retrievable and not present in any profile
file. Demo: create, export, re-import a profile.

**Status (2026-08-07): ✅ Done.** The portable format from §3.5 is implemented in
`Aimon.Infrastructure`: a versioned, source-generated `System.Text.Json` layer (camelCase, string
enums, indented, nulls omitted) using on-disk **DTOs decoupled from the Domain** so the schema can
evolve independently; `ProfileMapper` reconstructs Domain value objects (and re-validates) on load.
`FileProfileStore` (`IProfileStore`) keeps one folder per profile (`profile.json` +
optional `hard-settings.json`) under the app-data folder and exports/imports a single zip bundle.
`DpapiSecretStore` (`ISecretStore`) stores API keys encrypted with Windows DPAPI
(`CurrentUser` scope + app entropy), keyed by provider, in a JSON map that never contains
plaintext; the DPAPI calls are guarded with `OperatingSystem.IsWindows()` so the cross-platform seam
stays clean. JSON Schemas live in `config/` (`profile.schema.json`, `hard-settings.schema.json`) and
a validated sample bundle in `samples/profiles/friendly-assistant/`. DI defaults both stores to the
current user's app-data folder; `InternalsVisibleTo` lets the tests drive the internal adapters with
temp paths. Verified: `dotnet build -c Release` → 0 warnings / 0 errors; `dotnet test` → 43/43
passing, covering lossless save→load round-trip, list/delete, export→import into a clean store,
DPAPI set/get/overwrite/contains/delete, "plaintext never appears in profile or secret files", and a
guard test that the bundled sample still loads through the real serializer.

### Phase 3 — Request pipeline walking skeleton (OpenAI + Interpreter + Router)
**Goal:** real streaming chat, end to end, with hard-settings enforcement and live `AgentState`.

- `IAiProvider` OpenAI adapter: streaming chat, emitting `TextDelta`, `StateChange`, and
  `ProviderEvent`; lifecycle-derived `AgentState` (Idle→Working→Idle / Error).
- `Interpreter`: `ExecutionPlan` → `ProviderRequest` (model, params, system/context messages).
- `Router`: builds context + plan from profile fields + conversation state + hard settings +
  user message; **enforces hard settings** (block disallowed actions/models/topics before send).
- Minimal chat UI in Desktop consuming the stream (text + state indicator).

**Acceptance / test:** unit tests for Router/Interpreter with a mock provider; contract test of the
OpenAI adapter against recorded SSE; gated live smoke test. Demo: type a message, watch tokens
stream and the state indicator move Idle→Working→Idle; a hard-settings-blocked request is refused
locally and shows `Error`.

**Status (2026-08-07): ✅ Done.** The walking skeleton streams end to end. **Application**: `Router`
selects the model (task-style override → profile preferences), **enforces hard settings locally**
(blocked model, or a blocked topic matched in the user message) and otherwise assembles system +
user messages into an `ExecutionPlan`; `Interpreter` maps that to a `ProviderRequest`; `ChatService`
orchestrates the turn — a block short-circuits to `AgentState.Error` + a `blocked` event without
contacting the provider, otherwise the provider stream is passed straight through. **Infrastructure**:
`OpenAIProvider` (`IAiProvider`) calls the OpenAI streaming Chat Completions API as a typed
`HttpClient`, pulls the key from `ISecretStore`, derives lifecycle `AgentState`
(Working→Idle, or Error) and translates SSE into `TextDelta`/`ProviderEvent`; sampling params are
sent only when they deviate from defaults (so reasoning models accepting only the default work).
**Desktop**: a minimal chat view (API-key entry that saves to the secret store, model box, streamed
transcript, live state label) consuming `IChatService`. The Phase 0 `IGreetingService` shim was
removed now that a real use case is wired into the UI. Verified: `dotnet build -c Release` → 0
warnings / 0 errors; `dotnet test` → 55/55 passing — Router (model selection, model/topic blocks,
no-model), Interpreter mapping, ChatService (pass-through + block-without-calling-provider), and the
OpenAI adapter **contract test against recorded SSE** (assembles "Hello world", finish, Working→Idle)
plus no-key/non-success error paths and the request-shape check. A **gated live smoke test**
(`AIMON_OPENAI_API_KEY`, optional `AIMON_OPENAI_MODEL`) is a no-op without a key. Try it:
`dotnet run --project src/Aimon.Desktop` → save your key, set the model, send a message.

### Phase 4 — Conversation State Reducer
**Goal:** multi-turn context stays compact and feeds the Router.

- `IConversationStateReducer`: previous state + latest exchange → new lightweight state
  (start with a deterministic/heuristic reducer; optional LLM-assisted reducer behind the port).
- Router consumes the reduced state on the next turn (closes the loop in the flowchart).

**Acceptance / test:** deterministic reducer unit-tested (bounded size, retains key facts across
N turns); demo of a multi-turn conversation that references earlier context.

**Status (2026-08-07): ✅ Done.** The pipeline loop is closed. `ConversationStateReducer`
(`IConversationStateReducer`, Application) is a **deterministic, model-free** fold: each completed
`Exchange` bumps `TurnCount`, pulls first-person / "remember" sentences from the user message into a
**bounded** `SalientFacts` list (deduped, newest-wins, capped) and extends a length-bounded rolling
`Summary`. `ChatService` now accumulates the assistant text and final `AgentState` as it passes the
provider stream through, then reduces the turn and surfaces the new `ConversationState` as a terminal
`conversation` `ProviderEvent`; a **blocked turn leaves state untouched** (no event). The Router
already bakes `ConversationState` into the system prompt (via `SystemPromptBuilder`), so the Desktop
VM simply holds the running state, adopts each `conversation` event, and feeds it into the next
`SendMessageRequest` — earlier context (e.g. the user's name/preferences) now survives across turns.
Verified: `dotnet build -c Release` → 0 warnings / 0 errors; `dotnet test` → 62/62 passing — the new
reducer suite covers turn-count increment, first-person fact capture, transient-question rejection,
fact retention across N intervening turns, identical-fact dedupe, and **bounded fact-list + summary**
over a 50-turn conversation; `ChatService` tests assert the reduced-state event on the happy path and
its absence on a block.

### Phase 5 — Profile Compiler (NL → structured, review/confirm loop)
**Goal:** users author profiles in plain language with AI assistance.

- `IProfileCompiler` adapter: natural-language description → structured profile draft.
- Review/confirm/revise loop exactly as in the flowchart: **Structured profile → Review →
  (Revise → recompile) | (Confirm → store natural + structured)**.
- Compiler model is configurable (BYO key path).

**Acceptance / test:** unit tests with a mock compiler for the review/revise/confirm state machine;
schema validation of compiler output; demo: describe a persona in prose → get a structured draft →
revise once → confirm → persisted (both natural + structured).

**Status (2026-08-07): ✅ Done.** Users can author profiles from prose. The `IProfileCompiler` port
now takes a `ProfileCompilationRequest` (description, plus an optional previous draft + revision
instruction), and `OpenAIProfileCompiler` (Infrastructure) implements it against OpenAI with a
**configurable compiler model** (`ProfileCompilerOptions`, BYO key from the `ISecretStore`),
requesting a JSON-object response and mapping it **through the Domain value objects** so malformed
output surfaces as a validation error. The **review/confirm/revise state machine** lives in
`Aimon.Application/Authoring`: `IProfileDraftSessionFactory.BeginAsync` compiles the first draft and
returns a `ProfileDraftSession` that the user `ReviseAsync`es (each call recompiles from the previous
draft + instruction, bumping `RevisionCount`) until `ConfirmAsync` — which **persists the
natural-language source and structured draft together** via `IProfileStore` and closes the session.
`ProfileDraftValidator` gives the "schema validation of compiler output" gate: a draft with no
personality/rules (or a task style missing a name) is flagged, and **confirm is refused while
invalid** so a malformed profile is never stored. Model choice is intentionally left out of the
compiled output (it's a separate, user-driven concern). Verified: `dotnet build -c Release` → 0
warnings / 0 errors; `dotnet test` → 73/73 passing — the state-machine suite (first compile, revise
recompiles from the prior draft + instruction, confirm persists natural + structured, confirm rejected
while invalid then succeeds after a revision, no changes after confirm) with a mock compiler +
in-memory store, and the OpenAI compiler contract tests (JSON → structured mapping, a request shape
carrying `response_format`, the configured model and `stream:false`, a revision that includes the
prior draft and instruction, and no-key and non-success errors). A **gated live smoke test** compiles
a persona from prose when
`AIMON_OPENAI_API_KEY` is set. A **basic authoring UI** is included: the Desktop window is now a
tabbed shell (`ShellViewModel`) with the streaming **Chat** tab and a **Profile Compiler** tab
(`ProfileCompilerViewModel` + `ProfileCompilerView`) that drives the describe → compile → review →
revise → confirm loop and saves through `IProfileStore`, sharing the API key with chat via the secret
store. The fuller profile **manager** (edit / import / export / switch active profile) remains Phase 7
(Desktop UX integration).

### Phase 6 — Graphics Compiler + Graphics Engine (state-driven frames)
**Goal:** the avatar reacts to the AI's semantic state — the core differentiator.

- `IGraphicsCompiler` (Infra + tooling): package user frames/sketches into an `AnimationLibrary`
  with a `manifest.json` keyed by `AgentState` (frame list, fps, loop, transitions).
- `IGraphicsEngine`: subscribe to the `StateChange`/`AgentState` stream (and text), select and
  play the matching frame set, pass text + reactions through to the chat interface.
- Frame player/renderer in Desktop (frame sequencing, looping, state transitions).
- Ship a sample animation library in `samples/`.

**Acceptance / test:** engine unit-tested with a scripted state sequence (asserts correct frame set
per state and clean transitions, incl. `Error`); headless render/VM test; demo: run a conversation
and watch the avatar move through Idle/Listening/Working/Error.

**Status (2026-08-07): ✅ Done.** The avatar reacts to the AI's semantic state. **Graphics Compiler**:
`FileGraphicsCompiler` (`IGraphicsCompiler`, Infrastructure) packages a folder of authored frames into
an `AnimationLibrary` — it reads an authoritative `manifest.json` (keyed by `AgentState`, with
frames/fps/loop) when present, otherwise discovers frames by convention (one subfolder per state) and
**writes a manifest so the folder becomes a self-describing library**; every referenced frame must
exist or compilation fails. **Graphics Engine**: `FrameSequencer` (`IGraphicsEngine`, Application) is a
deterministic frame-per-state player — it selects the frame set for the current state (**falling back
to Idle** when a state is unmapped) and steps frames on demand (looping, or holding the last frame of a
one-shot set); all timing is external so the sequencing is unit-testable without a UI. **Desktop**: an
`AvatarView`/`AvatarViewModel` sits beside the chat, drives the engine with a dispatcher timer clocked
at each set's fps, and — per D1 "reactions first" — draws a **state-coloured, pulsing placeholder**
(a real bitmap frame renderer is a later drop-in behind the same engine). `MainViewModel` forwards the
turn's `AgentState` changes to the avatar (Listening on send → Working while streaming → Idle, or Error
on a block/failure). A **sample library** ships at `samples/graphics/default-avatar/` (manifest + SVG
placeholder frames for all four states). Verified: `dotnet build -c Release` → 0 warnings / 0 errors;
`dotnet test` → 86/86 passing — the engine suite (per-state selection, Idle fallback, looping,
one-shot hold, a scripted Idle→Working→Error→Idle sequence, inert-before-load, `FrameChanged` events)
and the compiler suite (convention scan, manifest write, authoritative manifest with overrides, missing
frame + no-frame failures, plus a guard that the bundled sample compiles with a frame set per state).
The headless render/VM test is deferred with the rest of the Desktop test harness; the sequencing logic
it would cover lives in `FrameSequencer` and is fully unit-tested. Demo: run a conversation and watch
the avatar move Idle → Listening → Working → Idle (and to Error on a blocked/failed turn).

### Phase 7 — Desktop UX integration
**Goal:** a cohesive application, not a set of parts.

- Full chat view; profile manager (create / compile / edit / import / export / switch); settings
  (API keys, model prefs, hard settings); avatar view; per-personality theming.
- MVVM wiring via `CommunityToolkit.Mvvm`; dispatcher marshaling for streams; graceful busy/error
  states tied to `AgentState`.

**Acceptance / test:** headless VM tests for the main flows; navigation/state tests; usability
demo covering the full loop from profile selection to a themed, animated conversation.

**Status (2026-08-08): ✅ Done (core loop).** The parts are now one app. The Desktop window is a
tabbed shell (`ShellViewModel`) — **Chat**, **Profiles**, **Profile Compiler** — with an active-profile
**banner themed by a per-profile accent** (a deterministic colour derived from the profile name).
`ProfileManagerViewModel` lists saved profiles (`IProfileStore.ListAsync`), and **switching the active
profile** (`Activate`) loads the full profile and raises an event the shell forwards into the chat and
theming; it also supports refresh and delete. The **chat now uses the active profile** — its
personality, interaction rules, and **hard settings** drive each turn (the Model box stays authoritative
for the model), and switching profiles starts a fresh conversation; with no profile active it falls back
to a built-in default. Compiling and confirming a profile in the Compiler tab **auto-refreshes** the
Profiles list. The avatar (Phase 6) is wired to the live `AgentState`, giving the full loop: pick a
profile → its accent themes the shell → chat with its persona → the avatar animates through
Idle/Listening/Working/Error. A new **`Aimon.Desktop.Tests`** project covers the main VM flows (list
loads sorted, activate loads the full profile + raises the event, delete removes + refreshes, commands
gated without a selection). Verified: `dotnet build -c Release` → 0 warnings / 0 errors; `dotnet test`
→ 90/90 passing.

**Deferred to Phase 13 (hardening/polish):** file-dialog **import/export** UI (the store-level
export/import already exists from Phase 2 — only the picker remains); a dedicated **settings editor**
for hard settings / model preferences (currently authored via the compiler + profile files); loading a
**profile-specific animation library** (the avatar uses the built-in library until a library registry
maps `AnimationLibraryId` → a packaged folder); and full **Avalonia.Headless render** tests (the VM
flows are covered by plain VM tests).

### Phase 8 — Image Generation & Vision (OpenAI)
**Goal:** a turn can generate an image and accept images as input (OpenAI-only for now), with the
router auto-detecting image requests and a manual override as a fallback.

- Per-turn `TurnMode` (`Auto` / `Chat` / `Image`) threaded
  `SendMessageRequest → RoutingContext → Router → ExecutionPlan → ProviderRequest`; the Router offers
  the image tool only in `Auto` and **still enforces hard settings** in every mode.
- **Auto-detection via tool-calling:** the chat request exposes a `generate_image` tool; if the model
  calls it, the OpenAI adapter generates the image and runs one follow-up completion for the caption.
- **Manual override:** `Image` mode calls `images/generations` directly from the prompt; `Chat`
  disables the tool.
- **Vision input:** `Message` carries `ImageAttachment`s, sent as multimodal `text` + `image_url`
  content parts on the chat path; `ProviderStreamItem.ImageArtifact` carries generated bytes back.
- Desktop renders a bubble list (text + inline images) with attach/thumbnail and per-image save.

**Acceptance / test:** unit tests for the tool-call → image → caption loop, forced image mode, and
vision content-part serialization; Router tests for the mode → tool mapping; a gated live smoke test
(generation + vision). Demo: "draw a fox" in Auto shows an inline image + caption; attach a photo and
ask about it; force generation via `Image` mode; `Chat` never generates.

**Status (2026-08-09): ✅ Done.** _(Added after Phase 7; the previously-planned billing and hardening
phases are now Phase 12 and Phase 13, after the agentic CLI integration and the backend fleet were
later inserted as Phases 9–11.)_ A
turn can now generate and consume images, OpenAI-only.
**Application/Domain**: a per-turn `TurnMode` (`Auto`/`Chat`/`Image`) flows from `SendMessageRequest`
through `RoutingContext`, the `Router`, `ExecutionPlan`, and `ProviderRequest`; the Router sets
`ImageToolEnabled` only in `Auto` and **still enforces hard settings** (blocked model/topic) in every
mode. `Message` gained an additive `Attachments` list (`ImageAttachment` = bytes + media type) and the
closed `ProviderStreamItem` union gained an `ImageArtifact` variant. **Infrastructure**: `OpenAIProvider`
owns the whole image flow — in `Auto` it offers a `generate_image` tool and, on a tool call, hits
`images/generations` then runs **one** follow-up streaming completion for the caption; `Image` mode
calls `images/generations` directly from the prompt; **vision** input is serialized as multimodal
`text` + `image_url` content parts on the normal chat path. The wire DTOs were **split** so a request
message's `content` is a `JsonNode` (plain string *or* a parts array) without disturbing the
response/compiler DTOs; new image DTOs are registered in the source-generated `OpenAIJsonContext`;
`OpenAIProviderOptions` gained `ImageModel` (`gpt-image-1`) + `ImageSize`, reusing the existing base URL
and `ISecretStore` key. **Desktop**: the flat-string transcript became an
`ObservableCollection<ChatMessageViewModel>` of text/image bubbles rendered by an `ItemsControl`, plus a
**mode dropdown**, an **Attach** button with a thumbnail strip, and a per-image **Save** button, all
behind a new `IImagePicker` (`StorageProviderImagePicker`). Verified: `dotnet build` → 0 warnings / 0
errors; `dotnet test` → **94/94 passing** (excluding Live) — new `OpenAIProviderImageTests` (auto
tool-call → image + caption, forced image mode, vision multimodal parts) and `RouterTests` mode→tool
mapping, plus a **gated live smoke test** (`OpenAIImageLiveSmokeTests`, `AIMON_OPENAI_API_KEY`) for real
generation + vision. Design notes: `planning/image_generation_plan.md`. **Out of scope:** image *edits*
(`images/edits`) and non-OpenAI image providers.

### Phase 9 — Agentic CLI backends (Claude Code first)
**Goal:** Aimon can drive an **external agentic coding CLI** — Claude Code first — as a first-class
backend behind the existing personalized chat + avatar, operating on a user-chosen project folder,
with **Codex, Cursor, and GitHub Copilot CLIs as drop-in additions** later.

**Decisions for this phase (confirmed with the product owner):**

| # | Decision | Consequence for the design |
|---|----------|----------------------------|
| A1 | **CLI agent = a selectable *backend*, not a separate app.** It plugs in behind the existing chat surface and gains a **workspace** (working directory). Persona, hard settings, and the avatar's `AgentState` animations still apply. | Reuse the `ProviderStreamItem` stream, the chat transcript, and the Graphics Engine unchanged; add a backend-selection seam + a workspace/session notion to the pipeline. |
| A2 | **Auth via each CLI's own login** (e.g. `claude login` / its own key or subscription). Aimon detects availability + version and surfaces guidance; a BYO key from `ISecretStore` is injected as an env var only when the user has set one. | No mandatory secret-store work for the agent path; instead an availability/version detector and clear "not installed / not logged in" UX. |
| A3 | **Full-auto edits** in the selected folder from the start (Claude Code's bypass-permissions mode). | No approval round-trip UI needed now; the folder picker carries a clear "this agent can modify files here" warning. A confirm/plan permission mode is a later, isolated refinement. |
| A4 | **Session resume from the start.** Aimon maps each conversation to the CLI's own **session id**, so the agent's memory carries multi-turn context. | On the agent path, Aimon **bypasses the Conversation State Reducer** (Phase 4) and instead persists the session id on the conversation and passes it back next turn (`--resume`/`--continue`). |

**New seams (kept extensibility-first so Codex/Cursor/Copilot are pure add-ons):**

- **Backend abstraction & registry (Application + Domain).** A new port `IAgentBackend` exposing the
  **same** `IAsyncEnumerable<ProviderStreamItem>` stream (so the UI + Graphics Engine are untouched)
  plus a `BackendDescriptor` capability record (`BackendId`, `DisplayName`, `BackendKind =
  ModelProvider | CliAgent`, `SupportsWorkspace/Sessions/Images`, `AuthKind`, availability/version).
  An `IChatBackendRegistry` lists descriptors and resolves one by id; the existing OpenAI
  `IAiProvider` is wrapped as the **default** backend so current behavior is preserved.
- **Workspace & agent turn model (Domain).** `WorkspaceContext` (working-directory path; room for
  allowed-paths/git info later), an opaque `AgentSessionId`, an `AgentPermissionMode` enum (`Full`
  now; `Confirm`/`Plan` reserved), and an `AgentTurnRequest` (the agent analogue of `ProviderRequest`:
  prompt, workspace, permission mode, session id, optional model + attachments). Agent activity rides
  the existing `ProviderStreamItem.ProviderEvent` with a small set of standard kinds (`tool`,
  `file-edit`, `session`, `usage`, `agent-error`); typed variants and a `UsingTool` `AgentState`
  are noted as an optional, isolated follow-up.
- **Backend selection threaded through the pipeline.** `SendMessageRequest` → `RoutingContext` →
  Router → plan gain a chosen `BackendId` + optional `WorkspaceContext`/`AgentSessionId` (defaulting
  to the OpenAI model backend). The **Router still enforces hard settings** on the agent path (blocked
  topic in the message; blocked backend/model) before anything is launched, and passes the persona
  system prompt to the CLI (e.g. Claude Code `--append-system-prompt`) so the profile still shapes the
  agent. `ChatService` resolves the backend from the registry; on the agent path it skips the reducer
  (A4) and surfaces a terminal `session` event the caller persists.

**Infrastructure:**

- **Reusable CLI execution core.** `CliAgentProcessHost` resolves the executable (PATH or configured
  path), spawns it in the workspace dir with per-CLI argv/env, feeds the prompt (arg or stdin), reads
  stdout as a stream (JSON-lines / `stream-json`), maps items to `ProviderStreamItem` via a per-CLI
  mapper, captures stderr, enforces a timeout, and on cancellation **kills the process tree**
  (non-zero exit → `AgentState.Error` + `agent-error` event). An `IProcessRunner` seam makes the
  parser/mapper unit-testable against recorded fixtures with no real process. A `CliAgentLocator`
  detects availability + version (`claude --version`).
- **Claude Code adapter.** `ClaudeCodeAgentBackend` (`IAgentBackend`) built on the core: runs
  `claude -p "<prompt>" --output-format stream-json --verbose` with bypass-permissions (A3),
  `--resume <sessionId>`/`--continue` (A4), and the persona system prompt, in the chosen folder. Maps
  Claude Code's stream (init/`session_id`, assistant text deltas, `tool_use`/`tool_result`, final
  `result`+usage) to `TextDelta` / `ProviderEvent(tool|file-edit|session|usage)` /
  `StateChange(Working→Idle|Error)`. Auth relies on the CLI's own login (A2), with optional
  `ANTHROPIC_API_KEY` env injection when a key is present. Capability descriptor advertises
  workspace + sessions.

**Desktop UX:**

- A **backend picker** in the chat area (Model provider / Claude Code / …future). Selecting a CLI
  agent reveals a **workspace folder picker** (`IWorkspacePicker`, StorageProvider-based) with a
  clear "this agent can modify files in this folder" warning, and a **permission-mode** control
  (full-auto default per A3).
- An **availability/version indicator** ("Claude Code v… detected" / "not installed — install & run
  `claude login`") that disables send when the CLI is unavailable.
- Agent **tool/file-edit events** render as distinct bubbles in the existing transcript; the **avatar
  reacts** to the agent's `AgentState` via the existing Graphics Engine (Idle→Working→Idle, Error on
  failure). The CLI **session id is persisted on the active conversation** so follow-up turns resume.

**Extensibility:** per-CLI options record (exe path, default model, permission mode, extra args/env);
a short **ADR / "adding a CLI agent backend" guide** documenting the four extension points (descriptor
+ argv/env builder + event mapper + registration) and naming the next adapters explicitly — **Codex
CLI, Cursor CLI (`cursor-agent`), GitHub Copilot CLI** — each reduced to: install-detection command,
non-interactive streaming argv, session-resume flag, and an event-schema mapper.

**Acceptance / test:** unit — registry lists/resolves backends by id; Router enforces hard settings
and builds the agent plan on the CLI path; `ChatService` selects the agent backend, skips the reducer,
and emits the `session` event. Contract/replay — `CliAgentProcessHost` + the Claude Code mapper parse
**recorded `stream-json` fixtures** (init→session id, assistant text, a tool-use/file-edit, result +
usage → correct `ProviderStreamItem`s) with no real process via `IProcessRunner`; availability
detection parses `claude --version` and a missing exe yields an unavailable descriptor. Gated live
smoke test (behind an env flag / `claude` on PATH) — run a real one-shot `claude -p` in a temp
workspace, assert streamed text + a result + a session id, then a second turn resumes it. Demo: pick
the Claude Code backend, choose a folder, ask for a small change → text streams, tool/file-edit
bubbles appear, the avatar moves Idle→Working→Idle; a follow-up relies on the resumed session; a
hard-settings-blocked request is refused locally before the CLI launches.

**Out of scope (this phase):** the Codex/Cursor/Copilot adapters themselves (seams + docs only), a
confirm/approval permission round-trip (full-auto only for now), MCP-config passthrough, and any
non-desktop transport.

**Status (2026-08-10): ✅ Done.** Aimon drives Claude Code as a first-class backend. **Domain**
(`Aimon.Domain.Agents`): `BackendId`, `WorkspaceContext`, `AgentSessionId`, `AgentPermissionMode`,
and `AgentTurnRequest`. **Application**: the `IAgentBackend` port + `BackendDescriptor` /
`BackendAvailability` / `BackendKind` / `BackendAuthKind`, and `IChatBackendRegistry`
(`ChatBackendRegistry` wraps the OpenAI provider as the **default** backend, so the model path is
unchanged). `Router.RouteAgent` **enforces hard settings** and builds the persona system prompt with
an **empty ConversationState** (A4 — the CLI carries memory); backend/workspace/session are threaded
through `SendMessageRequest`; `ChatService` dispatches model vs. agent turns — the agent path **skips
the reducer** and passes the stream through. **Infrastructure** (`Aimon.Infrastructure.Agents`):
`IProcessRunner`/`ProcessRunner` (stdin prompt, stderr drain, **process-tree kill** on cancel),
`CliAgentProcessHost` (Working → mapped items → Idle / Error+`agent-error`), `CliExecutableResolver`
(PATH + Windows `.cmd` shim wrapping via `cmd.exe`), and the Claude Code adapter — a `stream-json`
mapper (session/text/`tool`/`file-edit`/`usage`), an argv builder (`-p --output-format stream-json
--verbose`, `--permission-mode bypassPermissions` for A3, `--resume` for A4, `--append-system-prompt`
for the persona), and a `--version` availability probe. Auth is the CLI's own login (A2). **Desktop**:
a **backend picker**, a **workspace folder picker** (`IWorkspacePicker`) with a "can modify files"
warning, an availability/version indicator, agent tool/file-edit events rendered as bubbles, the
avatar driven by the agent's `AgentState`, and the **session id persisted** on the conversation for
resume. **Extensibility**: `docs/adr/0001-agentic-cli-backends.md` documents the four extension
points (descriptor + argv builder + event mapper + registration) and the per-CLI specifics for the
next adapters. Verified: `dotnet build -c Release` → 0 warnings / 0 errors; `dotnet test` → **126
passing** (excluding Live) — registry, `RouteAgent`, and ChatService agent-path (Application);
`stream-json` mapper fixtures, process-host lifecycle, argv/availability (Infrastructure); and
backend-picker wiring (Desktop) — plus a **gated live smoke test** (`AIMON_CLAUDE_LIVE=1`) that runs
a real one-shot `claude -p` and a session resume. **Out of scope / deferred:** the
Codex/Cursor/Copilot adapters (seams + ADR only), a confirm/approval permission UI (full-auto only),
and MCP-config passthrough.

### Phase 10 — Model-provider fleet (Gemini, Claude, DeepSeek, Grok)

**Goal:** the *model* half of the backend picker becomes a **fleet** — any number of commercial chat
providers side by side, selectable per turn under the same persona, hard settings, and avatar — and
adding provider N+1 becomes an options record plus a mapper, never a pipeline or UI change.

**Why now:** Phase 9 built the backend seam but only generalized the *agent* half. The registry still
wraps exactly one `IAiProvider` as "the model provider" and `ChatService` takes that single provider by
constructor injection, so a second HTTP provider cannot coexist today. This phase makes the registry
symmetric and then fills it.

**Decisions for this phase (to confirm with the product owner):**

| # | Decision | Consequence for the design |
|---|----------|----------------------------|
| B1 | **Symmetric registry.** `IChatBackendRegistry` stops privileging one model provider: it holds **N `IAiProvider`s and N `IAgentBackend`s** keyed by `BackendId`, and gains `TryGetProvider`. `ChatService` resolves the provider *from the registry* instead of taking one injected `IAiProvider`. OpenAI stays the default backend id. | The one structural change in the phase; every provider after it is purely additive. Turns that name no backend behave exactly as today. |
| B2 | **OpenAI-compatible family first.** DeepSeek and Grok (xAI) — and Gemini via its OpenAI-compatibility endpoint — speak the OpenAI chat-completions wire format, so they are **configurations** of one reusable `OpenAICompatibleProvider` (base URL, secret-key name, headers, capability flags), not hand-written adapters. | Three providers for the cost of options records + fixtures. Also makes local / OpenAI-compatible endpoints (Ollama, LM Studio, vLLM) a later config entry rather than a code change. |
| B3 | **Native adapters only where the wire format genuinely differs.** Anthropic (Messages API, its own SSE event names, persona as a top-level `system` field) gets a real adapter. Gemini gets a native `streamGenerateContent` adapter **only if** the compat endpoint proves lossy for what Aimon uses (streaming, vision, tool-calling). | Fidelity where it matters without four bespoke HTTP clients. Open question 6 in §7 decides the Gemini branch before the phase starts. |
| B4 | **Per-provider keys behind one settings surface.** Each provider declares its own `SecretKeyName`; the Desktop settings section lists every *registered* provider with a key box and a key-set indicator, replacing today's single hardcoded `"openai"` box. | The key UI scales with the registry — no new UI per provider. |
| B5 | **Data-driven model catalog.** Model ids live in a shipped `config/models.json` (per provider: id, display name, capability flags — streaming / vision / image generation / tools — and optional context window), loaded at startup, **plus a free-text override** so a model released tomorrow is usable today. | Model line-ups churn far faster than Aimon releases; adding one must never require a rebuild. |

**Application / Domain:**

- **Registry generalization (B1).** `IChatBackendRegistry` gains `TryGetProvider(BackendId, out IAiProvider)`
  alongside `TryGetAgent`; `Descriptors` lists both kinds in one ordered catalog; `DefaultBackendId` comes
  from configuration rather than "whichever `IAiProvider` happened to be injected". `ChatService`
  dispatches on the resolved backend's `Kind`, not on `id == DefaultBackendId`.
- **Capabilities become data, not constants.** `BackendDescriptor` gains the flags the Router needs
  (`SupportsVision`, `SupportsTools`, `SupportsImageGeneration`, `SupportsStreaming`), sourced from the
  provider's options / catalog entry — replacing the hardcoded `SupportsImages = true` the registry
  currently stamps on the model backend.
- **Router honours them.** `ImageToolEnabled` is set in `Auto` only when the selected backend advertises
  image generation; a turn carrying `ImageAttachment`s against a text-only backend is refused locally
  with a clear reason (the existing `blocked` path) instead of failing at the wire; **hard settings are
  still enforced first** — `IsModelBlocked` is extended to match `provider:model` as well as a bare model
  id, so a profile can block one model or a whole provider.
- **One availability probe for both kinds.** `CheckAvailabilityAsync` is hoisted so the registry can
  answer it for *any* `BackendId`: a CLI agent probes its executable (as today), a model provider reports
  whether its key is present in `ISecretStore`. The picker greys out an unusable provider exactly the way
  it greys out an uninstalled CLI.
- **Fail fast on mismatch.** A profile's `ModelRef.Provider` must agree with the selected backend; the
  Interpreter validates this once and reports it, rather than silently sending a Gemini model id to
  OpenAI (today the Desktop always builds `new ModelRef("openai", …)`).

**Infrastructure:**

- Extract the reusable core of `OpenAIProvider` into an **`OpenAICompatibleProvider`** parameterized by an
  options record (base address, secret-key name, extra headers, capability flags, image model), and
  register **OpenAI / DeepSeek / Grok / Gemini-compat** as instances of it. Several `IAiProvider`s coexist
  via **keyed DI** (`AddKeyedSingleton` / keyed typed `HttpClient`s), each with its own timeout and
  resilience policy.
- **`AnthropicProvider`** — `/v1/messages` with `stream: true` and the `x-api-key` + `anthropic-version`
  headers; maps `message_start` / `content_block_delta` / `message_delta` / `message_stop` to `TextDelta` /
  `StateChange` / a `usage` `ProviderEvent`; persona in the top-level `system` field; vision as base64
  `image` content blocks.
- **`GeminiProvider`** (only if B3 triggers) — `:streamGenerateContent` with `contents` / `parts`,
  `inline_data` for vision, `systemInstruction` for the persona.
- **A shared provider contract suite.** One abstract xUnit base class every `IAiProvider` must pass
  against its own recorded SSE fixtures: text streaming order, cancellation mid-stream, transport error →
  `AgentState.Error`, persona placement, and vision serialization when the descriptor advertises it. This
  is the test-side scalability lever — a new provider inherits the suite instead of re-writing it.

**Desktop UX:**

- The backend picker lists model providers **and** CLI agents from one registry; choosing a provider swaps
  the model dropdown to that provider's catalog entries (B5), with the free-text override intact.
- A **Providers & keys** settings section (B4): every registered provider with a key box, a
  key-set/not-set indicator, and Save/Clear — which also retires the key half of the Phase 7 settings
  deferral.
- The chosen backend + model are written to the active profile's `ModelPreferences.Preferred`, so a profile
  remembers the model it was authored for.

**Adding provider N+1 (the extensibility contract this phase must deliver):** for an OpenAI-compatible
provider — an options record, a secret-key name, a `config/models.json` entry, one DI registration, and
fixtures. For a native-protocol provider — the same, plus one stream mapper that passes the shared
contract suite. Documented in **ADR 0002 — model-provider fleet**, the counterpart to ADR 0001.

**Acceptance / test:** unit — the registry lists and resolves N providers + N agents by id; Router
capability gating (no image tool on a backend without it; vision refused on a text-only backend); hard
settings block `provider:model`; a `ModelRef` / backend mismatch fails fast. Contract/replay — the shared
suite green for every provider against recorded fixtures, no network. Gated live smoke — one per provider
behind its own env flag (`AIMON_OPENAI_API_KEY`, `AIMON_ANTHROPIC_API_KEY`, `AIMON_GEMINI_API_KEY`,
`AIMON_DEEPSEEK_API_KEY`, `AIMON_XAI_API_KEY`), skipped when absent. Demo: one profile, one question,
answered in turn by OpenAI, Claude, Gemini, DeepSeek and Grok — same persona, same avatar behaviour, the
model switch visible in the picker; a key-less provider is greyed out with guidance; a hard-settings-blocked
provider is refused before any request leaves the machine.

**Out of scope (this phase):** provider-specific extras (Anthropic extended thinking, Gemini grounding,
xAI live search), image *generation* on non-OpenAI providers (vision input is in scope), local /
self-hosted endpoints (enabled by B2 but not configured here), automatic failover between providers, and
cost/token accounting (Phase 12).

### Phase 11 — Remaining agentic CLI backends (Codex, Cursor, Copilot)

**Goal:** the *agent* half of the fleet is complete — Codex CLI, Cursor CLI (`cursor-agent`), and GitHub
Copilot CLI join Claude Code behind the same picker, each landing as the four small pieces ADR 0001
prescribes, with no pipeline or UI change.

**Per adapter (×3), reusing `CliAgentProcessHost`, `ProcessRunner`, and `CliExecutableResolver` unchanged:**

1. a `BackendDescriptor` (id, display name, `Kind = CliAgent`, capability flags, `AuthKind = CliNativeLogin`);
2. an **argv/env builder** turning an `AgentTurnRequest` into that CLI's non-interactive streaming
   invocation (prompt via **stdin** to dodge shell quoting; persona via the CLI's system-prompt flag;
   full-auto per Phase 9 decision A3);
3. an **`ICliStreamMapper`** translating its per-line output into ADR 0001's shared event vocabulary —
   `session` / `tool` / `file-edit` / `usage` / `agent-error` — dropping anything unrecognized and **never
   throwing**;
4. one line of registration in `Aimon.Infrastructure.DependencyInjection`.

**Cross-cutting work this phase adds:**

- **Pin the real contracts.** §7 open question 5 is answered here, per installed version: each CLI's
  install-detection command, non-interactive streaming argv, output schema, and session-resume flag are
  confirmed against a real run and **recorded as fixtures** under
  `tests/Aimon.Infrastructure.Tests/Fixtures/<cli>/`, with a refresh script in `build/` so fixtures can be
  re-captured when a CLI ships a new schema.
- **Sessions become a capability, not an assumption.** Phase 9 bypasses the Conversation State Reducer on
  the agent path because Claude Code carries its own session (A4). A CLI advertising
  `SupportsSessions = false` must instead go **through** the reducer, so `ChatService` keys that decision
  off the descriptor rather than off "is this an agent".
- **Per-CLI options + settings.** An options record per CLI (executable path override, default model,
  permission mode, extra args/env) surfaced in settings, so a user with a non-PATH install can point Aimon
  at it — and the picker shows per-CLI install/login guidance from its availability probe.
- ADR 0001's "specifics to pin" table is updated to the confirmed contract for all four CLIs.

**Acceptance / test:** contract/replay — mapper fixture tests per CLI (init → session id, assistant text, a
tool use, a file edit, terminal result + usage) with no real process via `IProcessRunner`; availability
parses each `--version` and a missing executable yields an unavailable descriptor; the registry lists all
four agents; `ChatService` uses the reducer for a `SupportsSessions = false` backend and skips it
otherwise. Gated live smoke per CLI (`AIMON_CODEX_LIVE=1`, `AIMON_CURSOR_LIVE=1`, `AIMON_COPILOT_LIVE=1`)
in a temp workspace. Demo: the same small change requested in the same folder through each of the four CLIs
under the same persona — text streams, tool/file-edit bubbles appear, the avatar moves Idle→Working→Idle,
and a follow-up turn resumes where the CLI supports it.

**Out of scope (this phase):** a confirm/approval permission round-trip (still full-auto only), MCP-config
passthrough, running two agents concurrently on one workspace, and any non-desktop transport.

### Phase 12 — Stub billing / licensing & quota (per D2)
**Goal:** the pricing tiers have real client-side seams against a mock backend.

- `ILicenseService` / `IQuotaService` with a **mock** backend: tier (Free/$1/$7–25/$40–100/PAYG),
  custom-profile limits, and model-tier gating enforced client-side.
- Router/Interpreter consult quota before dispatch; BYO-key path bypasses metering where the tier
  allows. Clean interface so a real subscription/proxy server drops in later.

**Acceptance / test:** unit tests for gating rules per tier (e.g., Free = limited custom profiles /
low-tier models); demo: switching mock tier changes which models/profiles are allowed.

### Phase 13 — Hardening, packaging & docs
**Goal:** shippable initial release.

- End-to-end resilience (timeouts, retries, cancellation), consistent error surfacing, log review.
- Architecture docs + ADRs in `docs/`; sample profiles/libraries in `samples/`; packaging/installer
  scripts in `build/`.
- Full test pass (unit + contract + gated live + headless UI); performance sanity on streaming.

**Acceptance / test:** green test suite; a packaged build launches on a clean Windows machine;
docs let a new contributor build and run.

---

## 5. Phase sequencing & dependencies

```
                                                    ┌─► 10 (model-provider fleet) ─┐
0 ─► 1 ─► 2 ─► 3 ─► 4 ─► 5 ─► 6 ─► 7 ─► 8 ─► 9 ─────┤                              ├─► 12 ─► 13
                 └► (walking skeleton demoable here) └─► 11 (remaining CLI agents) ─┘
```

- 0→1→2→3 are strictly sequential (foundation → skeleton).
- 4 and 5 both depend on 3 and can be built in parallel if resourced.
- 6 depends on the `AgentState` stream from 3.
- 7 integrates 3–6; 8 extends the Phase 3 pipeline and Phase 7 chat UI with image generation &
  vision.
- 9 adds agentic **CLI backends** (Claude Code) onto the Phase 3 streaming contract + Phase 7 chat
  UI / Phase 6 avatar; it depends on 3 (the `ProviderStreamItem` stream) and 6–7 (avatar + chat UI),
  and is independent of billing so 9 and 12 can proceed in either order.
- 10 and 11 fill out the two halves of the backend fleet and both build on 9's registry + ADR 0001.
  They are functionally independent of each other, but 10 makes `IChatBackendRegistry` symmetric, so
  if both are in flight **10 lands first** — otherwise the second one rewrites the registry and DI
  registration a second time.
- 12 layers billing onto 7 and meters whatever 10/11 registered; 13 hardens everything.

---

## 6. Risks & mitigations

| Risk | Mitigation |
|---|---|
| Graphics engine scope creep (the novel part). | Locked to a frame-per-state player behind `IGraphicsEngine`; richer avatar is a later, isolated replacement. Prototype in `prototypes/` before Phase 6 if needed. |
| Provider lock-in / streaming quirks. | All provider specifics sit behind `IAiProvider`; `AgentState` derived by Aimon so behavior is uniform across providers. |
| Cross-platform secret storage. | `ISecretStore` seam; Windows DPAPI now, other OSes later. |
| Billing/backend uncertainty. | Stubbed per D2; only client-side interfaces committed to. |
| Compiler output drift / invalid profiles. | JSON Schema validation + review/confirm loop before any profile is persisted. |

---

## 7. Assumptions & open questions

**Assumptions (proceeding unless corrected):**
- Windows-first, but Avalonia keeps the app cross-platform-ready.
- Initial release targets a single active provider (OpenAI) with the abstraction for more.
- No hosted server is built in this plan (mock billing only).

**Open questions to revisit before the phases they affect:**
1. **Phase 6:** exact source frame format the Graphics Compiler ingests (PNG sequences? vector?
   sprite sheets?) and the authoring workflow for non-artists.
2. **Phase 1/5:** the concrete field set of the *structured* profile (personality axes,
   interaction rules, per-task overrides) — to be finalized as the compiler is designed.
3. **Phase 3/12:** default model list and how tier gating maps to specific models per provider.
4. **Phase 12:** which capabilities are metered vs. BYO-key-only per tier.
5. **Phase 11:** exact non-interactive streaming contract per CLI (argv, output schema, session-resume
   flag) for Codex / Cursor / Copilot — to be pinned as each adapter is written, since these CLIs
   evolve quickly.
6. **Phase 10:** does Gemini's OpenAI-compatibility endpoint cover streaming, vision, and tool-calling
   faithfully enough to skip a native adapter (decision B3), or is `streamGenerateContent` required?
7. **Phase 10:** does the model catalog ship as static data (`config/models.json`) only, or also refresh
   from each provider's `/models` endpoint — and how is a stale entry surfaced to the user?

---

## 8. Definition of done (per phase, checklist)

- [ ] Code merged behind the correct layer; dependency rule respected.
- [ ] Unit/contract tests added and green; live/headless tests where applicable.
- [ ] Acceptance criteria demonstrably met via the phase's demo script.
- [ ] Docs/ADR updated for any decision made during the phase.
- [ ] No regression in earlier phases' demos.
