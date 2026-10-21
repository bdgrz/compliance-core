# Aggregate and command-handler coverage audit

Captured 2026-10-21 at source commit `a0cef38ade2533332c06922df0e696a202e75fd5`.
The row-level source map is [`aggregate-command-coverage.json`](aggregate-command-coverage.json).

## Method

The inventory scans C# declarations, `On<Fact>` reducer registrations, event
construction (including target-typed constructors), `IRequest` contracts,
`IRequestHandler` implementations, handler registrations, and aggregate
references in handler and shared-helper code. The JSON map includes relative
source paths and test files that mention each event, request, or aggregate.
Those test paths are navigation evidence; they do not prove that a test asserts
the full replay or behavior invariant for that row.

The classification follows the accepted product decisions and live feature
issues. A contract with no current producer is not treated as a new behavior
requirement by itself.

## Current coverage

- **404 domain-event contracts.** There are 396 aggregate reducer
  registrations across 383 fact contracts. The 381 currently emitted
  aggregate facts have reducers. `UserIdentityAuthenticated` is emitted
  through the audit path and has no aggregate reducer. Two reducer-only facts
  remain for replay compatibility.
- **20 contracts have neither a reducer nor a current producer.** Seven are
  confirmed feature gaps; 13 are superseded by current events or accepted
  decisions, as listed below.
- **99 aggregate roots.** Every root maps to at least one command or internal
  reaction path. Three roots are reached through shared helper services:
  `PopulationSnapshot` and `PopulationSnapshotChunk` through
  `PopulationSnapshotFreezer`, and `TenantManagerGuard` through
  `TenantManagerInvariant`.
- **648 request contracts** were found across Common and Core, including 646
  Common contracts. The scan classifies 347 handled commands, 13 handled
  internal reaction commands, 13 commands without handlers, 274 read or
  reconstruction requests, and one internal executor context. The 13 missing
  command handlers comprise eight active gaps and five readiness-import
  commands deferred by M0-D04.
- All **590 Common/Core handler registrations** composed by `AddCompliance`
  have a matching handler class. The app layer has two MCP forwarding adapters,
  inventoried separately because they forward to Common/Core requests and do
  not own aggregates.

The JSON contains the fact-to-reducer-to-emitter map and the separate
aggregate-to-command-to-handler map. It records direct aggregate references and
the helper-service paths used by commands; use the aggregate owner to join the
two maps. The command route proves that the handler can execute that aggregate,
not that every command listed for an aggregate emits every fact on its stream.
The composition regression test checks the inventory against compiled event,
request, and aggregate types, checks reducer owners and emitter paths against a
Roslyn scan of `src`, checks command routes in both directions, and checks each
listed Common/Core handler against Portia's actual registered handler
descriptors. App adapter rows identify their forwarded Core request and
handler type separately from the Core registration count.

## Confirmed fact gaps

| Domain facts | Owning issue | Direct dependency or policy gate |
| --- | --- | --- |
| `AuditObservationRecorded`, `AuditObservationFindingLinked` | [#301](https://github.com/bdgrz/compliance-core/issues/301) | Blocked by Type I baseline #298 and entry decision #297. |
| `OwnAttestRecordPackagePrepared`, `OwnAttestRecordDeliveryRecorded`, `OwnAttestRecordDeliveryVerified`, `DeliveredEngagementHoldReleased` | [#545](https://github.com/bdgrz/compliance-core/issues/545), [#546](https://github.com/bdgrz/compliance-core/issues/546) | Evidence governance #285 and service engagements #269 remain open; M0-D27 #125 defines the package and delivery boundary. |
| `TenantOffboardingCompleted` | [#128](https://github.com/bdgrz/compliance-core/issues/128) | Export #545, retention #546, and advisory-retention decision #346 gate the complete lifecycle. |

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

Eight event contracts have reducer registrations in more than one aggregate.
The JSON names every owner and source file. These are existing shared or
compatibility facts: import cancellation is applied by the batch and import
ledger; responsibility facts are applied by their owning source records;
work-item assignment facts retain the legacy adapter; and technology
revision facts are shared across the related inventory roots. The follow-up
coverage guard will preserve this explicit ownership list.

## Next implementation slice

The first dependency-free slice in this issue is a regression guard that
requires an explicit inventory row for every event, request, and aggregate
contract. It requires every emitted aggregate fact to name its reducer owner
and emitter, every aggregate to reach a registered command or internal
reaction handler, and every handled request to match its registered handler
type. Requests without handlers must remain explicitly classified with an
issue or accepted deferral. The guard does not close the seven feature gaps;
those stay with their owning issues.

For the next feature track, Jev's `jev-1.13.0` Choice favored the Type I
observation path (0.55 probability; confidence 0.44) over finishing the
regression guard (0.36). This is only a prioritization signal. Live GitHub state
still shows #297 and #298 open, so #301 remains blocked until its accepted Type
I baseline and entry-decision prerequisites are complete.
