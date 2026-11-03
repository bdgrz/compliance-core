# Member

- Classification: `business_aggregate`
- Source area: `AccessControl` (derived from source path; not a bounded-context ruling).
- Business aggregate: `true`
- Selected status: `SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED`


## Identity and invariant

**Selected identity:** Existing UUIDv5 of TenantId + UserId; membership episodes remain distinct and exact. Selected CommandId binds the original member/episode and intended transition.

**Invariant:** One tenant-user membership has one affiliation/expiry episode; deprovision closes authority and a fresh episode cannot start until authority cleanup completes.

**Additional decision rule:** Affiliation is exactly one of client_personnel, firm_staff, or guest; a guest requires a finite end date, persisted on the registration, access stops at the end of that UTC date, and grants cannot extend beyond it (guests are limited to Org Admin-granted programs and hold no approval rights). Deprovision records actor, timestamp, and reason, refuses self-deprovisioning, and removes team assignments and member-scoped grants prospectively while retaining platform identity, Person, history, business records, and unfinished work. A new invitation never restores old assignments or grants; each direct grant is bound to the episode active when issued, so prior-episode grants cannot become effective after rejoining.

**Must preserve:** [Placeholder: preservation rule not recorded.]

**Replay:** Current required authorization precedes replay; exact authorized retries resolve the original accepted result using the selected logical identity and immutable actor/intent/source binding. Fresh mutable feasibility, clock selection and body preparation cannot replace that original result. Expected revision is concurrency precondition, not a new identity.

**Lifecycle:** [Placeholder: a consolidated lifecycle sequence is not present in the selected owner record.]





## Commands

| Command | Kind | Identity field |
| --- | --- | --- |
| [CompleteMemberRetirement](../commands/member.md#completememberretirement) | command | CommandId |
| [RegisterMember](../commands/member.md#registermember) | command | CommandId |
| [SuspendMember](../commands/member.md#suspendmember) | http | CommandId |
| [DeprovisionMember](../commands/member.md#deprovisionmember) | http | CommandId |
| [ReinstateMember](../commands/member.md#reinstatemember) | http | CommandId |
| [ApplyMemberWithdrawal](../commands/member.md#applymemberwithdrawal) | receiver | CommandId |
| [ApplyMemberStatusChange](../commands/member.md#applymemberstatuschange) | receiver | CommandId |
| [ApplyMemberRestoration](../commands/member.md#applymemberrestoration) | receiver | CommandId |

## Events

| Event | Discriminator | Version |
| --- | --- | --- |
| [MemberAccessAndAssignmentsRemoved](../events/member.md#memberaccessandassignmentsremoved) | bdgrz.rbac.member.deprovision_cleanup_completed | 2 |
| [MemberDeprovisioned](../events/member.md#memberdeprovisioned) | bdgrz.rbac.member.deprovisioned | 2 |
| [MemberRegistered](../events/member.md#memberregistered) | bdgrz.rbac.member.registered | 1 |
| [MemberReinstated](../events/member.md#memberreinstated) | bdgrz.rbac.member.reinstated | 1 |
| [MemberSuspended](../events/member.md#membersuspended) | bdgrz.rbac.member.suspended | 1 |

## Cross-owner flows

| Row | Kind | Source owner | Target owner | Command |
| --- | --- | --- | --- | --- |
| [T046](../reactors/administrator-withdrawal-reactor.md#t046) | reaction_or_regular_process_stage | TenantAdministratorContinuity | Member | ApplyMemberWithdrawal |
| [T048](../reactors/apply-accepted-member-status-change-reactor.md#t048) | reaction_or_regular_process_stage | TenantAdministratorContinuity | Member | ApplyMemberStatusChange |
| [T051](../reactors/administrator-restoration-reactor.md#t051) | reaction_or_regular_process_stage | TenantAdministratorContinuity | Member | ApplyMemberRestoration |
| [S003](../reactors/remove-member-assignments-and-access-after-membership-ends-reactor.md#s003) | business_receiver | Member | Member | CompleteMemberRetirement |
| [S004](../reactors/remove-member-assignments-and-access-after-membership-ends-reactor.md#s004) | business_receiver | Member | TeamMember | RemoveRetiredMemberAccessAndAssignments |
| [S005](../reactors/remove-member-assignments-and-access-after-membership-ends-reactor.md#s005) | business_receiver | Member | AccessGrant | RemoveRetiredMemberAccessAndAssignments |
| [S013](../reactors/set-up-starting-tenant-access-reactor.md#s013) | business_receiver | Tenant | Member | RegisterMember |
| [S026](../reactors/activate-membership-after-invitation-acceptance-reactor.md#s026) | business_receiver | TenantInvitation | Member | RegisterMember |

## Product rules

- **D14-02** — An external consultant hired directly by a client gets a guest membership limited to programs the Org Admin grants, with no approval rights and a required end date. Decision: [m0-d14-advisor-collaboration.md](../decisions/m0-d14-advisor-collaboration.md).
- **D14-04** — The guest end date is persisted on the guest Member registration, access stops at the end of that UTC date, and later grant proposals cannot extend beyond it. Decision: [m0-d14-advisor-collaboration.md](../decisions/m0-d14-advisor-collaboration.md).
- **D25-03** — Each Membership records exactly one affiliation: client_personnel, firm_staff, or guest. Decision: [m0-d25-client-tenancy.md](../decisions/m0-d25-client-tenancy.md).
- **MD-01** — Deprovisioning permanently terminates one membership episode in one tenant; it is terminal and distinct from reversible suspension, and a deprovisioned person cannot be reinstated. Decision: [member-deprovisioning.md](../decisions/member-deprovisioning.md).
- **MD-02** — A fresh invitation after deprovisioning creates a new membership episode, and only after the prior episode's tenant authority cleanup completes. Decision: [member-deprovisioning.md](../decisions/member-deprovisioning.md).
- **MD-05** — The deprovision command records actor, timestamp, and reason. Decision: [member-deprovisioning.md](../decisions/member-deprovisioning.md).
- **MD-07** — A new invitation never restores old team assignments or member-scoped grants; each direct member grant is bound to the membership episode active when issued, so retained or delayed grant history from a prior episode cannot become effective after rejoining. Decision: [member-deprovisioning.md](../decisions/member-deprovisioning.md).

## Adopted architecture decisions

[Placeholder: no adopted architecture-decision reference is recorded.]

## Evidence and complete record

- Baseline source: [Placeholder: source file is not present in this checkout]
- Aggregate review: [Placeholder: no owner-specific review file is present]
- Full selected record: [JSON](../data/aggregates/member.json)

> Target definition is separate from runtime qualification. Implementation status recorded by the source: `SEPARATE_VERIFICATION_REQUIRED_IMPLEMENTATION_HALTED`.
