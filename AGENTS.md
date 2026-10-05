# AGENTS.md — rules for AI agents working in this repo

Paddock Principal is a motorsport management sim: a career from 1950 with real
people, where history can be changed. The owner (DatJikun) reviews; agents build.

## Read first
1. `VISION.md` — direction and all accepted decisions (PP-001…). Decisions win
   over everything else; do not silently contradict them.
2. `ROADMAP.md` — which phase is current. Work on the current phase only.
3. `DESIGN.md` (game systems) and `TECH.md` (architecture, invariants) as needed.

## Workflow (PP-024)
- Claude (reviewer/architect) writes tasks as GitHub Issues with acceptance
  criteria. Implement exactly the issue; if the issue is ambiguous or conflicts
  with VISION/TECH, stop and ask in the issue instead of guessing.
- One issue = one branch (`feat/<issue#>-short-name`) = one PR referencing the
  issue. Never push to `main`, never merge your own PR.
- A PR is ready when: builds, all tests pass, new behaviour has tests, and the
  PR description says what changed, why, and what was NOT done.
- Don't refactor or "improve" code outside the issue's scope — note it in the PR.

## UI work
- Current UI state and the owner's full feedback live in `ui/HANDOFF_UI.md`. Read it
  before touching `ui/`. Its §3 rules (no filler captions, segmented info, no
  over-wide layouts, confirm buttons, heavier screen transitions) are binding.
- The `impeccable` skill lives in `.claude/skills` and `.cursor/skills`. It expects
  a *visual* `DESIGN.md` at the repo root, but our `DESIGN.md` is the game-design
  document: never let a tool overwrite or "regenerate" it.

## Review checklist for game mechanics (PP-058, from the Peloton research)
A mechanic is not ready if any of these is true:
- a choice has no cost (raise a number, another number only goes up);
- difficulty comes from the UI (hidden limits, unclear budget) instead of the world;
- something fails silently (no inbox item, no reason shown);
- a faster simulation path uses different rules than the watched one;
- it adds admin between races that nobody would choose to click;
- a result is read straight from an attribute instead of emerging from the simulation;
- AI reads hidden truth or the future.

## Hard rules
- **Invariants in TECH §3 are non-negotiable**: no game logic in UI, determinism,
  isolated RNG streams, truth vs knowledge, passive Spy, stable IDs.
- **No new docs.** Fold design changes into the existing files: README, VISION, ROADMAP,
  DESIGN, TECH and GUIDE (PP-017, PP-056).
- **GUIDE.md describes the player-facing systems in plain Polish, around what the player
  chooses**, for friends and testers: no code, issue numbers or PP references. Give numbers as
  `{Class.Const}` placeholders. Run `node tools/docs/build-docs.mjs`: the build fails if a
  constant disappears.
  A proposal that changes a decision = a new PP entry, never an edit of an old one.
- **Code, identifiers, commits in English. Docs in Polish.** Player-facing text
  goes through translation keys in both `pl` and `en` (PP-021).
- **Numbers are estimates until calibrated** — label them, don't present guesses
  as facts.
- **Bug fixes start with a failing test** that reproduces the bug.
- Small, focused commits and PRs with a clear "why". Do not mix unrelated changes.
- Do not commit generated caches (`data/cache/`), saves (`*.paddock`) or
  third-party data. Jolpica/Ergast data is CC BY-NC-SA and stays local (PP-041).
