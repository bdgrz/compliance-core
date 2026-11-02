# Team

- Classification: `business_aggregate`
- Source area: `AccessControl` (derived from source path; not a bounded-context ruling).
- Business aggregate: `true`
- Selected status: `SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED`

## Identity and invariant

**Selected identity:** Tenant + TeamId; TeamId is immutable creation identity. Selected mutations use CommandId and the exact team binding.

**Invariant:** One team definition has an active/deleted lifecycle and built-in team cannot be deleted.

**Additional decision rule:** [Placeholder: no owner-specific decision rule is recorded.]

**Must preserve:** [Placeholder: preservation rule not recorded.]

**Replay:** Current required authorization precedes replay; exact authorized retries resolve the original accepted result using the selected logical identity and immutable actor/intent/source binding. Fresh mutable feasibility, clock selection and body preparation cannot replace that original result. Expected revision is concurrency precondition, not a new identity.

**Lifecycle:** [Placeholder: a consolidated lifecycle sequence is not present in the selected owner record.]

## Commands

| Command | Kind | Identity field |
| --- | --- | --- |
| [DefineTeam](../commands/team.md#defineteam) | http | TeamId |
| [DeleteTeam](../commands/team.md#deleteteam) | command | CommandId |

## Events

| Event | Discriminator | Version |
| --- | --- | --- |
| [TeamDefined](../events/team.md#teamdefined) | bdgrz.rbac.team.defined | 1 |
| [TeamDeleted](../events/team.md#teamdeleted) | bdgrz.rbac.team.deleted | 1 |

## Cross-owner flows

| Row | Kind | Source owner | Target owner | Command |
| --- | --- | --- | --- | --- |
| [S010](../reactors/team-cleanup-reactor.md#s010) | business_receiver | Team | TeamMember | RemoveTeamMember |
| [S012](../reactors/tenant-rbac-bootstrap-reactor.md#s012) | business_receiver | Tenant | Team | DefineTeam |

## Product rules

[Placeholder: product-rule traceability is not recorded for this owner.]

## Adopted architecture decisions

[Placeholder: no adopted architecture-decision reference is recorded.]

## Evidence and complete record

- Baseline source: [Placeholder: source file is not present in this checkout]
- Aggregate review: [Placeholder: no owner-specific review file is present]
- Full selected record: [JSON](../data/aggregates/team.json)

> Target definition is separate from runtime qualification. Implementation status recorded by the source: `SEPARATE_VERIFICATION_REQUIRED_IMPLEMENTATION_HALTED`.
