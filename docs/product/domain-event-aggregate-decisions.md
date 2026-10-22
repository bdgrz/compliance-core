# Domain event and aggregate decisions

Status: architecture adopted on 2026-10-22 under the user's instruction to settle the choices with Jev and select an option for close results. Implementation and migration proof are tracked separately from the design decisions.

The [complete decision register](domain-event-aggregate-decisions.json) assigns a disposition to every one of the baseline **409 event contracts and 102 aggregate roots**, plus eight selected additional facts. It retains Jev model/version, questions and source context, raw design answers, every root's probabilities, source evidence, and migration obligations. The root review used `jev-1.14.0`; 25 root results were close, and all have a selected option. No architectural tie remains pending.

The baseline is local commit `fc7b2543a1c591f8b2cd0787312f6af9e162b3f8` plus the existing Finding migration edits. It is not a claim about merged or production state. The [coverage inventory](aggregate-command-coverage.json) records the evolving implementation; the decision register records the chosen target. The earlier [gap analysis](domain-event-aggregate-gap-analysis.md) retains its original evidence and questions, now resolved by these choices.

## Event vocabulary

| Decision | Selected contract and owner |
| --- | --- |
| Received auditor observations and exceptions | `AuditObservation` owns immutable external wording/source versions, separate management responses and exact Finding links. `Finding` owns remediation. Use the existing `AuditObservationRecorded` and `AuditObservationFindingLinked` names with complete v2 payloads. |
| External corrections and management response | Add `AuditObservationCorrected` and `AuditObservationManagementResponseRecorded`. Each names its exact received version; neither rewrites earlier auditor wording. |
| Unapprovable pending operating plan | Add `ControlOperatingPlanProposalWithdrawn`, owned by the control's plan line. Withdraw the exact pending version before proposing its replacement. Keep prior approvals and occurrences. |
| Campaign delivery and negative outcomes | Add typed `CampaignMaterialDelivered`, `CampaignDeliveryFailed`, `PolicyAcknowledgementDeclined`, and `TrainingCompletionSourceFailed` on the per-person participant boundary. Keep fulfilment state separate from those observations. |
| Campaign reminders | Reuse shared work reminders, escalation and weekly digest. No new campaign scheduler or email to non-members. |
| Erroneous evidence support against the same exact target | Add `EvidenceSupportWithdrawn` to the existing support owner. Preserve original assertion and explicit corrected-link lineage. Different bytes and successor source versions continue to use new immutable identities. |
| Own-attest package and delivery | Reuse `ClientExport` with engagement-collaboration scope, exact source manifest, build and attributed required-package delivery. Canonical facts are `ClientExportBuilt` and `ClientExportAttestPackageDeliveryRecorded`. |
| Own-attest verification | Validate exact package/manifest/destination/receipt as part of the accepted canonical delivery decision. There is no second verification ceremony or duplicate verification event. |
| Delivery-dependent hold release | Reuse `ArtifactEngagementHoldReleased`; extend its authority with exact delivered-package and retention eligibility proof. |
| Offboarding completion | Existing `Tenant` owns rich v2 `TenantOffboardingCompleted`, after exact authoritative export, delivery and retention/hold completion proof. No second terminal tenant truth. |

The four unused placeholders `OwnAttestRecordPackagePrepared`, `OwnAttestRecordDeliveryRecorded`, `OwnAttestRecordDeliveryVerified`, and `DeliveredEngagementHoldReleased` are superseded by those canonical facts. They do not need duplicate producers. Keep only the compatibility required by retained stores until usage and migration proof permit deletion.

The other 13 previously superseded contracts retain their accepted replacements. The two reducer-only legacy types remain compatibility readers, `UserIdentityAuthenticated` remains an audit fact, and migration-control markers remain separate from business facts. A dormant type is not an instruction to invent behavior.

## Finding receipt and observation history decisions

Two follow-up calls to `jev-1.14.0` are retained in the complete register:

- Finding receipts: **explicit v2 with exact v1 compatibility** selected at 0.65 versus 0.35. Seven new native contracts require `RequestId`; the seven original v1 contract files remain unchanged. This close result is settled under the user's instruction.
- Observation history: **any exact recorded received version** selected at 0.99 versus 0.01. Management responses and links may target a historical received version after authoritative fenced-history proof. Command state retains one selected proof, without a lifetime history list. This routing/history change is still pending.

## Aggregate state

One independent mutable invariant owns one stable stream. An aggregate retains the current state required to decide its next command. Full revisions, responses, attempts, closures and delivery histories stay in retained facts, immutable snapshot/version content, or source-fenced history folds.

Durable idempotency does not require a growing receipt dictionary. A command-selected aggregate can replay its complete stream while retaining one matching historical request receipt alongside current state. Exact older retries return the original decision without appending; changed actor, command kind or intent conflicts. Every emitted decision must retain enough identity and intent to enforce that comparison. Malformed duplicate use of the selected request identity fails replay.

Immutable historical versions do not automatically become aggregate classes. Introduce an independent owner when a version, response, attempt or row has its own mutable invariant; otherwise use exact retained facts and read folds. All combined strings/collections must fit the complete serialized event and storage frame. Large immutable selections use chunked snapshots and a terminal manifest commitment. Lifetime histories must not be bounded by inventing a maximum number of otherwise valid business operations.

The complete register locks each root's target responsibilities. The main families are:

| Existing root or family | Selected boundary |
| --- | --- |
| Remediation | One bounded finding; finish source fence/import/parity and all-consumer cutover before retiring `RemediationLedger`. |
| Operations | Current/pending plan authority per control and current state per occurrence; frozen reassignment selection and source-bound completion gate. |
| Evaluations and evidence requests | One evaluation and one evidence request per independent record. Procedure and exact source versions remain attributable. |
| Readiness | Immutable assessment, current plan/annotation per stable gap, and narrow program authority for current assessment and initial Type I entry. |
| Risk governance | Per-risk current governance, independently changing action/assertion identities where required, and source-bound reassessment. Historical assessments/acceptances need no growing in-root arrays. |
| Mappings and applicability | Deterministic control/edition/criterion mapping and edition/criterion applicability identities. |
| Policy and training | Current policy/requirement authority with exact historical version folds; campaign header/frozen audience and per-person participant decisions. |
| Access populations and campaigns | Frozen population manifests, independent classifications/review items, and exact bulk-decision/completion authority. |
| Provider, location and process registers | Individual records with explicit deterministic normalized-name reservations where uniqueness is required. |
| Provider assurance | Individual reports, attributable reviews and coverage gaps; exact same-provider and risk/source relationships. |
| Imports | Batch execution and stable source-record claims, with a narrow accepting-source/commit visibility authority. |
| Advisor feedback | Handoff admission serialized with revocation/selection, then independently changing feedback threads. |
| Firm services and independence | Narrow client-wide professional exclusion/source authority; independent service, engagement/acceptance, assignment and evaluation records. |
| Firm staff and professional duties | Individual staff/designation identities plus canonical-user/assertion/scope uniqueness authorities. |
| Operator roster | Individual operator membership plus last-operator count/pending-transition authority; verified legacy cardinality bootstrap. |
| Last administrator | Preserve the existing narrow serialization authority; replay only command-selected subject/witness state plus the authoritative sequence. |
| Retention | Current retention basis and individual hold decisions, with a source-level disposition barrier. |
| Packages, auditor requests and populations | Exact immutable content/version manifests, independent build/response/sample/row/delivery decisions where required, and narrow terminal reconciliation. |
| Versioned single-subject roots | Keep stable identity and minimal current state; move historical versions, decisions and request receipts to source-fenced folds or immutable identities. |
| Digests and assignments | One dispatch/assignment identity plus bounded scheduling cursor; read complete attempts and assignment history outside command state. |
| Observation calendar | Preserve authoritative non-overlap; individual periods already have owners. Keep only narrow reservation/current transition authority. |

## Atomicity across owners

Existing atomic behavior is retained through explicit commitments:

- **Access-review bulk decisions:** freeze the exact selection in chunks, validate it, commit one batch decision, and gate visibility/completion until deterministic child effects reconcile. Do not silently turn the existing request into partial success.
- **Plan ownership:** approval binds the exact affected occurrence selection. Source authority fences old/new owner actions until each required reassignment is settled; a lagging projection cannot authorize either actor incorrectly.
- **Campaign admission and close:** parent admission binds request, person, exact material/inclusion, expected child revision and payload digest. Closing fences new admission, reconciles all admitted effects and freezes results before terminal close.
- **Advisor feedback:** committed handoff admission selects the exact feedback intent before child materialization. Admission is serialized with revoke/close; reading an open handoff and later writing a child is insufficient.
- **Independence, uniqueness, last administrator/operator, non-overlap and disposition:** retain the narrow authoritative reservation or transition gate. Source reads and projection totals do not substitute for it.

Coordinators retain at most one pending intent plus bounded cursors, counts and digests. Immutable operation identities and source-bound receipts supply recovery. Unknown commit/effect outcomes must be reconciled before abort, release or retry.

## Compatibility and migration

Ninety-eight baseline contracts embed complete view state. Historical arrays cannot be truncated without changing their retained meaning. Emit versioned delta payloads going forward and retain exact legacy decoders/reducers until migrated history and all consumers have proven parity. A stateless upcaster is appropriate only if the old payload independently supplies the new meaning.

Any stream or ownership change requires: fence/drain old writers; import one authorized source fact at a time; preserve physical position, event identity/version/time/type/payload lineage; resume with bounded progress; verify exact per-source/per-target semantic parity; use fresh projection checkpoint identities; cut over every writer, reader, source contributor and authorization consumer; then prune duplicate legacy paths. No event-store database mutation is part of this design.

## Implementation acceptance

The current code slice includes the bounded received-observation domain owner and the Finding native-v2/source-consumer cutover. It is an incomplete local checkpoint; see the [checkpoint and next steps](domain-event-aggregate-checkpoint.md). The decision register deliberately keeps implementation proof pending for the wider boundary changes. Selecting a target owner does not prove that its producers, consumers or migration are delivered.

Each slice follows focused failing behavior, implementation, focused and full unit tests, exact-head adversarial review and required CI. Domain event construction/replay, malformed input, wrong scope, stale revision, conflicting/exact older retries, byte bounds and partial migration/effect recovery are required evidence. Handler/source-reader/projector changes belong only where needed to emit or preserve the selected domain behavior safely.

Commercial consent/pricing, connectors, Privacy, deferred readiness-material import and auditor workpapers remain outside this event/aggregate implementation. Existing product policy is preserved; Jev selects architecture from the supplied evidence and does not supply external facts.
