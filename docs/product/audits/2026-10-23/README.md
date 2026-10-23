# Aggregate and reactor boundary specification

Current source head: `b7a2bd02cc90c6734c94391399a55c0da1621521`.

Read the selected design in this order:

1. [Concrete design lock review](design-lock-review.md) records source-backed corrections, Jev choices and [20 ordered regression traces](design-review-traces.json). [Implementation decisions](implementation-decisions.md): fixed identities, proof/content types, operation semantics, guard protocols, process store and exact framework prerequisites.
2. [Selected split owners](aggregate-split-targets.md) and [owner invariant protocols/tests](owner-protocols.md): exact destinations, business rules, failure paths and required races.
3. [Target source command contracts](target-command-contracts.md): all 161 commands referenced by split roots, including the three obsolete commands to remove, exact routes/fields/handler/first owner/facts and signature changes.
4. [Other current command identities](remaining-command-identities.md): 201 remaining source entries, with query/authentication exchanges identified and required identity/transport selection for the mutations.
5. [Additional source admission contracts](source-admission-contracts.md): exact identity, administrator, Type II, delivery and internal cancellation/abort entry points.
6. [Target reactors](target-reactor-contracts.md): every selected source fact, typed receiver command/handler, target owner, guards and recovery tests, plus the 27 ordered processes that own receiver dispatch order (the `processes` array in the JSON companion is normative).
7. [Target event schemas](target-event-contracts.md), [shared contract types and identity vectors](target-contract-types.json), and [new owner/process definitions](target-owner-extensions.json).
8. [Durable intent/result schemas](durable-intent-schemas.md), [revision/response semantics](revision-and-response-contracts.md), [canonical intent](canonical-intent-contracts.json), [native acceptance](native-acceptance-contract.md), and [protected capability responses](protected-response-contract.md).
9. [Choreography flows](choreography-flows.md): 20 Mermaid diagrams, plus [seven additional admission/independence flows](admission-choreography.md), covering the complete admission/publication/closure paths.

Current-source references remain pinned to the head above:

- [Full 103-owner source audit](aggregate-boundary-audit.md) and [owner/handler/guard/test source specification](aggregate-boundary-specification.md).
- [Current typed schemas](command-event-schemas.md): 651 request and 418 event contract types.
- [Current 24 reactor implementations](reactor-contract-catalog.md) and [missing producer/handler paths](command-handler-gaps.md).
- [Shared handler order](handler-execution-contract.md).
- Machine-readable JSON companions accompany every catalog.

The selected target design supersedes earlier speculative target names and aliases. Implementation remains pending; documentation checks do not prove behavior. Existing source catalogs describe current behavior and are not replacement acceptance evidence. Portia 0.9 supplies required typed identity binding. Realm-wide atomic native operation uniqueness is the explicit additional storage prerequisite tracked by [#1042](https://github.com/bdgrz/compliance-core/issues/1042), under [#1022](https://github.com/bdgrz/compliance-core/issues/1022). Because every first owner is route-determined, write paths use per-first-owner acceptance; only the operation status/result routes keyed by operation ID alone and cross-route duplicate detection remain disabled until it is delivered (see the scope section of [native acceptance](native-acceptance-contract.md)).

Jev source screens, design/naming comparisons, implementation decision comparisons and protocol preflight requests/responses are retained here. They are advisory; the adopted decisions, source evidence, disagreements and required regressions are recorded in implementation-decisions.md. Run `node tools/validate-aggregate-spec.mjs` for structural validation.

The current selected catalog accounts for 364 active source mutations, 115 receiver effects, 287 target native fact schemas and 43 named resolved intent/result records. Four query/authentication exchanges and three obsolete commands account for the rest of the 371 source mutation candidates. The 29 split roots yield 65 selected owners; separate owner overrides and extensions cover staging absorption, recovery and protected grants. These are design coverage counts, not implemented behavior.
