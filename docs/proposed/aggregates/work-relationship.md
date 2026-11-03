# WorkRelationship

- Classification: `business_aggregate`
- Source area: `Workforce` (derived from source path; not a bounded-context ruling).
- Business aggregate: `true`
- Selected status: `SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED`

## Identity and invariant

**Selected identity:** Existing tenant-scoped UUIDv5 of normalized SourceWorkerId. Supplied RelationshipId must equal that unchanged algorithm; selected revisions use CommandId with exact person/membership/source-worker binding.

**Invariant:** One stable source worker ID belongs permanently to one person; end date/status and sponsor/self-manager constraints must form valid governed relationship at each revision.

**Additional decision rule:** A relationship requires a person, worker type employee, contractor or external_collaborator, status pending, active, on_leave or ended, an end date when ended not before the start date, and department at most 200 characters; nobody may be their own manager or sponsor, and an external collaborator requires an accountable internal sponsor. A manual roster uses an organization-maintained worker identifier with any external source identifier as provenance only. A WorkRelationship links one Person to their record in a workforce source, using that source's stable worker ID. Its status and end date must agree; an ended relationship must have an end date.

**Must preserve:** [Placeholder: preservation rule not recorded.]

**Replay:** Current required authorization precedes replay; exact authorized retries resolve the original accepted result using the selected logical identity and immutable actor/intent/source binding. Fresh mutable feasibility, clock selection and body preparation cannot replace that original result. Expected revision is concurrency precondition, not a new identity.

**Lifecycle:** Allowed statuses are pending, active, on_leave, and ended. ReviseWorkRelationship records a status or terms change as WorkRelationshipRevised. Changes to people's work status follow comparison of accepted workforce roster versions. Changing this record alone does not grant or revoke platform or outside-service access.

## Commands

| Command | Kind | Identity field |
| --- | --- | --- |
| [RecordWorkRelationship](../commands/work-relationship.md#recordworkrelationship) | http | RelationshipId |
| [ReviseWorkRelationship](../commands/work-relationship.md#reviseworkrelationship) | command | CommandId |

## Events

| Event | Discriminator | Version |
| --- | --- | --- |
| [WorkRelationshipRecorded](../events/work-relationship.md#workrelationshiprecorded) | bdgrz.workforce.work-relationship.recorded | 1 |
| [WorkRelationshipRevised](../events/work-relationship.md#workrelationshiprevised) | bdgrz.workforce.work-relationship.revised | 1 |

## Cross-owner flows

[Placeholder: no choreography row names this owner.]

## Product rules

- **D06-03** — A workforce person requires a stable worker identifier, display name, work email, worker type (employee, contractor, external collaborator), lifecycle status, start and end dates, direct manager, and department or team; a manual roster uses an organization-maintained worker identifier, and an external source identifier is provenance only. Source: [m0-d06-workforce-source.md](../../product/decisions/m0-d06-workforce-source.md).
- **D06-06** — An external collaborator with access to a reviewed system must be recorded as a workforce person of type external collaborator with an accountable internal sponsor. Source: [m0-d06-workforce-source.md](../../product/decisions/m0-d06-workforce-source.md).

## Adopted architecture decisions

[Placeholder: no adopted architecture-decision reference is recorded.]

## Evidence and complete record

- Baseline source: [Placeholder: source file is not present in this checkout]
- Aggregate review: [Placeholder: no owner-specific review file is present]
- Full selected record: [JSON](../data/aggregates/work-relationship.json)

> Target definition is separate from runtime qualification. Implementation status recorded by the source: `SEPARATE_VERIFICATION_REQUIRED_IMPLEMENTATION_HALTED`.
