# MethodologyTemplate

- Classification: `business_aggregate`
- Source area: `MethodologyTemplates` (derived from source path; not a bounded-context ruling).
- Business aggregate: `true`
- Selected status: `SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED`

## Identity and invariant

**Selected identity:** Platform bdgrz + caller TemplateId; mutation CommandId is distinct from server-frozen version values.

**Invariant:** Publish current authored template only after independent review and distinct third-party approver; published content immutable; retirement stops further template revisions. Contributed records require exact accepted deidentified provenance.

**Additional decision rule:** [Placeholder: no owner-specific decision rule is recorded.]

**Must preserve:** [Placeholder: preservation rule not recorded.]

**Replay:** Current required authorization precedes replay; exact authorized retries resolve the original accepted result using the selected logical identity and immutable actor/intent/source binding. Fresh mutable feasibility, clock selection and body preparation cannot replace that original result. Expected revision is concurrency precondition, not a new identity.

**Lifecycle:** [Placeholder: a consolidated lifecycle sequence is not present in the selected owner record.]

## Commands

| Command | Kind | Identity field |
| --- | --- | --- |
| [ApproveMethodologyTemplateVersion](../commands/methodology-template.md#approvemethodologytemplateversion) | command | CommandId |
| [CreateMethodologyTemplate](../commands/methodology-template.md#createmethodologytemplate) | creation | TemplateId |
| [RetireMethodologyTemplate](../commands/methodology-template.md#retiremethodologytemplate) | command | CommandId |
| [ReviewMethodologyTemplate](../commands/methodology-template.md#reviewmethodologytemplate) | command | CommandId |
| [ReviseMethodologyTemplateDraft](../commands/methodology-template.md#revisemethodologytemplatedraft) | command | CommandId |

## Events

| Event | Discriminator | Version |
| --- | --- | --- |
| [MethodologyTemplateCreated](../events/methodology-template.md#methodologytemplatecreated) | bdgrz.methodologytemplates.methodology-template-created | 3 |
| [MethodologyTemplateDraftReviewed](../events/methodology-template.md#methodologytemplatedraftreviewed) | bdgrz.methodologytemplates.methodology-template-draft-reviewed | 3 |
| [MethodologyTemplateDraftRevised](../events/methodology-template.md#methodologytemplatedraftrevised) | bdgrz.methodologytemplates.methodology-template-draft-revised | 3 |
| [MethodologyTemplateRetired](../events/methodology-template.md#methodologytemplateretired) | bdgrz.methodologytemplates.methodology-template-retired | 3 |
| [MethodologyTemplateVersionPublished](../events/methodology-template.md#methodologytemplateversionpublished) | bdgrz.methodologytemplates.methodology-template-version-published | 3 |

## Cross-owner flows

[Placeholder: no choreography row names this owner.]

## Product rules

[Placeholder: product-rule traceability is not recorded for this owner.]

## Adopted architecture decisions

[Placeholder: no adopted architecture-decision reference is recorded.]

## Evidence and complete record

- Baseline source: [Placeholder: source file is not present in this checkout]
- Aggregate review: [methodology-template-review-2026-10-29.md](../../product/definitive-aggregate-choreography/methodology-template-review-2026-10-29.md)
- Full selected record: [JSON](../data/aggregates/methodology-template.json)

> Target definition is separate from runtime qualification. Implementation status recorded by the source: `SEPARATE_VERIFICATION_REQUIRED_IMPLEMENTATION_HALTED`.
