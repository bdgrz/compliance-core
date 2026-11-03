# AuditorSampleSelection

- Classification: `business_aggregate`
- Source area: `AuditorRequests` (derived from source path; not a bounded-context ruling).
- Business aggregate: `true`
- Selected status: `SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED`


## Identity and invariant

**Selected identity:** Tenant-scoped (requestId, selectionId); the identity is established by the admitting owner or reactor named in the choreography, never by a caller request key; mutations bind CommandId to this exact identity.

**Invariant:** Independent exact sample selection lifecycle.

**Additional decision rule:** A selection references population row identifiers and the exact population version's content hash; it never loads the latest population, so a later version does not move an existing sample. Selecting from an amended population is a new selection linked to the earlier one.

**Must preserve:** Admission cannot race resolution; exact response selections and dispositions are immutable proofs.

**Replay:** Current required authorization precedes replay; exact authorized retries resolve the original accepted result using the selected logical identity and immutable actor/intent/source binding. Fresh mutable feasibility, clock selection and body preparation cannot replace that original result. Expected revision is concurrency precondition, not a new identity.

**Lifecycle:** [Placeholder: a consolidated lifecycle sequence is not present in the selected owner record.]





## Commands

| Command | Kind | Identity field |
| --- | --- | --- |
| [RecordAuditorSampleSelection](../commands/auditor-sample-selection.md#recordauditorsampleselection) | receiver | CommandId |

## Events

| Event | Discriminator | Version |
| --- | --- | --- |
| [SampleSelectionRecorded](../events/auditor-sample-selection.md#sampleselectionrecorded) | bdgrz.auditorrequests.sample-selection-recorded | 1 |

## Cross-owner flows

| Row | Kind | Source owner | Target owner | Command |
| --- | --- | --- | --- | --- |
| [T105](../reactors/record-auditor-sample-selection-reactor.md#t105) | reaction_or_regular_process_stage | AuditorRequest | AuditorSampleSelection | RecordAuditorSampleSelection |

## Product rules

- **D17-03** — A sample selection references population row identifiers and the exact population version's content hash; a later population version does not move an existing sample, and selecting from an amended population is a new, linked selection. Decision: [m0-d17-audit-deliverables.md](../decisions/m0-d17-audit-deliverables.md).

## Adopted architecture decisions

[Placeholder: no adopted architecture-decision reference is recorded.]

## Evidence and complete record

- Baseline source: [Placeholder: source file is not present in this checkout]
- Aggregate review: [Placeholder: no owner-specific review file is present]
- Full selected record: [JSON](../data/aggregates/auditor-sample-selection.json)

> Target definition is separate from runtime qualification. Implementation status recorded by the source: `SEPARATE_VERIFICATION_REQUIRED_IMPLEMENTATION_HALTED`.
