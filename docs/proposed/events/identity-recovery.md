# IdentityRecovery events

Canonical events: **4**. Complete canonical event and available schema records: [owner event JSON](../data/events/identity-recovery.json).

| Target event | Selected source name | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- | --- |
| IdentityRecoveryAccepted | IdentityRecoveryAccepted | business | bdgrz.identity-recovery.identity-recovery-accepted | 1 | 9 | The recovery owner accepted the swap after both identity slots were protected. An accepted swap completes forward and can no longer be rejected. |
| IdentityRecoveryCompleted | IdentityRecoveryCompleted | business | bdgrz.identity-recovery.identity-recovery-completed | 1 | 9 | The accepted swap finished: the replacement is registered and the old identity revoked to it. |
| IdentityRecoveryPreparationRejected | IdentityRecoveryPreparationRejected | business | bdgrz.identity-recovery.identity-recovery-preparation-rejected | 1 | 9 | A definite competing association rejected the preparation before acceptance. It fences delayed protection and acceptance. |
| IdentityRecoveryPrepared | IdentityRecoveryPrepared | business | bdgrz.identity-recovery.identity-recovery-prepared | 1 | 9 | The recovery owner froze the exact both-proof-verified candidate from the claimed challenge. This is a preparation, not an accepted swap. |

## Event details

### IdentityRecoveryAccepted

| Canonical event detail | Value |
| --- | --- |
| Target event name | IdentityRecoveryAccepted |
| Selected source event name | IdentityRecoveryAccepted |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:IdentityRecoveryAccepted |
| Owner selection | one exact canonical authority owner |
| Target concrete type | IdentityRecoveryAccepted |
| Adopted wire | `{"discriminator":"bdgrz.identity-recovery.identity-recovery-accepted","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/UserIdentities/Events/IdentityRecoveryAccepted.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | The recovery owner accepted the swap after both identity slots were protected. An accepted swap completes forward and can no longer be rejected. |
| Selected source meaning | The recovery owner accepted the swap after both identity slots were protected. An accepted swap completes forward and can no longer be rejected. |
| Meaning source | record summary in src/Compliance.Common/Features/UserIdentities/Events/IdentityRecoveryAccepted.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | IdentityRecoveryAccepted |
| Schema discriminator | bdgrz.identity-recovery.identity-recovery-accepted |
| Schema discriminatorVersion | 1 |
| Schema owner | IdentityRecovery |
| Schema existingContract | null (recorded) |
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
| ChallengeId | Uuid | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

#### Nested content schemas

#### Content schema: identity-recovery-intent.v1

| Property | Value |
| --- | --- |
| field | Intent |
| schema | identity-recovery-intent.v1 |
| recordType | IdentityRecoveryIntent |
| rule | Both proofs verified before email claim/preparation, with only non-bearer provenance. Immutable candidate keyed by challenge. Protect old and replacement associations before IdentityRecoveryAccepted. Before acceptance, definite conflict rejects and fences; afterward register/verify replacement, revoke old, complete forward without caller confirmation. Later challenges cannot rewrite this intent. |

##### Fields

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| ChallengeId | Uuid | true |
| AccountId | Uuid | true |
| OldIdentityId | Uuid | true |
| ReplacementIdentityId | Uuid | true |
| OldProvider | string | true |
| OldSubject | string | true |
| ReplacementProvider | string | true |
| ReplacementSubject | string | true |
| EmailProofProvenance | ImmutableContentRef | true |
| ReplacementProofProvenance | ImmutableContentRef | true |

##### Variant: IdentityRecoveryIntent

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| ChallengeId | Uuid | true |
| AccountId | Uuid | true |
| OldIdentityId | Uuid | true |
| ReplacementIdentityId | Uuid | true |
| OldProvider | string | true |
| OldSubject | string | true |
| ReplacementProvider | string | true |
| ReplacementSubject | string | true |
| EmailProofProvenance | ImmutableContentRef | true |
| ReplacementProofProvenance | ImmutableContentRef | true |


#### Resolved intent schema

IdentityRecoveryIntent

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [T042](../reactors/identity-recovery-acceptance-reactor.md#t042) | logical prerequisite | IdentityRecovery | RegisterRecoveredIdentity | UserIdentity |
| [T043](../reactors/identity-recovery-acceptance-reactor.md#t043) | logical prerequisite | IdentityRecovery | RevokeRecoveredOldIdentity | UserIdentity |
| [T044](../reactors/identity-recovery-acceptance-reactor.md#t044) | logical prerequisite | IdentityRecovery | CompleteAcceptedIdentityRecovery | IdentityRecovery |
| [T097](../reactors/identity-recovery-acceptance-reactor.md#t097) | emitted semantic fact | IdentityRecovery | AcceptProtectedIdentityRecovery | IdentityRecovery |

### IdentityRecoveryCompleted

| Canonical event detail | Value |
| --- | --- |
| Target event name | IdentityRecoveryCompleted |
| Selected source event name | IdentityRecoveryCompleted |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:IdentityRecoveryCompleted |
| Owner selection | one exact canonical authority owner |
| Target concrete type | IdentityRecoveryCompleted |
| Adopted wire | `{"discriminator":"bdgrz.identity-recovery.identity-recovery-completed","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/UserIdentities/Events/IdentityRecoveryCompleted.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | The accepted swap finished: the replacement is registered and the old identity revoked to it. |
| Selected source meaning | The accepted swap finished: the replacement is registered and the old identity revoked to it. |
| Meaning source | record summary in src/Compliance.Common/Features/UserIdentities/Events/IdentityRecoveryCompleted.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | IdentityRecoveryCompleted |
| Schema discriminator | bdgrz.identity-recovery.identity-recovery-completed |
| Schema discriminatorVersion | 1 |
| Schema owner | IdentityRecovery |
| Schema existingContract | null (recorded) |
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
| ChallengeId | Uuid | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

#### Nested content schemas

#### Content schema: identity-recovery-intent.v1

| Property | Value |
| --- | --- |
| field | Intent |
| schema | identity-recovery-intent.v1 |
| recordType | IdentityRecoveryIntent |
| rule | Both proofs verified before email claim/preparation, with only non-bearer provenance. Immutable candidate keyed by challenge. Protect old and replacement associations before IdentityRecoveryAccepted. Before acceptance, definite conflict rejects and fences; afterward register/verify replacement, revoke old, complete forward without caller confirmation. Later challenges cannot rewrite this intent. |

##### Fields

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| ChallengeId | Uuid | true |
| AccountId | Uuid | true |
| OldIdentityId | Uuid | true |
| ReplacementIdentityId | Uuid | true |
| OldProvider | string | true |
| OldSubject | string | true |
| ReplacementProvider | string | true |
| ReplacementSubject | string | true |
| EmailProofProvenance | ImmutableContentRef | true |
| ReplacementProofProvenance | ImmutableContentRef | true |

##### Variant: IdentityRecoveryIntent

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| ChallengeId | Uuid | true |
| AccountId | Uuid | true |
| OldIdentityId | Uuid | true |
| ReplacementIdentityId | Uuid | true |
| OldProvider | string | true |
| OldSubject | string | true |
| ReplacementProvider | string | true |
| ReplacementSubject | string | true |
| EmailProofProvenance | ImmutableContentRef | true |
| ReplacementProofProvenance | ImmutableContentRef | true |


#### Resolved intent schema

IdentityRecoveryIntent

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [T044](../reactors/identity-recovery-acceptance-reactor.md#t044) | emitted semantic fact | IdentityRecovery | CompleteAcceptedIdentityRecovery | IdentityRecovery |
| [T045](../reactors/identity-recovery-acceptance-reactor.md#t045) | logical prerequisite | IdentityRecovery | SendIdentityRecoveryNotice | IdentityRecoveryNoticeProcess |
| [T057](../reactors/identity-recovery-acceptance-reactor.md#t057) | logical prerequisite | IdentityRecovery | EndRecoveryIdentityProtection | UserIdentity |
| [T058](../reactors/identity-recovery-acceptance-reactor.md#t058) | logical prerequisite | IdentityRecovery | CompleteEmailRecoveryChallenge | EmailAddress |

### IdentityRecoveryPreparationRejected

| Canonical event detail | Value |
| --- | --- |
| Target event name | IdentityRecoveryPreparationRejected |
| Selected source event name | IdentityRecoveryPreparationRejected |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:IdentityRecoveryPreparationRejected |
| Owner selection | one exact canonical authority owner |
| Target concrete type | IdentityRecoveryPreparationRejected |
| Adopted wire | `{"discriminator":"bdgrz.identity-recovery.identity-recovery-preparation-rejected","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/UserIdentities/Events/IdentityRecoveryPreparationRejected.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | A definite competing association rejected the preparation before acceptance. It fences delayed protection and acceptance. |
| Selected source meaning | A definite competing association rejected the preparation before acceptance. It fences delayed protection and acceptance. |
| Meaning source | record summary in src/Compliance.Common/Features/UserIdentities/Events/IdentityRecoveryPreparationRejected.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | IdentityRecoveryPreparationRejected |
| Schema discriminator | bdgrz.identity-recovery.identity-recovery-preparation-rejected |
| Schema discriminatorVersion | 1 |
| Schema owner | IdentityRecovery |
| Schema existingContract | null (recorded) |
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
| ChallengeId | Uuid | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

#### Nested content schemas

#### Content schema: identity-recovery-intent.v1

| Property | Value |
| --- | --- |
| field | Intent |
| schema | identity-recovery-intent.v1 |
| recordType | IdentityRecoveryIntent |
| rule | Both proofs verified before email claim/preparation, with only non-bearer provenance. Immutable candidate keyed by challenge. Protect old and replacement associations before IdentityRecoveryAccepted. Before acceptance, definite conflict rejects and fences; afterward register/verify replacement, revoke old, complete forward without caller confirmation. Later challenges cannot rewrite this intent. |

##### Fields

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| ChallengeId | Uuid | true |
| AccountId | Uuid | true |
| OldIdentityId | Uuid | true |
| ReplacementIdentityId | Uuid | true |
| OldProvider | string | true |
| OldSubject | string | true |
| ReplacementProvider | string | true |
| ReplacementSubject | string | true |
| EmailProofProvenance | ImmutableContentRef | true |
| ReplacementProofProvenance | ImmutableContentRef | true |

##### Variant: IdentityRecoveryIntent

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| ChallengeId | Uuid | true |
| AccountId | Uuid | true |
| OldIdentityId | Uuid | true |
| ReplacementIdentityId | Uuid | true |
| OldProvider | string | true |
| OldSubject | string | true |
| ReplacementProvider | string | true |
| ReplacementSubject | string | true |
| EmailProofProvenance | ImmutableContentRef | true |
| ReplacementProofProvenance | ImmutableContentRef | true |


#### Resolved intent schema

IdentityRecoveryIntent

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [T057](../reactors/identity-recovery-acceptance-reactor.md#t057) | logical prerequisite | IdentityRecovery | EndRecoveryIdentityProtection | UserIdentity |
| [A017](../reactors/unassigned-architecture-rows.md#a017) | emitted semantic fact | EmailAddress | RejectIdentityRecoveryPreparation | IdentityRecovery |

### IdentityRecoveryPrepared

| Canonical event detail | Value |
| --- | --- |
| Target event name | IdentityRecoveryPrepared |
| Selected source event name | IdentityRecoveryPrepared |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:IdentityRecoveryPrepared |
| Owner selection | one exact canonical authority owner |
| Target concrete type | IdentityRecoveryPrepared |
| Adopted wire | `{"discriminator":"bdgrz.identity-recovery.identity-recovery-prepared","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/UserIdentities/Events/IdentityRecoveryPrepared.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | The recovery owner froze the exact both-proof-verified candidate from the claimed challenge. This is a preparation, not an accepted swap. |
| Selected source meaning | The recovery owner froze the exact both-proof-verified candidate from the claimed challenge. This is a preparation, not an accepted swap. |
| Meaning source | record summary in src/Compliance.Common/Features/UserIdentities/Events/IdentityRecoveryPrepared.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | IdentityRecoveryPrepared |
| Schema discriminator | bdgrz.identity-recovery.identity-recovery-prepared |
| Schema discriminatorVersion | 1 |
| Schema owner | IdentityRecovery |
| Schema existingContract | null (recorded) |
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
| ChallengeId | Uuid | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

#### Nested content schemas

#### Content schema: identity-recovery-intent.v1

| Property | Value |
| --- | --- |
| field | Intent |
| schema | identity-recovery-intent.v1 |
| recordType | IdentityRecoveryIntent |
| rule | Both proofs verified before email claim/preparation, with only non-bearer provenance. Immutable candidate keyed by challenge. Protect old and replacement associations before IdentityRecoveryAccepted. Before acceptance, definite conflict rejects and fences; afterward register/verify replacement, revoke old, complete forward without caller confirmation. Later challenges cannot rewrite this intent. |

##### Fields

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| ChallengeId | Uuid | true |
| AccountId | Uuid | true |
| OldIdentityId | Uuid | true |
| ReplacementIdentityId | Uuid | true |
| OldProvider | string | true |
| OldSubject | string | true |
| ReplacementProvider | string | true |
| ReplacementSubject | string | true |
| EmailProofProvenance | ImmutableContentRef | true |
| ReplacementProofProvenance | ImmutableContentRef | true |

##### Variant: IdentityRecoveryIntent

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| ChallengeId | Uuid | true |
| AccountId | Uuid | true |
| OldIdentityId | Uuid | true |
| ReplacementIdentityId | Uuid | true |
| OldProvider | string | true |
| OldSubject | string | true |
| ReplacementProvider | string | true |
| ReplacementSubject | string | true |
| EmailProofProvenance | ImmutableContentRef | true |
| ReplacementProofProvenance | ImmutableContentRef | true |


#### Resolved intent schema

IdentityRecoveryIntent

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [T041](../reactors/identity-recovery-acceptance-reactor.md#t041) | logical prerequisite | IdentityRecovery | ProtectRecoveryIdentity | UserIdentity |
| [T056](../reactors/identity-recovery-acceptance-reactor.md#t056) | emitted semantic fact | EmailAddress | PrepareAccountRecovery | IdentityRecovery |
| [T097](../reactors/identity-recovery-acceptance-reactor.md#t097) | logical prerequisite | IdentityRecovery | AcceptProtectedIdentityRecovery | IdentityRecovery |




[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]
