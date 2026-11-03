# EvidenceRedaction

- Classification: `business_aggregate`
- Source area: `Evidence` (derived from source path; not a bounded-context ruling).
- Business aggregate: `true`
- Selected status: `SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED`

## Identity and invariant

**Selected identity:** Tenant / evidence-redactions / RedactionId, with exact source artifact/content/version. Selected preparations, approvals and other mutations use CommandId.

**Invariant:** A redaction approval must target current exact original/derived preparation and be independent of its preparer unless exact scoped waiver proof authorizes self-approval.

**Additional decision rule:** A redaction yields a new derived artifact with a new content identity linked to its original and never alters the original, which keeps its own retention and access rules. Any Contributor may prepare the derivative, but approval for disclosure requires a Compliance Lead.

**Must preserve:** [Placeholder: preservation rule not recorded.]

**Replay:** Current required authorization precedes replay; exact authorized retries resolve the original accepted result using the selected logical identity and immutable actor/intent/source binding. Fresh mutable feasibility, clock selection and body preparation cannot replace that original result. Expected revision is concurrency precondition, not a new identity.

**Lifecycle:** [Placeholder: a consolidated lifecycle sequence is not present in the selected owner record.]

## Commands

| Command | Kind | Identity field |
| --- | --- | --- |
| [ApproveEvidenceRedaction](../commands/evidence-redaction.md#approveevidenceredaction) | http | CommandId |
| [PrepareEvidenceRedaction](../commands/evidence-redaction.md#prepareevidenceredaction) | http | CommandId |

## Events

| Event | Discriminator | Version |
| --- | --- | --- |
| [EvidenceRedactionApproved](../events/evidence-redaction.md#evidenceredactionapproved) | bdgrz.evidence.redaction.approved | 2 |
| [EvidenceRedactionPrepared](../events/evidence-redaction.md#evidenceredactionprepared) | bdgrz.evidence.redaction.prepared | 2 |

## Cross-owner flows

[Placeholder: no choreography row names this owner.]

## Product rules

- **D16-09** — A redaction creates a new derived artifact with a new content identity linked to its original and never alters the original; the original keeps its own retention and access rules. Source: [m0-d16-evidence-handling.md](../../product/decisions/m0-d16-evidence-handling.md).

## Adopted architecture decisions

[Placeholder: no adopted architecture-decision reference is recorded.]

## Evidence and complete record

- Baseline source: [Placeholder: source file is not present in this checkout]
- Aggregate review: [evidence-redaction-review-2026-10-29.md](../../product/definitive-aggregate-choreography/evidence-redaction-review-2026-10-29.md)
- Full selected record: [JSON](../data/aggregates/evidence-redaction.json)

> Target definition is separate from runtime qualification. Implementation status recorded by the source: `SEPARATE_VERIFICATION_REQUIRED_IMPLEMENTATION_HALTED`.
