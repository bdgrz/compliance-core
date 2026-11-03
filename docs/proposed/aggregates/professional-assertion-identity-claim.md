# ProfessionalAssertionIdentityClaim

- Classification: `business_identity_or_uniqueness_authority`
- Source area: `AccessControl` (derived from source path; not a bounded-context ruling).
- Business aggregate: `true`
- Selected status: `SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED`


## Identity and invariant

**Selected identity:** Tenant-scoped platform / assertionId; the identity is established by the admitting owner or reactor named in the choreography, never by a caller request key; mutations bind CommandId to this exact identity.

**Invariant:** Permanent binding of signed assertion identity to digest, designation, canonical user and duty scope; claim identity cannot be recycled.

**Additional decision rule:** [Placeholder: no owner-specific decision rule is recorded.]

**Must preserve:** Exact predecessor, monotonically increasing authority revision, stable signed subject, valid staff source proof.

**Replay:** Current required authorization precedes replay; exact authorized retries resolve the original accepted result using the selected logical identity and immutable actor/intent/source binding. Fresh mutable feasibility, clock selection and body preparation cannot replace that original result. Expected revision is concurrency precondition, not a new identity.

**Lifecycle:** [Placeholder: a consolidated lifecycle sequence is not present in the selected owner record.]





## Commands

| Command | Kind | Identity field |
| --- | --- | --- |
| [ClaimProfessionalAssertionIdentity](../commands/professional-assertion-identity-claim.md#claimprofessionalassertionidentity) | receiver | CommandId |

## Events

| Event | Discriminator | Version |
| --- | --- | --- |
| [ProfessionalAssertionIdentityClaimed](../events/professional-assertion-identity-claim.md#professionalassertionidentityclaimed) | bdgrz.professional-assertion-identity-claim.professional-assertion-identity-claimed | 1 |

## Cross-owner flows

| Row | Kind | Source owner | Target owner | Command |
| --- | --- | --- | --- | --- |
| [T000](../reactors/apply-or-revoke-professional-duty-designation-reactor.md#t000) | reaction_or_regular_process_stage | FirmProfessionalDutyDesignation | ProfessionalAssertionIdentityClaim | ClaimProfessionalAssertionIdentity |
| [T001](../reactors/apply-or-revoke-professional-duty-designation-reactor.md#t001) | reaction_or_regular_process_stage | ProfessionalAssertionIdentityClaim | FirmProfessionalDutyDesignation | ApplyProfessionalDutyChange |

## Product rules

[Placeholder: product-rule traceability is not recorded for this owner.]

## Adopted architecture decisions

[Placeholder: no adopted architecture-decision reference is recorded.]

## Evidence and complete record

- Baseline source: [Placeholder: source file is not present in this checkout]
- Aggregate review: [Placeholder: no owner-specific review file is present]
- Full selected record: [JSON](../data/aggregates/professional-assertion-identity-claim.json)

> Target definition is separate from runtime qualification. Implementation status recorded by the source: `SEPARATE_VERIFICATION_REQUIRED_IMPLEMENTATION_HALTED`.
