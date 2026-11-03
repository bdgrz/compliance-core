# AccessGrant

- Classification: `business_aggregate`
- Source area: `AccessControl` (derived from source path; not a bounded-context ruling).
- Business aggregate: `true`
- Selected status: `SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED`

## Identity and invariant

**Selected identity:** Tenant + GrantId; immutable grant identity and exact issuance episode. Mutations use the selected CommandId with the original grant and episode binding.

**Invariant:** One stable grant identity has one immutable set of scope/principal/effective terms and, once revoked, can never be reissued.

**Additional decision rule:** A grant is issued for exactly one scope level (organization, program, engagement or deliberately shared resource); the scope id is immutable once issued. Coverage of narrower scopes by a wider grant is evaluated by the authorizer from the grant's scope, not stored on or decided by the grant.

**Must preserve:** [Placeholder: preservation rule not recorded.]

**Replay:** Current required authorization precedes replay; exact authorized retries resolve the original accepted result using the selected logical identity and immutable actor/intent/source binding. Fresh mutable feasibility, clock selection and body preparation cannot replace that original result. Expected revision is concurrency precondition, not a new identity.

**Lifecycle:** [Placeholder: a consolidated lifecycle sequence is not present in the selected owner record.]

## Commands

| Command | Kind | Identity field |
| --- | --- | --- |
| [GrantAccess](../commands/access-grant.md#grantaccess) | http | GrantId |
| [ProvisionGuestProgramAccess](../commands/access-grant.md#provisionguestprogramaccess) | command | CommandId |
| [RevokeAccessGrant](../commands/access-grant.md#revokeaccessgrant) | command | CommandId |

## Events

| Event | Discriminator | Version |
| --- | --- | --- |
| [AccessGrantIssued](../events/access-grant.md#accessgrantissued) | bdgrz.access-grant.issued | 1 |
| [AccessGrantRevoked](../events/access-grant.md#accessgrantrevoked) | bdgrz.access-grant.revoked | 1 |

## Cross-owner flows

| Row | Kind | Source owner | Target owner | Command |
| --- | --- | --- | --- | --- |
| [S005](../reactors/member-retirement-reactor.md#s005) | business_receiver | Member | AccessGrant | RemoveRetiredMemberAccessAndAssignments |
| [S006](../reactors/retired-member-grant-reactor.md#s006) | business_receiver | AccessGrant | AccessGrant | RemoveRetiredMemberAccessAndAssignments |
| [S028](../reactors/tenant-invitation-reactor.md#s028) | business_receiver | TenantInvitation | AccessGrant | ProvisionGuestProgramAccess |

## Product rules

[Placeholder: product-rule traceability is not recorded for this owner.]

## Adopted architecture decisions

[Placeholder: no adopted architecture-decision reference is recorded.]

## Evidence and complete record

- Baseline source: [Placeholder: source file is not present in this checkout]
- Aggregate review: [access-grant-review-2026-10-29.md](../../product/definitive-aggregate-choreography/access-grant-review-2026-10-29.md)
- Full selected record: [JSON](../data/aggregates/access-grant.json)

> Target definition is separate from runtime qualification. Implementation status recorded by the source: `SEPARATE_VERIFICATION_REQUIRED_IMPLEMENTATION_HALTED`.
