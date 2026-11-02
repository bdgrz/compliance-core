# SeparationOfDutiesWaiver

- Classification: `business_aggregate`
- Source area: `AccessControl` (derived from source path; not a bounded-context ruling).
- Business aggregate: `true`
- Selected status: `SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED`

## Identity and invariant

**Selected identity:** Tenant + caller WaiverId; original approval scope/subject/revision remains immutable.

**Invariant:** Exact record/version/action/beneficiary exception can be approved once only by a different administrator who is neither requester nor beneficiary, during its validity window.

**Additional decision rule:** A waiver is requested by an Org Admin with a required reason, a scope of record type, recording scope and beneficiary, and an expiry after the request; it is approved by a second Org Admin who is not the beneficiary, so a sole Org Admin cannot waive their own conflict. Without a waiver self-review and self-approval stay refused. The Compliance Lead alone approves acknowledgement waivers of at most 12 months, and waived people count as excepted, never acknowledged.

**Must preserve:** [Placeholder: preservation rule not recorded.]

**Replay:** Current required authorization precedes replay; exact authorized retries resolve the original accepted result using the selected logical identity and immutable actor/intent/source binding. Fresh mutable feasibility, clock selection and body preparation cannot replace that original result. Expected revision is concurrency precondition, not a new identity.

**Lifecycle:** [Placeholder: a consolidated lifecycle sequence is not present in the selected owner record.]

## Commands

| Command | Kind | Identity field |
| --- | --- | --- |
| [ApproveSeparationOfDutiesWaiver](../commands/separation-of-duties-waiver.md#approveseparationofdutieswaiver) | http | CommandId |
| [RecordSeparationOfDutiesWaiver](../commands/separation-of-duties-waiver.md#recordseparationofdutieswaiver) | http | WaiverId |

## Events

| Event | Discriminator | Version |
| --- | --- | --- |
| [SeparationOfDutiesWaiverApproved](../events/separation-of-duties-waiver.md#separationofdutieswaiverapproved) | bdgrz.sod-waiver.approved | 1 |
| [SeparationOfDutiesWaiverRecorded](../events/separation-of-duties-waiver.md#separationofdutieswaiverrecorded) | bdgrz.sod-waiver.recorded | 1 |

## Cross-owner flows

[Placeholder: no choreography row names this owner.]

## Product rules

- **D03-05** — Self-review and self-approval are blocked by default; the same member cannot review or approve a record they authored or own. Source: [m0-d03-roles-and-separation-of-duties.md](../../product/decisions/m0-d03-roles-and-separation-of-duties.md).
- **D03-06** — An Org Admin may record a separation-of-duties waiver that is time-bound, reason-required, and scoped to a record type, recording scope, beneficiary, approver, rationale, and expiry. Source: [m0-d03-roles-and-separation-of-duties.md](../../product/decisions/m0-d03-roles-and-separation-of-duties.md).
- **D03-07** — A separation-of-duties waiver must be approved by a second person who is an Org Admin and is not the member the waiver benefits; a sole Org Admin cannot waive their own conflict. Source: [m0-d03-roles-and-separation-of-duties.md](../../product/decisions/m0-d03-roles-and-separation-of-duties.md).

## Adopted architecture decisions

[Placeholder: no adopted architecture-decision reference is recorded.]

## Evidence and complete record

- Baseline source: [Placeholder: source file is not present in this checkout]
- Aggregate review: [Placeholder: no owner-specific review file is present]
- Full selected record: [JSON](../data/aggregates/separation-of-duties-waiver.json)

> Target definition is separate from runtime qualification. Implementation status recorded by the source: `SEPARATE_VERIFICATION_REQUIRED_IMPLEMENTATION_HALTED`.
