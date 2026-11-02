# ApplicationImportSource events

Canonical events: **2**. Complete canonical event and available schema records: [owner event JSON](../data/events/application-import-source.json).

| Event | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- |
| ApplicationImportSourceAdmissionRecorded | business | bdgrz.application_import_source.admission_recorded | 1 | 8 | The source admits exactly one batch. Revision is the source's logical revision after this fact; the plan was frozen against the revision before it. |
| ApplicationImportSourceSettled | business | bdgrz.application_import_source.settled | 1 | 8 | The source claim of one batch ends with the batch's exact terminal fact. A published batch adds its claims to the source; a settlement before any admission fences the batch against a late claim. |

## Event details

### ApplicationImportSourceAdmissionRecorded

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:ApplicationImportSourceAdmissionRecorded |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ApplicationImportSourceAdmissionRecorded |
| Adopted wire | `{"discriminator":"bdgrz.application_import_source.admission_recorded","version":1}` |
| Wire qualification | Adopted wire is the occupied discriminator declared in src/Compliance.Common/Features/Applications/Events/ApplicationImportSourceAdmissionRecorded.cs at 60029b8f (2026-10-29 reconciliation). The earlier selected wire is retained as supersededSelectedWire for provenance only. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | `{"discriminator":"bdgrz.application-import-source.application-import-source-admission-recorded","version":1}` |
| Meaning | The source admits exactly one batch. Revision is the source's logical revision after this fact; the plan was frozen against the revision before it. |
| Meaning source | record summary in src/Compliance.Common/Features/Applications/Events/ApplicationImportSourceAdmissionRecorded.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | ApplicationImportSourceAdmissionRecorded |
| Schema discriminator | bdgrz.application-import-source.application-import-source-admission-recorded |
| Schema discriminatorVersion | 1 |
| Schema owner | ApplicationImportSource |
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

#### Content schema: import-frozen-plan-intent.v1

| Property | Value |
| --- | --- |
| field | Intent |
| schema | import-frozen-plan-intent.v1 |
| recordType | ImportFrozenPlanIntent |
| rule | Sealed plan contains every business value previously distributed across PlanStarted/PlanRowFrozen. PlanSha256 is the digest of this full concrete intent, not only a row count/hash. Incoming rows retain source order and retirement rows retain the existing deterministic proposal ordering. No omitted claim may be inferred from projection absence. |

##### Fields

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| BatchId | Uuid | true |
| SourceKey | string | true |
| SourceNamespace | string | true |
| Coverage | string | true |
| ContentSha256 | string | true |
| SubmitterMemberId | Uuid | true |
| ApproverMemberId | Uuid | true |
| Rows | ImmutableSelectionManifest | true |
| TargetGroups | ImmutableSelectionManifest | true |
| SourceClaims | ImmutableSelectionManifest | true |
| OmissionProposal | ImmutableContentRef? | true |
| SourceCut | AuthoritativeReadCut | true |
| SubmitterDisplay | string | true |
| ApproverDisplay | string | true |

##### Variant: ImportFrozenPlanIntent

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| BatchId | Uuid | true |
| SourceKey | string | true |
| SourceNamespace | string | true |
| Coverage | string | true |
| ContentSha256 | string | true |
| SubmitterMemberId | Uuid | true |
| ApproverMemberId | Uuid | true |
| Rows | ImmutableSelectionManifest | true |
| TargetGroups | ImmutableSelectionManifest | true |
| SourceClaims | ImmutableSelectionManifest | true |
| OmissionProposal | ImmutableContentRef? | true |
| SourceCut | AuthoritativeReadCut | true |
| SubmitterDisplay | string | true |
| ApproverDisplay | string | true |


#### Resolved intent schema

ImportFrozenPlanIntent

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [T014](../reactors/import-source-admission-reactor.md#t014) | emitted semantic fact | ApplicationImportBatch | ClaimApplicationImportSource | ApplicationImportSource |
| [N000](../reactors/import-source-admission-reactor.md#n000) | logical prerequisite | ApplicationImportBatch | ExecuteApplicationImport | ApplicationImportBatch |

### ApplicationImportSourceSettled

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:ApplicationImportSourceSettled |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ApplicationImportSourceSettled |
| Adopted wire | `{"discriminator":"bdgrz.application_import_source.settled","version":1}` |
| Wire qualification | Adopted wire is the occupied discriminator declared in src/Compliance.Common/Features/Applications/Events/ApplicationImportSourceSettled.cs at 60029b8f (2026-10-29 reconciliation). The earlier selected wire is retained as supersededSelectedWire for provenance only. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | `{"discriminator":"bdgrz.application-import-source.application-import-source-settled","version":1}` |
| Meaning | The source claim of one batch ends with the batch's exact terminal fact. A published batch adds its claims to the source; a settlement before any admission fences the batch against a late claim. |
| Meaning source | record summary in src/Compliance.Common/Features/Applications/Events/ApplicationImportSourceSettled.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | ApplicationImportSourceSettled |
| Schema discriminator | bdgrz.application-import-source.application-import-source-settled |
| Schema discriminatorVersion | 1 |
| Schema owner | ApplicationImportSource |
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

#### Content schema: import-frozen-plan-intent.v1

| Property | Value |
| --- | --- |
| field | Intent |
| schema | import-frozen-plan-intent.v1 |
| recordType | ImportFrozenPlanIntent |
| rule | Sealed plan contains every business value previously distributed across PlanStarted/PlanRowFrozen. PlanSha256 is the digest of this full concrete intent, not only a row count/hash. Incoming rows retain source order and retirement rows retain the existing deterministic proposal ordering. No omitted claim may be inferred from projection absence. |

##### Fields

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| BatchId | Uuid | true |
| SourceKey | string | true |
| SourceNamespace | string | true |
| Coverage | string | true |
| ContentSha256 | string | true |
| SubmitterMemberId | Uuid | true |
| ApproverMemberId | Uuid | true |
| Rows | ImmutableSelectionManifest | true |
| TargetGroups | ImmutableSelectionManifest | true |
| SourceClaims | ImmutableSelectionManifest | true |
| OmissionProposal | ImmutableContentRef? | true |
| SourceCut | AuthoritativeReadCut | true |
| SubmitterDisplay | string | true |
| ApproverDisplay | string | true |

##### Variant: ImportFrozenPlanIntent

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| BatchId | Uuid | true |
| SourceKey | string | true |
| SourceNamespace | string | true |
| Coverage | string | true |
| ContentSha256 | string | true |
| SubmitterMemberId | Uuid | true |
| ApproverMemberId | Uuid | true |
| Rows | ImmutableSelectionManifest | true |
| TargetGroups | ImmutableSelectionManifest | true |
| SourceClaims | ImmutableSelectionManifest | true |
| OmissionProposal | ImmutableContentRef? | true |
| SourceCut | AuthoritativeReadCut | true |
| SubmitterDisplay | string | true |
| ApproverDisplay | string | true |


#### Resolved intent schema

ImportFrozenPlanIntent

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [T020](../reactors/import-source-admission-reactor.md#t020) | emitted semantic fact | ApplicationImportBatch | ReleaseApplicationImportSource | ApplicationImportSource |




[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]
