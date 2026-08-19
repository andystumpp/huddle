## Why

To run Huddle on a work laptop, everything it sends to an LLM must go through a **sanctioned tool with corporate auth and no API keys** — GitHub Copilot CLI (Entra-backed login). A spike confirmed that both the Claude CLI and the Copilot CLI can do **vision from a screenshot** (`claude -p "… @<path>"`; `copilot -p "…" --attachment <path> -s`) and text scenarios, using their own login. So Huddle can drop the Anthropic SDK and every API-key path entirely and run **CLI-only**: the same trusted CLI handles both the per-tick vision call and every scenario. On a work machine that means the screenshot is handed to the tool you're already authorized to use, not a separate API endpoint.

## What Changes

- **One config-selected CLI provider** handles both operations — text completion (scenarios) and image description (vision): `claude` | `copilot` | `agency` (Agency = the Copilot invocation with a different binary).
- **Vision moves to the CLI.** `MomentExtractor` stops calling the Anthropic SDK; it writes the resized screenshot to a temp file, asks the selected CLI to describe it, takes the 1–2 sentence summary, and **deletes the temp file immediately** (ephemeral — the raw image is never persisted; only the summary is stored, as today).
- **Scenarios become CLI-only.** Remove `ApiBackend` (the SDK path); keep the Claude CLI backend; add the Copilot/Agency backend.
- **Drop the `Anthropic` NuGet.** Replace the SDK's `Model`/`Effort` types on `ScenarioRequest` with a local model-name string + a small `Effort` enum.
- **Config** — a non-secret `huddle.config.json` (resolved like `huddle.env`) selects the provider and per-provider settings (`command`, `model`). No secrets, ever.
- **Safeguard — dismiss/redact:** a config denylist of foreground app names / window-title substrings suppresses the capture tick (no screenshot, no summary) for sensitive windows.

## Capabilities

### New Capabilities
<!-- None. The cross-cutting CLI provider is implementation shared by the two modified capabilities. -->

### Modified Capabilities
- `scenario-backend`: becomes CLI-only (drop the API/SDK backend), adds the Copilot/Agency provider, and selects the provider from `huddle.config.json`.
- `moment-capture`: the vision call runs through the selected CLI provider (not the Anthropic SDK), the screenshot temp file is ephemeral, and a config denylist can suppress capture for sensitive windows. The `ANTHROPIC_API_KEY` requirement is removed.

## Impact

- **Removed**: `Anthropic` NuGet dependency; `ApiBackend`; the SDK-based vision call in `MomentExtractor`; the `ANTHROPIC_API_KEY` vision requirement.
- **New**: a CLI provider abstraction (text + vision) with `ClaudeCliProvider` and `CopilotCliProvider` (Agency via config); `huddle.config.json` loader; vision temp-file handling; capture denylist.
- **Modified**: `MomentExtractor` (CLI vision), `IScenarioBackend`/factory (CLI-only, provider selection), `ScenarioRequest` (local `Model`/`Effort` types), the tick capture path (denylist check).
- **External**: relies on a logged-in `claude` (personal) or `copilot`/agency binary (work); each handles its own Entra/OAuth auth.
- **Known risks (see design)**: Copilot's `-p` is argument-only → Learnings' ~64K prompt exceeds the command-line limit (mitigate per provider); Copilot/Agency web-search availability is unknown → Efficiency Insights grounding is provider-dependent; Copilot model names vary per install.
- **Non-goals**: Azure OpenAI or any API-key/SDK path (deliberately dropped); remote/hosted providers; full pixel-level redaction (v1 is skip-on-denylist + ephemeral temp).
