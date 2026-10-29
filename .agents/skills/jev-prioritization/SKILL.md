---
name: jev-prioritization
description: How to ask TypeSafe Jev for work-priority rankings or design judgment calls in compliance-core (API, key handling, score questions, state, and how to treat the answers).
---

# Jev-assisted prioritization

- Backlog triage, closure criteria and PR preflight go through `scripts/backlog-jev.mjs` (see `AGENTS.md`,
  "JEV and Backlog Workflow"). Use this skill for other priority or design-judgment questions, such as
  ordering aggregates or choosing between boundary options.
- First check for a connected TypeSafe/Jev tool. Otherwise use the installed `typesafe-ai` skill and the
  current [System One API docs](https://docs.typesafe.ai/api/system-one); do not rely on remembered endpoints
  or response shapes when the live docs are available.
- The direct API is `POST https://api.typesafe.ai/v1/systemone`, with bearer authentication and JSON
  containing `model`, `state` and named `questions`. `jev-latest` is the documented model alias; verify
  available aliases with `GET /v1/models` when reachable. The API key is `~/.config/typesafe/jev.key`: load it
  only into the environment of the command that needs it, e.g.
  `TYPESAFE_API_KEY="$(tr -d '[:space:]' < ~/.config/typesafe/jev.key)" node script.mjs`. Never print, log,
  commit, or copy the key into prompts, source, fixtures, or documentation.
- Prefer separate typed `score` questions for separate ranking dimensions. Each question has
  `type: "score"`, focused `instructions`, and an ordered `criteria` array with concrete descriptions.
  Preserve each answer's `score`, `probabilities`, `confidence`, and `legend`; define any combination or
  ranking rule explicitly before applying it. See the [Score primitive docs](https://docs.typesafe.ai/primitives/score).
- Give Jev a compact, source-backed state: current backlog/catalog inputs, candidate facts/owners, and relevant
  contract or implementation evidence. Record the input revision/date and model when retaining a ranking.
  Jev judgments are advisory: they do not establish business authority, event ownership, aggregate
  boundaries, source qualification, or stream identity. Validate those from repository sources and behavior
  tests.
- Treat low confidence, diffuse probabilities, or conflicting dimensions as a prompt for manual evidence
  review. If the API or DNS is unavailable, state that Jev was not run and continue from the canonical
  repository evidence without inventing model scores.
