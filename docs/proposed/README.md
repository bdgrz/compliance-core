# Domain model

This catalog is the source of truth for Compliance Core domain rules and the selected target write model. It documents business ownership, invariants, commands, completed events, processes, and reactors. Unknown or conflicting facts are marked explicitly in their owner records.

## Domain catalog

- [Domain overview](domain-model.md).
- [Aggregates](aggregates/README.md): business invariants and lifecycle ownership.
- [Commands](commands/README.md): requested actions and identities.
- [Events](events/README.md): completed facts and wire compatibility.
- [Processes](processes/README.md) and [reactors](reactors/README.md): cross-owner business choreography.
- [Read models](read-models.md): domain queries and projections.
- [External-source boundary](partner-seam.md).
- [Boundary decisions](aggregate-boundary-notes.md), and [selected decisions](decisions-log.md).
- [Write-model names](write-model-names.md) and [selected rules](write-model-domain-rules.md).

## Bounded contexts (source of truth)

The write model is being redesigned one bounded context at a time. Each context
doc in [contexts](contexts) is hand-authored and authoritative. It covers the
context's aggregates, commands, events and reactors, its Mermaid flow diagrams,
its decisions with Jev evidence, and its open questions. Once a context is
locked, its generated owner pages are deleted.

| Context | Status |
| --- | --- |
| [Platform access](contexts/platform-access.md) | Locked 2026-11-03 |
| [Firm services](contexts/firm-services.md) | Locked 2026-11-03 |
| [Program and scope](contexts/program-and-scope.md) | Locked 2026-11-03 |
| [Workforce assurance](contexts/workforce-assurance.md) | Locked 2026-11-03 |
| [Application inventory](contexts/application-inventory.md) | Locked 2026-11-03 |
| [Technology and information inventory](contexts/technology-and-information-inventory.md) | Locked 2026-11-03 |
| [Examinations](contexts/examinations.md) | Locked 2026-11-03 |
| [Auditor collaboration](contexts/auditor-collaboration.md) | Locked 2026-11-03 |
| [Commitments](contexts/commitments.md) | Draft |

Pages under `aggregates/`, `commands/`, `events/`, `processes/` and `reactors/`
were generated from the earlier code-derived catalog. They are input only and do
not override a context doc. The generator and its JSON inputs were removed on
2026-11-03, so edit Markdown directly.
