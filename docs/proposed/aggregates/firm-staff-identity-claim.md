# FirmStaffIdentityClaim

- Classification: `business_identity_or_uniqueness_authority`
- Source area: `AccessControl` (derived from source path; not a bounded-context ruling).
- Business aggregate: `true`
- Selected status: `SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED`


## Identity and invariant

**Selected identity:** Tenant-scoped platform / canonicalUserId; the identity is established by the admitting owner or reactor named in the choreography, never by a caller request key; mutations bind CommandId to this exact identity.

**Invariant:** Permanent canonical user to staffMemberId/practice mapping; disable/status changes never release this mapping.

**Additional decision rule:** A firm staff member's practice designation (advisory or attest) is recorded exactly once on the platform-level firm-staff directory and never in any client tenant.

**Must preserve:** Canonical user maps to exactly one immutable staff/practice identity; status proofs remain versioned and authoritative.

**Replay:** Current required authorization precedes replay; exact authorized retries resolve the original accepted result using the selected logical identity and immutable actor/intent/source binding. Fresh mutable feasibility, clock selection and body preparation cannot replace that original result. Expected revision is concurrency precondition, not a new identity.

**Lifecycle:** [Placeholder: a consolidated lifecycle sequence is not present in the selected owner record.]





## Commands

| Command | Kind | Identity field |
| --- | --- | --- |
| [ClaimFirmStaffIdentity](../commands/firm-staff-identity-claim.md#claimfirmstaffidentity) | receiver | CommandId |

## Events

| Event | Discriminator | Version |
| --- | --- | --- |
| [FirmStaffIdentityClaimed](../events/firm-staff-identity-claim.md#firmstaffidentityclaimed) | bdgrz.firm-staff-identity-claim.firm-staff-identity-claimed | 1 |

## Cross-owner flows

| Row | Kind | Source owner | Target owner | Command |
| --- | --- | --- | --- | --- |
| [T002](../reactors/firm-staff-registration-reactor.md#t002) | reaction_or_regular_process_stage | FirmStaffRecord | FirmStaffIdentityClaim | ClaimFirmStaffIdentity |
| [T003](../reactors/firm-staff-registration-reactor.md#t003) | reaction_or_regular_process_stage | FirmStaffIdentityClaim | FirmStaffRecord | CompleteFirmStaffRegistration |

## Product rules

- **D25-04** — A firm staff member's practice designation (advisory or attest) is recorded once on the platform-level firm-staff directory, not in any client tenant. Decision: [m0-d25-client-tenancy.md](../decisions/m0-d25-client-tenancy.md).

## Adopted architecture decisions

[Placeholder: no adopted architecture-decision reference is recorded.]

## Evidence and complete record

- Baseline source: [Placeholder: source file is not present in this checkout]
- Aggregate review: [Placeholder: no owner-specific review file is present]
- Full selected record: [JSON](../data/aggregates/firm-staff-identity-claim.json)

> Target definition is separate from runtime qualification. Implementation status recorded by the source: `SEPARATE_VERIFICATION_REQUIRED_IMPLEMENTATION_HALTED`.
