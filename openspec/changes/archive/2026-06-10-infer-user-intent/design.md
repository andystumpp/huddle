## Context

We're shipping moments end-to-end now. The pipeline works; the summaries are accurate. What they're missing is *purpose*. From the user's screenshot of three real observations: each one names what's on screen but none of them says what the user is *for*. Andy's product instinct is that Huddle's whole reason for existing is to answer the latter — a teammate who can read the trajectory of your morning and tell you what you're trying to do, not just what your monitor currently shows.

ADR 0001 D2 ("Understand pass") already specifies the input shape: "the frame plus the last N moment summaries (start small, e.g. N=6)." We deferred the trail in the manual-capture change to validate the single call cheaply. The pipeline is now in place — the trail is the next obvious increment.

This change is prompt + input plumbing. No store changes, no UI changes, no new types.

## Goals / Non-Goals

**Goals:**

- Each moment is framed as *intent*, not *description* — "you're trying to / you're verifying / you're likely working on …" rather than "you're looking at / you're reading …"
- The model sees the previous 6 moments alongside the new screenshot, so it can read trajectory rather than guess from one frame.
- Voice tolerates uncertainty: hedge when the trail is thin or ambiguous, commit when the trail is clear.
- Re-inferred fresh every tick — no "current goal" state carried between captures.
- Cost stays in the same ballpark (~30 % up, not 3×).

**Non-Goals:**

- **No structured output** — the response is still a single text field. Splitting into `observation` + `goal_hypothesis` belongs in a later change once we know what shape the cards want.
- **No goal stickiness / persistence** — every tick re-infers from scratch. Per-user explicit decision.
- **No confidence field** — the hedging is in the prose, not a separate score.
- **No card layout change** — the existing `MomentCard` renders the intent sentence as the main body; the section header still reads "OBSERVATIONS". (We may rename to "WHAT YOU'RE WORKING ON" in a follow-up, but not here.)
- **No prompt caching** — system prompt is too small to hit the 2048-token cacheable minimum on Sonnet 4.6, and the trail rotates per tick anyway.
- **No scenarios** — efficiency / social-ideas prompts still don't exist. Intent inference is the foundation they'll sit on.

## Decisions

### D1. Prompt rewrite — intent over description

- **System prompt** (replaces the current one in `MomentExtractor`):

  > You are Huddle's eye. You see one screenshot of the user's current screen, the foreground app and window title, and brief summaries of the user's recent moments (prior captures, newest first).
  >
  > In a single 1–2 sentence response, infer what the user is currently trying to accomplish. Read the trail of recent moments for trajectory — what they've been doing the last several minutes tells you more about purpose than the one frame does.
  >
  > Voice:
  > - Dry. Observant. Specific. Second-person.
  > - Hedged when the trail doesn't pin it down ("you're likely…", "you seem to be…", "it looks like you're trying to…"). Confident when the trajectory is unambiguous.
  > - Anchor in concrete details — name files, branches, tickets, specific UI states.
  > - No greetings. No "I see". No "looks like" as a tic.
  > - If nothing intentional seems to be happening (idle, browsing, between tasks), say so plainly — a single hedged sentence is fine.
  >
  > Frame the response as **intent** ("you're trying to X" / "you're verifying X" / "you're likely shipping X") rather than **description** ("you're looking at X"). Do not propose what to do about it. Do not greet, summarize, or meta-comment.

- **Rationale:** the literal verb shift is the largest lever. Asking for intent forces the model to *infer*, not transcribe. The hedge guidance is what keeps confidently-wrong guesses from feeling like the app gaslighting the user.

### D2. Read the trail at the orchestration layer, not inside `MomentExtractor`

- **Choice:** `ExtractAsync` takes a new parameter `IReadOnlyList<Moment> recent` rather than fetching from `MomentStore` itself. `PeekPanelWindow.OnSchedulerTick` calls `MomentStore.RecentAsync(6)` before `MomentExtractor.ExtractAsync(jpeg, foreground, recent)`.
- **Rationale:** keeps `MomentExtractor` purely about the Claude call — no storage dependency. The store-then-call sequence lives where the orchestration already lives. Easier to test, easier to replace.
- **Alternative considered:** have `MomentExtractor` pull from the store itself. Rejected — couples the extractor to a specific persistence backend.

### D3. Trail text format

- **Choice:** A single text block appended to the user message, before the foreground info. Format per entry:

  ```
  - 6 min ago, Code.exe ("panel.xaml.cs — huddle"): You're verifying the panel surgery diff against the spec; the file has 866 inserts and 437 deletes.
  ```

- **Rationale:** newest-first, terse, prose-readable. Relative time (`6 min ago`) is more useful than absolute timestamps for trajectory reading.
- **Edge cases:**
  - Empty store (first tick after install): omit the "Recent moments" block entirely; the prompt still works as a single-shot intent inference.
  - Window title contains line breaks: collapse to spaces, trim to 80 chars.
  - Summary contains line breaks: same treatment.
- **No JSON. No XML. Plain text.** The model reads prose better than structured payloads for short context like this.

### D4. Skip the trail on the first capture

- **Choice:** if `recent.Count == 0`, the user message contains only the image + foreground line, exactly as today. The prompt's "summaries of recent moments" phrasing tolerates absence — the model doesn't lecture about missing context.
- **Rationale:** ADR 0001 doesn't require a warm-up. First moment after a fresh install should still produce a useful single-shot.

### D5. Don't pass the *new* moment into its own trail

- **Choice:** `MomentStore.RecentAsync(6)` is called *before* the new moment is built and persisted. So the model never sees its own about-to-be-written output. The trail is strictly history.
- **Rationale:** prevents weird self-referential prompts ("Recent: 0 min ago, the user is …"). Even though we technically can't insert before calling Claude, ordering matters: read trail → call → persist → push to UI.

### D6. Re-infer fresh each tick (no goal stickiness)

- **Choice:** No carried state. No "current goal" field. Each call is independent — the model derives intent from (image + trail) every time, and the answer can pivot if the trajectory pivots.
- **Rationale:** the user explicitly chose this. Sticky-goal could come back if we see the moments feel choppy in practice, but it adds state we don't want to maintain yet.

### D7. Hedging budget — language, not a number

- **Choice:** the prompt instructs the model when to hedge ("when the trail doesn't pin it down"). We do not add a structured confidence score. The voice is the signal.
- **Rationale:** a numeric confidence is meaningless without UI to render it, and the prose hedging is the actual user-facing signal of certainty.

## Risks / Trade-offs

- **[Model over-hedges to the point of saying nothing useful]** → Mitigation: the prompt also tells it to commit when the trail is clear. If we see "you might be doing something with files" style mush in practice, tighten the prompt with a concrete example or two.
- **[Trail summaries contain stale or wrong inferences, which compound]** → Accepted. The hedged voice helps; "you might be verifying the refactor based on the prior moments" is better than "you are verifying" even if the prior moment was off. If a single bad inference poisons several minutes of trail, fresh-each-tick means it can self-correct as soon as the screen changes.
- **[Cost ~30 % uplift]** → Accepted; flagged in proposal. The pause button is still the user's lever. We can downshift to Haiku if cost becomes the binding constraint.
- **[Window-title PII in the trail also goes to Claude]** → Already true for the current frame; the trail just adds *recent* titles. Same trust boundary. Worth noting for the eventual privacy pass but not addressed here.

## Open Questions

- Is "OBSERVATIONS" the right section header now that the content is intent rather than description? Possible rename: "WHAT YOU'RE WORKING ON" or "RIGHT NOW". Decided to defer — not changing UI in this slice.
- Should the trail include the relative-time prefix at all, or just rely on order? Defaulting to including it (it's cheap and informative); easy to drop if it muddles the prompt.
- Six moments — too many, too few, just right? At 3-min cadence that's the last 18 minutes. Could grow to 10 (~30 min) if intent inference still feels short-sighted. Start at 6; revisit after looking at a day of moments.
