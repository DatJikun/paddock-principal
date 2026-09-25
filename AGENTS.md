# AGENTS.md — rules for AI agents working in this repo

Paddock Principal is a motorsport management sim: a career from 1950 with real
people, where history can be changed. The owner (DatJikun) reviews; agents build.

## Read first
1. `VISION.md` — direction and all accepted decisions (PP-001…). Decisions win
   over everything else; do not silently contradict them.
2. `ROADMAP.md` — which phase is current. Work on the current phase only.
3. `DESIGN.md` (game systems) and `TECH.md` (architecture, invariants) as needed.

## Hard rules
- **Invariants in TECH §3 are non-negotiable**: no game logic in UI, determinism,
  isolated RNG streams, truth vs knowledge, passive Spy, stable IDs.
- **No new docs.** Fold design changes into the existing five files (PP-017).
  A proposal that changes a decision = a new PP entry, never an edit of an old one.
- **Code, identifiers, commits in English. Docs in Polish.** Player-facing text
  goes through translation keys in both `pl` and `en` (PP-021).
- **Numbers are estimates until calibrated** — label them, don't present guesses
  as facts.
- **Bug fixes start with a failing test** that reproduces the bug.
- Small, focused commits and PRs with a clear "why". Do not mix unrelated changes.
- Do not commit generated caches (`data/cache/`), saves (`*.paddock`) or
  third-party data whose license has not been checked (TECH §6.1).
