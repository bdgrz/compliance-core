# AuditOutcome

- Classification: `business_aggregate`
- Source area: `Examinations` (derived from source path; not a bounded-context ruling).
- Business aggregate: `true`
- Selected status: `SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED`


## Identity and invariant

**Selected identity:** Tenant / audit-outcomes / OutcomeId, with immutable ProgramId, delivered package and snapshot provenance. OutcomeId is the selected immutable creation identity.

**Invariant:** One auditor-supplied report conclusion is immutable for its outcome identity and exact delivered package/snapshot provenance.

**Additional decision rule:** Only an issued report and the auditor-authored outcome exactly as received are retained; the platform stores no attest workpaper, test conclusion or report draft and never authors, edits or recalculates the auditor's opinion. Management evaluations and sign-offs stay separate records and are never merged into the outcome.

**Must preserve:** [Placeholder: preservation rule not recorded.]

**Replay:** Current required authorization precedes replay; exact authorized retries resolve the original accepted result using the selected logical identity and immutable actor/intent/source binding. Fresh mutable feasibility, clock selection and body preparation cannot replace that original result. Expected revision is concurrency precondition, not a new identity.

**Lifecycle:** [Placeholder: a consolidated lifecycle sequence is not present in the selected owner record.]





## Commands

| Command | Kind | Identity field |
| --- | --- | --- |
| [RecordExaminationOutcome](../commands/audit-outcome.md#recordexaminationoutcome) | http | OutcomeId |

## Events

| Event | Discriminator | Version |
| --- | --- | --- |
| [AuditOutcomeRecorded](../events/audit-outcome.md#auditoutcomerecorded) | bdgrz.examinations.audit-outcome-recorded | 2 |

## Cross-owner flows

[Placeholder: no choreography row names this owner.]

## Product rules

- **D27-01** — The platform holds no attest workpaper, test conclusion, or report draft; it may retain only an issued report and the auditor-authored outcome as received, and never authors or recalculates the auditor's opinion. Decision: [m0-d27-attest-scope.md](../decisions/m0-d27-attest-scope.md).
- **D27-02** — Management's evaluations and sign-offs remain separate from the auditor-authored outcome received. Decision: [m0-d27-attest-scope.md](../decisions/m0-d27-attest-scope.md).

## Adopted architecture decisions

[Placeholder: no adopted architecture-decision reference is recorded.]

## Evidence and complete record

- Baseline source: [Placeholder: source file is not present in this checkout]
- Aggregate review: [Placeholder: no owner-specific review file is present]
- Full selected record: [JSON](../data/aggregates/audit-outcome.json)

> Target definition is separate from runtime qualification. Implementation status recorded by the source: `SEPARATE_VERIFICATION_REQUIRED_IMPLEMENTATION_HALTED`.
