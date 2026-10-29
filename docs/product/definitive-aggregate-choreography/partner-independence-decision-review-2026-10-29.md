# PartnerIndependenceDecision aggregate review, 2026-10-29

Scope: aggregate and event only; focused RED was captured. Handler, reactor, source verification and shared catalog changes are outside this round.

## Identity and boundary

One tenant/evaluation stream owns one immutable personal partner evaluation of an exact engagement revision, service epoch and ratified-rule digest. Keep the evaluation and its immutable content references together so one identity cannot materialize competing decisions. `ServiceEngagementIndependence` admits and pins the decision; the source-use process and receiver verify exact engagement, service, directory, duty, rule and ratification proofs, and the original actor. The reactor delivers that authorized decision; the evaluation alone does not approve production acceptance.

## Lifecycle and evidence

| State | Decision | Outcome |
|---|---|---|
| Fresh | Materialize authorized own evaluation | One `PartnerIndependenceEvaluationRecorded`, revision 1 |
| Fresh | Different root or owner, empty identity, invalid child routing or cause | Refused without a fact |
| Recorded | Another direct materialization | Conflict, no fact |
| Recorded | Exact authorized receiver redelivery | Original fact resolved by receiver without another fact |

The owner previously checked operation owner but did not bind the operation root to its `EvaluationId`, nor enforce nonempty tenant/evaluation/logical identity, expected command, nonempty causation, or same-tenant engagement first-owner/own target routing. It could consequently materialize a fact claiming this evaluation while its provenance named another decision. The normal receiver validates the target, root and source before constructing a child operation; this does not protect direct owner admission from inconsistent provenance. The new `PartnerIndependenceDecisionOwnerTests` cover these invalid admissions, valid materialization and replay, second materialization refusal and stable stream identity. Before the fix, the coordinator ran `dotnet test test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore --filter 'FullyQualifiedName~MethodologyTemplateContributionOwnerTests|FullyQualifiedName~PartnerIndependenceDecisionOwnerTests|FullyQualifiedName~PlatformOperatorRoster'`: 21 failed and 49 passed across the combined selection, including accepted invalid partner child provenance. `Materialize` and replay now share the owner-decidable identity and routing checks. The exact source is still verified by the receiver. GREEN is pending.

Existing `PartnerIndependenceEvaluationTests` and `EngagementPersonalDecisionTests` cover source-authorized process materialization and valid replay; they were not rerun in this phase. The source verifier must establish exact source event, engagement, actor, evaluation input and rule digest; this owner can verify only its own identity and the source stream's tenant and area. Concurrency, native-store recovery and end-to-end authorization are separate evidence.

The selected event matches `PartnerIndependenceEvaluationRecorded`. The selected materialization command's catalog `effectKind` says `claim_professional_assertion_identity`, inconsistent with this owner and its description; coordinator must reconcile that shared catalog entry. Event fields potentially repeat identity/provenance and need independent Jev review before any versioned event change.

## Independent review before build

The normal materializer verifies the authorized source and derives child provenance from the frozen admitted intent; the new checks also apply on replay, so already persisted facts that fail them would no longer hydrate. No production history inventory was performed. Owner tests check direct invalid admission, one fact and valid replay; they do not prove durable competing append or exact source authorization.

The previous source routing check accepted any resource text in the correct tenant and `service-engagements` area. An engagement owner uses a nonempty canonical UUID engagement ID, so an invalid resource cannot be an actual source owner. The coordinator ran `dotnet test test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore --filter 'FullyQualifiedName~MethodologyTemplateContributionOwnerTests|FullyQualifiedName~PartnerIndependenceDecisionOwnerTests|FullyQualifiedName~PlatformOperatorRoster'` after adding `invalid_source` and empty-causation cases: the invalid source case failed and 71 tests passed. The owner now requires a nonempty canonical UUID resource on both admission and replay. Exact source event and exact engagement binding still require the receiver's source proof. GREEN remains pending.

Jev `jev-1.14.0` (advisory; exact request and response in `implementation-jev/partnerindependencedecision-test-impl-review-2026-10-29-*.json`):

| Question | Result | Disposition |
|---|---|---|
| Assertion claim effect label differs from partner materialization | Noul 0.48 | Uncertain; command and code directly establish a distinct operation |
| Catalog effect kind | correct effect kind 0.86 | Report for coordinator's shared-file reconciliation |
| Event OperationId repeats stream resource | Noul 0.59 | Uncertain; direct constructor/fact code establishes the repetition |
| Payload disposition | retain 0.43; source resolution 0.30; version event 0.27 | Unresolved; no wire change |
| Non-UUID source is an engagement stream | Noul 0.19 | Consistent with typed source identity; focused RED/fix pending |
| Tests prove refusal of second materialization without fact | Noul 0.69 | Moderate; assertions independently inspected |

GREEN and independent history compatibility evidence remain pending.

## Wave 10 coordinator validation (2026-10-29)

Release compilation and the contribution, partner decision, roster and acknowledgement selection passed 115 tests. The full non-broker, non-web suite passed 7,887 and failed five: two ArtifactDownloadGrant coverage-map tests and three weekly-schedule original-result tests; the same five failed on hosted develop before wave 2. The suite is not green. Checked-in producers were reviewed; persisted histories, legacy wire migration and production behavior remain unqualified.

Whitespace verification passed for the changed C# files in the combined second and third batches using `dotnet format whitespace Compliance.slnx --verify-no-changes --no-restore --include <files>`.
