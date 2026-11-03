# TenantIdentityProvider events

Canonical events: **5**. Complete canonical event and available schema records: [owner event JSON](../data/events/tenant-identity-provider.json).

| Target event | Selected source name | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- | --- |
| TenantIdentityProviderConfigured | TenantIdentityProviderConfigured | business | bdgrz.useridentities.tenant-identity-provider-configured | 2 | 12 | The native acceptance of ConfigureTenantIdentityProvider: public OIDC configuration and a reference to the protected content that holds the secret-store pointer, never the pointer itself. |
| TenantIdentityProviderCredentialsRotated | TenantIdentityProviderCredentialsRotated | business | bdgrz.useridentities.tenant-identity-provider-credentials-rotated | 2 | 11 | The native acceptance of RotateTenantIdentityProviderCredentials: a new credential epoch whose secret-store pointer lives in protected content, and a forced fresh connection test. |
| TenantIdentityProviderDisabled | TenantIdentityProviderDisabled | business | bdgrz.useridentities.tenant-identity-provider-disabled | 2 | 9 | The native acceptance of DisableTenantIdentityProvider; a disabled connection stays terminal. |
| TenantIdentityProviderTestRequested | TenantIdentityProviderTestRequested | business | bdgrz.user-identities.tenant-identity-provider-test-requested | 1 | 10 | The native acceptance of TestTenantIdentityProvider: a request to test one exact credential epoch. |
| TenantIdentityProviderTested | TenantIdentityProviderTested | business | bdgrz.useridentities.tenant-identity-provider-tested | 3 | 10 | The native acceptance of RecordTenantIdentityProviderTestResult: a bounded safe test result for the exact request event and credential epoch. Provider tokens and response bodies are never persisted. |

## Event details

### TenantIdentityProviderConfigured

| Canonical event detail | Value |
| --- | --- |
| Target event name | TenantIdentityProviderConfigured |
| Selected source event name | TenantIdentityProviderConfigured |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:TenantIdentityProviderConfigured |
| Owner selection | one exact canonical authority owner |
| Target concrete type | TenantIdentityProviderConfigured |
| Adopted wire | `{"discriminator":"bdgrz.useridentities.tenant-identity-provider-configured","version":2}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/UserIdentities/Events/TenantIdentityProviderConfigured.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | The native acceptance of ConfigureTenantIdentityProvider: public OIDC configuration and a reference to the protected content that holds the secret-store pointer, never the pointer itself. |
| Selected source meaning | The native acceptance of ConfigureTenantIdentityProvider: public OIDC configuration and a reference to the protected content that holds the secret-store pointer, never the pointer itself. |
| Meaning source | record summary in src/Compliance.Common/Features/UserIdentities/Events/TenantIdentityProviderConfigured.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | TenantIdentityProviderConfigured |
| Schema discriminator | bdgrz.useridentities.tenant-identity-provider-configured |
| Schema discriminatorVersion | 2 |
| Schema owner | TenantIdentityProvider |
| Schema existingContract | src/Compliance.Common/Features/UserIdentities/TenantIdentityProviderConfigured.cs |
| Schema status | selected target schema; implementation pending |
| Schema reason | native fact of a retained owner command |
| Schema changes | None listed |
| Schema reducerRule | Validate owner/generation and local predecessor; apply only this owner delta or immutable content reference. Never hydrate a foreign aggregate or mutate based on a projection. |
| Schema serializer | Register concrete target event and every proof/content variant in ComplianceCoreJsonContext and attributed persistence preflight coverage. |
| Schema nativeResponseRule | FirstResponse is non-null only on this logical command first native accepting fact; it references FirstResponseDescriptor. OperationResult is non-null on the declared terminal business fact and references MutationResultContent. Both use verified content and static concrete result serializers; neither is reconstructed from current state. |

#### Event fields

| Field | Type | Required |
| --- | --- | --- |
| TenantId | Uuid | true |
| ProviderConnectionId | Uuid | true |
| Issuer | string | true |
| ClientId | string | true |
| AllowedAudiences | IReadOnlyList<string> | true |
| CredentialReference | ImmutableContentRef | true |
| ActorMemberId | Uuid | true |
| ActorDisplay | string | true |
| ConfiguredAt | DateTimeOffset | true |
| Operation | OperationProvenance | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### TenantIdentityProviderCredentialsRotated

| Canonical event detail | Value |
| --- | --- |
| Target event name | TenantIdentityProviderCredentialsRotated |
| Selected source event name | TenantIdentityProviderCredentialsRotated |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:TenantIdentityProviderCredentialsRotated |
| Owner selection | one exact canonical authority owner |
| Target concrete type | TenantIdentityProviderCredentialsRotated |
| Adopted wire | `{"discriminator":"bdgrz.useridentities.tenant-identity-provider-credentials-rotated","version":2}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/UserIdentities/Events/TenantIdentityProviderCredentialsRotated.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | The native acceptance of RotateTenantIdentityProviderCredentials: a new credential epoch whose secret-store pointer lives in protected content, and a forced fresh connection test. |
| Selected source meaning | The native acceptance of RotateTenantIdentityProviderCredentials: a new credential epoch whose secret-store pointer lives in protected content, and a forced fresh connection test. |
| Meaning source | record summary in src/Compliance.Common/Features/UserIdentities/Events/TenantIdentityProviderCredentialsRotated.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | TenantIdentityProviderCredentialsRotated |
| Schema discriminator | bdgrz.useridentities.tenant-identity-provider-credentials-rotated |
| Schema discriminatorVersion | 2 |
| Schema owner | TenantIdentityProvider |
| Schema existingContract | src/Compliance.Common/Features/UserIdentities/TenantIdentityProviderCredentialsRotated.cs |
| Schema status | selected target schema; implementation pending |
| Schema reason | native fact of a retained owner command |
| Schema changes | None listed |
| Schema reducerRule | Validate owner/generation and local predecessor; apply only this owner delta or immutable content reference. Never hydrate a foreign aggregate or mutate based on a projection. |
| Schema serializer | Register concrete target event and every proof/content variant in ComplianceCoreJsonContext and attributed persistence preflight coverage. |
| Schema nativeResponseRule | FirstResponse is non-null only on this logical command first native accepting fact; it references FirstResponseDescriptor. OperationResult is non-null on the declared terminal business fact and references MutationResultContent. Both use verified content and static concrete result serializers; neither is reconstructed from current state. |

#### Event fields

| Field | Type | Required |
| --- | --- | --- |
| TenantId | Uuid | true |
| ProviderConnectionId | Uuid | true |
| CredentialReference | ImmutableContentRef | true |
| RotationReference | string | true |
| CredentialRevision | long | true |
| ActorMemberId | Uuid | true |
| ActorDisplay | string | true |
| RotatedAt | DateTimeOffset | true |
| Operation | OperationProvenance | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### TenantIdentityProviderDisabled

| Canonical event detail | Value |
| --- | --- |
| Target event name | TenantIdentityProviderDisabled |
| Selected source event name | TenantIdentityProviderDisabled |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:TenantIdentityProviderDisabled |
| Owner selection | one exact canonical authority owner |
| Target concrete type | TenantIdentityProviderDisabled |
| Adopted wire | `{"discriminator":"bdgrz.useridentities.tenant-identity-provider-disabled","version":2}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/UserIdentities/Events/TenantIdentityProviderDisabled.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | The native acceptance of DisableTenantIdentityProvider; a disabled connection stays terminal. |
| Selected source meaning | The native acceptance of DisableTenantIdentityProvider; a disabled connection stays terminal. |
| Meaning source | record summary in src/Compliance.Common/Features/UserIdentities/Events/TenantIdentityProviderDisabled.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | TenantIdentityProviderDisabled |
| Schema discriminator | bdgrz.useridentities.tenant-identity-provider-disabled |
| Schema discriminatorVersion | 2 |
| Schema owner | TenantIdentityProvider |
| Schema existingContract | src/Compliance.Common/Features/UserIdentities/TenantIdentityProviderDisabled.cs |
| Schema status | selected target schema; implementation pending |
| Schema reason | native fact of a retained owner command |
| Schema changes | None listed |
| Schema reducerRule | Validate owner/generation and local predecessor; apply only this owner delta or immutable content reference. Never hydrate a foreign aggregate or mutate based on a projection. |
| Schema serializer | Register concrete target event and every proof/content variant in ComplianceCoreJsonContext and attributed persistence preflight coverage. |
| Schema nativeResponseRule | FirstResponse is non-null only on this logical command first native accepting fact; it references FirstResponseDescriptor. OperationResult is non-null on the declared terminal business fact and references MutationResultContent. Both use verified content and static concrete result serializers; neither is reconstructed from current state. |

#### Event fields

| Field | Type | Required |
| --- | --- | --- |
| TenantId | Uuid | true |
| ProviderConnectionId | Uuid | true |
| Rationale | string | true |
| ActorMemberId | Uuid | true |
| ActorDisplay | string | true |
| DisabledAt | DateTimeOffset | true |
| Operation | OperationProvenance | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### TenantIdentityProviderTestRequested

| Canonical event detail | Value |
| --- | --- |
| Target event name | TenantIdentityProviderTestRequested |
| Selected source event name | TenantIdentityProviderTestRequested |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:TenantIdentityProviderTestRequested |
| Owner selection | one exact canonical authority owner |
| Target concrete type | TenantIdentityProviderTestRequested |
| Adopted wire | `{"discriminator":"bdgrz.user-identities.tenant-identity-provider-test-requested","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/UserIdentities/Events/TenantIdentityProviderTestRequested.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | The native acceptance of TestTenantIdentityProvider: a request to test one exact credential epoch. |
| Selected source meaning | The native acceptance of TestTenantIdentityProvider: a request to test one exact credential epoch. |
| Meaning source | record summary in src/Compliance.Common/Features/UserIdentities/Events/TenantIdentityProviderTestRequested.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | TenantIdentityProviderTestRequested |
| Schema discriminator | bdgrz.user-identities.tenant-identity-provider-test-requested |
| Schema discriminatorVersion | 1 |
| Schema owner | TenantIdentityProvider |
| Schema existingContract | src/Compliance.Common/Features/UserIdentities/TenantIdentityProviderTestRequested.cs |
| Schema status | selected target schema; implementation pending |
| Schema reason | native fact of a retained owner command |
| Schema changes | None listed |
| Schema reducerRule | Validate owner/generation and local predecessor; apply only this owner delta or immutable content reference. Never hydrate a foreign aggregate or mutate based on a projection. |
| Schema serializer | Register concrete target event and every proof/content variant in ComplianceCoreJsonContext and attributed persistence preflight coverage. |
| Schema nativeResponseRule | FirstResponse is non-null only on this logical command first native accepting fact; it references FirstResponseDescriptor. OperationResult is non-null on the declared terminal business fact and references MutationResultContent. Both use verified content and static concrete result serializers; neither is reconstructed from current state. |

#### Event fields

| Field | Type | Required |
| --- | --- | --- |
| TenantId | Uuid | true |
| ProviderConnectionId | Uuid | true |
| TestKey | string | true |
| CredentialRevision | long | true |
| RequestedByMemberId | Uuid | true |
| RequestedByDisplay | string | true |
| RequestedAt | DateTimeOffset | true |
| Operation | OperationProvenance | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### TenantIdentityProviderTested

| Canonical event detail | Value |
| --- | --- |
| Target event name | TenantIdentityProviderTested |
| Selected source event name | TenantIdentityProviderTested |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:TenantIdentityProviderTested |
| Owner selection | one exact canonical authority owner |
| Target concrete type | TenantIdentityProviderTested |
| Adopted wire | `{"discriminator":"bdgrz.useridentities.tenant-identity-provider-tested","version":3}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/UserIdentities/Events/TenantIdentityProviderTested.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | The native acceptance of RecordTenantIdentityProviderTestResult: a bounded safe test result for the exact request event and credential epoch. Provider tokens and response bodies are never persisted. |
| Selected source meaning | The native acceptance of RecordTenantIdentityProviderTestResult: a bounded safe test result for the exact request event and credential epoch. Provider tokens and response bodies are never persisted. |
| Meaning source | record summary in src/Compliance.Common/Features/UserIdentities/Events/TenantIdentityProviderTested.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | TenantIdentityProviderTested |
| Schema discriminator | bdgrz.useridentities.tenant-identity-provider-tested |
| Schema discriminatorVersion | 3 |
| Schema owner | TenantIdentityProvider |
| Schema existingContract | src/Compliance.Common/Features/UserIdentities/TenantIdentityProviderTested.cs |
| Schema status | selected target schema; implementation pending |
| Schema reason | native fact of a retained owner command |
| Schema changes | None listed |
| Schema reducerRule | Validate owner/generation and local predecessor; apply only this owner delta or immutable content reference. Never hydrate a foreign aggregate or mutate based on a projection. |
| Schema serializer | Register concrete target event and every proof/content variant in ComplianceCoreJsonContext and attributed persistence preflight coverage. |
| Schema nativeResponseRule | FirstResponse is non-null only on this logical command first native accepting fact; it references FirstResponseDescriptor. OperationResult is non-null on the declared terminal business fact and references MutationResultContent. Both use verified content and static concrete result serializers; neither is reconstructed from current state. |

#### Event fields

| Field | Type | Required |
| --- | --- | --- |
| TenantId | Uuid | true |
| ProviderConnectionId | Uuid | true |
| TestRequestEventId | Uuid | true |
| CredentialRevision | long | true |
| TestedByActor | ActorReference | true |
| Result | TenantIdentityProviderTestResultContent | true |
| TestedAt | DateTimeOffset | true |
| Operation | OperationProvenance | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]




[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]
