# PlatformOperatorRoster

- Classification: `business_governor`
- Source area: `Tenants` (derived from source path; not a bounded-context ruling).
- Business aggregate: `true`
- Selected status: `SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED`

## Identity and invariant

**Selected identity:** Existing fixed platform roster ID bc279ad5-e9ae-5033-a174-4d1350ce2b07. The complete current UserId/AssignmentId episode set is one authority; selected RosterId must match the fixed roster and mutations use CommandId.

**Invariant:** One atomic platform roster decision binds every exact active operator identity and assignment episode, current acting-operator eligibility, and the last-operator prohibition. Keep all current identities in verified immutable authority with native OCC; no count-only substitute, membership cap, or separate PlatformOperatorGrant root.

**Additional decision rule:** Grant and revoke by an existing operator each record actor, subject, time, and reason with no two-person rule. Configured operator IDs seed the roster only while the platform has no operator; afterward in-app grants and revocations are authoritative and a configured ID never re-grants a revoked operator on restart.

**Must preserve:** [Placeholder: preservation rule not recorded.]

**Replay:** Current required authorization precedes replay; exact authorized retries resolve the original accepted result using the selected logical identity and immutable actor/intent/source binding. Fresh mutable feasibility, clock selection and body preparation cannot replace that original result. Expected revision is concurrency precondition, not a new identity.

**Lifecycle:** [Placeholder: a consolidated lifecycle sequence is not present in the selected owner record.]

## Commands

| Command | Kind | Identity field |
| --- | --- | --- |
| [GrantPlatformOperator](../commands/platform-operator-roster.md#grantplatformoperator) | command | CommandId |
| [RevokePlatformOperator](../commands/platform-operator-roster.md#revokeplatformoperator) | command | CommandId |
| [CreatePlatformOperatorRoster](../commands/platform-operator-roster.md#createplatformoperatorroster) | creation | RosterId |

## Events

| Event | Discriminator | Version |
| --- | --- | --- |
| [PlatformOperatorGranted](../events/platform-operator-roster.md#platformoperatorgranted) | bdgrz.platform.operator.granted | 1 |
| [PlatformOperatorRevoked](../events/platform-operator-roster.md#platformoperatorrevoked) | bdgrz.platform.operator.revoked | 1 |
| [PlatformOperatorRosterCreated](../events/platform-operator-roster.md#platformoperatorrostercreated) | bdgrz.platform.operator-roster.seeded | 1 |

## Cross-owner flows

[Placeholder: no choreography row names this owner.]

## Product rules

- **D25-11** — An existing operator grants or revokes operator status in the product, each audit logged with actor, subject, time, and reason, with no two-person rule. Source: [m0-d25-client-tenancy.md](../../product/decisions/m0-d25-client-tenancy.md).
- **D25-12** — Configured operator IDs bootstrap the operator roster only while the platform has no operator; afterward in-app grants and revocations are authoritative and a configured ID never re-grants a revoked operator on restart. Source: [m0-d25-client-tenancy.md](../../product/decisions/m0-d25-client-tenancy.md).
- **D25-13** — The last remaining platform operator cannot be revoked. Source: [m0-d25-client-tenancy.md](../../product/decisions/m0-d25-client-tenancy.md).

## Adopted architecture decisions

- `OWNER_PlatformOperatorGrant` — one_roster_authority.

## Evidence and complete record

- Baseline source: [Placeholder: source file is not present in this checkout]
- Aggregate review: [platform-operator-roster-review-2026-10-29.md](../../product/definitive-aggregate-choreography/platform-operator-roster-review-2026-10-29.md)
- Full selected record: [JSON](../data/aggregates/platform-operator-roster.json)

> Target definition is separate from runtime qualification. Implementation status recorded by the source: `SEPARATE_VERIFICATION_REQUIRED_IMPLEMENTATION_HALTED`.
