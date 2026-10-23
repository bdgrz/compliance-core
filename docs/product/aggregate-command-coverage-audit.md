# Aggregate and command-handler coverage audit

Historical snapshot captured 2026-10-21. Current command, handler, guard, event and reactor coverage is in the [2026-10-23 specification](audits/2026-10-23/README.md). The counts below describe the older source snapshot.

Captured 2026-10-21 at source commit `f054498a56b8e86f3d6019b5e09eb8784be05e8e`, with the current uncommitted finding-migration slice included in the row map.
The row-level source map is [`aggregate-command-coverage.json`](aggregate-command-coverage.json).

## Method

The inventory scans C# declarations, `On<Fact>` reducer registrations, explicit
source-stream replay guards, event
construction (including target-typed constructors), `IRequest` contracts,
`IRequestHandler` implementations, handler registrations, and typed aggregate
mutation targets in handler and shared-helper code. It checks every callable
fact-producing aggregate operation against registered command and
internal-reaction routes, including facts built by the shared
`ResponsibilitySet` helper. The JSON map includes relative source paths and test files that mention each event, request, or aggregate.
Those test paths are navigation evidence; they do not prove that a test asserts
the full replay or behavior invariant for that row.

The classification follows the accepted product decisions and live feature
issues. A contract with no current producer is not treated as a new behavior
requirement by itself.

## Current coverage

- **409 domain-event contracts.** There are 409 aggregate reducer
  registrations across 388 fact contracts. The 381 currently emitted business
  aggregate facts have reducers. `UserIdentityAuthenticated` is emitted
  through the audit path and has no aggregate reducer. Two reducer-only facts
  remain for replay compatibility. Five migration-control facts are inventoried
  separately from business facts.
- **20 contracts have neither a reducer nor a current producer.** Seven are
  confirmed feature gaps; 13 are superseded by current events or accepted
  decisions, as listed below.
- **102 aggregate roots.** 101 have a registered command or internal-reaction
  mutation path. `FindingAggregate`, `FindingMigrationAggregate`, and the
  lightweight `RemediationMigrationFenceAggregate` are reached
  through the new migration request; the seven normal finding producer methods
  still need production command and reader cutover under #1022.
  `LegacyWorkAssignmentLedger` is a read-only replay adapter for old assignment
  streams. Three roots are reached through shared helper services:
  `PopulationSnapshot` and `PopulationSnapshotChunk` through
  `PopulationSnapshotFreezer`, and `TenantManagerGuard` through
  `TenantManagerInvariant`.
- **7 source-stream replay guards.** `RemediationMigrationFenceAggregate`
  validates the tenant, program, and finding identity of legacy finding facts
  while replaying the source stream; those scope checks are listed separately
  from state-owning reducers.
- **649 request contracts** were found across Common and Core, including 647
  Common contracts. The scan classifies 347 handled commands, 14 handled
  internal reaction commands, 13 commands without handlers, 274 read or
  reconstruction requests, and one internal executor context. The 13 missing
  command handlers comprise eight active gaps and five readiness-import
  commands deferred by M0-D04.
- All **591 Common/Core handler registrations** composed by `AddCompliance`
  have a matching handler class. The app layer has two MCP forwarding adapters,
  inventoried separately because they forward to Common/Core requests and do
  not own aggregates.

The JSON contains the fact-to-reducer-to-emitter map, source-stream replay
guards kept separate from reducers, the separate
aggregate-to-command-to-handler map, and each producer operation that still
lacks a registered route. It records direct aggregate references and the
helper-service paths used by commands; use the aggregate owner to join the
maps. Publisher detection uses conventional publisher names and callback type
syntax. The producer call graph is a source-derived syntactic heuristic matched
by method name and compatible arity. Neither scan resolves receiver or symbol
identity, so same-named calls on unrelated objects can be candidates for human
review rather than semantic proof. The regression checks those candidates
against actual mutation targets and records the exceptions below.
Legacy emitters that have no current caller remain explicit until a verified
stream migration preserves replay; the inventory does not treat their stored
event types as safe to delete. The command route proves that the handler can
execute that aggregate, not that every command listed for an aggregate emits
every fact on its stream.
The composition regression test checks the inventory against compiled event,
request, and aggregate types, checks reducer owners and emitter paths against a
Roslyn scan of `src`, checks syntactic producer route candidates and command
routes in both directions, and checks each listed Common/Core handler against
Portia's actual registered handler descriptors. App adapter rows identify
their forwarded Core request and handler type separately from the Core
registration count.

## Confirmed fact gaps

| Domain facts | Owning issue | Direct dependency or policy gate |
| --- | --- | --- |
| `AuditObservationRecorded`, `AuditObservationFindingLinked` | [#301](https://github.com/bdgrz/compliance-core/issues/301) | Blocked by Type I baseline #298 and entry decision #297. |
| `OwnAttestRecordPackagePrepared`, `OwnAttestRecordDeliveryRecorded`, `OwnAttestRecordDeliveryVerified`, `DeliveredEngagementHoldReleased` | [#545](https://github.com/bdgrz/compliance-core/issues/545), [#546](https://github.com/bdgrz/compliance-core/issues/546) | Evidence governance #285 and service engagements #269 remain open; M0-D27 #125 defines the package and delivery boundary. |
| `TenantOffboardingCompleted` | [#128](https://github.com/bdgrz/compliance-core/issues/128) | Export #545, retention #546, and advisory-retention decision #346 gate the complete lifecycle. |

## Aggregate fact producers without registered command routes

The route guard also checks each callable aggregate operation that constructs a replayable fact, rather than
only checking that each aggregate root has at least one command. It finds **37 producer operations**
that do not reach a registered command or internal-reaction handler:

| Current path | Producer operations | Owning issue or disposition |
| --- | --- | --- |
| No source caller or registered request route | `AuditPackage.RecordBuildProgress` | [#302](https://github.com/bdgrz/compliance-core/issues/302) |
| No source caller or registered request route | `ArtifactDisposition.Approve`, `ArtifactDisposition.RequestExecution` | [#285](https://github.com/bdgrz/compliance-core/issues/285) |
| No source caller or registered request route | `TypeIIPeriod.RecordCloseValidation`, `TypeIIPeriod.AmendClose` | [#310](https://github.com/bdgrz/compliance-core/issues/310) |
| No source caller or registered request route | `ClientExport.Request`, `ClientExport.RecordAttestPackageDelivery` | [#545](https://github.com/bdgrz/compliance-core/issues/545) |
| No source caller or registered request route | `MethodologyTemplate.ReviseDraftWithAcceptedContribution`, `TemplateApplication.RecordDraftCreated` | [#276](https://github.com/bdgrz/compliance-core/issues/276) |
| No source caller or registered request route | `ProgramRollover.Revise`, `ProgramRollover.RecordNextPeriodCreated` | [#316](https://github.com/bdgrz/compliance-core/issues/316) |
| No source caller or registered request route | `TypeIIPlan.Revise`, `ManagementAssertion.Revise` | [#303](https://github.com/bdgrz/compliance-core/issues/303) |
| Legacy emitter with no current caller | `ImportBatch.Cancel` | [#1022](https://github.com/bdgrz/compliance-core/issues/1022); retain event replay until a verified stream migration is delivered. |
| Direct aggregate-executor call from delivery reactor | `TenantInvitation.RecordDeliverySent`, `TenantInvitation.RecordDeliveryFailure` | [#127](https://github.com/bdgrz/compliance-core/issues/127) |
| Direct aggregate-executor call from delivery reactor | `EmailAddress.RecordDeliverySent`, `RecordDeliveryFailure`, `RecordRecoveryDeliverySent`, `RecordRecoveryDeliveryFailure` | Needs a specific backend issue; #127's backend children are closed and no open email-challenge issue owns this route. |
| Direct aggregate-executor call from digest processor | `WorkDigestDispatch.Schedule`, `StartAttempt`, `RecordSent`, `RecordSkipped`, `RecordPermanentRejection`, `RecordUnknown`, `RecordInterruptedAttempt`, `RecordTransientRejection`, `RecordRetryWindowExpired`, `RecordMissedWeek` | [#121](https://github.com/bdgrz/compliance-core/issues/121) |
| No registered request route for the in-progress per-finding boundary | `FindingAggregate.Raise`, `Revise`, `AddAction`, `CompleteAction`, `LinkAcceptance`, `Close`, `Reopen` | [#1022](https://github.com/bdgrz/compliance-core/issues/1022); the system migration request now routes fence, import, and cursor facts; production finding commands and readers still use the legacy root. |

The direct reactor and processor paths currently execute aggregate methods and can persist their facts;
they bypass the registered request-handler composition. They are listed because the requested wiring
requires all fact-producing aggregate operations to have an explicit registered request path. They do
not count as missing event construction. `ImportBatch.Cancel` is a duplicate legacy producer for
`ApplicationImportCanceled`; the active cancellation handler executes the source ledger operation.
Its event contract and reducer remain until the old batch-stream history has a verified migration.
The source-linked rows, caller paths, and issue dispositions are in `unwiredAggregateProducers` in
the JSON map.

## Superseded or deferred contracts

| Contracts without reducer or producer | Current owner or accepted disposition |
| --- | --- |
| `FutureControlScheduleRevised` | T2-02 #305 uses approved control-operating-plan facts to record cadence and future work. |
| `MissingOccurrenceRecorded` | T2-02 #305 and T3-01 #310 use the generated expected population and close reconciliation. |
| `ObservationPeriodEnded` | T3-01 #310 closes through `ObservationPeriodCloseFrozen`. |
| `NextObservationPeriodStarted`, `OpenRemediationCarriedForward` | T3-07 uses `ProgramRollover` and `ProgramNextPeriodCreated`. |
| `TypeIIOperatingPlanProposed`, `TypeIIOperatingPlanReviewed`, `TypeIIOperatingPlanApproved` | T1-07 #303 uses the richer `Examinations.TypeIIPlan*` facts. Its handlers operate the `TypeIIPlan` aggregate. |
| `GovernanceReviewScheduled`, `ManagementReviewRecorded`, `ManagementReviewActionRecorded`, `ManagementReviewActionCompleted` | M0-D19 #76 makes periodic reviews ordinary control occurrences and routes requested work through shared Findings. |
| `IndependenceRuleVersionRatified` | M0-D26 #124 uses `IndependenceRuleRatificationRecorded`. |

`SystemInstanceDeclared` and `TenantIdentityProviderTestedV2` have reducers but
no current producer. Both are retained for historical replay/compatibility;
the latter contract is explicitly documented as legacy. They are listed in
the JSON map.

## Missing command handlers

| Command | Owning issue | Status |
| --- | --- | --- |
| `AmendTypeIIPeriodClose` | [#310](https://github.com/bdgrz/compliance-core/issues/310) | Depends on #307, #308, and #309. |
| `ApproveArtifactDisposition`, `ExecuteArtifactDisposition` | [#285](https://github.com/bdgrz/compliance-core/issues/285) | Evidence-capture and current eligibility composition remain incomplete. |
| `RequestClientExport`, `RequestOwnAttestRecordPackage`, `RecordOwnAttestRecordPackageResult`, `RecordOwnAttestRecordDelivery` | [#545](https://github.com/bdgrz/compliance-core/issues/545) | Depends on evidence governance #285 and service engagements #269. The own-attest package root is missing. |
| `CompleteTenantOffboarding` | [#128](https://github.com/bdgrz/compliance-core/issues/128) | Depends on export and retention; #346 gates advisory-material disposition. |

The five missing readiness-import commands remain deferred under M0-D04:
`StageReadinessMaterialImport`, `ResolveReadinessMaterialImportRow`,
`ExecuteReadinessMaterialImport`, `CancelReadinessMaterialImport`, and
`AcceptReadinessMaterialImport`.

## Multi-owner facts

Sixteen event contracts have reducer registrations in more than one aggregate.
The JSON names every owner and source file. Seven finding events are a temporary
dual-replay state while the per-finding target is built; their old reducer can be
removed only after source-to-target parity and consumer cutover. The migration
fence is emitted by a lightweight source-stream aggregate and reduced by that
aggregate plus the legacy writer root, which uses it to reject further writes.
The remaining eight are existing shared or compatibility facts: import cancellation is
applied by the batch and import ledger; responsibility facts are applied by
their owning source records; work-item assignment facts retain the legacy
adapter; and technology revision facts are shared across the related inventory
roots. The follow-up coverage guard will preserve this explicit ownership list.

## Current implementation slice

The source-linked coverage guard inventories every event, request, aggregate,
reducer, emitter, and registered handler. The active #1022 slice now has a
bounded per-finding command aggregate, a bounded per-program migration
coordinator, a read-side history fold, an exact versioned legacy fence, and
paired source-lineage imports. A named system request processes one physical
source offset per call, including audit cursor progress. Production finding
command and reader cutover, full source-to-target parity proof, and legacy-root
pruning remain open. The guard does not close the seven
policy-gated feature gaps; those stay with their owning issues.

Jev's `jev-1.13.0` Choice favored `finish_finding_migration` (0.56) over
`audit_observation_facts` (0.43), with 0.49 confidence. This is a
prioritization signal, not a domain decision. GitHub still shows #297 and #298
open, so #301 remains blocked until its Type I baseline and entry-decision
prerequisites are complete.
