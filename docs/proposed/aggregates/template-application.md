# TemplateApplication

- Classification: `business_aggregate`
- Source area: `MethodologyTemplates` (derived from source path; not a bounded-context ruling).
- Business aggregate: `true`
- Selected status: `SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED`


## Identity and invariant

**Selected identity:** Tenant / template-applications / existing UUIDv5 of ProgramId and TemplateId + trimmed ApplicationKey. ApplicationKey is the selected logical creation key; per-entry UUIDv5(applicationId, entryKey) and selected mutation CommandIds remain exact.

**Invariant:** One application pins exact published template provenance, fixed planned tenant draft identities and completion state; one pending later-version adoption proposal receives one immutable disposition before next successor.

**Additional decision rule:** [Placeholder: no owner-specific decision rule is recorded.]

**Must preserve:** [Placeholder: preservation rule not recorded.]

**Replay:** Current required authorization precedes replay; exact authorized retries resolve the original accepted result using the selected logical identity and immutable actor/intent/source binding. Fresh mutable feasibility, clock selection and body preparation cannot replace that original result. Expected revision is concurrency precondition, not a new identity.

**Lifecycle:** [Placeholder: a consolidated lifecycle sequence is not present in the selected owner record.]





## Commands

| Command | Kind | Identity field |
| --- | --- | --- |
| [ApplyMethodologyTemplate](../commands/template-application.md#applymethodologytemplate) | creation | ApplicationKey |
| [ProposeTemplateSuccessorAdoption](../commands/template-application.md#proposetemplatesuccessoradoption) | command | CommandId |
| [DecideWhetherToAdoptNewTemplateVersion](../commands/template-application.md#decidewhethertoadoptnewtemplateversion) | command | CommandId |

## Events

| Event | Discriminator | Version |
| --- | --- | --- |
| [MethodologyTemplateApplied](../events/template-application.md#methodologytemplateapplied) | bdgrz.methodologytemplates.methodology-template-applied | 3 |
| [TemplateApplicationDraftCreated](../events/template-application.md#templateapplicationdraftcreated) | bdgrz.methodologytemplates.template-application-draft-created | 1 |
| [TemplateSuccessorAdoptionProposed](../events/template-application.md#templatesuccessoradoptionproposed) | bdgrz.methodologytemplates.template-successor-adoption-proposed | 3 |
| [TemplateUpdateDecisionRecorded](../events/template-application.md#templateupdatedecisionrecorded) | bdgrz.methodologytemplates.template-successor-disposition-recorded | 3 |

## Cross-owner flows

[Placeholder: no choreography row names this owner.]

## Product rules

[Placeholder: product-rule traceability is not recorded for this owner.]

## Adopted architecture decisions

[Placeholder: no adopted architecture-decision reference is recorded.]

## Evidence and complete record

- Baseline source: [Placeholder: source file is not present in this checkout]
- Aggregate review: [Placeholder: no owner-specific review file is present]
- Full selected record: [JSON](../data/aggregates/template-application.json)

> Target definition is separate from runtime qualification. Implementation status recorded by the source: `SEPARATE_VERIFICATION_REQUIRED_IMPLEMENTATION_HALTED`.
