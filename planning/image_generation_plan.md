# Image Generation & Vision (OpenAI)

Status: **implemented** on `feat/phase-8-image-generation`. Builds on the Phase 3 request pipeline
(`Router → Interpreter → IAiProvider`); does not touch the Phase 6 avatar graphics engine.

## Goal

Let a turn produce an image and accept images as input, OpenAI-only for now:

- **Image generation** — text prompt → image (`images/generations`, model `gpt-image-1`).
- **Vision (image input)** — attach image(s) to a chat turn (multimodal `chat/completions`).
- **Auto-detection** — the chat model decides via a `generate_image` tool call.
- **Manual override** — a per-turn `TurnMode` (`Auto` / `Chat` / `Image`) the user picks in the UI.

## How it routes

| Mode | Behaviour |
|------|-----------|
| `Auto` | Chat request offers the `generate_image` tool (`tool_choice: auto`). If the model calls it, the adapter generates the image and does one follow-up completion for the caption. |
| `Chat` | Plain streaming chat; the tool is not offered. |
| `Image` | Skips the chat model; calls `images/generations` directly with the user's prompt. |

`TurnMode` flows `SendMessageRequest → RoutingContext → Router → ExecutionPlan → ProviderRequest`.
The Router sets `ImageToolEnabled = (Mode == Auto)`. Hard-settings blocks (blocked model/topic) still
apply in every mode.

## Key seams

- **Domain:** `Message.Attachments` (`ImageAttachment` = bytes + media type); `ProviderStreamItem.ImageArtifact`;
  `TurnMode`; `Mode`/`ImageToolEnabled` on `ExecutionPlan` + `ProviderRequest`.
- **Adapter (`OpenAIProvider`):** the tool loop lives here (chat → images → follow-up caption). Vision
  input is emitted as multimodal content parts (`text` + `image_url` data URL). Wire DTOs in
  `OpenAIDocuments.cs` (request message `content` is a `JsonNode` — string or parts array); new image
  DTOs registered in `OpenAIJsonContext`. `ImageModel`/`ImageSize` on `OpenAIProviderOptions`.
- **Desktop:** the flat-string transcript became an `ObservableCollection<ChatMessageViewModel>`
  (text/image bubbles) rendered by an `ItemsControl` in `ChatView.axaml`. A mode `ComboBox`, an
  `Attach` button + thumbnail strip, and a per-image `Save` button, backed by `IImagePicker`
  (`StorageProviderImagePicker`).

## Notes / limits

- Vision needs a vision-capable chat model (the Model box); errors surface if the model can't see images.
- The tool loop is capped at one image round.
- Image **edits** (input image → edited image via `images/edits`) are out of scope.

## Tests

- Unit (`OpenAIProviderImageTests`): auto tool-call → image + caption; forced image mode; vision content parts.
- Router (`RouterTests`): mode → `ImageToolEnabled` mapping.
- Live smoke (`OpenAIImageLiveSmokeTests`, gated on `AIMON_OPENAI_API_KEY`): real generation + real vision.
