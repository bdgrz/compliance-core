# Command and handler gaps

This index makes the missing command paths explicit. The current aggregate catalog covers 389 existing command-to-owner links. It does not turn an unregistered producer method into a supported API. The current source map classifies 41 producer operations without a complete registered request path: 17 need an owner request/handler, 6 are invoked directly by reactors, 10 by the digest processor, and 8 are obsolete legacy producers to remove after cutover.

## Existing command contracts with no handler

| Command | Handler to add | Aggregate owner(s) | Guard and completion proof | Required focused tests |
|---|---|---|---|---|
| `AmendTypeIIPeriodClose` | `AmendTypeIIPeriodCloseHandler` | `TypeIIPeriod` | Authorize the close authority; bind exact period revision, accepted close snapshot and amendment source proofs; preserve immutable close lineage. | Exact retry; changed snapshot conflict; complete source coverage; unknown append recovery; approval/revision race. |
| `ApproveArtifactDisposition` | `ApproveArtifactDispositionHandler` | `ArtifactDisposition` | Require exact prepared content identity and independent approval; authoritative active-hold, reliance and retention proofs; fail closed on missing/unknown reads. | Hold/reliance blocks approval; exact approval replay; changed actor/source conflict; concurrent hold creation; no append on rejection. |
| `ExecuteArtifactDisposition` | `ExecuteArtifactDispositionHandler` | `ArtifactDisposition` plus technical disposal worker | Recheck exact approval, elapsed retention, active holds and reliance before one-use execution; reconcile unknown external disposal before retry. | Unknown storage outcome; repeated exact command; late hold; missing proof; terminal tombstone and no re-execution. |
| `RequestClientExport` | `RequestClientExportHandler` | `ClientExport` | Authorize exact client/engagement and selected export manifest; bind immutable package version and recipient approval. | Same request ID exact retry; changed manifest/recipient conflicts; no delivery before approved manifest. |
| `RequestOwnAttestRecordPackage` | `RequestOwnAttestRecordPackageHandler` | `ClientExport` | Bind requesting professional, service engagement, exact period and approved package manifest; preserve personal authority and separation of duties. | Wrong engagement/actor rejected; exact replay; changed period conflicts; unapproved package rejected. |
| `RecordOwnAttestRecordPackageResult` | `RecordOwnAttestRecordPackageResultHandler` | `ClientExport` | Accept trusted build result only for exact package/version/archive hash and process attempt; technical attempts stay outside business facts. | Wrong hash/version; unknown build callback; duplicate callback; superseded attempt. |
| `RecordOwnAttestRecordDelivery` | `RecordOwnAttestRecordDeliveryHandler` | `ClientExport` | Require exact manifest, archive, validation, recipient approval and delivery effect proof. | Unknown external delivery reconciliation; duplicate callback; recipient/version mismatch; current approval revalidation. |
| `CompleteTenantOffboarding` | `CompleteTenantOffboardingHandler` | `Tenant` as the terminal owner; use a durable typed offboarding continuation if the sequence spans owners | Require exact completed export, retention/disposition and advisory proofs. The Tenant owner records only terminal completion; the handler passes immutable exact-scope proofs and never infers completion from missing reads. | Crash after each prerequisite; missing/unknown proof fails closed; replay after later tenant state; exact terminal retry. |

These handler names describe the missing composition seam. They do not authorize new policy or change the current fail-closed disposal and offboarding gates. Repeated mutations require a required typed `CommandId` route value; stable resource identity may serve only when it identifies one immutable creation for its full lifecycle.

## Producer operations without a registered command route

| Current owner method | Fact | Handler/route definition | Current disposition |
|---|---|---|---|
| `ArtifactDisposition.Approve` | `ArtifactDispositionApproved` | `ApproveArtifactDispositionHandler` → `ArtifactDisposition` | Add under #285. |
| `ArtifactDisposition.RequestExecution` | `ArtifactDispositionExecutionRequested` | `ExecuteArtifactDispositionHandler` → `ArtifactDisposition` + technical worker | Add under #285. |
| `AuditObservation.Record` | `AuditObservationRecorded` | `RecordAuditObservationHandler` → `AuditObservation` | Add under #301 after baseline/source gates. |
| `AuditObservation.Correct` | `AuditObservationCorrected` | `CorrectAuditObservationHandler` → `AuditObservation` | Add under #301 with exact observation revision. |
| `AuditObservation.LinkFinding` | `AuditObservationFindingLinked` | `LinkAuditObservationFindingHandler` → `AuditObservation` and exact `FindingAggregate` source proof | Add under #301; do not let the observation owner mutate a finding aggregate. |
| `AuditObservation.Respond` | `AuditObservationManagementResponseRecorded` | `RecordAuditObservationResponseHandler` → `AuditObservation` | Add under #301 with stable response identity. |
| `AuditPackage.RecordBuildProgress` | `AuditPackageBuildProgressRecorded` | `RecordAuditPackageBuildResultHandler` → build process owner; exact manifest/version result proof to `AuditPackage` | Add under #302; move attempt/cursor state out of the domain stream. |
| `ClientExport.Request` | `ClientExportRequested` | `RequestClientExportHandler` → `ClientExport` | Add under #545. |
| `ClientExport.RecordAttestPackageDelivery` | `ClientExportAttestPackageDeliveryRecorded` | `RecordOwnAttestRecordDeliveryHandler` → `ClientExport` | Add under #545 after #285/#269 gates. |
| `ManagementAssertion.Revise` | `ManagementAssertionRevised` | `ReviseManagementAssertionHandler` → `ManagementAssertion` | Add under #303 with immutable authored version identity. |
| `MethodologyTemplate.ReviseDraftWithAcceptedContribution` | `MethodologyTemplateDraftRevised` | `ReviseMethodologyTemplateDraftHandler` → `MethodologyTemplate` | Add under #276; bind exact accepted deidentified contribution proof. |
| `ProgramRollover.RecordNextPeriodCreated` | `ProgramNextPeriodCreated` | `RecordProgramRolloverSuccessorHandler` → `ProgramRollover` | Add under #316 with exact successor period/plan IDs. |
| `ProgramRollover.Revise` | `ProgramRolloverRevised` | `ReviseProgramRolloverHandler` → `ProgramRollover` | Add under #316 with exact proposal revision. |
| `TemplateApplication.RecordDraftCreated` | `TemplateApplicationDraftCreated` | `RecordTemplateApplicationDraftHandler` → `TemplateApplication` | Add under #276 with the exact target draft and source template version. |
| `TypeIIPeriod.AmendClose` | `TypeIIPeriodCloseAmended` | `AmendTypeIIPeriodCloseHandler` → `TypeIIPeriod` | Add under #310 after complete-close source reconciliation. |
| `TypeIIPeriod.RecordCloseValidation` | `ObservationPeriodCloseProposed` | `RecordTypeIIPeriodCloseValidationHandler` → `TypeIIPeriod` | Add under #310; exact expected population, source revisions and unresolved-material disposition required. |
| `TypeIIPlan.Revise` | `TypeIIPlanRevised` | `ReviseTypeIIPlanHandler` → `TypeIIPlan` | Add under #303 with exact plan version and period binding. |

## Direct reactor and processor writes

### Email and invitation delivery

The email and invitation delivery reactors currently call aggregate executor operations directly for Sent/Failed outcomes. Keep external I/O in a delivery process, but make every owner mutation an explicit internal typed command with an exact challenge/invitation ID, recipient, immutable payload hash and attempt identity. Preserve the accepted at-least-once challenge/invitation rule. Unknown transport outcome is not equivalent to definite rejection. Tests must cover send-success-before-append crash, ambiguous SMTP result, exact repeat, changed payload, old callback after a newer attempt, and checkpoint recovery.

### Digest dispatch

`WorkDigestDispatchProcessor` directly invokes ten `WorkDigestDispatch` operations: `Schedule`, `StartAttempt`, `RecordSent`, `RecordSkipped`, `RecordPermanentRejection`, `RecordUnknown`, `RecordInterruptedAttempt`, `RecordTransientRejection`, `RecordRetryWindowExpired`, and `RecordMissedWeek`. Keep the aggregate/process owner per dispatch identity, but route these transitions through typed internal requests. Technical attempt/cursor state belongs in the process record, not business domain-event vocabulary. Preserve the stricter no-automatic-retry rule after unknown send. Tests cover one member-week identity, one in-flight effect, attempt order, definite transient retry, unknown reconciliation and checkpoint replay.

## Legacy emitters to remove

The eight legacy producer rows are `ImportBatch.Cancel` and the seven `RemediationLedger` methods (`Raise`, `Revise`, `AddAction`, `CompleteAction`, `LinkAcceptance`, `Close`, `Reopen`). Do not add new handlers. Keep only required history decoding until the accepted target owner has exact source-fact parity and all readers/writers have cut over; then remove legacy reducer/emitter paths.

## Deferred requests

The five readiness material-import commands remain deferred under M0-D04: `StageReadinessMaterialImport`, `AcceptReadinessMaterialImport`, `ResolveReadinessMaterialImportRow`, `ExecuteReadinessMaterialImport`, and `CancelReadinessMaterialImport`. Define their target owner, full frozen batch, target reservation order and publication gate before adding routes; do not attach them to `ReadinessLedger` by default.
