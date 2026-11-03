# EmailAddress events

Canonical events: **6**. Complete canonical event and available schema records: [owner event JSON](../data/events/email-address.json).

| Target event | Selected source name | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- | --- |
| EmailAddressReserved | EmailAddressReserved | reservation | bdgrz.email-address.reserved | 1 | 5 | The native acceptance of ReserveEmail, the first reservation of the address. |
| EmailAddressVerified | EmailAddressVerified | business | bdgrz.email-address.verified | 1 | 6 | The native acceptance of CompleteEmailChallenge; the bearer token never enters the fact. |
| EmailChallengeIssued | EmailChallengeIssued | business | bdgrz.email-address.challenge-issued | 1 | 9 | The native acceptance of IssueEmailChallenge. It carries the hash of the bearer token, never the token; delivery of the token to the mailbox is a technical effect of the email challenge delivery process and leaves no fact on the address stream. |
| IdentityRecoveryChallengeClaimed | IdentityRecoveryChallengeClaimed | business | bdgrz.identity-recovery.challenge-claimed | 1 | 13 | Consumes an email proof challenge for one old and one replacement identity and freezes the exact candidate. It is the native acceptance of CompleteIdentityRecovery; the proofs are retained as non-bearer provenance. |
| IdentityRecoveryChallengeCompleted | IdentityRecoveryChallengeCompleted | business | bdgrz.identity-recovery.challenge-completed | 1 | 10 | Completes the original email challenge of a finished recovery; a newer challenge slot is never cleared. |
| IdentityRecoveryChallengeIssued | IdentityRecoveryChallengeIssued | business | bdgrz.identity-recovery.challenge-issued | 1 | 9 | A recovery challenge was issued to a verified email address for a user, valid until its expiry, so the holder of that address can complete identity recovery. |

## Event details

### EmailAddressReserved

| Canonical event detail | Value |
| --- | --- |
| Target event name | EmailAddressReserved |
| Selected source event name | EmailAddressReserved |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:EmailAddressReserved |
| Owner selection | one exact canonical authority owner |
| Target concrete type | EmailAddressReserved |
| Adopted wire | `{"discriminator":"bdgrz.email-address.reserved","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/UserIdentities/Events/EmailAddressReserved.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | The native acceptance of ReserveEmail, the first reservation of the address. |
| Selected source meaning | The native acceptance of ReserveEmail, the first reservation of the address. |
| Meaning source | record summary in src/Compliance.Common/Features/UserIdentities/Events/EmailAddressReserved.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | EmailAddressReserved |
| Schema discriminator | bdgrz.email-address.reserved |
| Schema discriminatorVersion | 1 |
| Schema owner | EmailAddress |
| Schema existingContract | src/Compliance.Common/Features/UserIdentities/EmailAddressReserved.cs |
| Schema status | selected target schema; implementation pending |
| Schema reason | native fact of a retained owner command |
| Schema changes | None listed |
| Schema reducerRule | Validate owner/generation and local predecessor; apply only this owner delta or immutable content reference. Never hydrate a foreign aggregate or mutate based on a projection. |
| Schema serializer | Register concrete target event and every proof/content variant in ComplianceCoreJsonContext and attributed persistence preflight coverage. |
| Schema nativeResponseRule | FirstResponse is non-null only on this logical command first native accepting fact; it references FirstResponseDescriptor. OperationResult is non-null on the declared terminal business fact and references MutationResultContent. Both use verified content and static concrete result serializers; neither is reconstructed from current state. |

#### Event fields

| Field | Type | Required |
| --- | --- | --- |
| UserId | Uuid | true |
| EmailAddress | string | true |
| Operation | OperationProvenance | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [S035](../reactors/email-reservation-reactor.md#s035) | emitted semantic fact | UserIdentity | ReserveEmail | EmailAddress |

### EmailAddressVerified

| Canonical event detail | Value |
| --- | --- |
| Target event name | EmailAddressVerified |
| Selected source event name | EmailAddressVerified |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:EmailAddressVerified |
| Owner selection | one exact canonical authority owner |
| Target concrete type | EmailAddressVerified |
| Adopted wire | `{"discriminator":"bdgrz.email-address.verified","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/UserIdentities/Events/EmailAddressVerified.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | The native acceptance of CompleteEmailChallenge; the bearer token never enters the fact. |
| Selected source meaning | The native acceptance of CompleteEmailChallenge; the bearer token never enters the fact. |
| Meaning source | record summary in src/Compliance.Common/Features/UserIdentities/Events/EmailAddressVerified.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | EmailAddressVerified |
| Schema discriminator | bdgrz.email-address.verified |
| Schema discriminatorVersion | 1 |
| Schema owner | EmailAddress |
| Schema existingContract | src/Compliance.Common/Features/UserIdentities/EmailAddressVerified.cs |
| Schema status | selected target schema; implementation pending |
| Schema reason | native fact of a retained owner command |
| Schema changes | None listed |
| Schema reducerRule | Validate owner/generation and local predecessor; apply only this owner delta or immutable content reference. Never hydrate a foreign aggregate or mutate based on a projection. |
| Schema serializer | Register concrete target event and every proof/content variant in ComplianceCoreJsonContext and attributed persistence preflight coverage. |
| Schema nativeResponseRule | FirstResponse is non-null only on this logical command first native accepting fact; it references FirstResponseDescriptor. OperationResult is non-null on the declared terminal business fact and references MutationResultContent. Both use verified content and static concrete result serializers; neither is reconstructed from current state. |

#### Event fields

| Field | Type | Required |
| --- | --- | --- |
| UserId | Uuid | true |
| EmailAddress | string | true |
| ChallengeId | Uuid | true |
| Operation | OperationProvenance | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### EmailChallengeIssued

| Canonical event detail | Value |
| --- | --- |
| Target event name | EmailChallengeIssued |
| Selected source event name | EmailChallengeIssued |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:EmailChallengeIssued |
| Owner selection | one exact canonical authority owner |
| Target concrete type | EmailChallengeIssued |
| Adopted wire | `{"discriminator":"bdgrz.email-address.challenge-issued","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/UserIdentities/Events/EmailChallengeIssued.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | The native acceptance of IssueEmailChallenge. It carries the hash of the bearer token, never the token; delivery of the token to the mailbox is a technical effect of the email challenge delivery process and leaves no fact on the address stream. |
| Selected source meaning | The native acceptance of IssueEmailChallenge. It carries the hash of the bearer token, never the token; delivery of the token to the mailbox is a technical effect of the email challenge delivery process and leaves no fact on the address stream. |
| Meaning source | record summary in src/Compliance.Common/Features/UserIdentities/Events/EmailChallengeIssued.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | EmailChallengeIssued |
| Schema discriminator | bdgrz.email-address.challenge-issued |
| Schema discriminatorVersion | 1 |
| Schema owner | EmailAddress |
| Schema existingContract | src/Compliance.Common/Features/UserIdentities/EmailChallengeIssued.cs |
| Schema status | selected target schema; implementation pending |
| Schema reason | native fact of a retained owner command |
| Schema changes | None listed |
| Schema reducerRule | Validate owner/generation and local predecessor; apply only this owner delta or immutable content reference. Never hydrate a foreign aggregate or mutate based on a projection. |
| Schema serializer | Register concrete target event and every proof/content variant in ComplianceCoreJsonContext and attributed persistence preflight coverage. |
| Schema nativeResponseRule | FirstResponse is non-null only on this logical command first native accepting fact; it references FirstResponseDescriptor. OperationResult is non-null on the declared terminal business fact and references MutationResultContent. Both use verified content and static concrete result serializers; neither is reconstructed from current state. |

#### Event fields

| Field | Type | Required |
| --- | --- | --- |
| UserId | Uuid | true |
| EmailAddress | string | true |
| ChallengeId | Uuid | true |
| TokenHash | string | true |
| ExpiresAt | DateTimeOffset | true |
| TokenKeyId | string | true |
| Operation | OperationProvenance | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [T115](../reactors/email-challenge-delivery-reactor.md#t115) | physical root, logical prerequisite | EmailAddress | DeliverEmailChallenge | EmailChallengeDeliveryLedger |
| [T116](../reactors/email-challenge-delivery-reactor.md#t116) | physical root | EmailAddress | DeliverRecoveryChallenge | EmailChallengeDeliveryLedger |
| [N001](../reactors/email-challenge-delivery-reactor.md#n001) | physical root, logical prerequisite | EmailAddress | RecordEmailChallengeDeliverySent | EmailChallengeDeliveryLedger |
| [N002](../reactors/email-challenge-delivery-reactor.md#n002) | physical root, logical prerequisite | EmailAddress | RecordEmailChallengeDeliveryFailed | EmailChallengeDeliveryLedger |

### IdentityRecoveryChallengeClaimed

| Canonical event detail | Value |
| --- | --- |
| Target event name | IdentityRecoveryChallengeClaimed |
| Selected source event name | IdentityRecoveryChallengeClaimed |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:IdentityRecoveryChallengeClaimed |
| Owner selection | one exact canonical authority owner |
| Target concrete type | IdentityRecoveryChallengeClaimed |
| Adopted wire | `{"discriminator":"bdgrz.identity-recovery.challenge-claimed","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/UserIdentities/Events/IdentityRecoveryChallengeClaimed.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | Consumes an email proof challenge for one old and one replacement identity and freezes the exact candidate. It is the native acceptance of CompleteIdentityRecovery; the proofs are retained as non-bearer provenance. |
| Selected source meaning | Consumes an email proof challenge for one old and one replacement identity and freezes the exact candidate. It is the native acceptance of CompleteIdentityRecovery; the proofs are retained as non-bearer provenance. |
| Meaning source | record summary in src/Compliance.Common/Features/UserIdentities/Events/IdentityRecoveryChallengeClaimed.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | IdentityRecoveryChallengeClaimed |
| Schema discriminator | bdgrz.identity-recovery.challenge-claimed |
| Schema discriminatorVersion | 1 |
| Schema owner | EmailAddress |
| Schema existingContract | src/Compliance.Common/Features/UserIdentities/IdentityRecoveryChallengeClaimed.cs |
| Schema status | selected target schema; implementation pending |
| Schema reason | source admission contract |
| Schema changes | None listed |
| Schema reducerRule | Validate owner/generation and local predecessor; apply only this owner delta or immutable content reference. Never hydrate a foreign aggregate or mutate based on a projection. |
| Schema serializer | Register concrete target event and every proof/content variant in ComplianceCoreJsonContext and attributed persistence preflight coverage. |
| Schema nativeResponseRule | FirstResponse is non-null only on this logical command first native accepting fact; it references FirstResponseDescriptor. OperationResult is non-null on the declared terminal business fact and references MutationResultContent. Both use verified content and static concrete result serializers; neither is reconstructed from current state. |

#### Event fields

| Field | Type | Required |
| --- | --- | --- |
| UserId | Uuid | true |
| EmailAddress | string | true |
| ChallengeId | Uuid | true |
| OldIdentityId | Uuid | true |
| ReplacementIdentityId | Uuid | true |
| Operation | OperationProvenance | true |
| OriginalActor | ActorReference | true |
| VerifiedEmailProof | ImmutableContentRef | true |
| VerifiedReplacementProof | ImmutableContentRef | true |
| AcceptedAt | DateTimeOffset | true |
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
| [T041](../reactors/identity-recovery-acceptance-reactor.md#t041) | physical root | IdentityRecovery | ProtectRecoveryIdentity | UserIdentity |
| [T042](../reactors/identity-recovery-acceptance-reactor.md#t042) | physical root | IdentityRecovery | RegisterRecoveredIdentity | UserIdentity |
| [T043](../reactors/identity-recovery-acceptance-reactor.md#t043) | physical root | IdentityRecovery | RevokeRecoveredOldIdentity | UserIdentity |
| [T044](../reactors/identity-recovery-acceptance-reactor.md#t044) | physical root | IdentityRecovery | CompleteAcceptedIdentityRecovery | IdentityRecovery |
| [T045](../reactors/identity-recovery-acceptance-reactor.md#t045) | physical root | IdentityRecovery | SendIdentityRecoveryNotice | IdentityRecoveryNoticeProcess |
| [T056](../reactors/identity-recovery-acceptance-reactor.md#t056) | physical root, logical prerequisite | EmailAddress | PrepareAccountRecovery | IdentityRecovery |
| [T057](../reactors/identity-recovery-acceptance-reactor.md#t057) | physical root | IdentityRecovery | EndRecoveryIdentityProtection | UserIdentity |
| [T058](../reactors/identity-recovery-acceptance-reactor.md#t058) | physical root | IdentityRecovery | CompleteEmailRecoveryChallenge | EmailAddress |
| [T097](../reactors/identity-recovery-acceptance-reactor.md#t097) | physical root | IdentityRecovery | AcceptProtectedIdentityRecovery | IdentityRecovery |
| [A017](../reactors/unassigned-architecture-rows.md#a017) | physical root, logical prerequisite | EmailAddress | RejectIdentityRecoveryPreparation | IdentityRecovery |

### IdentityRecoveryChallengeCompleted

| Canonical event detail | Value |
| --- | --- |
| Target event name | IdentityRecoveryChallengeCompleted |
| Selected source event name | IdentityRecoveryChallengeCompleted |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:IdentityRecoveryChallengeCompleted |
| Owner selection | one exact canonical authority owner |
| Target concrete type | IdentityRecoveryChallengeCompleted |
| Adopted wire | `{"discriminator":"bdgrz.identity-recovery.challenge-completed","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/UserIdentities/Events/IdentityRecoveryChallengeCompleted.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | Completes the original email challenge of a finished recovery; a newer challenge slot is never cleared. |
| Selected source meaning | Completes the original email challenge of a finished recovery; a newer challenge slot is never cleared. |
| Meaning source | record summary in src/Compliance.Common/Features/UserIdentities/Events/IdentityRecoveryChallengeCompleted.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | IdentityRecoveryChallengeCompleted |
| Schema discriminator | bdgrz.identity-recovery.challenge-completed |
| Schema discriminatorVersion | 1 |
| Schema owner | EmailAddress |
| Schema existingContract | src/Compliance.Common/Features/UserIdentities/IdentityRecoveryChallengeCompleted.cs |
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
| EmailAddress | string | true |
| ChallengeId | Uuid | true |
| OldIdentityId | Uuid | true |
| ReplacementIdentityId | Uuid | true |
| CompletedAt | DateTimeOffset | true |
| Operation | OperationProvenance | true |
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
| [T058](../reactors/identity-recovery-acceptance-reactor.md#t058) | emitted semantic fact | IdentityRecovery | CompleteEmailRecoveryChallenge | EmailAddress |

### IdentityRecoveryChallengeIssued

| Canonical event detail | Value |
| --- | --- |
| Target event name | IdentityRecoveryChallengeIssued |
| Selected source event name | IdentityRecoveryChallengeIssued |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:IdentityRecoveryChallengeIssued |
| Owner selection | one exact canonical authority owner |
| Target concrete type | IdentityRecoveryChallengeIssued |
| Adopted wire | `{"discriminator":"bdgrz.identity-recovery.challenge-issued","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/UserIdentities/Events/IdentityRecoveryChallengeIssued.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | A recovery challenge was issued to a verified email address for a user, valid until its expiry, so the holder of that address can complete identity recovery. |
| Selected source meaning | A recovery challenge was issued to a verified email address for a user, valid until its expiry, so the holder of that address can complete identity recovery. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/UserIdentities/Events/IdentityRecoveryChallengeIssued.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | IdentityRecoveryChallengeIssued |
| Schema discriminator | bdgrz.identity-recovery.challenge-issued |
| Schema discriminatorVersion | 1 |
| Schema owner | EmailAddress |
| Schema existingContract | src/Compliance.Common/Features/UserIdentities/IdentityRecoveryChallengeIssued.cs |
| Schema status | selected target schema; implementation pending |
| Schema reason | source admission contract |
| Schema changes | None listed |
| Schema reducerRule | Validate owner/generation and local predecessor; apply only this owner delta or immutable content reference. Never hydrate a foreign aggregate or mutate based on a projection. |
| Schema serializer | Register concrete target event and every proof/content variant in ComplianceCoreJsonContext and attributed persistence preflight coverage. |
| Schema nativeResponseRule | FirstResponse is non-null only on this logical command first native accepting fact; it references FirstResponseDescriptor. OperationResult is non-null on the declared terminal business fact and references MutationResultContent. Both use verified content and static concrete result serializers; neither is reconstructed from current state. |

#### Event fields

| Field | Type | Required |
| --- | --- | --- |
| UserId | Uuid | true |
| EmailAddress | string | true |
| ChallengeId | Uuid | true |
| TokenHash | string | true |
| ExpiresAt | DateTimeOffset | true |
| TokenKeyId | string | true |
| Operation | OperationProvenance | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [T115](../reactors/email-challenge-delivery-reactor.md#t115) | physical root | EmailAddress | DeliverEmailChallenge | EmailChallengeDeliveryLedger |
| [T116](../reactors/email-challenge-delivery-reactor.md#t116) | physical root, logical prerequisite | EmailAddress | DeliverRecoveryChallenge | EmailChallengeDeliveryLedger |
| [N001](../reactors/email-challenge-delivery-reactor.md#n001) | physical root, logical prerequisite | EmailAddress | RecordEmailChallengeDeliverySent | EmailChallengeDeliveryLedger |
| [N002](../reactors/email-challenge-delivery-reactor.md#n002) | physical root, logical prerequisite | EmailAddress | RecordEmailChallengeDeliveryFailed | EmailChallengeDeliveryLedger |




[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]
