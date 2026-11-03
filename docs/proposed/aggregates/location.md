# Location

- Classification: `business_aggregate`
- Source area: `TechnologyInventory` (derived from source path; not a bounded-context ruling).
- Business aggregate: `true`
- Selected status: `SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED`

## Identity and invariant

**Selected identity:** Tenant-scoped locationId; `LocationId` on `RecordLocation` is the caller-supplied immutable creation identity (exact immutable creation retry) so an exact retry resolves the original result and a different id is a different record; later mutations use CommandId bound to this identity and the expected revision.

**Invariant:** Independent location record lifecycle.

**Additional decision rule:** A location keeps the same identity and kind over time. Only an active location must have a unique name within its kind.

**Must preserve:** Kind is immutable; only active same-kind names are unique.

**Replay:** Current required authorization precedes replay; exact authorized retries resolve the original accepted result using the selected logical identity and immutable actor/intent/source binding. Fresh mutable feasibility, clock selection and body preparation cannot replace that original result. Expected revision is concurrency precondition, not a new identity.

**Lifecycle:** A location starts active. ReviseLocation changes its details or status through the accepted location-change process. When a location retires, its old name becomes available after the updated record is saved; historical records keep its ID. The target does not prohibit restoring a retired location.

## Commands

| Command | Kind | Identity field |
| --- | --- | --- |
| [RecordLocation](../commands/location.md#recordlocation) | http | LocationId |
| [ReviseLocation](../commands/location.md#reviselocation) | http | CommandId |
| [CancelLocationChange](../commands/location.md#cancellocationchange) | abort | CommandId |
| [ApplyLocationChange](../commands/location.md#applylocationchange) | receiver | CommandId |

## Events

| Event | Discriminator | Version |
| --- | --- | --- |
| [LocationChangeAccepted](../events/location.md#locationchangeaccepted) | bdgrz.location.location-change-accepted | 1 |
| [LocationChangeCanceled](../events/location.md#locationchangecanceled) | bdgrz.location.location-change-canceled | 1 |
| [LocationRevisionRecorded](../events/location.md#locationrevisionrecorded) | bdgrz.inventory.location.revision_recorded | 1 |

## Cross-owner flows

| Row | Kind | Source owner | Target owner | Command |
| --- | --- | --- | --- | --- |
| [T025](../reactors/location-change-accepted-reactor.md#t025) | reaction_or_regular_process_stage | Location | LocationNameClaim | ClaimLocationName |
| [T026](../reactors/location-change-accepted-reactor.md#t026) | reaction_or_regular_process_stage | LocationNameClaim | Location | ApplyLocationChange |
| [T027](../reactors/location-change-accepted-reactor.md#t027) | reaction_or_regular_process_stage | Location | LocationNameClaim | ReleaseLocationName |
| [A015](../reactors/unassigned-architecture-rows.md#a015) | process_abort_binding | Location | Location | CancelLocationChange |

## Product rules

[Placeholder: product-rule traceability is not recorded for this owner.]

## Adopted architecture decisions

[Placeholder: no adopted architecture-decision reference is recorded.]

## Evidence and complete record

- Baseline source: [Placeholder: source file is not present in this checkout]
- Aggregate review: [location-review-2026-10-27.md](../../product/definitive-aggregate-choreography/location-review-2026-10-27.md)
- Full selected record: [JSON](../data/aggregates/location.json)

> Target definition is separate from runtime qualification. Implementation status recorded by the source: `SEPARATE_VERIFICATION_REQUIRED_IMPLEMENTATION_HALTED`.
