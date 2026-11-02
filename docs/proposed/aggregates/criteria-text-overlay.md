# CriteriaTextOverlay

- Classification: `business_aggregate`
- Source area: `Criteria` (derived from source path; not a bounded-context ruling).
- Business aggregate: `true`
- Selected status: `SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED`

## Identity and invariant

**Selected identity:** Tenant-scoped (editionId, normalizedCriterionId); the identity is established by the admitting owner or reactor named in the choreography, never by a caller request key; mutations bind CommandId to this exact identity.

**Invariant:** One licensed text overlay and sequential revision line.

**Additional decision rule:** Accepts only licensed text carrying edition, criterion identifier, supplier, license reference, and usage flags, with license restrictions expressed solely as usage flags; licensed AICPA text is refused for platform-level content and exists only in this tenant- or template-owned overlay.

**Must preserve:** Exact prior revision and one request identity/intent; license/source proof validated per overlay.

**Replay:** Current required authorization precedes replay; exact authorized retries resolve the original accepted result using the selected logical identity and immutable actor/intent/source binding. Fresh mutable feasibility, clock selection and body preparation cannot replace that original result. Expected revision is concurrency precondition, not a new identity.

**Lifecycle:** [Placeholder: a consolidated lifecycle sequence is not present in the selected owner record.]

## Commands

| Command | Kind | Identity field |
| --- | --- | --- |
| [SetCriteriaTextOverlay](../commands/criteria-text-overlay.md#setcriteriatextoverlay) | http | CommandId |

## Events

| Event | Discriminator | Version |
| --- | --- | --- |
| [CriteriaTextOverlayEntryRevised](../events/criteria-text-overlay.md#criteriatextoverlayentryrevised) | bdgrz.criteria-overlay.entry-revised | 2 |

## Cross-owner flows

[Placeholder: no choreography row names this owner.]

## Product rules

- **D02-06** — Licensed criteria text lives only in a tenant- or template-owned text overlay that records edition, identifier, supplier, license reference, and usage flags; it is never stored in platform-level content, and license restrictions are expressed as usage flags. Source: [m0-d02-criteria-content.md](../../product/decisions/m0-d02-criteria-content.md).

## Adopted architecture decisions

[Placeholder: no adopted architecture-decision reference is recorded.]

## Evidence and complete record

- Baseline source: [Placeholder: source file is not present in this checkout]
- Aggregate review: [criteria-text-overlay-review-2026-10-29.md](../../product/definitive-aggregate-choreography/criteria-text-overlay-review-2026-10-29.md)
- Full selected record: [JSON](../data/aggregates/criteria-text-overlay.json)

> Target definition is separate from runtime qualification. Implementation status recorded by the source: `SEPARATE_VERIFICATION_REQUIRED_IMPLEMENTATION_HALTED`.
