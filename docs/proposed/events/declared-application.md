# DeclaredApplication events

Canonical events: **7**. Complete canonical event and available schema records: [owner event JSON](../data/events/declared-application.json).

| Event | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- |
| ApplicationDeclared | business | bdgrz.application.declared | 1 | 17 | An application was declared for the tenant with its name, purpose and owner reference by a named member, as the application's first revision. |
| ApplicationImportEffectCanceled | business | bdgrz.declared_application.import_effect_canceled | 1 | 8 | An import batch was fenced on its target application after cancellation, even if it never held the application, so any delayed reservation or application of that batch is refused. |
| ApplicationImportEffectPending | reservation | bdgrz.application_import.effect_pending | 2 | 11 | One frozen import row's change to an application was provisionally applied while the batch holds the application, pending the batch's final settlement; it is not yet a published revision. |
| ApplicationImportTargetAuthorized | reservation | bdgrz.declared_application.import_target_authorized | 1 | 8 | The target application grants the exclusive hold of one batch group. Revision is the published content revision at the hold, which a hold never advances; the group's frozen baseline and candidate revisions are carried so the hold rebuilds from the ordered stream alone. |
| ApplicationImportTargetSettled | reservation | bdgrz.declared_application.import_target_settled | 1 | 8 | The target hold ends with the batch's exact terminal fact. A published disposition promotes the frozen candidate to the application's content without a second revision increment; a canceled or failed disposition discards it. Revision is the published content revision after the settlement. |
| ApplicationRetired | business | bdgrz.application.retired | 1 | 13 | An application was retired effective on a stated date with a reason, optionally merged into a successor application; its history, instances and snapshot references remain. |
| ApplicationRevised | business | bdgrz.application.revised | 1 | 18 | A declared application's name, purpose or owner reference was revised by a named member, recorded as the application's next revision. |

## Event details

### ApplicationDeclared

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:ApplicationDeclared |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ApplicationDeclared |
| Adopted wire | `{"discriminator":"bdgrz.application.declared","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Applications/Events/ApplicationDeclared.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | An application was declared for the tenant with its name, purpose and owner reference by a named member, as the application's first revision. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/Applications/Events/ApplicationDeclared.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | ApplicationDeclared |
| Schema discriminator | bdgrz.application.declared |
| Schema discriminatorVersion | 1 |
| Schema owner | DeclaredApplication |
| Schema existingContract | src/Compliance.Common/Features/Applications/ApplicationDeclared.cs |
| Schema status | selected target schema; implementation pending |
| Schema reason | source command accepted result |
| Schema changes | None listed |
| Schema reducerRule | Validate owner/generation and local predecessor; apply only this owner delta or immutable content reference. Never hydrate a foreign aggregate or mutate based on a projection. |
| Schema serializer | Register concrete target event and every proof/content variant in ComplianceCoreJsonContext and attributed persistence preflight coverage. |
| Schema nativeResponseRule | FirstResponse is non-null only on this logical command first native accepting fact; it references FirstResponseDescriptor. OperationResult is non-null on the declared terminal business fact and references MutationResultContent. Both use verified content and static concrete result serializers; neither is reconstructed from current state. |

#### Event fields

| Field | Type | Required |
| --- | --- | --- |
| TenantId | Uuid | true |
| ApplicationId | Uuid | true |
| Name | string | true |
| Purpose | string | true |
| OwnerReference | string? | true |
| ActorMemberId | Uuid | true |
| ActorDisplay | string | true |
| ChangedAt | DateTimeOffset | true |
| Classification | string? | false |
| SystemOwnerPersonId | Uuid? | false |
| AccessOwnerPersonId | Uuid? | false |
| IsRestricted | bool | false |
| ApplicationKind | string? | false |
| Criticality | string? | false |
| Operation | OperationProvenance | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [X000](../reactors/application-name-change-reactor.md#x000) | physical root, logical prerequisite | DeclaredApplication | ClaimApplicationName | ApplicationNameClaim |

### ApplicationImportEffectCanceled

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:ApplicationImportEffectCanceled |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ApplicationImportEffectCanceled |
| Adopted wire | `{"discriminator":"bdgrz.declared_application.import_effect_canceled","version":1}` |
| Wire qualification | Adopted wire is the occupied discriminator declared in src/Compliance.Common/Features/Applications/Events/ApplicationImportEffectCanceled.cs at 60029b8f (2026-10-29 reconciliation). The earlier selected wire is retained as supersededSelectedWire for provenance only. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | `{"discriminator":"bdgrz.declared-application.application-import-effect-canceled","version":1}` |
| Meaning | An import batch was fenced on its target application after cancellation, even if it never held the application, so any delayed reservation or application of that batch is refused. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/Applications/Events/ApplicationImportEffectCanceled.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | ApplicationImportEffectCanceled |
| Schema discriminator | bdgrz.declared-application.application-import-effect-canceled |
| Schema discriminatorVersion | 1 |
| Schema owner | DeclaredApplication |
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

#### Content schema: import-target-group-intent.v1

| Property | Value |
| --- | --- |
| field | Intent |
| schema | import-target-group-intent.v1 |
| recordType | ImportTargetGroupIntent |
| rule | One exclusive hold and one native append batch per target group. Preserve existing create_new/link_existing/retire semantics: one create; links do not overwrite authored application metadata; retirement changes active state once. Validate every row against the frozen group baseline and full existing source-claim rules. Apply original row order; no per-row lock upgrade or repeated baseline compare after earlier append. |

##### Fields

| Field | Type | Required |
| --- | --- | --- |
| BatchId | Uuid | true |
| ApplicationId | Uuid | true |
| InitialApplicationRevision | ulong | true |
| Rows | ImmutableSelectionManifest | true |
| CandidateContent | ImmutableContentRef | true |
| CandidateContentRevision | ulong | true |
| RequiredRetirementProofs | ImmutableSelectionManifest | true |

##### Variant: ImportTargetGroupIntent

| Field | Type | Required |
| --- | --- | --- |
| BatchId | Uuid | true |
| ApplicationId | Uuid | true |
| InitialApplicationRevision | ulong | true |
| Rows | ImmutableSelectionManifest | true |
| CandidateContent | ImmutableContentRef | true |
| CandidateContentRevision | ulong | true |
| RequiredRetirementProofs | ImmutableSelectionManifest | true |


#### Resolved intent schema

ImportTargetGroupIntent

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [T018](../reactors/import-cancellation-reactor.md#t018) | emitted semantic fact | ApplicationImportBatch | FenceApplicationImportTarget | DeclaredApplication |

### ApplicationImportEffectPending

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:ApplicationImportEffectPending |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ApplicationImportEffectPending |
| Adopted wire | `{"discriminator":"bdgrz.application_import.effect_pending","version":2}` |
| Wire qualification | Adopted wire is the occupied discriminator declared in src/Compliance.Common/Features/Applications/Events/ApplicationImportEffectPending.cs at 60029b8f (2026-10-29 reconciliation). The earlier selected wire is retained as supersededSelectedWire for provenance only. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | `{"discriminator":"bdgrz.application_import.effect_pending","version":1}` |
| Meaning | One frozen import row's change to an application was provisionally applied while the batch holds the application, pending the batch's final settlement; it is not yet a published revision. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/Applications/Events/ApplicationImportEffectPending.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | ApplicationImportEffectPending |
| Schema discriminator | bdgrz.application_import.effect_pending |
| Schema discriminatorVersion | 1 |
| Schema owner | DeclaredApplication |
| Schema existingContract | src/Compliance.Common/Features/Applications/ApplicationImportEffectPending.cs |
| Schema status | selected target schema; implementation pending |
| Schema reason | typed reactor receiver accepted business fact |
| Schema changes | Replace the removed ApplicationImportPlanStarted payload with FrozenPlan, a verified reference to the sealed ImportFrozenPlanIntent; PlanSha256 remains the digest of that complete intent. |
| Schema reducerRule | Validate owner/generation and local predecessor; apply only this owner delta or immutable content reference. Never hydrate a foreign aggregate or mutate based on a projection. |
| Schema serializer | Register concrete target event and every proof/content variant in ComplianceCoreJsonContext and attributed persistence preflight coverage. |
| Schema nativeResponseRule | FirstResponse is non-null only on this logical command first native accepting fact; it references FirstResponseDescriptor. OperationResult is non-null on the declared terminal business fact and references MutationResultContent. Both use verified content and static concrete result serializers; neither is reconstructed from current state. |

#### Event fields

| Field | Type | Required |
| --- | --- | --- |
| TenantId | Uuid | true |
| ApplicationId | Uuid | true |
| PlanRevision | long | true |
| PlanSha256 | string | true |
| FrozenPlan | ImmutableContentRef | true |
| Row | ApplicationImportPlannedRow | true |
| RecordedAt | DateTimeOffset | true |
| Operation | OperationProvenance | true |
| Intent | ImmutableContentRef | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

#### Nested content schemas

#### Content schema: import-target-group-intent.v1

| Property | Value |
| --- | --- |
| field | Intent |
| schema | import-target-group-intent.v1 |
| recordType | ImportTargetGroupIntent |
| rule | One exclusive hold and one native append batch per target group. Preserve existing create_new/link_existing/retire semantics: one create; links do not overwrite authored application metadata; retirement changes active state once. Validate every row against the frozen group baseline and full existing source-claim rules. Apply original row order; no per-row lock upgrade or repeated baseline compare after earlier append. |

##### Fields

| Field | Type | Required |
| --- | --- | --- |
| BatchId | Uuid | true |
| ApplicationId | Uuid | true |
| InitialApplicationRevision | ulong | true |
| Rows | ImmutableSelectionManifest | true |
| CandidateContent | ImmutableContentRef | true |
| CandidateContentRevision | ulong | true |
| RequiredRetirementProofs | ImmutableSelectionManifest | true |

##### Variant: ImportTargetGroupIntent

| Field | Type | Required |
| --- | --- | --- |
| BatchId | Uuid | true |
| ApplicationId | Uuid | true |
| InitialApplicationRevision | ulong | true |
| Rows | ImmutableSelectionManifest | true |
| CandidateContent | ImmutableContentRef | true |
| CandidateContentRevision | ulong | true |
| RequiredRetirementProofs | ImmutableSelectionManifest | true |


#### Resolved intent schema

ImportTargetGroupIntent

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [T016](../reactors/import-source-admission-reactor.md#t016) | emitted semantic fact | ApplicationImportBatch | ApplyApplicationImportEffect | DeclaredApplication |

### ApplicationImportTargetAuthorized

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:ApplicationImportTargetAuthorized |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ApplicationImportTargetAuthorized |
| Adopted wire | `{"discriminator":"bdgrz.declared_application.import_target_authorized","version":1}` |
| Wire qualification | Adopted wire is the occupied discriminator declared in src/Compliance.Common/Features/Applications/Events/ApplicationImportTargetAuthorized.cs at 60029b8f (2026-10-29 reconciliation). The earlier selected wire is retained as supersededSelectedWire for provenance only. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | `{"discriminator":"bdgrz.declared-application.application-import-target-authorized","version":1}` |
| Meaning | The target application grants the exclusive hold of one batch group. Revision is the published content revision at the hold, which a hold never advances; the group's frozen baseline and candidate revisions are carried so the hold rebuilds from the ordered stream alone. |
| Meaning source | record summary in src/Compliance.Common/Features/Applications/Events/ApplicationImportTargetAuthorized.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | ApplicationImportTargetAuthorized |
| Schema discriminator | bdgrz.declared-application.application-import-target-authorized |
| Schema discriminatorVersion | 1 |
| Schema owner | DeclaredApplication |
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

#### Content schema: import-target-group-intent.v1

| Property | Value |
| --- | --- |
| field | Intent |
| schema | import-target-group-intent.v1 |
| recordType | ImportTargetGroupIntent |
| rule | One exclusive hold and one native append batch per target group. Preserve existing create_new/link_existing/retire semantics: one create; links do not overwrite authored application metadata; retirement changes active state once. Validate every row against the frozen group baseline and full existing source-claim rules. Apply original row order; no per-row lock upgrade or repeated baseline compare after earlier append. |

##### Fields

| Field | Type | Required |
| --- | --- | --- |
| BatchId | Uuid | true |
| ApplicationId | Uuid | true |
| InitialApplicationRevision | ulong | true |
| Rows | ImmutableSelectionManifest | true |
| CandidateContent | ImmutableContentRef | true |
| CandidateContentRevision | ulong | true |
| RequiredRetirementProofs | ImmutableSelectionManifest | true |

##### Variant: ImportTargetGroupIntent

| Field | Type | Required |
| --- | --- | --- |
| BatchId | Uuid | true |
| ApplicationId | Uuid | true |
| InitialApplicationRevision | ulong | true |
| Rows | ImmutableSelectionManifest | true |
| CandidateContent | ImmutableContentRef | true |
| CandidateContentRevision | ulong | true |
| RequiredRetirementProofs | ImmutableSelectionManifest | true |


#### Resolved intent schema

ImportTargetGroupIntent

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [T015](../reactors/import-source-admission-reactor.md#t015) | emitted semantic fact | ApplicationImportBatch | ReserveApplicationImportTarget | DeclaredApplication |

### ApplicationImportTargetSettled

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:ApplicationImportTargetSettled |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ApplicationImportTargetSettled |
| Adopted wire | `{"discriminator":"bdgrz.declared_application.import_target_settled","version":1}` |
| Wire qualification | Adopted wire is the occupied discriminator declared in src/Compliance.Common/Features/Applications/Events/ApplicationImportTargetSettled.cs at 60029b8f (2026-10-29 reconciliation). The earlier selected wire is retained as supersededSelectedWire for provenance only. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | `{"discriminator":"bdgrz.declared-application.application-import-target-settled","version":1}` |
| Meaning | The target hold ends with the batch's exact terminal fact. A published disposition promotes the frozen candidate to the application's content without a second revision increment; a canceled or failed disposition discards it. Revision is the published content revision after the settlement. |
| Meaning source | record summary in src/Compliance.Common/Features/Applications/Events/ApplicationImportTargetSettled.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | ApplicationImportTargetSettled |
| Schema discriminator | bdgrz.declared-application.application-import-target-settled |
| Schema discriminatorVersion | 1 |
| Schema owner | DeclaredApplication |
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

#### Content schema: import-target-group-intent.v1

| Property | Value |
| --- | --- |
| field | Intent |
| schema | import-target-group-intent.v1 |
| recordType | ImportTargetGroupIntent |
| rule | One exclusive hold and one native append batch per target group. Preserve existing create_new/link_existing/retire semantics: one create; links do not overwrite authored application metadata; retirement changes active state once. Validate every row against the frozen group baseline and full existing source-claim rules. Apply original row order; no per-row lock upgrade or repeated baseline compare after earlier append. |

##### Fields

| Field | Type | Required |
| --- | --- | --- |
| BatchId | Uuid | true |
| ApplicationId | Uuid | true |
| InitialApplicationRevision | ulong | true |
| Rows | ImmutableSelectionManifest | true |
| CandidateContent | ImmutableContentRef | true |
| CandidateContentRevision | ulong | true |
| RequiredRetirementProofs | ImmutableSelectionManifest | true |

##### Variant: ImportTargetGroupIntent

| Field | Type | Required |
| --- | --- | --- |
| BatchId | Uuid | true |
| ApplicationId | Uuid | true |
| InitialApplicationRevision | ulong | true |
| Rows | ImmutableSelectionManifest | true |
| CandidateContent | ImmutableContentRef | true |
| CandidateContentRevision | ulong | true |
| RequiredRetirementProofs | ImmutableSelectionManifest | true |


#### Resolved intent schema

ImportTargetGroupIntent

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [T021](../reactors/import-source-admission-reactor.md#t021) | emitted semantic fact | ApplicationImportBatch | ReleaseApplicationImportTarget | DeclaredApplication |

### ApplicationRetired

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:ApplicationRetired |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ApplicationRetired |
| Adopted wire | `{"discriminator":"bdgrz.application.retired","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Applications/Events/ApplicationRetired.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | An application was retired effective on a stated date with a reason, optionally merged into a successor application; its history, instances and snapshot references remain. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/Applications/Events/ApplicationRetired.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | ApplicationRetired |
| Schema discriminator | bdgrz.application.retired |
| Schema discriminatorVersion | 1 |
| Schema owner | DeclaredApplication |
| Schema existingContract | src/Compliance.Common/Features/Applications/ApplicationRetired.cs |
| Schema status | selected target schema; implementation pending |
| Schema reason | source command accepted result |
| Schema changes | None listed |
| Schema reducerRule | Validate owner/generation and local predecessor; apply only this owner delta or immutable content reference. Never hydrate a foreign aggregate or mutate based on a projection. |
| Schema serializer | Register concrete target event and every proof/content variant in ComplianceCoreJsonContext and attributed persistence preflight coverage. |
| Schema nativeResponseRule | FirstResponse is non-null only on this logical command first native accepting fact; it references FirstResponseDescriptor. OperationResult is non-null on the declared terminal business fact and references MutationResultContent. Both use verified content and static concrete result serializers; neither is reconstructed from current state. |

#### Event fields

| Field | Type | Required |
| --- | --- | --- |
| TenantId | Uuid | true |
| ApplicationId | Uuid | true |
| Revision | long | true |
| EffectiveAt | DateTimeOffset | true |
| Reason | string | true |
| MergedIntoApplicationId | Uuid? | true |
| ActorMemberId | Uuid | true |
| ActorDisplay | string | true |
| ChangedAt | DateTimeOffset | true |
| MergedIntoApplicationRevision | long? | false |
| Operation | OperationProvenance | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [X002](../reactors/application-name-change-reactor.md#x002) | physical root, logical prerequisite | DeclaredApplication | ReleaseApplicationName | ApplicationNameClaim |
| [X005](../reactors/application-retirement-reactor.md#x005) | physical root, logical prerequisite | DeclaredApplication | RemoveApplicationRelationship | ApplicationRelationship |

### ApplicationRevised

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:ApplicationRevised |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ApplicationRevised |
| Adopted wire | `{"discriminator":"bdgrz.application.revised","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Applications/Events/ApplicationRevised.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | A declared application's name, purpose or owner reference was revised by a named member, recorded as the application's next revision. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/Applications/Events/ApplicationRevised.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | ApplicationRevised |
| Schema discriminator | bdgrz.application.revised |
| Schema discriminatorVersion | 1 |
| Schema owner | DeclaredApplication |
| Schema existingContract | src/Compliance.Common/Features/Applications/ApplicationRevised.cs |
| Schema status | selected target schema; implementation pending |
| Schema reason | source command accepted result |
| Schema changes | None listed |
| Schema reducerRule | Validate owner/generation and local predecessor; apply only this owner delta or immutable content reference. Never hydrate a foreign aggregate or mutate based on a projection. |
| Schema serializer | Register concrete target event and every proof/content variant in ComplianceCoreJsonContext and attributed persistence preflight coverage. |
| Schema nativeResponseRule | FirstResponse is non-null only on this logical command first native accepting fact; it references FirstResponseDescriptor. OperationResult is non-null on the declared terminal business fact and references MutationResultContent. Both use verified content and static concrete result serializers; neither is reconstructed from current state. |

#### Event fields

| Field | Type | Required |
| --- | --- | --- |
| TenantId | Uuid | true |
| ApplicationId | Uuid | true |
| Revision | long | true |
| Name | string | true |
| Purpose | string | true |
| OwnerReference | string? | true |
| ActorMemberId | Uuid | true |
| ActorDisplay | string | true |
| ChangedAt | DateTimeOffset | true |
| Classification | string? | false |
| SystemOwnerPersonId | Uuid? | false |
| AccessOwnerPersonId | Uuid? | false |
| IsRestricted | bool | false |
| ApplicationKind | string? | false |
| Criticality | string? | false |
| Operation | OperationProvenance | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [X000](../reactors/application-name-change-reactor.md#x000) | physical root, logical prerequisite | DeclaredApplication | ClaimApplicationName | ApplicationNameClaim |
| [X001](../reactors/application-name-change-reactor.md#x001) | emitted semantic fact | ApplicationNameClaim | ApplyApplicationNameChange | DeclaredApplication |
| [X002](../reactors/application-name-change-reactor.md#x002) | physical root, logical prerequisite | DeclaredApplication | ReleaseApplicationName | ApplicationNameClaim |


## Wire/schema version discrepancies

- **ApplicationImportEffectPending** — [Placeholder: reconcile schema version 1 with adopted version 2].

[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]
