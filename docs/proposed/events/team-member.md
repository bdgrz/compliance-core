# TeamMember events

Canonical events: **2**. Complete canonical event and available schema records: [owner event JSON](../data/events/team-member.json).

| Target event | Selected source name | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- | --- |
| TeamMemberAssigned | TeamMemberAssigned | business | bdgrz.rbac.team-member.assigned | 1 | 8 | A member was assigned to a team for one membership episode. The fact is the native acceptance of the logical CommandId on the team-member stream. |
| TeamMemberRemoved | TeamMemberRemoved | business | bdgrz.rbac.team-member.removed | 2 | 10 | A member's team assignment ended. The fact is the native acceptance of the logical CommandId on the team-member stream; a retirement removal carries the termination event that proves the closed membership episode. |

## Event details

### TeamMemberAssigned

| Canonical event detail | Value |
| --- | --- |
| Target event name | TeamMemberAssigned |
| Selected source event name | TeamMemberAssigned |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:TeamMemberAssigned |
| Owner selection | one exact canonical authority owner |
| Target concrete type | TeamMemberAssigned |
| Adopted wire | `{"discriminator":"bdgrz.rbac.team-member.assigned","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/AccessControl/Events/TeamMemberAssigned.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | A member was assigned to a team for one membership episode. The fact is the native acceptance of the logical CommandId on the team-member stream. |
| Selected source meaning | A member was assigned to a team for one membership episode. The fact is the native acceptance of the logical CommandId on the team-member stream. |
| Meaning source | record summary in src/Compliance.Common/Features/AccessControl/Events/TeamMemberAssigned.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | TeamMemberAssigned |
| Schema discriminator | bdgrz.rbac.team-member.assigned |
| Schema discriminatorVersion | 1 |
| Schema owner | TeamMember |
| Schema existingContract | src/Compliance.Common/Features/AccessControl/TeamMemberAssigned.cs |
| Schema status | selected target schema; implementation pending |
| Schema reason | typed reactor receiver accepted business fact |
| Schema changes | None listed |
| Schema reducerRule | Validate owner/generation and local predecessor; apply only this owner delta or immutable content reference. Never hydrate a foreign aggregate or mutate based on a projection. |
| Schema serializer | Register concrete target event and every proof/content variant in ComplianceCoreJsonContext and attributed persistence preflight coverage. |
| Schema nativeResponseRule | FirstResponse is non-null only on this logical command first native accepting fact; it references FirstResponseDescriptor. OperationResult is non-null on the declared terminal business fact and references MutationResultContent. Both use verified content and static concrete result serializers; neither is reconstructed from current state. |

#### Event fields

| Field | Type | Required |
| --- | --- | --- |
| TenantId | Uuid | true |
| TeamId | Uuid | true |
| MemberId | Uuid | true |
| MembershipEpisodeId | Uuid | false |
| Operation | OperationProvenance | true |
| Intent | ImmutableContentRef | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

#### Nested content schemas

#### Content schema: administrator-transition-intent.v1

| Property | Value |
| --- | --- |
| field | Intent |
| schema | administrator-transition-intent.v1 |
| recordType | AdministratorTransitionIntent |
| rule | Member source revision and guard generation are distinct. Freeze administrators-team scope and exact transition branch; TransitionKind includes register_administrators_assignment for a first contribution and the neutral kinds for source mutations that change no availability. Withdraw availability before source mutation; restore source after unfinished withdrawals drain, then restore guard only if all current eligibility and remaining reason checks pass. |

##### Fields

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| MemberId | Uuid | true |
| MemberEpisodeId | Uuid | true |
| MemberRevision | ulong | true |
| AssignmentId | Uuid? | true |
| AssignmentGeneration | ulong? | true |
| ExpectedAssignmentRevision | ulong? | true |
| TransitionKind | AdministratorTransitionKind | true |
| Reason | string? | true |
| GuardGeneration | ulong | true |
| EligibilityRequirements | ImmutableSelectionManifest | true |

##### Variant: AdministratorTransitionIntent

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| MemberId | Uuid | true |
| MemberEpisodeId | Uuid | true |
| MemberRevision | ulong | true |
| AssignmentId | Uuid? | true |
| AssignmentGeneration | ulong? | true |
| ExpectedAssignmentRevision | ulong? | true |
| TransitionKind | AdministratorTransitionKind | true |
| Reason | string? | true |
| GuardGeneration | ulong | true |
| EligibilityRequirements | ImmutableSelectionManifest | true |


#### Resolved intent schema

AdministratorTransitionIntent

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [T052](../reactors/administrator-restoration-reactor.md#t052) | logical prerequisite | Member\|TeamMember | CompleteAdministratorRestoration | TenantManagerGuard |
| [T098](../reactors/administrator-assignment-restoration-reactor.md#t098) | emitted semantic fact | TenantManagerGuard | ApplyAdministratorAssignmentRestoration | TeamMember |
| [S007](../reactors/retired-team-assignment-reactor.md#s007) | physical root, logical prerequisite | TeamMember | RemoveRetiredMemberAccessAndAssignments | TeamMember |
| [S016](../reactors/tenant-rbac-bootstrap-reactor.md#s016) | emitted semantic fact | Tenant | AssignTeamMember | TeamMember |
| [S027](../reactors/tenant-invitation-reactor.md#s027) | emitted semantic fact | TenantInvitation | AssignTeamMember | TeamMember |
| [S034](../reactors/tenant-self-service-activation-reactor.md#s034) | physical root, logical prerequisite | TeamMember | ActivateTenant | Tenant |

### TeamMemberRemoved

| Canonical event detail | Value |
| --- | --- |
| Target event name | TeamMemberRemoved |
| Selected source event name | TeamMemberRemoved |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:TeamMemberRemoved |
| Owner selection | one exact canonical authority owner |
| Target concrete type | TeamMemberRemoved |
| Adopted wire | `{"discriminator":"bdgrz.rbac.team-member.removed","version":2}` |
| Wire qualification | Adopted wire is the occupied discriminator declared in src/Compliance.Common/Features/AccessControl/Events/TeamMemberRemoved.cs at 60029b8f (2026-10-29 reconciliation). The earlier selected wire is retained as supersededSelectedWire for provenance only. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | `{"discriminator":"bdgrz.rbac.team-member.removed","version":1}` |
| Target meaning | A member's team assignment ended. The fact is the native acceptance of the logical CommandId on the team-member stream; a retirement removal carries the termination event that proves the closed membership episode. |
| Selected source meaning | A member's team assignment ended. The fact is the native acceptance of the logical CommandId on the team-member stream; a retirement removal carries the termination event that proves the closed membership episode. |
| Meaning source | record summary in src/Compliance.Common/Features/AccessControl/Events/TeamMemberRemoved.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | TeamMemberRemoved |
| Schema discriminator | bdgrz.rbac.team-member.removed |
| Schema discriminatorVersion | 1 |
| Schema owner | TeamMember |
| Schema existingContract | src/Compliance.Common/Features/AccessControl/TeamMemberRemoved.cs |
| Schema status | selected target schema; implementation pending |
| Schema reason | typed reactor receiver accepted business fact |
| Schema changes | None listed |
| Schema reducerRule | Validate owner/generation and local predecessor; apply only this owner delta or immutable content reference. Never hydrate a foreign aggregate or mutate based on a projection. |
| Schema serializer | Register concrete target event and every proof/content variant in ComplianceCoreJsonContext and attributed persistence preflight coverage. |
| Schema nativeResponseRule | FirstResponse is non-null only on this logical command first native accepting fact; it references FirstResponseDescriptor. OperationResult is non-null on the declared terminal business fact and references MutationResultContent. Both use verified content and static concrete result serializers; neither is reconstructed from current state. |

#### Event fields

| Field | Type | Required |
| --- | --- | --- |
| TenantId | Uuid | true |
| TeamId | Uuid | true |
| MemberId | Uuid | true |
| MembershipEpisodeId | Uuid | true |
| AssignmentEventId | Uuid | true |
| TerminationEventId | Uuid? | false |
| Operation | OperationProvenance | true |
| Intent | ImmutableContentRef | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

#### Nested content schemas

#### Content schema: administrator-transition-intent.v1

| Property | Value |
| --- | --- |
| field | Intent |
| schema | administrator-transition-intent.v1 |
| recordType | AdministratorTransitionIntent |
| rule | Member source revision and guard generation are distinct. Freeze administrators-team scope and exact transition branch; TransitionKind includes register_administrators_assignment for a first contribution and the neutral kinds for source mutations that change no availability. Withdraw availability before source mutation; restore source after unfinished withdrawals drain, then restore guard only if all current eligibility and remaining reason checks pass. |

##### Fields

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| MemberId | Uuid | true |
| MemberEpisodeId | Uuid | true |
| MemberRevision | ulong | true |
| AssignmentId | Uuid? | true |
| AssignmentGeneration | ulong? | true |
| ExpectedAssignmentRevision | ulong? | true |
| TransitionKind | AdministratorTransitionKind | true |
| Reason | string? | true |
| GuardGeneration | ulong | true |
| EligibilityRequirements | ImmutableSelectionManifest | true |

##### Variant: AdministratorTransitionIntent

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| MemberId | Uuid | true |
| MemberEpisodeId | Uuid | true |
| MemberRevision | ulong | true |
| AssignmentId | Uuid? | true |
| AssignmentGeneration | ulong? | true |
| ExpectedAssignmentRevision | ulong? | true |
| TransitionKind | AdministratorTransitionKind | true |
| Reason | string? | true |
| GuardGeneration | ulong | true |
| EligibilityRequirements | ImmutableSelectionManifest | true |


#### Resolved intent schema

AdministratorTransitionIntent

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [T047](../reactors/administrator-assignment-withdrawal-reactor.md#t047) | emitted semantic fact | TenantManagerGuard | ApplyAdministratorAssignmentWithdrawal | TeamMember |
| [T049](../reactors/administrator-neutral-assignment-mutation-reactor.md#t049) | emitted semantic fact | TenantManagerGuard | RemoveAdministratorAssignment | TeamMember |
| [T050](../reactors/administrator-withdrawal-reactor.md#t050) | logical prerequisite | Member\|TeamMember | CompleteAdministratorWithdrawal | TenantManagerGuard |
| [S004](../reactors/member-retirement-reactor.md#s004) | emitted semantic fact | Member | RemoveRetiredMemberAccessAndAssignments | TeamMember |
| [S007](../reactors/retired-team-assignment-reactor.md#s007) | emitted semantic fact | TeamMember | RemoveRetiredMemberAccessAndAssignments | TeamMember |
| [S010](../reactors/team-cleanup-reactor.md#s010) | emitted semantic fact | Team | RemoveTeamMember | TeamMember |


## Wire/schema version discrepancies

- **TeamMemberRemoved** — [Placeholder: reconcile schema version 1 with adopted version 2].

[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]
