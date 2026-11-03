# UserIdentity events

Canonical events: **6**. Complete canonical event and available schema records: [owner event JSON](../data/events/user-identity.json).

| Target event | Selected source name | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- | --- |
| RecoveryIdentityProtected | RecoveryIdentityProtected | reservation | bdgrz.user-identity.recovery-identity-protected | 1 | 8 | A provider identity was protected for one recovery challenge; until that protection is settled, no other account can associate the same provider identity. |
| RecoveryIdentityProtectionEnded | RecoveryIdentityProtectionSettled | reservation | bdgrz.user-identity.recovery-identity-protection-settled | 1 | 8 | The protection of one slot for one recovery challenge is settled. A slot that was never protected is settled too, which fences a delayed protection for the same challenge. |
| UserIdentityAuthenticated | UserIdentityAuthenticated | audit | bdgrz.user-identity.authenticated | 1 | [Placeholder: schema absent] | Audits successful authentication through a registered provider identity. |
| UserIdentityProfileObserved | UserIdentityProfileObserved | audit | bdgrz.user-identity.profile-observed | 2 | [Placeholder: schema absent] | Audited provider display text admitted at the proven identity revision; never identity or authority. |
| UserIdentityRegistered | UserIdentityRegistered | business | bdgrz.user-identity.registered | 1 | 9 | Records the provider-neutral identity linked to a platform user. A registration caused by an identity recovery names the recovery challenge and carries the receiver's operation provenance and frozen intent. |
| UserIdentityRevoked | UserIdentityRevoked | business | bdgrz.user-identity.revoked | 1 | 9 | Records replacement of a provider identity while preserving its platform user. A revocation caused by an identity recovery names the recovery challenge and carries the receiver's provenance and frozen intent. |

## Event details

### RecoveryIdentityProtected

| Canonical event detail | Value |
| --- | --- |
| Target event name | RecoveryIdentityProtected |
| Selected source event name | RecoveryIdentityProtected |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:RecoveryIdentityProtected |
| Owner selection | one exact canonical authority owner |
| Target concrete type | RecoveryIdentityProtected |
| Adopted wire | `{"discriminator":"bdgrz.user-identity.recovery-identity-protected","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/UserIdentities/Events/RecoveryIdentityProtected.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | A provider identity was protected for one recovery challenge; until that protection is settled, no other account can associate the same provider identity. |
| Selected source meaning | A provider identity was protected for one recovery challenge; until that protection is settled, no other account can associate the same provider identity. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/UserIdentities/Events/RecoveryIdentityProtected.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | RecoveryIdentityProtected |
| Schema discriminator | bdgrz.user-identity.recovery-identity-protected |
| Schema discriminatorVersion | 1 |
| Schema owner | UserIdentity |
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
| [T041](../reactors/identity-recovery-acceptance-reactor.md#t041) | emitted semantic fact | IdentityRecovery | ProtectRecoveryIdentity | UserIdentity |

### RecoveryIdentityProtectionEnded

| Canonical event detail | Value |
| --- | --- |
| Target event name | RecoveryIdentityProtectionEnded |
| Selected source event name | RecoveryIdentityProtectionSettled |
| Name decision | Names the end of protection for the exact recovery identity slot, including when protection never began. |
| Canonical semantic key | event:RecoveryIdentityProtectionSettled |
| Owner selection | one exact canonical authority owner |
| Target concrete type | RecoveryIdentityProtectionSettled |
| Adopted wire | `{"discriminator":"bdgrz.user-identity.recovery-identity-protection-settled","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/UserIdentities/Events/RecoveryIdentityProtectionSettled.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | The protection of one slot for one recovery challenge is settled. A slot that was never protected is settled too, which fences a delayed protection for the same challenge. |
| Selected source meaning | The protection of one slot for one recovery challenge is settled. A slot that was never protected is settled too, which fences a delayed protection for the same challenge. |
| Meaning source | record summary in src/Compliance.Common/Features/UserIdentities/Events/RecoveryIdentityProtectionSettled.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | RecoveryIdentityProtectionSettled |
| Schema discriminator | bdgrz.user-identity.recovery-identity-protection-settled |
| Schema discriminatorVersion | 1 |
| Schema owner | UserIdentity |
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
| [T057](../reactors/identity-recovery-acceptance-reactor.md#t057) | emitted semantic fact | IdentityRecovery | EndRecoveryIdentityProtection | UserIdentity |

### UserIdentityAuthenticated

| Canonical event detail | Value |
| --- | --- |
| Target event name | UserIdentityAuthenticated |
| Selected source event name | UserIdentityAuthenticated |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:UserIdentityAuthenticated |
| Owner selection | UserIdentity AuditEvent emits to independent physical audit session stream; no aggregate reducer/history increment |
| Target concrete type | UserIdentityAuthenticated |
| Adopted wire | `{"discriminator":"bdgrz.user-identity.authenticated","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/UserIdentities/Events/UserIdentityAuthenticated.cs at 60029b8f. |
| Schema status | excluded_business_schema_not_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | Audits successful authentication through a registered provider identity. |
| Selected source meaning | Audits successful authentication through a registered provider identity. |
| Meaning source | record summary in src/Compliance.Common/Features/UserIdentities/Events/UserIdentityAuthenticated.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### UserIdentityProfileObserved

| Canonical event detail | Value |
| --- | --- |
| Target event name | UserIdentityProfileObserved |
| Selected source event name | UserIdentityProfileObserved |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:UserIdentityProfileObserved |
| Owner selection | UserIdentity AuditEvent emits to independent physical audit session stream; no aggregate reducer/history increment |
| Target concrete type | UserIdentityProfileObserved |
| Adopted wire | `{"discriminator":"bdgrz.user-identity.profile-observed","version":2}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/UserIdentities/Events/UserIdentityProfileObserved.cs at 60029b8f. |
| Schema status | excluded_business_schema_not_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | Audited provider display text admitted at the proven identity revision; never identity or authority. |
| Selected source meaning | Audited provider display text admitted at the proven identity revision; never identity or authority. |
| Meaning source | record summary in src/Compliance.Common/Features/UserIdentities/Events/UserIdentityProfileObserved.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### UserIdentityRegistered

| Canonical event detail | Value |
| --- | --- |
| Target event name | UserIdentityRegistered |
| Selected source event name | UserIdentityRegistered |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:UserIdentityRegistered |
| Owner selection | one exact canonical authority owner |
| Target concrete type | UserIdentityRegistered |
| Adopted wire | `{"discriminator":"bdgrz.user-identity.registered","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/UserIdentities/Events/UserIdentityRegistered.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | Records the provider-neutral identity linked to a platform user. A registration caused by an identity recovery names the recovery challenge and carries the receiver's operation provenance and frozen intent. |
| Selected source meaning | Records the provider-neutral identity linked to a platform user. A registration caused by an identity recovery names the recovery challenge and carries the receiver's operation provenance and frozen intent. |
| Meaning source | record summary in src/Compliance.Common/Features/UserIdentities/Events/UserIdentityRegistered.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | UserIdentityRegistered |
| Schema discriminator | bdgrz.user-identity.registered |
| Schema discriminatorVersion | 1 |
| Schema owner | UserIdentity |
| Schema existingContract | src/Compliance.Common/Features/UserIdentities/UserIdentityRegistered.cs |
| Schema status | selected target schema; implementation pending |
| Schema reason | typed reactor receiver accepted business fact |
| Schema changes | None listed |
| Schema reducerRule | Validate owner/generation and local predecessor; apply only this owner delta or immutable content reference. Never hydrate a foreign aggregate or mutate based on a projection. |
| Schema serializer | Register concrete target event and every proof/content variant in ComplianceCoreJsonContext and attributed persistence preflight coverage. |
| Schema nativeResponseRule | FirstResponse is non-null only on this logical command first native accepting fact; it references FirstResponseDescriptor. OperationResult is non-null on the declared terminal business fact and references MutationResultContent. Both use verified content and static concrete result serializers; neither is reconstructed from current state. |

#### Event fields

| Field | Type | Required |
| --- | --- | --- |
| UserId | Uuid | true |
| Provider | string | true |
| Identifier | string | true |
| EmailAddress | string? | true |
| Operation | OperationProvenance | true |
| RecoveryChallengeId | Uuid? | true |
| Intent | ImmutableContentRef | true |
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
| [T042](../reactors/identity-recovery-acceptance-reactor.md#t042) | emitted semantic fact | IdentityRecovery | RegisterRecoveredIdentity | UserIdentity |
| [S035](../reactors/email-reservation-reactor.md#s035) | physical root, logical prerequisite | UserIdentity | ReserveEmail | EmailAddress |

### UserIdentityRevoked

| Canonical event detail | Value |
| --- | --- |
| Target event name | UserIdentityRevoked |
| Selected source event name | UserIdentityRevoked |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:UserIdentityRevoked |
| Owner selection | one exact canonical authority owner |
| Target concrete type | UserIdentityRevoked |
| Adopted wire | `{"discriminator":"bdgrz.user-identity.revoked","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/UserIdentities/Events/UserIdentityRevoked.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | Records replacement of a provider identity while preserving its platform user. A revocation caused by an identity recovery names the recovery challenge and carries the receiver's provenance and frozen intent. |
| Selected source meaning | Records replacement of a provider identity while preserving its platform user. A revocation caused by an identity recovery names the recovery challenge and carries the receiver's provenance and frozen intent. |
| Meaning source | record summary in src/Compliance.Common/Features/UserIdentities/Events/UserIdentityRevoked.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | UserIdentityRevoked |
| Schema discriminator | bdgrz.user-identity.revoked |
| Schema discriminatorVersion | 1 |
| Schema owner | UserIdentity |
| Schema existingContract | src/Compliance.Common/Features/UserIdentities/UserIdentityRevoked.cs |
| Schema status | selected target schema; implementation pending |
| Schema reason | typed reactor receiver accepted business fact |
| Schema changes | None listed |
| Schema reducerRule | Validate owner/generation and local predecessor; apply only this owner delta or immutable content reference. Never hydrate a foreign aggregate or mutate based on a projection. |
| Schema serializer | Register concrete target event and every proof/content variant in ComplianceCoreJsonContext and attributed persistence preflight coverage. |
| Schema nativeResponseRule | FirstResponse is non-null only on this logical command first native accepting fact; it references FirstResponseDescriptor. OperationResult is non-null on the declared terminal business fact and references MutationResultContent. Both use verified content and static concrete result serializers; neither is reconstructed from current state. |

#### Event fields

| Field | Type | Required |
| --- | --- | --- |
| UserIdentityId | Uuid | true |
| UserId | Uuid | true |
| ReplacementIdentityId | Uuid | true |
| RevokedAt | DateTimeOffset | true |
| Operation | OperationProvenance | true |
| RecoveryChallengeId | Uuid? | true |
| Intent | ImmutableContentRef | true |
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
| [T043](../reactors/identity-recovery-acceptance-reactor.md#t043) | emitted semantic fact | IdentityRecovery | RevokeRecoveredOldIdentity | UserIdentity |




[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]
