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
| D3 | **Providers = OpenAI first.** Implement the OpenAI adapter behind an `IAiProvider` port; Anthropic, Google, and local/OpenAI-compatible endpoints are added later without touching Application/Domain. | Single well-supported streaming provider to validate the whole pipeline. |

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
| Commercial AI Provider (stream text + events) | Infrastructure | `IAiProvider` (OpenAI adapter) |
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
- **Contract/replay** — provider adapter tested against recorded SSE fixtures (no network).
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

### Phase 8 — Stub billing / licensing & quota (per D2)
**Goal:** the pricing tiers have real client-side seams against a mock backend.

- `ILicenseService` / `IQuotaService` with a **mock** backend: tier (Free/$1/$7–25/$40–100/PAYG),
  custom-profile limits, and model-tier gating enforced client-side.
- Router/Interpreter consult quota before dispatch; BYO-key path bypasses metering where the tier
  allows. Clean interface so a real subscription/proxy server drops in later.

**Acceptance / test:** unit tests for gating rules per tier (e.g., Free = limited custom profiles /
low-tier models); demo: switching mock tier changes which models/profiles are allowed.

### Phase 9 — Hardening, packaging & docs
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
0 ─► 1 ─► 2 ─► 3 ─► 4 ─► 5 ─► 6 ─► 7 ─► 8 ─► 9
                 └► (walking skeleton demoable here)
```

- 0→1→2→3 are strictly sequential (foundation → skeleton).
- 4 and 5 both depend on 3 and can be built in parallel if resourced.
- 6 depends on the `AgentState` stream from 3.
- 7 integrates 3–6; 8 layers onto 7; 9 hardens everything.

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
3. **Phase 3/8:** default model list and how tier gating maps to specific OpenAI models.
4. **Phase 8:** which capabilities are metered vs. BYO-key-only per tier.

---

## 8. Definition of done (per phase, checklist)

- [ ] Code merged behind the correct layer; dependency rule respected.
- [ ] Unit/contract tests added and green; live/headless tests where applicable.
- [ ] Acceptance criteria demonstrably met via the phase's demo script.
- [ ] Docs/ADR updated for any decision made during the phase.
- [ ] No regression in earlier phases' demos.
