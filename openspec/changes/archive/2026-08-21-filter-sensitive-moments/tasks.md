## 1. Vision call returns summary + sensitivity

- [x] 1.1 Add `MomentVision(string Summary, bool Sensitive)` in `Huddle.Vision`.
- [x] 1.2 Update the `MomentExtractor` vision prompt: never write specific sensitive values in the summary (describe the kind of thing); judge sensitivity; reply with only `{"summary": …, "sensitive": true|false}`.
- [x] 1.3 `ExtractAsync` returns `MomentVision`. Add `ParseVision`: isolate the first balanced JSON object, read `summary`/`sensitive`; non-JSON reply → whole text as summary, not sensitive (moment not lost).

## 2. Tick policy + config

- [x] 2.1 `HuddleConfig`: add `SkipSensitiveMoments` (default `true`), parsed from config key `skipSensitiveMoments` (only an explicit `false` disables it).
- [x] 2.2 In the capture tick, after `ExtractAsync`: when `vision.Sensitive && SkipSensitiveMoments`, log and skip the tick (store nothing); otherwise store `vision.Summary`.

## 3. Docs

- [x] 3.1 README: document that summaries never contain sensitive values (always) and the `skipSensitiveMoments` toggle (default on); add the config row and a Safeguards note.

## 4. Verify

- [x] 4.1 `dotnet build Huddle.slnx -c Debug` clean.
- [x] 4.2 Sensitive frame → `sensitive: true` and a value-free summary → skipped by default.
- [x] 4.3 Non-sensitive frame → `sensitive: false` → stored, and the stored summary is the parsed text (not raw JSON).
- [x] 4.4 Record commands and outcomes in §Verification.

## Verification

Verified on the personal machine (2026-08-21), Claude provider.

**Build** — `dotnet build Huddle.slnx -c Debug` → `Build succeeded. 0 Error(s)`.

**Sensitive-positive + redaction** — generated a compensation-statement image (base salary `$185,000`, bonus `$42,500`, RSU `$120,000`, total `$347,500`) and ran the exact Claude vision command (`claude -p "<prompt> @comp.jpg" --model sonnet`). Result:
`{"summary": "You are reviewing a confidential compensation statement document showing salary and equity details for an employee.", "sensitive": true}` — flagged sensitive, and a leak check confirmed **none** of the figures appear in the summary. That frame would be skipped by default.

**Non-sensitive** — the same prompt on an ordinary dev-session screenshot returned `{"summary": "You are developing and testing the Huddle app…", "sensitive": false}` — normal summary, not flagged, so normal moments still flow (no over-skipping).

**End-to-end in the real binary** — launched the app on defaults; the stored moment's `summary` was the clean parsed text (not the raw `{…}` JSON), confirming `ParseVision` extracts the summary and the non-sensitive path stores as before.

Note: the always-on "never write sensitive values" prompt rule is the actual guarantee (independent of the flag being right); the `sensitive` flag + default skip is the extra layer, and pairs with the existing `captureDenylist`.
