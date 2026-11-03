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

## Structured inputs

The renderer inputs and paired owner records live in [data](data). Regenerate catalog pages with `node docs/proposed/generate.mjs`. The manifest records the input hashes and catalog counts.
