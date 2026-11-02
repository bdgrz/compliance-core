# ArtifactDisclosure

- Classification: `business_aggregate`
- Source area: `Retention` (derived from source path; not a bounded-context ruling).
- Business aggregate: `true`
- Selected status: `SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED`

## Identity and invariant

**Selected identity:** Tenant / artifact-disclosures / DisclosureId, with exact artifact content, handling-policy revision and recipient/purpose binding. Selected mutations use CommandId.

**Invariant:** One disclosure binds immutable artifact content, handling-policy revision, recipient and purpose; delivery requires that approved identity and revocation is final.

**Additional decision rule:** Disclosure to an auditor is admitted only for an artifact in available state (quarantined or uninspected artifacts are refused) or for its approved redacted derivative, and every disclosure records the recipient, purpose and handling-policy revision.

**Must preserve:** [Placeholder: preservation rule not recorded.]

**Replay:** Current required authorization precedes replay; exact authorized retries resolve the original accepted result using the selected logical identity and immutable actor/intent/source binding. Fresh mutable feasibility, clock selection and body preparation cannot replace that original result. Expected revision is concurrency precondition, not a new identity.

**Lifecycle:** [Placeholder: a consolidated lifecycle sequence is not present in the selected owner record.]

## Commands

| Command | Kind | Identity field |
| --- | --- | --- |
| [ApproveArtifactDisclosure](../commands/artifact-disclosure.md#approveartifactdisclosure) | command | CommandId |
| [RecordArtifactDisclosureDelivery](../commands/artifact-disclosure.md#recordartifactdisclosuredelivery) | command | CommandId |
| [RevokeArtifactDisclosure](../commands/artifact-disclosure.md#revokeartifactdisclosure) | command | CommandId |

## Events

| Event | Discriminator | Version |
| --- | --- | --- |
| [ArtifactDisclosureApproved](../events/artifact-disclosure.md#artifactdisclosureapproved) | bdgrz.retention.artifact-disclosure-approved | 1 |
| [ArtifactDisclosureDelivered](../events/artifact-disclosure.md#artifactdisclosuredelivered) | bdgrz.retention.artifact-disclosure-delivered | 1 |
| [ArtifactDisclosureRevoked](../events/artifact-disclosure.md#artifactdisclosurerevoked) | bdgrz.retention.artifact-disclosure-revoked | 1 |

## Cross-owner flows

[Placeholder: no choreography row names this owner.]

## Product rules

- **D16-13** — Only available, non-quarantined artifacts, or their approved redacted derivatives, may be shared with auditors, and every disclosure is logged. Source: [m0-d16-evidence-handling.md](../../product/decisions/m0-d16-evidence-handling.md).

## Adopted architecture decisions

[Placeholder: no adopted architecture-decision reference is recorded.]

## Evidence and complete record

- Baseline source: [Placeholder: source file is not present in this checkout]
- Aggregate review: [artifact-disclosure-review-2026-10-29.md](../../product/definitive-aggregate-choreography/artifact-disclosure-review-2026-10-29.md)
- Full selected record: [JSON](../data/aggregates/artifact-disclosure.json)

> Target definition is separate from runtime qualification. Implementation status recorded by the source: `SEPARATE_VERIFICATION_REQUIRED_IMPLEMENTATION_HALTED`.
