# MethodologyTemplateContribution aggregate review, 2026-10-29

Scope: aggregate and domain-event stage. Handlers, content preparation, platform template publication and source authorities are separate.

## Identity, invariant and boundary

One tenant/contribution stream decides the lifecycle of one immutable deidentified proposal: an independent platform reviewer approves or declines it; only an approved proposal can be accepted, by a third distinct platform operator. Keep those lifecycle facts together because identity, review outcome and person separation must be decided together. The tenant-owned source lineage and deidentification content remain outside platform template state. M0-D25 requires client content to enter platform content only via an explicit reviewed deidentified contribution; content inspection and publication are downstream operations, not evidence supplied by the owner alone.

| State | Decision | Result |
|---|---|---|
| Empty | Valid proposal from tenant member | Proposed fact |
| Proposed | Different user reviews with explicit outcome | Approved → reviewed; declined → terminal declined |
| Reviewed | User distinct from proposer and reviewer accepts | Accepted fact |
| Any | Wrong state, self review, contributor/reviewer acceptance or invalid identity | Refusal with no fact |
| Selected committed operation | Exact retry | Native executor resolves original result before mutable preparation |

Existing `MethodologyTemplateContributionNativeTests` cover the distinct people rule, original retry after acceptance, compact content references and unavailable/corrupt content. `MethodologyTemplateContributionTests` cover reviewed, deidentified sharing and refusal before review. Those tests were not rerun in this phase; separate durable and hosted qualification remains.

## Gap and focused tests

The direct `Propose`, `Review` and `Accept` methods checked lifecycle, but their replay reducer additionally requires nonempty acting user, exact member mapping, own root operation, kind, actor, chronology and attributed response. Invalid direct inputs reached `RaiseEvent` and encountered those checks in the reducer instead of returning a refusal before a fact. The owner now applies the same reducer checks to each candidate fact before raising it, and the review/accept preparation guards reject empty user IDs. The normal operation wrapper constructs the expected `OwnerProvenance` and verifies the authenticated actor before use. `MethodologyTemplateContributionOwnerTests.ShouldRefuseWithoutFactGivenInvalidNativeAdmission` exercises invalid identity, member, owner, root, causation, kind and time across the three stages using source-shaped valid setup.

The owner events in `aggregates.json` retain baseline typed `State` descriptions; `events.json` and actual wire contracts identify compact version 3 facts containing immutable content references. This catalog discrepancy needs independent Jev/source review and coordinator reconciliation. No event schema change is made here.

## Evidence

Coordinator RED before the guard: `dotnet test test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore --filter 'FullyQualifiedName~MethodologyTemplateContributionOwnerTests|FullyQualifiedName~PartnerIndependenceDecisionOwnerTests|FullyQualifiedName~PlatformOperatorRoster'` reported 21 failed and 49 passed across the combined selection. Invalid contribution admissions threw while raising events. Independent Jev review and GREEN remain pending.

## Independent review before build

The changed candidate check calls the same `Require` used by replay, before `RaiseEvent`, and applies lifecycle/independence guards first. Normal `MethodologyTemplateContributionOperations` builds same-owner root provenance, expected command kind, member/platform actor mapping and a null original revision as currently implemented. The test setups exercise valid proposal and review before invalid later-stage calls, but build and GREEN remain pending. The executor source resolves a selected accepted operation before calling preparation, then rechecks on an OCC race; this supports the documented retry ordering, though it does not by itself establish durable-store behavior.

The selected review contract requires a caller `ExpectedRevision`, while current `ReviewTemplateContribution` lacks it and the handler prepares against the freshly read revision. The current root provenance explicitly requires `OriginalExpectedRevision` null; it therefore does not implement that selected concurrency contract. This needs coordinated command, handler and owner changes outside this aggregate-only review, so mark the owner partial for the named mismatch. `AcceptTemplateContribution` also has no expected revision in the current command; its selected contract must be assessed separately rather than inferred from review.

The `State` descriptions in `aggregates.json` are explicitly labeled `baselineContract`; the selected `events.json` and concrete v3 records agree on compact content-reference facts. This is historical baseline evidence, not by itself a contradictory current event requirement. No schema change is requested. Identity fields are repeated across compact facts and the operation/stream; consumer and migration needs for any versioned simplification are unresolved.

Jev `jev-1.14.0` is advisory; exact input and response are in `implementation-jev/methodologytemplatecontribution-test-impl-review-2026-10-29-{request,response}.json`. Live TypeSafe docs were inaccessible, so the repository's previously working request schema was used.

| Question | Result | Disposition |
|---|---|---|
| Shared pre-append/replay provenance rule | yes 0.62 | Uncertain signal; direct source comparison confirms matching calls |
| Selected review revision absent from live commands | yes 0.94 | Confirmed in command and handler; partial pending coordinated contract/owner work |
| Historical `State` baseline versus selected compact event | baseline history 0.98 | Labels and selected v3 catalog support no current contract contradiction |
| Distinct-person rule | yes 0.57 | Uncertain; direct `PrepareReview`/`PrepareAccept` checks support it; process tests still need rerun |
| Executor replay before preparation | yes 0.36 | Low score; executor source confirms `DecideAsync` before `Prepare`, with OCC recheck |
| Repeated identity payload | needs source resolution 0.79 | Leave event unchanged pending consumer/migration evidence |

## Wave 10 coordinator validation (2026-10-29)

Release compilation and the contribution, partner decision, roster and acknowledgement selection passed 115 tests. The full non-broker, non-web suite passed 7,887 and failed five: two ArtifactDownloadGrant coverage-map tests and three weekly-schedule original-result tests; the same five failed on hosted develop before wave 2. The suite is not green. Checked-in producers were reviewed; persisted histories, legacy wire migration and production behavior remain unqualified.

Whitespace verification passed for the changed C# files in the combined second and third batches using `dotnet format whitespace Compliance.slnx --verify-no-changes --no-restore --include <files>`.
