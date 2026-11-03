# ApplicationRelationship

- Classification: `business_aggregate`
- Source area: `Applications` (derived from source path; not a bounded-context ruling).
- Business aggregate: `true`
- Selected status: `SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED`


## Identity and invariant

**Selected identity:** Tenant + exact directed source application / relationship kind / target application tuple under its unchanged existing UUIDv5 IdFor. Supplied RelationshipId must equal this tuple identity; it is not an arbitrary caller UUID. Source and target direction is significant; accepted EV-D03/EV-D04 remove any separate tuple claim/release owner.

**Invariant:** One exact directed source/kind/target relationship lifecycle with unchanged deterministic UUIDv5 tuple identity; active endpoint/source-visibility and exact source/target revision guards remain. No independent tuple claim/release authority is introduced.

**Additional decision rule:** [Placeholder: no owner-specific decision rule is recorded.]

**Must preserve:** Retirement requires an approved successor for exact app revision; import holds block incompatible local change.

**Replay:** Current required authorization precedes replay; exact authorized retries resolve the original accepted result using the selected logical identity and immutable actor/intent/source binding. Fresh mutable feasibility, clock selection and body preparation cannot replace that original result. Expected revision is concurrency precondition, not a new identity.

**Lifecycle:** [Placeholder: a consolidated lifecycle sequence is not present in the selected owner record.]





## Commands

| Command | Kind | Identity field |
| --- | --- | --- |
| [AddApplicationRelationship](../commands/application-relationship.md#addapplicationrelationship) | http | RelationshipId |
| [ApproveApplicationSuccessor](../commands/application-relationship.md#approveapplicationsuccessor) | http | CommandId |
| [RemoveApplicationRelationship](../commands/application-relationship.md#removeapplicationrelationship) | http | CommandId |

## Events

| Event | Discriminator | Version |
| --- | --- | --- |
| [ApplicationRelationshipAdded](../events/application-relationship.md#applicationrelationshipadded) | bdgrz.application.relationship.added | 1 |
| [ApplicationRelationshipApproved](../events/application-relationship.md#applicationrelationshipapproved) | bdgrz.application.relationship.approved | 1 |
| [ApplicationRelationshipRemoved](../events/application-relationship.md#applicationrelationshipremoved) | bdgrz.application.relationship.removed | 1 |

## Cross-owner flows

| Row | Kind | Source owner | Target owner | Command |
| --- | --- | --- | --- | --- |
| [X005](../reactors/remove-relationships-after-application-retirement-reactor.md#x005) | planned_reaction | DeclaredApplication | ApplicationRelationship | RemoveApplicationRelationship |

## Product rules

[Placeholder: product-rule traceability is not recorded for this owner.]

## Adopted architecture decisions

[Placeholder: no adopted architecture-decision reference is recorded.]

## Evidence and complete record

- Baseline source: [Placeholder: source file is not present in this checkout]
- Aggregate review: [Placeholder: no owner-specific review file is present]
- Full selected record: [JSON](../data/aggregates/application-relationship.json)

> Target definition is separate from runtime qualification. Implementation status recorded by the source: `SEPARATE_VERIFICATION_REQUIRED_IMPLEMENTATION_HALTED`.
