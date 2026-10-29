# ProviderAssuranceGap aggregate review, 2026-10-29

Scope is aggregate and domain-event stage. The coordinator captured combined RED before the source fix; GREEN and independent Jev review remain pending.

## Identity, invariant and boundary

The tenant/provider/gap tuple selects a single gap stream. The owner records exact provider/source lineage, closes once only with a newer or different source, and serializes admission of each unique risk/acceptance pair while the gap is open. A separate `ProviderRiskAcceptance` owns the materialized link. A link admitted before closure may materialize after the gap closes; a projection or a later open check must not revoke that already-admitted decision. The exact selected native history and OCC serialize link admission and closure without retaining a lifetime pair set in aggregate state. Authority to validate captured provider/risk source proofs remains with their owning records and the receiver.

| State | Decision | Result |
|---|---|---|
| Absent | Record own gap at valid provider revision | `ProviderCoverageGapRecorded` |
| Open | Admit a new exact risk/acceptance pair at selected current cut | `ProviderCoverageGapRiskAcceptanceAdmitted`, revision advances |
| Open | Close against newer or different source at current revision | `ProviderCoverageGapClosed`, terminal |
| Closed, stale revision or already admitted pair | Admit or close | Refusal; no new fact |
| Accepted operation | Exact authorized retry | Original result; changed binding conflicts |

## Finding and tests

`ProviderGapHistory.ReadAsync` verifies every root's scope, logical/root identity, null causation, own owner addresses (null `FirstOwner` is permitted as own), nonempty actor/time, intent hash and per-command kind, episode, original expected revision, attribution and event reference schemas. A fresh `Record` currently checks only logical ID, owner and provider revision; `Close` checks logical ID/owner plus lifecycle and verified-cut guards. These calls can append facts that the same history verifier later refuses. Existing normal handlers prepare a matching root, but direct owner admission must not accept an un-replayable fact.

`ProviderAssuranceGapOwnerTests` exercise otherwise-valid first record roots with changed provenance/attribution/reference schema and a valid null-`FirstOwner` control, plus verified-cut close with varied root provenance/attribution/schema and valid control. The coordinator captured RED before production changes. The selected-cut `AdmitLink` cases likewise cover root fields, nonempty risk/acceptance pair and intent/response references, with a valid nullable-FirstOwner control. Existing `ProviderAssuranceGapTests` cover exact retry after later changes, changed intent, closure source, duplicate pair and delayed link materialization after closure. Old `provider-gap-qualification.json` reports earlier local green counts, not present-head qualification.

## Event and qualification limits

The selected catalog and code include recorded/closed and link-admission facts. Shared schemas and generated registrations are coordinator-owned and unchanged. Captured source proof validation and linked child materialization belong to receiver/process stages. Tests on controlled stores do not establish migration, hosted CI, broker or durable-store recovery. No build or test has run as part of this independent review.

## RED and admission fix

The coordinator ran `dotnet test test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore --filter 'FullyQualifiedName~PolicyCampaignParticipantOwnerTests|FullyQualifiedName~PracticeIndependenceCommitmentOwnerTests|FullyQualifiedName~ProviderAssuranceGapOwnerTests'` before the fix: 53 failed and 8 passed across the combined three-owner selection. The gap cases exposed invalid provenance and pair admission before the owner rejected them.

Record, close and link admission now validate native root IDs, null causation, operation kind/episode, own scope, expected revision, actor/time and intent digest against the same structural rules enforced by `ProviderGapHistory`. Record and close also bind event actor/time and reference schemas; admission validates its pair and reference schemas. Null `FirstOwner` remains accepted as the owner, matching retained history and existing callers. The owner still does not establish source fact authenticity or immutable content integrity merely from a reference; those remain receiver and source-owner responsibilities. No replay rule changed. GREEN and independent review remain pending.

## Independent review before build

The changed `ValidRoot` matches `ProviderGapHistory` for selected root fields, including `(FirstOwner ?? Owner) == Stream`. This fallback is material: the production `Provenance` helper used by the record, close and link handlers leaves the optional `FirstOwner` null. Each checked-in handler supplies the expected kind, episode, original revision, actor/time and content schemas. The new valid controls explicitly use null `FirstOwner`; their immutable references are synthetic and establish reference shape, not content-store or durable-store behavior. The selected admission retains the pair, while source proof and child materialization remain later-stage checks. No replay rule or event type was changed.

The selected owner event names, discriminators and declared fields match the three current concrete record types. One content-contract discrepancy remains: the target-event-contracts native-response rule specifies `MutationResultContent` for the terminal `OperationResult`; current record/close handlers write a direct `ProviderCoverageGapRegistration` reference and the history verifier requires that direct schema. The original response descriptor still points to the same direct result. Whether this existing contract satisfies every selected consumer and how occupied v1 histories are handled require source and migration qualification; neither changing the v1 payload nor claiming the mismatch resolved is justified by this review. The payload also repeats owner identity and attribution alongside operation and stream metadata; any simplification needs separate compatibility qualification.

Jev `jev-1.14.0` scored 0.91 that the change preserves null `FirstOwner`, 0.65 that an admitted pair can materialize after closure, and 0.97 for the result-schema mismatch. It chose qualifying the existing direct result at 0.85 versus 0.15 source resolution and 0.00 immediate v1 rewrite. Payload duplication scored 0.93; versioned change 0.64 versus source resolution 0.22 and retaining the existing schema 0.14 did not establish a concrete migration. The adopted event summary independently states that closure never erases an admitted link. Exact credential-free inputs and outputs are in `implementation-jev/providerassurancegap-test-impl-review-2026-10-29-{request,response}.json`; Jev is advisory. Live TypeSafe OpenAPI was available for request format.

GREEN, existing focused behavior tests, broader suite and format remain for the coordinator. The native result content-schema discrepancy and historical compatibility are named open qualification items; no shared event, content schema or handler was changed in this review.

## Coordinator validation (2026-10-29)

Release compilation passed with zero warnings and errors. The related participant, practice-commitment and provider-gap selection passed 124 tests; existing rollover selection passed 18 tests but does not test the missing execution claim. Changed C# files passed whitespace verification. The full non-broker, non-web suite passed 7,951 and failed five: two ArtifactDownloadGrant coverage-map tests and three weekly-schedule original-result tests; the same five failed on hosted develop before wave 2. The full suite is not green. Current producer paths were reviewed; persisted histories, hosted CI, and production behavior remain unqualified.
