# SystemDescription

- Classification: `business_aggregate`
- Source area: `SystemDescription` (derived from source path; not a bounded-context ruling).
- Business aggregate: `true`
- Selected status: `SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED`

## Identity and invariant

**Selected identity:** Tenant-scoped (tenantId, programId, descriptionId); `DescriptionId` on `CreateSystemDescriptionDraft` is the caller-supplied immutable creation identity (exact immutable creation retry) so an exact retry resolves the original result and a different id is a different record; later mutations use CommandId bound to this identity and the expected revision.

**Invariant:** Immutable narrative versions, completeness and approval against exact source snapshot.

**Additional decision rule:** [Placeholder: no owner-specific decision rule is recorded.]

**Must preserve:** No approved content or source snapshot mutates; exact changes and period decisions are admitted before approval.

**Replay:** Current required authorization precedes replay; exact authorized retries resolve the original accepted result using the selected logical identity and immutable actor/intent/source binding. Fresh mutable feasibility, clock selection and body preparation cannot replace that original result. Expected revision is concurrency precondition, not a new identity.

**Lifecycle:** [Placeholder: a consolidated lifecycle sequence is not present in the selected owner record.]

## Commands

| Command | Kind | Identity field |
| --- | --- | --- |
| [ApproveSystemDescription](../commands/system-description.md#approvesystemdescription) | http | CommandId |
| [BindSystemDescriptionSources](../commands/system-description.md#bindsystemdescriptionsources) | http | CommandId |
| [BindSystemDescriptionToExamination](../commands/system-description.md#bindsystemdescriptiontoexamination) | http | CommandId |
| [CreateSystemDescriptionDraft](../commands/system-description.md#createsystemdescriptiondraft) | http | DescriptionId |
| [ProposeSystemDescriptionSuccessor](../commands/system-description.md#proposesystemdescriptionsuccessor) | http | CommandId |
| [RecordSystemDescriptionNoChangeDecision](../commands/system-description.md#recordsystemdescriptionnochangedecision) | http | CommandId |
| [ReviewSystemDescription](../commands/system-description.md#reviewsystemdescription) | http | CommandId |
| [ReviseSystemDescriptionDraft](../commands/system-description.md#revisesystemdescriptiondraft) | http | CommandId |

## Events

| Event | Discriminator | Version |
| --- | --- | --- |
| [SystemDescriptionApproved](../events/system-description.md#systemdescriptionapproved) | bdgrz.systemdescription.system-description-approved | 2 |
| [SystemDescriptionLinkedToExamination](../events/system-description.md#systemdescriptionlinkedtoexamination) | bdgrz.systemdescription.system-description-bound | 2 |
| [SystemDescriptionDraftCreated](../events/system-description.md#systemdescriptiondraftcreated) | bdgrz.systemdescription.system-description-draft-created | 2 |
| [SystemDescriptionDraftRevised](../events/system-description.md#systemdescriptiondraftrevised) | bdgrz.systemdescription.system-description-draft-revised | 2 |
| [SystemDescriptionNoChangeRecorded](../events/system-description.md#systemdescriptionnochangerecorded) | bdgrz.systemdescription.system-description-no-change-recorded | 2 |
| [SystemDescriptionReviewed](../events/system-description.md#systemdescriptionreviewed) | bdgrz.systemdescription.system-description-reviewed | 2 |
| [SystemDescriptionSourcesRecorded](../events/system-description.md#systemdescriptionsourcesrecorded) | bdgrz.systemdescription.system-description-sources-bound | 2 |
| [SystemDescriptionSuccessorProposed](../events/system-description.md#systemdescriptionsuccessorproposed) | bdgrz.systemdescription.system-description-successor-proposed | 2 |

## Cross-owner flows

| Row | Kind | Source owner | Target owner | Command |
| --- | --- | --- | --- | --- |
| [S020](../reactors/system-description-decision-reactor.md#s020) | business_receiver | SystemDescription | SignificantChange | RecordSignificantChangeSystemDescriptionDecision |
| [S021](../reactors/system-description-decision-reactor.md#s021) | business_receiver | SystemDescription | ComplianceIncident | RecordComplianceIncidentSystemDescriptionDecision |

## Product rules

[Placeholder: product-rule traceability is not recorded for this owner.]

## Adopted architecture decisions

[Placeholder: no adopted architecture-decision reference is recorded.]

## Evidence and complete record

- Baseline source: [Placeholder: source file is not present in this checkout]
- Aggregate review: [system-description-review-2026-10-29.md](../../product/definitive-aggregate-choreography/system-description-review-2026-10-29.md)
- Full selected record: [JSON](../data/aggregates/system-description.json)

> Target definition is separate from runtime qualification. Implementation status recorded by the source: `SEPARATE_VERIFICATION_REQUIRED_IMPLEMENTATION_HALTED`.
