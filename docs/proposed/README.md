# Proposed domain model

This is the working target for Compliance Core implementation. The current refinement scope is the write model: commands → events → reactors. The naming decisions below are intended to become the implementation names after the affected contracts are updated.

The proposed pages explain business outcomes and preserve enough source names and wire details to map the target back to current contracts. Unknown or conflicting details use `[Placeholder: ...]`.

## Current target: commands → events → reactors

- [Write model names](write-model-names.md) records target names and the reason for each changed name.
- [Write model domain rules](write-model-domain-rules.md) records the selected business rules for commands, events, and reactors.
- [Commands](commands/README.md) state the requested actions; [events](events/README.md) state completed facts; [reactors](reactors/README.md) state why each reaction exists.
- [Domain model](domain-model.md), [product scope](scope.md), and [journeys](journeys.md) provide broader product context.
- [Aggregates](aggregates/README.md) — one page per business owner and technical responsibility.

## Later reference material

- [Read models](read-models.md) and [processes](processes/README.md) remain available for later refinement.
- [Worker catalog](worker-catalog.md) and [registration plan](reactors/registration-plan.md) record implementation context.
- [Provider and external-source seams](partner-seam.md) records governed provider facts, the manual-first connector boundary, and placeholders for future partner contracts.
- [Architecture notes](architecture-overview.md), [boundary notes](aggregate-boundary-notes.md), and [product-rule traceability](product-rule-traceability.md).
- [decisions](decisions-log.md), [open details](open-gaps.md), and [source map](sources.md).

## Inventory

| Catalog | Rows |
| --- | --- |
| businessOwners | 131 |
| technicalResponsibilities | 14 |
| selectedCommandContracts | 507 |
| canonicalEvents | 501 |
| targetEventSchemas | 295 |
| choreographyRows | 196 |
| registeredRootTypes | 65 |
| namedReactorRoots | 72 |
| targetCommandNames | 503 |
| targetEventNames | 501 |
| processDefinitions | 30 |
| architectureDecisions | 35 |
| productRulesNeedingImplementationEvidence | 77 |

## Authority and qualification

The proposed target names are recorded in [write-model-names.json](data/write-model-names.json). The definitive catalog and current code provide the source names, business rules, route contracts, and event wires used to reconcile that target. Renamed event types keep their selected discriminator strings and versions.

Implementation evidence and delivery sequencing remain in [aggregate-status.md](../product/definitive-aggregate-choreography/aggregate-status.md) and [aggregate-backlog.md](../product/definitive-aggregate-choreography/aggregate-backlog.md).

## Regeneration

Run `node docs/proposed/generate.mjs` from the repository root after updating structured JSON catalogs. It refreshes catalog-derived pages and snapshots; captured product prose, boundary notes, and open-gap explanations are maintained in docs/proposed. The renderer does not delete generated files missing from a later source snapshot.

Source hashes: [data/manifest.json](data/manifest.json).
