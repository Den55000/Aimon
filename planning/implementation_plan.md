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

### Phase 4 — Conversation State Reducer
**Goal:** multi-turn context stays compact and feeds the Router.

- `IConversationStateReducer`: previous state + latest exchange → new lightweight state
  (start with a deterministic/heuristic reducer; optional LLM-assisted reducer behind the port).
- Router consumes the reduced state on the next turn (closes the loop in the flowchart).

**Acceptance / test:** deterministic reducer unit-tested (bounded size, retains key facts across
N turns); demo of a multi-turn conversation that references earlier context.

### Phase 5 — Profile Compiler (NL → structured, review/confirm loop)
**Goal:** users author profiles in plain language with AI assistance.

- `IProfileCompiler` adapter: natural-language description → structured profile draft.
- Review/confirm/revise loop exactly as in the flowchart: **Structured profile → Review →
  (Revise → recompile) | (Confirm → store natural + structured)**.
- Compiler model is configurable (BYO key path).

**Acceptance / test:** unit tests with a mock compiler for the review/revise/confirm state machine;
schema validation of compiler output; demo: describe a persona in prose → get a structured draft →
revise once → confirm → persisted (both natural + structured).

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
