# Sample profiles

Each folder here is a **portable Aimon profile bundle** — the same layout the app reads from disk
and exports/imports as a single zip (see `planning/implementation_plan.md`, §3.5).

```
<profile>/
  profile.json          # metadata + natural-language source + compiled structured profile
  hard-settings.json    # optional safety blocks (present only when the profile defines them)
  graphics/             # animation library (added in Phase 6)
```

- `profile.json` is validated by [`config/profile.schema.json`](../config/profile.schema.json).
- `hard-settings.json` is validated by [`config/hard-settings.schema.json`](../config/hard-settings.schema.json).
- **API keys are never stored in a profile.** They live in the OS secret store (Windows DPAPI).

## friendly-assistant

A warm, concise general assistant with per-task styles for `coding` and `email`, a couple of
preferred OpenAI models, and a few example hard-settings blocks.
