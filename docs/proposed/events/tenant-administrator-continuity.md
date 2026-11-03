# TenantAdministratorContinuity events

Canonical events: **6**. Complete canonical event and available schema records: [owner event JSON](../data/events/tenant-administrator-continuity.json).

| Target event | Selected source name | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- | --- |
| TenantAdministratorRestorationAuthorized | TenantManagerRestorationAccepted | business | bdgrz.tenant-manager-guard.tenant-manager-restoration-accepted | 2 | 8 | A first or restored administrator assignment was authorized while preserving the rule that the workspace keeps an active administrator; the assignment follows. |
| TenantManagerRestorationCompleted | TenantManagerRestorationCompleted | business | bdgrz.tenant-manager-guard.tenant-manager-restoration-completed | 2 | 8 | The source restoration a restoration operation admitted was applied; availability is restored separately, and only when no other reason remains. Owner is the source stream the operation mutates, Revision the authored revision of that owner (negative when unconstrained) and Generation the guard generation of this fact. |
| TenantManagerRestored | TenantManagerRestored | business | bdgrz.rbac.manager_guard.restored | 2 | 7 | The guard restored a member's availability as an administrator for Reason after the exact source restoration and current eligibility were proven. |
| TenantMemberChangeAuthorized | TenantManagerSourceMutationAdmitted | business | bdgrz.tenant-manager-guard.tenant-manager-source-mutation-admitted | 2 | 8 | A tenant member change was authorized because it does not affect the number of active tenant administrators. The member change itself is recorded afterward. |
| TenantAdministratorWithdrawalAuthorized | TenantManagerWithdrawalAccepted | business | bdgrz.tenant-manager-guard.tenant-manager-withdrawal-accepted | 2 | 8 | Removal of an administrator contribution was authorized because another active tenant administrator remains; the source membership change follows. |
| TenantManagerWithdrawalCompleted | TenantManagerWithdrawalCompleted | business | bdgrz.tenant-manager-guard.tenant-manager-withdrawal-completed | 2 | 8 | The source change a withdrawal admitted was applied; the withdrawal operation is settled and its reason stays withheld. Owner is the source stream the operation mutates, Revision the authored revision of that owner (negative when unconstrained) and Generation the guard generation of this fact. |

## Event details

### TenantAdministratorRestorationAuthorized

| Canonical event detail | Value |
| --- | --- |
| Target event name | TenantAdministratorRestorationAuthorized |
| Selected source event name | TenantManagerRestorationAccepted |
| Name decision | Names the completed authorization for a first or restored administrator assignment. |
| Canonical semantic key | event:TenantManagerRestorationAccepted |
| Owner selection | one exact canonical authority owner |
| Target concrete type | TenantManagerRestorationAccepted |
| Adopted wire | `{"discriminator":"bdgrz.tenant-manager-guard.tenant-manager-restoration-accepted","version":2}` |
| Wire qualification | Adopted wire is the occupied discriminator declared in src/Compliance.Common/Features/AccessControl/Events/TenantManagerRestorationAccepted.cs at 60029b8f (2026-10-29 reconciliation). The earlier selected wire is retained as supersededSelectedWire for provenance only. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | `{"discriminator":"bdgrz.tenant-manager-guard.tenant-manager-restoration-accepted","version":1}` |
| Target meaning | A first or restored administrator assignment was authorized while preserving the rule that the workspace keeps an active administrator; the assignment follows. |
| Selected source meaning | The tenant manager guard admitted the restoration, or first registration, of an administrator's contribution to the tenant's manager count, so that availability is counted before its source change is made. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/AccessControl/Events/TenantManagerRestorationAccepted.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | TenantManagerRestorationAccepted |
| Schema discriminator | bdgrz.tenant-manager-guard.tenant-manager-restoration-accepted |
| Schema discriminatorVersion | 1 |
| Schema owner | TenantManagerGuard |
| Schema status | selected target schema; implementation pending |
| Schema reason | source admission contract |
| Schema changes | None listed |
| Schema reducerRule | Validate owner/generation and local predecessor; apply only this owner delta or immutable content reference. Never hydrate a foreign aggregate or mutate based on a projection. |
| Schema serializer | Register concrete target event and every proof/content variant in ComplianceCoreJsonContext and attributed persistence preflight coverage. |
| Schema nativeResponseRule | FirstResponse is non-null only on this logical command first native accepting fact; it references FirstResponseDescriptor. OperationResult is non-null on the declared terminal business fact and references MutationResultContent. Both use verified content and static concrete result serializers; neither is reconstructed from current state. |

#### Event fields

| Field | Type | Required |
| --- | --- | --- |
| Owner | EventStreamAddress | true |
| Revision | long | true |
| Generation | long | true |
| Intent | ImmutableContentRef | true |
| SourceProofs | ImmutableContentRef? | true |
| Operation | OperationProvenance | true |
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
| [T051](../reactors/administrator-restoration-reactor.md#t051) | physical root, logical prerequisite | TenantAdministratorContinuity | ApplyMemberRestoration | Member |
| [T052](../reactors/administrator-restoration-reactor.md#t052) | physical root | Member\|TeamMember | CompleteAdministratorRestoration | TenantAdministratorContinuity |
| [T098](../reactors/administrator-assignment-restoration-reactor.md#t098) | physical root, logical prerequisite | TenantAdministratorContinuity | ApplyAdministratorAssignmentRestoration | TeamMember |

### TenantManagerRestorationCompleted

| Canonical event detail | Value |
| --- | --- |
| Target event name | TenantManagerRestorationCompleted |
| Selected source event name | TenantManagerRestorationCompleted |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:TenantManagerRestorationCompleted |
| Owner selection | one exact canonical authority owner |
| Target concrete type | TenantManagerRestorationCompleted |
| Adopted wire | `{"discriminator":"bdgrz.tenant-manager-guard.tenant-manager-restoration-completed","version":2}` |
| Wire qualification | Adopted wire is the occupied discriminator declared in src/Compliance.Common/Features/AccessControl/Events/TenantManagerRestorationCompleted.cs at 60029b8f (2026-10-29 reconciliation). The earlier selected wire is retained as supersededSelectedWire for provenance only. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | `{"discriminator":"bdgrz.tenant-manager-guard.tenant-manager-restoration-completed","version":1}` |
| Target meaning | The source restoration a restoration operation admitted was applied; availability is restored separately, and only when no other reason remains. Owner is the source stream the operation mutates, Revision the authored revision of that owner (negative when unconstrained) and Generation the guard generation of this fact. |
| Selected source meaning | The source restoration a restoration operation admitted was applied; availability is restored separately, and only when no other reason remains. Owner is the source stream the operation mutates, Revision the authored revision of that owner (negative when unconstrained) and Generation the guard generation of this fact. |
| Meaning source | record summary in src/Compliance.Common/Features/AccessControl/Events/TenantManagerRestorationCompleted.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | TenantManagerRestorationCompleted |
| Schema discriminator | bdgrz.tenant-manager-guard.tenant-manager-restoration-completed |
| Schema discriminatorVersion | 1 |
| Schema owner | TenantManagerGuard |
| Schema status | selected target schema; implementation pending |
| Schema reason | typed reactor receiver accepted business fact |
| Schema changes | None listed |
| Schema reducerRule | Validate owner/generation and local predecessor; apply only this owner delta or immutable content reference. Never hydrate a foreign aggregate or mutate based on a projection. |
| Schema serializer | Register concrete target event and every proof/content variant in ComplianceCoreJsonContext and attributed persistence preflight coverage. |
| Schema nativeResponseRule | FirstResponse is non-null only on this logical command first native accepting fact; it references FirstResponseDescriptor. OperationResult is non-null on the declared terminal business fact and references MutationResultContent. Both use verified content and static concrete result serializers; neither is reconstructed from current state. |

#### Event fields

| Field | Type | Required |
| --- | --- | --- |
| Owner | EventStreamAddress | true |
| Revision | long | true |
| Generation | long | true |
| Intent | ImmutableContentRef | true |
| SourceProofs | ImmutableContentRef? | true |
| Operation | OperationProvenance | true |
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
| [T052](../reactors/administrator-restoration-reactor.md#t052) | emitted semantic fact | Member\|TeamMember | CompleteAdministratorRestoration | TenantAdministratorContinuity |

### TenantManagerRestored

| Canonical event detail | Value |
| --- | --- |
| Target event name | TenantManagerRestored |
| Selected source event name | TenantManagerRestored |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:TenantManagerRestored |
| Owner selection | one exact canonical authority owner |
| Target concrete type | TenantManagerRestored |
| Adopted wire | `{"discriminator":"bdgrz.rbac.manager_guard.restored","version":2}` |
| Wire qualification | Adopted wire is the occupied discriminator declared in src/Compliance.Common/Features/AccessControl/Events/TenantManagerRestored.cs at 60029b8f (2026-10-29 reconciliation). The earlier selected wire is retained as supersededSelectedWire for provenance only. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | `{"discriminator":"bdgrz.rbac.manager_guard.restored","version":1}` |
| Target meaning | The guard restored a member's availability as an administrator for Reason after the exact source restoration and current eligibility were proven. |
| Selected source meaning | The guard restored a member's availability as an administrator for Reason after the exact source restoration and current eligibility were proven. |
| Meaning source | record summary in src/Compliance.Common/Features/AccessControl/Events/TenantManagerRestored.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | TenantManagerRestored |
| Schema discriminator | bdgrz.rbac.manager_guard.restored |
| Schema discriminatorVersion | 1 |
| Schema owner | TenantManagerGuard |
| Schema existingContract | src/Compliance.Common/Features/AccessControl/TenantManagerRestored.cs |
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
| [T052](../reactors/administrator-restoration-reactor.md#t052) | emitted semantic fact | Member\|TeamMember | CompleteAdministratorRestoration | TenantAdministratorContinuity |

### TenantMemberChangeAuthorized

| Canonical event detail | Value |
| --- | --- |
| Target event name | TenantMemberChangeAuthorized |
| Selected source event name | TenantManagerSourceMutationAdmitted |
| Name decision | Names the completed authorization for a member change that does not alter administrator coverage. |
| Canonical semantic key | event:TenantManagerSourceMutationAdmitted |
| Owner selection | one exact canonical authority owner |
| Target concrete type | TenantManagerSourceMutationAdmitted |
| Adopted wire | `{"discriminator":"bdgrz.tenant-manager-guard.tenant-manager-source-mutation-admitted","version":2}` |
| Wire qualification | Adopted wire is the occupied discriminator declared in src/Compliance.Common/Features/AccessControl/Events/TenantManagerSourceMutationAdmitted.cs at 60029b8f (2026-10-29 reconciliation). The earlier selected wire is retained as supersededSelectedWire for provenance only. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | `{"discriminator":"bdgrz.tenant-manager-guard.tenant-manager-source-mutation-admitted","version":1}` |
| Target meaning | A tenant member change was authorized because it does not affect the number of active tenant administrators. The member change itself is recorded afterward. |
| Selected source meaning | The guard admitted a source mutation of a member episode that has no contribution and no pending transition. It changes no availability but makes the mutation visible to any later registration for the same episode. Owner is the source stream the operation mutates, Revision the authored revision of that owner (negative when unconstrained) and Generation the guard generation of this fact. |
| Meaning source | record summary in src/Compliance.Common/Features/AccessControl/Events/TenantManagerSourceMutationAdmitted.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | TenantManagerSourceMutationAdmitted |
| Schema discriminator | bdgrz.tenant-manager-guard.tenant-manager-source-mutation-admitted |
| Schema discriminatorVersion | 1 |
| Schema owner | TenantManagerGuard |
| Schema status | selected target schema; implementation pending |
| Schema reason | source admission contract; administrator-capable source mutation that changes no availability |
| Schema changes | None listed |
| Schema reducerRule | Validate owner/generation and that no contribution or pending transition exists for this member episode (or that the mutation cannot change availability); record the admitted source mutation so a later registration/withdrawal for the same episode observes it. Apply only this owner delta; never hydrate Member or TeamMember. |
| Schema serializer | Register concrete target event and every proof/content variant in ComplianceCoreJsonContext and attributed persistence preflight coverage. |
| Schema nativeResponseRule | FirstResponse is non-null only on this logical command first native accepting fact; it references FirstResponseDescriptor. OperationResult is non-null on the declared terminal business fact and references MutationResultContent. Both use verified content and static concrete result serializers; neither is reconstructed from current state. |

#### Event fields

| Field | Type | Required |
| --- | --- | --- |
| Owner | EventStreamAddress | true |
| Revision | long | true |
| Generation | long | true |
| Intent | ImmutableContentRef | true |
| SourceProofs | ImmutableContentRef? | true |
| Operation | OperationProvenance | true |
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
| [T048](../reactors/apply-accepted-member-status-change-reactor.md#t048) | physical root, logical prerequisite | TenantAdministratorContinuity | ApplyMemberStatusChange | Member |
| [T049](../reactors/remove-unneeded-administrator-assignment-reactor.md#t049) | physical root, logical prerequisite | TenantAdministratorContinuity | RemoveAdministratorAssignment | TeamMember |

### TenantAdministratorWithdrawalAuthorized

| Canonical event detail | Value |
| --- | --- |
| Target event name | TenantAdministratorWithdrawalAuthorized |
| Selected source event name | TenantManagerWithdrawalAccepted |
| Name decision | Names the completed authorization to remove an administrator contribution when another active administrator remains. |
| Canonical semantic key | event:TenantManagerWithdrawalAccepted |
| Owner selection | one exact canonical authority owner |
| Target concrete type | TenantManagerWithdrawalAccepted |
| Adopted wire | `{"discriminator":"bdgrz.tenant-manager-guard.tenant-manager-withdrawal-accepted","version":2}` |
| Wire qualification | Adopted wire is the occupied discriminator declared in src/Compliance.Common/Features/AccessControl/Events/TenantManagerWithdrawalAccepted.cs at 60029b8f (2026-10-29 reconciliation). The earlier selected wire is retained as supersededSelectedWire for provenance only. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | `{"discriminator":"bdgrz.tenant-manager-guard.tenant-manager-withdrawal-accepted","version":1}` |
| Target meaning | Removal of an administrator contribution was authorized because another active tenant administrator remains; the source membership change follows. |
| Selected source meaning | The tenant manager guard admitted withdrawing an administrator's contribution before its source change, so that administrator stops counting as available and the last manager cannot be removed. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/AccessControl/Events/TenantManagerWithdrawalAccepted.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | TenantManagerWithdrawalAccepted |
| Schema discriminator | bdgrz.tenant-manager-guard.tenant-manager-withdrawal-accepted |
| Schema discriminatorVersion | 1 |
| Schema owner | TenantManagerGuard |
| Schema status | selected target schema; implementation pending |
| Schema reason | source admission contract |
| Schema changes | None listed |
| Schema reducerRule | Validate owner/generation and local predecessor; apply only this owner delta or immutable content reference. Never hydrate a foreign aggregate or mutate based on a projection. |
| Schema serializer | Register concrete target event and every proof/content variant in ComplianceCoreJsonContext and attributed persistence preflight coverage. |
| Schema nativeResponseRule | FirstResponse is non-null only on this logical command first native accepting fact; it references FirstResponseDescriptor. OperationResult is non-null on the declared terminal business fact and references MutationResultContent. Both use verified content and static concrete result serializers; neither is reconstructed from current state. |

#### Event fields

| Field | Type | Required |
| --- | --- | --- |
| Owner | EventStreamAddress | true |
| Revision | long | true |
| Generation | long | true |
| Intent | ImmutableContentRef | true |
| SourceProofs | ImmutableContentRef? | true |
| Operation | OperationProvenance | true |
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
| [T046](../reactors/administrator-withdrawal-reactor.md#t046) | physical root, logical prerequisite | TenantAdministratorContinuity | ApplyMemberWithdrawal | Member |
| [T047](../reactors/administrator-assignment-withdrawal-reactor.md#t047) | physical root, logical prerequisite | TenantAdministratorContinuity | ApplyAdministratorAssignmentWithdrawal | TeamMember |
| [T050](../reactors/administrator-withdrawal-reactor.md#t050) | physical root | Member\|TeamMember | CompleteAdministratorWithdrawal | TenantAdministratorContinuity |

### TenantManagerWithdrawalCompleted

| Canonical event detail | Value |
| --- | --- |
| Target event name | TenantManagerWithdrawalCompleted |
| Selected source event name | TenantManagerWithdrawalCompleted |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:TenantManagerWithdrawalCompleted |
| Owner selection | one exact canonical authority owner |
| Target concrete type | TenantManagerWithdrawalCompleted |
| Adopted wire | `{"discriminator":"bdgrz.tenant-manager-guard.tenant-manager-withdrawal-completed","version":2}` |
| Wire qualification | Adopted wire is the occupied discriminator declared in src/Compliance.Common/Features/AccessControl/Events/TenantManagerWithdrawalCompleted.cs at 60029b8f (2026-10-29 reconciliation). The earlier selected wire is retained as supersededSelectedWire for provenance only. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | `{"discriminator":"bdgrz.tenant-manager-guard.tenant-manager-withdrawal-completed","version":1}` |
| Target meaning | The source change a withdrawal admitted was applied; the withdrawal operation is settled and its reason stays withheld. Owner is the source stream the operation mutates, Revision the authored revision of that owner (negative when unconstrained) and Generation the guard generation of this fact. |
| Selected source meaning | The source change a withdrawal admitted was applied; the withdrawal operation is settled and its reason stays withheld. Owner is the source stream the operation mutates, Revision the authored revision of that owner (negative when unconstrained) and Generation the guard generation of this fact. |
| Meaning source | record summary in src/Compliance.Common/Features/AccessControl/Events/TenantManagerWithdrawalCompleted.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | TenantManagerWithdrawalCompleted |
| Schema discriminator | bdgrz.tenant-manager-guard.tenant-manager-withdrawal-completed |
| Schema discriminatorVersion | 1 |
| Schema owner | TenantManagerGuard |
| Schema status | selected target schema; implementation pending |
| Schema reason | typed reactor receiver accepted business fact |
| Schema changes | None listed |
| Schema reducerRule | Validate owner/generation and local predecessor; apply only this owner delta or immutable content reference. Never hydrate a foreign aggregate or mutate based on a projection. |
| Schema serializer | Register concrete target event and every proof/content variant in ComplianceCoreJsonContext and attributed persistence preflight coverage. |
| Schema nativeResponseRule | FirstResponse is non-null only on this logical command first native accepting fact; it references FirstResponseDescriptor. OperationResult is non-null on the declared terminal business fact and references MutationResultContent. Both use verified content and static concrete result serializers; neither is reconstructed from current state. |

#### Event fields

| Field | Type | Required |
| --- | --- | --- |
| Owner | EventStreamAddress | true |
| Revision | long | true |
| Generation | long | true |
| Intent | ImmutableContentRef | true |
| SourceProofs | ImmutableContentRef? | true |
| Operation | OperationProvenance | true |
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
| [T050](../reactors/administrator-withdrawal-reactor.md#t050) | emitted semantic fact | Member\|TeamMember | CompleteAdministratorWithdrawal | TenantAdministratorContinuity |


## Wire/schema version discrepancies

- **TenantManagerRestorationAccepted** — [Placeholder: reconcile schema version 1 with adopted version 2].
- **TenantManagerRestorationCompleted** — [Placeholder: reconcile schema version 1 with adopted version 2].
- **TenantManagerRestored** — [Placeholder: reconcile schema version 1 with adopted version 2].
- **TenantManagerSourceMutationAdmitted** — [Placeholder: reconcile schema version 1 with adopted version 2].
- **TenantManagerWithdrawalAccepted** — [Placeholder: reconcile schema version 1 with adopted version 2].
- **TenantManagerWithdrawalCompleted** — [Placeholder: reconcile schema version 1 with adopted version 2].

[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]
