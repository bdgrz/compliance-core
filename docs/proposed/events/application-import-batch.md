# ApplicationImportBatch events

Canonical events: **9**. Complete canonical event and available schema records: [owner event JSON](../data/events/application-import-batch.json).

| Event | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- |
| ApplicationImportCanceled | business | bdgrz.application_import.canceled | 2 | 11 | The batch is canceled: every target was fenced and nothing was published. |
| ApplicationImportCancellationAccepted | business | bdgrz.application_import.cancellation_accepted | 1 | 8 | The root native acceptance of a cancellation. It is the batch-local fence: a later commit of the batch is refused. Every target is then fenced before any hold is released. |
| ApplicationImportCommitted | business | bdgrz.application_import.committed | 2 | 14 | The one publication fact of a batch: every target is published together. EffectsManifest lists the native proof of every frozen row; FrozenRows is the complete frozen row list. SourceGeneration is the source revision the plan was frozen against. |
| ApplicationImportExecutionAuthorized | reservation | bdgrz.application_import.execution_authorized | 1 | 8 | The batch authorizes execution after its source claim: the target reservations, applications and publication are driven by this fact. It carries the sealed plan's intent unchanged. |
| ApplicationImportFailed | business | bdgrz.application_import.failed | 2 | 12 | A terminal failure of a batch. Existing native business fact, rehomed on the batch. |
| ApplicationImportPlanSealed | business | bdgrz.application_import.plan_sealed | 2 | 12 | The root native acceptance of a batch plan. FrozenPlan is the complete ImportFrozenPlanIntent: every row, target group and source claim, the actors, counts, digest and source cut. It is the root of ApplicationImportExecutionProcess. |
| ApplicationImportRetirementProposalSealed | business | bdgrz.application_import.retirement_proposal_sealed | 2 | 7 | The preparer's omission proposal, sealed with its complete immutable content. It is never authority to retire: acceptance still requires current impact evidence. |
| ApplicationImportRowCorrelated | business | bdgrz.application_import.row_correlated | 2 | 17 | A personal link-existing or create-new decision for one staged row. |
| ApplicationImportStaged | business | bdgrz.application_import.staged | 2 | 14 | The immutable staged observation of a source and the root native acceptance of its submission. The complete row content is the immutable RowsManifest; RowCount and InvalidCount summarize it for readers. |

## Event details

### ApplicationImportCanceled

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:ApplicationImportCanceled |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ApplicationImportCanceled |
| Adopted wire | `{"discriminator":"bdgrz.application_import.canceled","version":2}` |
| Wire qualification | Adopted wire is the occupied discriminator declared in src/Compliance.Common/Features/Applications/Events/ApplicationImportCanceled.cs at 60029b8f (2026-10-29 reconciliation). The earlier selected wire is retained as supersededSelectedWire for provenance only. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | `{"discriminator":"bdgrz.application_import.canceled","version":1}` |
| Meaning | The batch is canceled: every target was fenced and nothing was published. |
| Meaning source | record summary in src/Compliance.Common/Features/Applications/Events/ApplicationImportCanceled.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | ApplicationImportCanceled |
| Schema discriminator | bdgrz.application_import.canceled |
| Schema discriminatorVersion | 1 |
| Schema owner | ApplicationImportBatch |
| Schema existingContract | src/Compliance.Common/Features/Applications/ApplicationImportCanceled.cs |
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
| BatchId | Uuid | true |
| Revision | long | true |
| Reason | string | true |
| ActorMemberId | Uuid | true |
| ActorDisplay | string | true |
| CanceledAt | DateTimeOffset | true |
| Operation | OperationProvenance | true |
| Intent | ImmutableContentRef | true |
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
| [T019](../reactors/import-cancellation-reactor.md#t019) | emitted semantic fact | ApplicationImportBatch | CompleteApplicationImportCancellation | ApplicationImportBatch |
| [T020](../reactors/import-source-admission-reactor.md#t020) | logical prerequisite | ApplicationImportBatch | ReleaseApplicationImportSource | ApplicationImportSource |
| [T021](../reactors/import-source-admission-reactor.md#t021) | logical prerequisite | ApplicationImportBatch | ReleaseApplicationImportTarget | DeclaredApplication |

### ApplicationImportCancellationAccepted

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:ApplicationImportCancellationAccepted |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ApplicationImportCancellationAccepted |
| Adopted wire | `{"discriminator":"bdgrz.application_import.cancellation_accepted","version":1}` |
| Wire qualification | Adopted wire is the occupied discriminator declared in src/Compliance.Common/Features/Applications/Events/ApplicationImportCancellationAccepted.cs at 60029b8f (2026-10-29 reconciliation). The earlier selected wire is retained as supersededSelectedWire for provenance only. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | `{"discriminator":"bdgrz.application-import-batch.application-import-cancellation-accepted","version":1}` |
| Meaning | The root native acceptance of a cancellation. It is the batch-local fence: a later commit of the batch is refused. Every target is then fenced before any hold is released. |
| Meaning source | record summary in src/Compliance.Common/Features/Applications/Events/ApplicationImportCancellationAccepted.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | ApplicationImportCancellationAccepted |
| Schema discriminator | bdgrz.application-import-batch.application-import-cancellation-accepted |
| Schema discriminatorVersion | 1 |
| Schema owner | ApplicationImportBatch |
| Schema existingContract | null (recorded) |
| Schema status | selected target schema; implementation pending |
| Schema reason | source command accepted result |
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
| [T018](../reactors/import-cancellation-reactor.md#t018) | physical root, logical prerequisite | ApplicationImportBatch | FenceApplicationImportTarget | DeclaredApplication |
| [T019](../reactors/import-cancellation-reactor.md#t019) | physical root, logical prerequisite | ApplicationImportBatch | CompleteApplicationImportCancellation | ApplicationImportBatch |
| [T020](../reactors/import-source-admission-reactor.md#t020) | physical root | ApplicationImportBatch | ReleaseApplicationImportSource | ApplicationImportSource |
| [T021](../reactors/import-source-admission-reactor.md#t021) | physical root | ApplicationImportBatch | ReleaseApplicationImportTarget | DeclaredApplication |
| [A013](../reactors/unassigned-architecture-rows.md#a013) | emitted semantic fact | ApplicationImportBatch | CancelApplicationImport | ApplicationImportBatch |

### ApplicationImportCommitted

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:ApplicationImportCommitted |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ApplicationImportCommitted |
| Adopted wire | `{"discriminator":"bdgrz.application_import.committed","version":2}` |
| Wire qualification | Adopted wire is the occupied discriminator declared in src/Compliance.Common/Features/Applications/Events/ApplicationImportCommitted.cs at 60029b8f (2026-10-29 reconciliation). The earlier selected wire is retained as supersededSelectedWire for provenance only. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | `{"discriminator":"bdgrz.application_import.committed","version":1}` |
| Meaning | The one publication fact of a batch: every target is published together. EffectsManifest lists the native proof of every frozen row; FrozenRows is the complete frozen row list. SourceGeneration is the source revision the plan was frozen against. |
| Meaning source | record summary in src/Compliance.Common/Features/Applications/Events/ApplicationImportCommitted.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | ApplicationImportCommitted |
| Schema discriminator | bdgrz.application_import.committed |
| Schema discriminatorVersion | 1 |
| Schema owner | ApplicationImportBatch |
| Schema existingContract | src/Compliance.Common/Features/Applications/ApplicationImportCommitted.cs |
| Schema status | selected target schema; implementation pending |
| Schema reason | typed reactor receiver accepted business fact |
| Schema changes | Externalize complete IReadOnlyList<ApplicationImportEffectProof> to verified immutable content; preserve all items/order/identity, no truncation. |
| Schema reducerRule | Validate owner/generation and local predecessor; apply only this owner delta or immutable content reference. Never hydrate a foreign aggregate or mutate based on a projection. |
| Schema serializer | Register concrete target event and every proof/content variant in ComplianceCoreJsonContext and attributed persistence preflight coverage. |
| Schema nativeResponseRule | FirstResponse is non-null only on this logical command first native accepting fact; it references FirstResponseDescriptor. OperationResult is non-null on the declared terminal business fact and references MutationResultContent. Both use verified content and static concrete result serializers; neither is reconstructed from current state. |

#### Event fields

| Field | Type | Required |
| --- | --- | --- |
| TenantId | Uuid | true |
| SourceKey | string | true |
| SourceNamespace | string | true |
| BatchId | Uuid | true |
| Revision | long | true |
| PlanSha256 | string | true |
| EffectsManifest | ImmutableContentRef | true |
| CommittedAt | DateTimeOffset | true |
| Operation | OperationProvenance | true |
| FrozenRows | ImmutableContentRef | true |
| SourceGeneration | long | true |
| Intent | ImmutableContentRef | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

#### Nested content schemas

#### Content schema: ApplicationImportCommitted.EffectsManifest.v1

| Property | Value |
| --- | --- |
| field | EffectsManifest |
| schema | ApplicationImportCommitted.EffectsManifest.v1 |
| contentType | IReadOnlyList<ApplicationImportEffectProof> |
| rule | Full exact original collection with complete count and digest; readers verify content before use. |

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
| [T017](../reactors/import-source-admission-reactor.md#t017) | emitted semantic fact | ApplicationImportBatch | CommitApplicationImportBatch | ApplicationImportBatch |
| [T020](../reactors/import-source-admission-reactor.md#t020) | logical prerequisite | ApplicationImportBatch | ReleaseApplicationImportSource | ApplicationImportSource |
| [T021](../reactors/import-source-admission-reactor.md#t021) | logical prerequisite | ApplicationImportBatch | ReleaseApplicationImportTarget | DeclaredApplication |

### ApplicationImportExecutionAuthorized

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:ApplicationImportExecutionAuthorized |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ApplicationImportExecutionAuthorized |
| Adopted wire | `{"discriminator":"bdgrz.application_import.execution_authorized","version":1}` |
| Wire qualification | Adopted wire is the occupied discriminator declared in src/Compliance.Common/Features/Applications/Events/ApplicationImportExecutionAuthorized.cs at 60029b8f (2026-10-29 reconciliation). The earlier selected wire is retained as supersededSelectedWire for provenance only. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | `{"discriminator":"bdgrz.application-import-batch.application-import-execution-authorized","version":1}` |
| Meaning | The batch authorizes execution after its source claim: the target reservations, applications and publication are driven by this fact. It carries the sealed plan's intent unchanged. |
| Meaning source | record summary in src/Compliance.Common/Features/Applications/Events/ApplicationImportExecutionAuthorized.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | ApplicationImportExecutionAuthorized |
| Schema discriminator | bdgrz.application-import-batch.application-import-execution-authorized |
| Schema discriminatorVersion | 1 |
| Schema owner | ApplicationImportBatch |
| Schema existingContract | null (recorded) |
| Schema status | selected target schema; implementation pending |
| Schema reason | source command accepted result |
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
| [T015](../reactors/import-source-admission-reactor.md#t015) | logical prerequisite | ApplicationImportBatch | ReserveApplicationImportTarget | DeclaredApplication |
| [T016](../reactors/import-source-admission-reactor.md#t016) | logical prerequisite | ApplicationImportBatch | ApplyApplicationImportEffect | DeclaredApplication |
| [T017](../reactors/import-source-admission-reactor.md#t017) | logical prerequisite | ApplicationImportBatch | CommitApplicationImportBatch | ApplicationImportBatch |
| [N000](../reactors/import-source-admission-reactor.md#n000) | emitted semantic fact | ApplicationImportBatch | ExecuteApplicationImport | ApplicationImportBatch |

### ApplicationImportFailed

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:ApplicationImportFailed |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ApplicationImportFailed |
| Adopted wire | `{"discriminator":"bdgrz.application_import.failed","version":2}` |
| Wire qualification | Adopted wire is the occupied discriminator declared in src/Compliance.Common/Features/Applications/Events/ApplicationImportFailed.cs at 60029b8f (2026-10-29 reconciliation). The earlier selected wire is retained as supersededSelectedWire for provenance only. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | `{"discriminator":"bdgrz.application_import.failed","version":1}` |
| Meaning | A terminal failure of a batch. Existing native business fact, rehomed on the batch. |
| Meaning source | record summary in src/Compliance.Common/Features/Applications/Events/ApplicationImportFailed.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | ApplicationImportFailed |
| Schema discriminator | bdgrz.application_import.failed |
| Schema discriminatorVersion | 1 |
| Schema owner | ApplicationImportBatch |
| Schema existingContract | src/Compliance.Common/Features/Applications/ApplicationImportFailed.cs |
| Schema status | selected target schema; implementation pending |
| Schema reason | existing native business fact, rehomed with its invariant |
| Schema changes | None listed |
| Schema reducerRule | Validate owner/generation and local predecessor; apply only this owner delta or immutable content reference. Never hydrate a foreign aggregate or mutate based on a projection. |
| Schema serializer | Register concrete target event and every proof/content variant in ComplianceCoreJsonContext and attributed persistence preflight coverage. |
| Schema nativeResponseRule | FirstResponse is non-null only on this logical command first native accepting fact; it references FirstResponseDescriptor. OperationResult is non-null on the declared terminal business fact and references MutationResultContent. Both use verified content and static concrete result serializers; neither is reconstructed from current state. |

#### Event fields

| Field | Type | Required |
| --- | --- | --- |
| TenantId | Uuid | true |
| SourceKey | string | true |
| SourceNamespace | string | true |
| BatchId | Uuid | true |
| Revision | long | true |
| PlanSha256 | string | true |
| RowId | Uuid? | true |
| FailureCode | string | true |
| FailedAt | DateTimeOffset | true |
| Operation | OperationProvenance | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [T020](../reactors/import-source-admission-reactor.md#t020) | logical prerequisite | ApplicationImportBatch | ReleaseApplicationImportSource | ApplicationImportSource |
| [T021](../reactors/import-source-admission-reactor.md#t021) | logical prerequisite | ApplicationImportBatch | ReleaseApplicationImportTarget | DeclaredApplication |

### ApplicationImportPlanSealed

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:ApplicationImportPlanSealed |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ApplicationImportPlanSealed |
| Adopted wire | `{"discriminator":"bdgrz.application_import.plan_sealed","version":2}` |
| Wire qualification | Adopted wire is the occupied discriminator declared in src/Compliance.Common/Features/Applications/Events/ApplicationImportPlanSealed.cs at 60029b8f (2026-10-29 reconciliation). The earlier selected wire is retained as supersededSelectedWire for provenance only. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | `{"discriminator":"bdgrz.application_import.plan_sealed","version":1}` |
| Meaning | The root native acceptance of a batch plan. FrozenPlan is the complete ImportFrozenPlanIntent: every row, target group and source claim, the actors, counts, digest and source cut. It is the root of ApplicationImportExecutionProcess. |
| Meaning source | record summary in src/Compliance.Common/Features/Applications/Events/ApplicationImportPlanSealed.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | ApplicationImportPlanSealed |
| Schema discriminator | bdgrz.application_import.plan_sealed |
| Schema discriminatorVersion | 1 |
| Schema owner | ApplicationImportBatch |
| Schema existingContract | src/Compliance.Common/Features/Applications/ApplicationImportPlanSealed.cs |
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
| BatchId | Uuid | true |
| Revision | long | true |
| PlanSha256 | string | true |
| Operation | OperationProvenance | true |
| FrozenPlan | ImmutableContentRef | true |
| RowCount | long | true |
| TargetGroupCount | long | true |
| SourceKey | string | true |
| SourceNamespace | string | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

#### Nested content schemas

#### Content schema: import-frozen-plan-intent.v1

| Property | Value |
| --- | --- |
| field | FrozenPlan |
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
| [T014](../reactors/import-source-admission-reactor.md#t014) | physical root, logical prerequisite | ApplicationImportBatch | ClaimApplicationImportSource | ApplicationImportSource |
| [T015](../reactors/import-source-admission-reactor.md#t015) | physical root | ApplicationImportBatch | ReserveApplicationImportTarget | DeclaredApplication |
| [T016](../reactors/import-source-admission-reactor.md#t016) | physical root | ApplicationImportBatch | ApplyApplicationImportEffect | DeclaredApplication |
| [T017](../reactors/import-source-admission-reactor.md#t017) | physical root | ApplicationImportBatch | CommitApplicationImportBatch | ApplicationImportBatch |
| [T020](../reactors/import-source-admission-reactor.md#t020) | physical root | ApplicationImportBatch | ReleaseApplicationImportSource | ApplicationImportSource |
| [T021](../reactors/import-source-admission-reactor.md#t021) | physical root | ApplicationImportBatch | ReleaseApplicationImportTarget | DeclaredApplication |
| [N000](../reactors/import-source-admission-reactor.md#n000) | physical root, logical prerequisite | ApplicationImportBatch | ExecuteApplicationImport | ApplicationImportBatch |
| [A013](../reactors/unassigned-architecture-rows.md#a013) | physical root, logical prerequisite | ApplicationImportBatch | CancelApplicationImport | ApplicationImportBatch |

### ApplicationImportRetirementProposalSealed

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:ApplicationImportRetirementProposalSealed |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ApplicationImportRetirementProposalSealed |
| Adopted wire | `{"discriminator":"bdgrz.application_import.retirement_proposal_sealed","version":2}` |
| Wire qualification | Adopted wire is the occupied discriminator declared in src/Compliance.Common/Features/Applications/Events/ApplicationImportRetirementProposalSealed.cs at 60029b8f (2026-10-29 reconciliation). The earlier selected wire is retained as supersededSelectedWire for provenance only. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | `{"discriminator":"bdgrz.application_import.retirement_proposal_sealed","version":1}` |
| Meaning | The preparer's omission proposal, sealed with its complete immutable content. It is never authority to retire: acceptance still requires current impact evidence. |
| Meaning source | record summary in src/Compliance.Common/Features/Applications/Events/ApplicationImportRetirementProposalSealed.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | ApplicationImportRetirementProposalSealed |
| Schema discriminator | bdgrz.application_import.retirement_proposal_sealed |
| Schema discriminatorVersion | 1 |
| Schema owner | ApplicationImportBatch |
| Schema existingContract | src/Compliance.Common/Features/Applications/ApplicationImportRetirementProposalSealed.cs |
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
| BatchId | Uuid | true |
| Revision | long | true |
| ProposalSha256 | string | true |
| Operation | OperationProvenance | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### ApplicationImportRowCorrelated

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:ApplicationImportRowCorrelated |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ApplicationImportRowCorrelated |
| Adopted wire | `{"discriminator":"bdgrz.application_import.row_correlated","version":2}` |
| Wire qualification | Adopted wire is the occupied discriminator declared in src/Compliance.Common/Features/Applications/Events/ApplicationImportRowCorrelated.cs at 60029b8f (2026-10-29 reconciliation). The earlier selected wire is retained as supersededSelectedWire for provenance only. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | `{"discriminator":"bdgrz.application_import.row_correlated","version":1}` |
| Meaning | A personal link-existing or create-new decision for one staged row. |
| Meaning source | record summary in src/Compliance.Common/Features/Applications/Events/ApplicationImportRowCorrelated.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | ApplicationImportRowCorrelated |
| Schema discriminator | bdgrz.application_import.row_correlated |
| Schema discriminatorVersion | 1 |
| Schema owner | ApplicationImportBatch |
| Schema existingContract | src/Compliance.Common/Features/Applications/ApplicationImportRowCorrelated.cs |
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
| SourceKey | string | true |
| SourceNamespace | string | true |
| BatchId | Uuid | true |
| Revision | long | true |
| RowId | Uuid | true |
| SourceRecordId | string | true |
| Decision | string | true |
| ApplicationId | Uuid | true |
| ExpectedApplicationRevision | long? | true |
| Reason | string | true |
| ActorMemberId | Uuid | true |
| ActorDisplay | string | true |
| RecordedAt | DateTimeOffset | true |
| Operation | OperationProvenance | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### ApplicationImportStaged

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:ApplicationImportStaged |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ApplicationImportStaged |
| Adopted wire | `{"discriminator":"bdgrz.application_import.staged","version":2}` |
| Wire qualification | Adopted wire is the occupied discriminator declared in src/Compliance.Common/Features/Applications/Events/ApplicationImportStaged.cs at 60029b8f (2026-10-29 reconciliation). The earlier selected wire is retained as supersededSelectedWire for provenance only. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | `{"discriminator":"bdgrz.application_import.staged","version":1}` |
| Meaning | The immutable staged observation of a source and the root native acceptance of its submission. The complete row content is the immutable RowsManifest; RowCount and InvalidCount summarize it for readers. |
| Meaning source | record summary in src/Compliance.Common/Features/Applications/Events/ApplicationImportStaged.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | ApplicationImportStaged |
| Schema discriminator | bdgrz.application_import.staged |
| Schema discriminatorVersion | 1 |
| Schema owner | ApplicationImportBatch |
| Schema existingContract | src/Compliance.Common/Features/Applications/ApplicationImportStaged.cs |
| Schema status | selected target schema; implementation pending |
| Schema reason | source command accepted result |
| Schema changes | Externalize complete IReadOnlyList<ApplicationImportStagedRow> to verified immutable content; preserve all items/order/identity, no truncation. |
| Schema reducerRule | Validate owner/generation and local predecessor; apply only this owner delta or immutable content reference. Never hydrate a foreign aggregate or mutate based on a projection. |
| Schema serializer | Register concrete target event and every proof/content variant in ComplianceCoreJsonContext and attributed persistence preflight coverage. |
| Schema nativeResponseRule | FirstResponse is non-null only on this logical command first native accepting fact; it references FirstResponseDescriptor. OperationResult is non-null on the declared terminal business fact and references MutationResultContent. Both use verified content and static concrete result serializers; neither is reconstructed from current state. |

#### Event fields

| Field | Type | Required |
| --- | --- | --- |
| TenantId | Uuid | true |
| BatchId | Uuid | true |
| SubmissionId | Uuid | true |
| SourceKey | string | true |
| SourceNamespace | string | true |
| Coverage | string | true |
| ContentSha256 | string | true |
| RowsManifest | ImmutableContentRef | true |
| ActorMemberId | Uuid | true |
| ActorDisplay | string | true |
| SubmittedAt | DateTimeOffset | true |
| Operation | OperationProvenance | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

#### Nested content schemas

#### Content schema: ApplicationImportStaged.RowsManifest.v1

| Property | Value |
| --- | --- |
| field | RowsManifest |
| schema | ApplicationImportStaged.RowsManifest.v1 |
| contentType | IReadOnlyList<ApplicationImportStagedRow> |
| rule | Full exact original collection with complete count and digest; readers verify content before use. |


[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]


## Wire/schema version discrepancies

- **ApplicationImportCanceled** — [Placeholder: reconcile schema version 1 with adopted version 2].
- **ApplicationImportCommitted** — [Placeholder: reconcile schema version 1 with adopted version 2].
- **ApplicationImportFailed** — [Placeholder: reconcile schema version 1 with adopted version 2].
- **ApplicationImportPlanSealed** — [Placeholder: reconcile schema version 1 with adopted version 2].
- **ApplicationImportRetirementProposalSealed** — [Placeholder: reconcile schema version 1 with adopted version 2].
- **ApplicationImportRowCorrelated** — [Placeholder: reconcile schema version 1 with adopted version 2].
- **ApplicationImportStaged** — [Placeholder: reconcile schema version 1 with adopted version 2].

[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]
