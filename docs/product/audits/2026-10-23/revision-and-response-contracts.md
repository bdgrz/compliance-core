# Revision scopes, admission responses and coherent reads

This contract replaces ambiguous references to “the revision” in earlier sketches. Public expected revisions, source proof versions, generations and physical offsets have distinct meanings.

| Value | Meaning | Changes when |
|---|---|---|
| Public `Revision` / `ResourceRevision` | Logical revision of the named public resource or compact parent admission authority | A named business mutation/admission for that resource succeeds. Source-use grants/settlements and technical attempts do not advance it. |
| Immutable content/version ID | Exact authored/approved version, e.g. PackageVersion, PlanVersionId, assessment fingerprint | That content/version is explicitly replaced under its owning policy. It is never inferred from a stream position. |
| Proof `OwnerRevision` | `DomainEvent.Metadata.AggregateVersion` for the exact native event | Each state-changing native event, including source-use authorization, advances Portia state version; audits follow Portia semantics. |
| `PhysicalPosition` / resource offset | Native record's zero-based resource-stream offset | Every persisted record, including audit records. Expected append position is the hydrated physical tail, not a public revision. |
| Source `Generation` | Frozen authorization episode of the original admitted workflow | A new admission episode; it never changes for that operation's continuations. |
| `ReservationGeneration` | Receiver-local fencing token allocated with the first grant for a ReservationKey | A new receiving reservation. Exact acquire retry returns the original token. |

Every public command's `expectedRevisionScope` names the counter it checks. Existing `ExpectedRevision` fields that remain retain the owning resource's logical counter; source/content revision fields retain their distinct meaning. Internal receiver `ExpectedRevision` is the original frozen logical target revision, or explicitly null when only the exact authorization/generation plus physical OCC is required. It must not be replaced with a later revision to make a stale intent pass.

## First owner and public fields

| Command | First mutation owner | Required expected value | Child behavior |
|---|---|---|---|
| RecordBulkAccessDecision | AccessReviewCampaign | ExpectedCampaignRevision | Admit complete preview/selection, then create batch at revision 0. Each selected item has its own frozen ExpectedItemRevision. |
| RunReadinessAssessment | ProgramReadiness | ExpectedProgramRevision | Admit one pending latest adoption, freeze inputs, compute assessment at its own new revision, then adopt exact admission generation. |
| LinkProviderCoverageGapRiskAcceptance | ProviderAssuranceGap | ExpectedGapRevision | Admit open-gap/unique-pair linkage, then materialize immutable ProviderRiskAcceptance. |
| AddCorrectiveAction | FindingAggregate | ExpectedFindingRevision | Admit action and finding generation, then materialize independent action at revision 0. |
| CreateAuditResponseDraft / RecordAuditorRequestComment | AuditorRequest | ExpectedRequestRevision | Admit complete response/comment before child materialization. |
| RecordAdvisorFeedback | AdvisorHandoff | ExpectedHandoffRevision | Admit exact selected version while open/ready before thread materialization. |
| AcknowledgePolicy / RecordTrainingCompletion / ApproveCampaignWaiver | PolicyDistributionCampaign | ExpectedCampaignRevision | Admit complete participant fact; child writes use the accepted proof. |
| RequestAuditPackageBuild / RecordAuditPackageDelivery | AuditPackageManifest | ExpectedManifestRevision | Freeze exact version/build or personal handoff recording. A new child revision is not compared to the parent revision. |

Parent admission counters advance on their accepted business admission, final decision/adoption, reopen or explicit rejected/canceled admission. Content-record counters (Provider, Location, OperationalProcess) advance on final record content/lifecycle facts; pending accepted changes use a separate generation and block conflicting new writes. Immutable child counters start at 1 on materialization. Existing single-owner business counters remain defined by their current methods and are scoped to that same owner after narrowing.

Import application public content revision preserves existing semantics: create establishes revision 1; link_existing does not overwrite application content or advance its content revision; retire advances content revision once. Provisional row/native permission facts advance physical state, not published application content. A pending candidate has a frozen CandidateContentRevision. Publication-cut reads return that candidate revision only when its batch is published; local settlement then promotes it without a second content revision increment. Bulk item decisions use the same pattern: provisional facts do not replace the published decision, and canceled provisional facts never advance its public revision. The item carries its exact planned resulting revision; publication and settlement expose it once.

Source-use grants can coexist over one unchanged content event even if OwnerRevision/physical tail advance. A writer with a matching public revision is still blocked while an incompatible use is active. After settlement, that same public revision can be used if content did not change. A completed-parent settlement additionally installs the parent-generation closure fence; releasing the use cannot make the child writable again. A canceled-parent settlement clears only this authorization and fences delayed acquisition for its exact key.

GET detail/list DTOs return `EntityConcurrency { ResourceType, ResourceId, Revision, Episode }` for each relevant resource. Parent and child tokens are distinct: campaign and items, program readiness and assessments/plans, gap and links, finding and actions, request and responses, package manifest and deliveries. Existing flat parent revision fields cannot be copied onto every child. An old result stores its own original revisions; never fill them from a later projection row.

## Original write response and status

A root native admission freezes its first response descriptor in a verified immutable content reference before append. The descriptor is bound to the native fact, not a separate receipt event.

- Immediate completion: HTTP 200 with the existing typed Common result; Unit is an empty object. Every exact write retry returns the same status and original result.
- Work requiring continuation: HTTP 202 with `OperationAcceptedResponse { OperationId, StatusUrl, AcceptedAt }`. Every exact write retry returns this same original 202/body, including after completion. It never changes to a new completed write response.
- GET `StatusUrl`: returns `OperationStatusResponse`, which can progress from pending to completed/conflict/failed/unknown and exposes an authorized typed result descriptor. GET its ResultUrl to obtain the immutable original Common result; no raw content-store reference is public. Authorization for the original resource is applied before lookup.

Tenant status path is `/api/v1/tenants/{tenant_id}/operations/{operation_id}`; platform path is `/api/v1/platform/operations/{operation_id}`. OperationId is the logical UUID carried by IIdentifiedRequest, including deterministic UUID derivation from an existing required BuildKey/GenerationKey/source tuple. These GET requests are ordinary authorized query handlers (`GetOperationStatusHandler` and `GetPlatformOperationStatusHandler`); they cannot execute or retry work. Malformed IDs are validation errors; absent route segments do not bind. Domain intent/revision conflicts map to Conflict; current visibility rules retain their existing forbidden/not-found behavior.

A continuation result uses its own LogicalId and kind, with RootOperationId and CausationEventId linking it to the exact original root. The root resolver verifies that causal chain and expected terminal fact kind. It cannot adopt any unrelated native fact that merely repeats a RootOperationId. Native first-binding lookup and terminal result lookup validate source owner, type, episode and digest. Technical build failure may be reported as a technical process outcome, but no gate treats it as an approved build/archive fact.

Required terminal fact rules are in `operation-outcome-contracts.json`. A retry checks accepted native intent before reading mutable dependencies, generating server timestamps or evaluating current content versions. On OCC/guard failure, recheck the authoritative binding before returning failure, since an identical concurrent invocation may already have committed.

## Fixed-cut publication reads

All composed reads that can expose import or bulk provisional effects capture one committed global cut per response and select only publication facts within it. Reuse that cut across all pages and resources in the response. A publication fact is immutable; positive cache entries are safe. A missing publication outside this fixed cut cannot be refreshed midway through the response. Native target candidates and publication proofs are verified before returning a new version. Missing/corrupt content or incomplete source coverage produces a transient/fail-closed response, never a partial new batch.

Fitz 2.0 snapshot continuation tokens apply to **global selectors**; passing a captured watermark/fingerprint to a realm selector is rejected by the broker. The selected adapter uses the existing global read facility:

1. Start `IStreamClient.ReadPageAsync("stream://**", ...)` with no filter to capture `Cursor.CapturedWatermark` and its cursor fingerprint. This watermark is an exclusive global frontier, W. Empty W=0 is valid.
2. Use global area/resource selectors to fetch required native facts. Each selector has its own fresh broker snapshot W2 >= W and its own latest continuation fingerprint. Do not reuse another selector's fingerprint. Keep only records whose GlobalOffset < W, verify concrete requested realm before deserializing/returning business content, and stop after coverage reaches W.
3. Resume each selector using its most recently returned fingerprint, captured W2 and next global offset. Reject filtered/retained-away relevant records as incomplete authority. A page size is not a lifetime limit. A completed empty scan records checkpoint W=0, never offset 1.
4. Persist source cuts and source event/resource/global positions in the immutable read snapshot. Query projections can accelerate discovery only when fenced to this same cut and followed by complete authoritative tails. No projection absence proves completeness.

`IAuthoritativeSnapshotReader` is a backend internal adapter over the pinned Fitz IStreamClient and the concrete domain serializer. It exposes `CaptureAsync()` and `ReadAtAsync(selector, tenantScope, cut, cursor)`; it does not append or grant authority. This is not a new broker snapshot guarantee. Pinned 2.0 XML documentation confirms StreamReadCursor.CapturedWatermark/Fingerprint, and local broker source explicitly restricts continuation to global selectors. Public callers cannot invoke global scans or obtain another realm's data.

Required races include two overlapping successive batches while a list is paged, a publication appearing after W, delayed target settlement, a missing projected target after publication, selector-token mismatch, empty cut followed by first append, and resource/realm/global offset confusion.

Sample selection first compares ExpectedRequestRevision on AuditorRequest, then creates independent selection revision one. Personal engagement decisions compare ExpectedEngagementRevision and ExpectedServiceEpoch; client previews compare ExpectedServiceEpoch. Neither compares the former tenant-ledger sequence against a new child. Protected download admission uses ExpectedArtifactRevision, returns a fixed 202, and exposes the original encrypted ticket only through currently authorized result GET.

Authored signed long revision/version/generation inputs must be nonnegative (creation zero only where declared); convert to metadata/unsigned representations with checked conversion. A generated append position or reservation generation never fills a missing authored expected field.
