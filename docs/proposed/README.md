# Proposed domain model

A structured, source-backed capture of the Compliance Core domain. This first pass mechanically reorganizes domain, owner, command, event, choreography, process, read-model, and decision information already in the repository. It adds no domain rules.

Unknown or conflicting details use `[Placeholder: ...]`. Complete selected per-owner records are under data/; paths outside this checkout are replaced by explicit placeholders.

## Start here

- [Domain model](domain-model.md), [product scope](scope.md), and [journeys](journeys.md).
- [Aggregates](aggregates/README.md) — one page per business owner and technical responsibility.
- [Commands](commands/README.md) and [events](events/README.md).
- [Reactors](reactors/README.md), [processes](processes/README.md), and the [worker catalog](worker-catalog.md); [registration plan](reactors/registration-plan.md).
- [Provider and external-source seams](partner-seam.md) records governed provider facts, the manual-first connector boundary, and placeholders for future partner contracts.
- [Architecture notes](architecture-overview.md), [boundary notes](aggregate-boundary-notes.md), and [product-rule traceability](product-rule-traceability.md).
- [Read models](read-models.md), [decisions](decisions-log.md), and [open details](open-gaps.md).
- [Jev naming review](naming-review.md) records an advisory first pass over aggregate, responsibility, command, and event names.
- [Source map](sources.md).

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
| processDefinitions | 30 |
| architectureDecisions | 35 |
| productRulesNotYetInSource | 77 |

## Authority and qualification

The definitive aggregate/choreography catalog remains the selected target architecture. This documentation reorganizes it for navigation. Target selection, source presence, or catalog completeness does not prove producer/receiver delivery, persistence, replay, recovery, or runtime qualification.

Implementation evidence and delivery sequencing remain in [aggregate-status.md](../product/definitive-aggregate-choreography/aggregate-status.md) and [aggregate-backlog.md](../product/definitive-aggregate-choreography/aggregate-backlog.md).

## Regeneration

Run `node docs/proposed/generate.mjs` from the repository root after updating structured JSON catalogs. It refreshes catalog-derived pages and snapshots; captured product prose, boundary notes, and open-gap explanations are maintained in docs/proposed. The renderer does not delete generated files missing from a later source snapshot.

Source hashes: [data/manifest.json](data/manifest.json).
