# Extracting insights with Copilot (or any assistant)

A method for mining your own work for **publishable, transferable insights** —
the reframes and heuristics buried in your architecture docs, POCs, and design
reviews — without leaking anything confidential.

It uses a two-step, two-hat flow: one prompt **extracts** candidates, a second
prompt **judges and sharpens** them. Extraction and evaluation are kept separate
on purpose — a reporter that also judges blurs a wrong fact and a wrong opinion
into the same sentence.

## The one hard rule

Whatever comes out has to be **sanitized enough to leave the machine.** The
extraction prompt forces abstraction, but *you* are the final gate: read each
output and confirm there is no confidential specific before it goes anywhere.
Sanitization and quality point the same way — a reframe that only makes sense
with internal nouns attached is not portable anyway.

## What to point it at

Open the real sources and give the assistant access (in VS Code Copilot Chat,
`@workspace`, or just open the file):

- architecture docs
- design docs (e.g. "when to make X deterministic vs. agent-decided")
- POC readmes and design notes
- PR and design-review descriptions
- knowledge-graph / architecture-agent code

The observed regularities live in the work, not in your chat history.

## The quality bar

In priority order:

1. **Reframes that read as an observed regularity** — a near-fact about how these
   systems behave that reality could prove wrong. Shape: *"A vs B is really about
   C"* / *"X isn't about P, it's about Q."* Strong examples:
   - "An agent's autonomy is bounded by definability, not accuracy."
   - "Reliability comes from deterministic scaffolding around a nondeterministic
     core, not from making the core reliable."
   - "Context is structure, not volume — an agent reasons over the relations you
     make explicit, not the tokens you supply."
2. **Heuristics** — *"when X, prefer Y, because Z"* — only if you can name where it flips.
3. **Tendencies** — *"the model/system defaults to Z; steer against it with W."*

Reject:

- **Workflow tips** about *using* an AI assistant to code (force it to commit,
  re-verify regenerations, split the PR). Crowded genre, not differentiating.
- **Defensible stances** / matters of emphasis someone could reasonably disagree
  with. If the sharpest thing is a stance, label it "debatable," don't dress it
  as a finding.
- **Bare prescriptions** (*"do Y instead of X"*). Dig one level down to the
  reframe underneath.

## Prompt A — extraction

```
You are helping me extract publishable, transferable insights from my own
work as a principal-level software architect building AI/agent systems.

WHAT I'M LOOKING FOR (in priority order):
1. REFRAMES that read as an OBSERVED REGULARITY — a near-fact about how these
   systems behave that reality could prove wrong. Shape: "A vs B is really
   about C" or "X isn't about P, it's about Q."
   Strong examples (the bar):
   - "An agent's autonomy is bounded by definability, not accuracy."
   - "Reliability comes from deterministic scaffolding around a nondeterministic
      core, not from making the core reliable."
   - "Context is structure, not volume — an agent reasons over the relations you
      make explicit, not the tokens you supply."
2. HEURISTICS: "when X, prefer Y, because Z" — only if you can name where it flips.
3. TENDENCIES: "the model/system defaults to Z; steer against it with W."

REJECT (do not output these):
- Workflow tips about USING an AI assistant to code (force it to commit, re-verify
  regenerations, split the PR). Crowded genre, not differentiating.
- Defensible STANCES / matters of emphasis someone could reasonably disagree with.
  If the sharpest thing is a stance, label it "debatable," don't dress it as a finding.
- Bare prescriptions ("do Y instead of X"). Dig one level down to the reframe under it.

SANITIZATION (mandatory — output must be safe to publish):
- No product, org, team, agent, platform, or colleague names.
- No absolute counts. Every quantity is a ratio or an N-of-M.
- The insight is the transferable pattern; my specific work is only the evidence.

FOR EACH insight, give me:
- Thread: the recurring topic/tension I keep circling in this material.
- Reframe (one sentence): the crystallized claim.
- Type: reframe | heuristic | tendency.
- Where it holds / where it breaks (the boundary — required).
- Confidence it's an observed regularity vs. a stance (high/medium/debatable).

Prefer 3–5 genuinely strong ones over a long list. If the material only yields
workflow tips or stances, say so rather than padding. Ground each in what the
source actually shows.
```

## Prompt B — the sharpener

Run this on any raw output that's close but not quite there.

```
Take this insight and pressure-test it to the top bar:
1. Is it an OBSERVED REGULARITY I could be empirically wrong about, or just a
   defensible stance? If a stance, tell me plainly and try to find the
   observed version underneath.
2. State where it BREAKS. No boundary = slogan.
3. Rewrite it as one sentence, sanitized (no internal nouns, ratios not counts),
   in a plain, declarative voice — no hype.
```

## After extraction

Bring the sanitized survivors into your working log and write the best one up in
your own voice. Extracting more doesn't count; publishing one does.
