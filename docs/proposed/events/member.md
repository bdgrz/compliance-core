# Member events

Canonical events: **5**. Complete canonical event and available schema records: [owner event JSON](../data/events/member.json).

| Target event | Selected source name | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- | --- |
| MemberAccessAndAssignmentsRemoved | MemberDeprovisionCleanupCompleted | business | bdgrz.rbac.member.deprovision_cleanup_completed | 2 | [Placeholder: schema absent] | Cleanup of a deprovisioned member's access authority for one membership episode was completed against the original termination proof. |
| MemberDeprovisioned | MemberDeprovisioned | business | bdgrz.rbac.member.deprovisioned | 2 | 12 | A member's membership episode was ended by deprovisioning, with a stated reason by a named actor, removing the member's access to the tenant. |
| MemberRegistered | MemberRegistered | business | bdgrz.rbac.member.registered | 1 | [Placeholder: schema absent] | A member was registered. The registration's logical CommandId is the membership episode id, and the fact is the native acceptance of that logical operation on the member stream. |
| MemberReinstated | MemberReinstated | business | bdgrz.rbac.member.reinstated | 1 | 11 | A suspended member was reinstated with their affiliation by a named actor, restoring their access to the tenant. |
| MemberSuspended | MemberSuspended | business | bdgrz.rbac.member.suspended | 1 | 11 | A member was suspended with a stated reason by a named actor, blocking their access until reinstated. |

## Event details

### MemberAccessAndAssignmentsRemoved

| Canonical event detail | Value |
| --- | --- |
| Target event name | MemberAccessAndAssignmentsRemoved |
| Selected source event name | MemberDeprovisionCleanupCompleted |
| Name decision | Names the completed removal of direct access and team assignments for the exact deprovisioned membership episode. |
| Canonical semantic key | event:MemberDeprovisionCleanupCompleted |
| Owner selection | one exact canonical authority owner |
| Target concrete type | MemberDeprovisionCleanupCompleted |
| Adopted wire | `{"discriminator":"bdgrz.rbac.member.deprovision_cleanup_completed","version":2}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/AccessControl/Events/MemberDeprovisionCleanupCompleted.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | Cleanup of a deprovisioned member's access authority for one membership episode was completed against the original termination proof. |
| Selected source meaning | Cleanup of a deprovisioned member's access authority for one membership episode was completed against the original termination proof. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/AccessControl/Events/MemberDeprovisionCleanupCompleted.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [S003](../reactors/member-retirement-reactor.md#s003) | emitted semantic fact | Member | CompleteMemberRetirement | Member |

### MemberDeprovisioned

| Canonical event detail | Value |
| --- | --- |
| Target event name | MemberDeprovisioned |
| Selected source event name | MemberDeprovisioned |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:MemberDeprovisioned |
| Owner selection | one exact canonical authority owner |
| Target concrete type | MemberDeprovisioned |
| Adopted wire | `{"discriminator":"bdgrz.rbac.member.deprovisioned","version":2}` |
| Wire qualification | Adopted wire is the occupied discriminator declared in src/Compliance.Common/Features/AccessControl/Events/MemberDeprovisioned.cs at 60029b8f (2026-10-29 reconciliation). The earlier selected wire is retained as supersededSelectedWire for provenance only. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | `{"discriminator":"bdgrz.rbac.member.deprovisioned","version":1}` |
| Target meaning | A member's membership episode was ended by deprovisioning, with a stated reason by a named actor, removing the member's access to the tenant. |
| Selected source meaning | A member's membership episode was ended by deprovisioning, with a stated reason by a named actor, removing the member's access to the tenant. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/AccessControl/Events/MemberDeprovisioned.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | MemberDeprovisioned |
| Schema discriminator | bdgrz.rbac.member.deprovisioned |
| Schema discriminatorVersion | 1 |
| Schema owner | Member |
| Schema existingContract | src/Compliance.Common/Features/AccessControl/MemberDeprovisioned.cs |
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
| MemberId | Uuid | true |
| UserId | Uuid | true |
| DeprovisionedByMemberId | Uuid | true |
| DeprovisionedByDisplay | string | true |
| DeprovisionedAt | DateTimeOffset | true |
| Reason | string | true |
| MembershipEpisodeId | Uuid | true |
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
| [T046](../reactors/administrator-withdrawal-reactor.md#t046) | emitted semantic fact | TenantManagerGuard | ApplyMemberWithdrawal | Member |
| [T048](../reactors/administrator-neutral-member-mutation-reactor.md#t048) | emitted semantic fact | TenantManagerGuard | ApplyMemberStatusChange | Member |
| [T050](../reactors/administrator-withdrawal-reactor.md#t050) | logical prerequisite | Member\|TeamMember | CompleteAdministratorWithdrawal | TenantManagerGuard |
| [S003](../reactors/member-retirement-reactor.md#s003) | physical root, logical prerequisite | Member | CompleteMemberRetirement | Member |
| [S004](../reactors/member-retirement-reactor.md#s004) | physical root, logical prerequisite | Member | RemoveRetiredMemberAccessAndAssignments | TeamMember |
| [S005](../reactors/member-retirement-reactor.md#s005) | physical root, logical prerequisite | Member | RemoveRetiredMemberAccessAndAssignments | AccessGrant |

### MemberRegistered

| Canonical event detail | Value |
| --- | --- |
| Target event name | MemberRegistered |
| Selected source event name | MemberRegistered |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:MemberRegistered |
| Owner selection | one exact canonical authority owner |
| Target concrete type | MemberRegistered |
| Adopted wire | `{"discriminator":"bdgrz.rbac.member.registered","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/AccessControl/Events/MemberRegistered.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | A member was registered. The registration's logical CommandId is the membership episode id, and the fact is the native acceptance of that logical operation on the member stream. |
| Selected source meaning | A member was registered. The registration's logical CommandId is the membership episode id, and the fact is the native acceptance of that logical operation on the member stream. |
| Meaning source | record summary in src/Compliance.Common/Features/AccessControl/Events/MemberRegistered.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [S013](../reactors/tenant-rbac-bootstrap-reactor.md#s013) | emitted semantic fact | Tenant | RegisterMember | Member |
| [S026](../reactors/tenant-invitation-reactor.md#s026) | emitted semantic fact | TenantInvitation | RegisterMember | Member |

### MemberReinstated

| Canonical event detail | Value |
| --- | --- |
| Target event name | MemberReinstated |
| Selected source event name | MemberReinstated |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:MemberReinstated |
| Owner selection | one exact canonical authority owner |
| Target concrete type | MemberReinstated |
| Adopted wire | `{"discriminator":"bdgrz.rbac.member.reinstated","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/AccessControl/Events/MemberReinstated.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | A suspended member was reinstated with their affiliation by a named actor, restoring their access to the tenant. |
| Selected source meaning | A suspended member was reinstated with their affiliation by a named actor, restoring their access to the tenant. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/AccessControl/Events/MemberReinstated.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | MemberReinstated |
| Schema discriminator | bdgrz.rbac.member.reinstated |
| Schema discriminatorVersion | 1 |
| Schema owner | Member |
| Schema existingContract | src/Compliance.Common/Features/AccessControl/MemberReinstated.cs |
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
| MemberId | Uuid | true |
| UserId | Uuid | true |
| Affiliation | string | true |
| ReinstatedByMemberId | Uuid | true |
| ReinstatedByDisplay | string | true |
| ReinstatedAt | DateTimeOffset | true |
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
| [T048](../reactors/administrator-neutral-member-mutation-reactor.md#t048) | emitted semantic fact | TenantManagerGuard | ApplyMemberStatusChange | Member |
| [T051](../reactors/administrator-restoration-reactor.md#t051) | emitted semantic fact | TenantManagerGuard | ApplyMemberRestoration | Member |
| [T052](../reactors/administrator-restoration-reactor.md#t052) | logical prerequisite | Member\|TeamMember | CompleteAdministratorRestoration | TenantManagerGuard |

### MemberSuspended

| Canonical event detail | Value |
| --- | --- |
| Target event name | MemberSuspended |
| Selected source event name | MemberSuspended |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:MemberSuspended |
| Owner selection | one exact canonical authority owner |
| Target concrete type | MemberSuspended |
| Adopted wire | `{"discriminator":"bdgrz.rbac.member.suspended","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/AccessControl/Events/MemberSuspended.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | A member was suspended with a stated reason by a named actor, blocking their access until reinstated. |
| Selected source meaning | A member was suspended with a stated reason by a named actor, blocking their access until reinstated. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/AccessControl/Events/MemberSuspended.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | MemberSuspended |
| Schema discriminator | bdgrz.rbac.member.suspended |
| Schema discriminatorVersion | 1 |
| Schema owner | Member |
| Schema existingContract | src/Compliance.Common/Features/AccessControl/MemberSuspended.cs |
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
| MemberId | Uuid | true |
| UserId | Uuid | true |
| SuspendedByMemberId | Uuid | true |
| SuspendedByDisplay | string | true |
| SuspendedAt | DateTimeOffset | true |
| Reason | string | true |
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
| [T046](../reactors/administrator-withdrawal-reactor.md#t046) | emitted semantic fact | TenantManagerGuard | ApplyMemberWithdrawal | Member |
| [T048](../reactors/administrator-neutral-member-mutation-reactor.md#t048) | emitted semantic fact | TenantManagerGuard | ApplyMemberStatusChange | Member |
| [T050](../reactors/administrator-withdrawal-reactor.md#t050) | logical prerequisite | Member\|TeamMember | CompleteAdministratorWithdrawal | TenantManagerGuard |


## Wire/schema version discrepancies

- **MemberDeprovisioned** — [Placeholder: reconcile schema version 1 with adopted version 2].

[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]
