# IndependenceRuleRatificationCatalog aggregate review, 2026-10-29

Scope: aggregate and domain-event stage; no shared catalog, schema, handler or reactor changes. The focused root-provenance regression demonstrated RED before the fix; GREEN remains pending.

## Boundary and lifecycle

The existing platform singleton stream owns the current ratification sequence and monotonically advancing active rule version. A named professional ratification pins a rule digest, designation and staff revision in immutable content; personal history is retained in chained immutable content, not an aggregate roster. Keep the active monotonic authority on one stream; independently held source-use reservations prevent a new ratification while an admitted engagement decision is pinned. Exact immutable history proves absence of a previously used ratification identity. The framework supplies physical stream OCC, while the owner compares both native sequence and physical cut before append.

| State | Decision | Outcome |
|---|---|---|
| Ready, newer rule, complete exact content and unused ratification ID | Append | `IndependenceRuleRatificationRecorded`, sequence advances |
| Stale cut, held use, stale version, changed/invalid content or incomplete identity absence proof | Append | Refusal, no fact |
| Active catalog | Authorize/settle engagement use | Use fact; business sequence unchanged |
| Selected accepted operation | Exact retry | Native operation executor returns original result; changed binding conflicts |

The caller checks current professional duty and the exact rule draft, then prepares a signed/pinned immutable body. The aggregate validates structure, attribution, digest, current owner history and sequence; it cannot independently determine whether the referenced staff and duty facts remain valid. `IndependenceRuleRatificationOperations` rereads them but does not visibly hold source owners through append. Concurrent duty revocation or rule change between read and append remains a cross-owner authority question, not demonstrated safe by owner tests; investigate source authority and coordination before marking this owner fully qualified.

## Coverage and candidate fix

`IndependenceCatalogNativeBoundaryTests.ShouldRefuseHistoricalIdentityReuseOrUnprovenAbsenceGivenNativeRatificationAppend` checks history uniqueness and no-fact refusal; `IndependenceRuleCatalogLifetimeTests.ShouldRefuseNewRatificationGivenHeldUseAndAcceptAfterSettlement` checks the held-use gate; existing native read and lifetime tests cover retained history, plus `IndependenceRuleRatificationCatalogTests` exercises pure monotonic ratification with designation. No owner-direct test was found for a false child causation on a root ratification.

The selected `RatifyIndependenceRuleVersion` is an HTTP root, and `OwnerProvenance.For` creates it with null `CausationEventId`. `Validate` checks matching root and logical IDs but currently accepts a non-null causation. `IndependenceRuleRatificationCatalogOwnerTests.ShouldAcceptOnlyOwnRootGivenPreparedFirstRatification` tests a valid root and the otherwise-identical foreign-causation case. The coordinator captured RED before the owner fix: a ratification with foreign causation was accepted. The owner now refuses any non-null causation on this root operation. The difference concerns native root provenance and not the correctness of independent source proofs. Exact retry and changed intent after later ratification and direct competing append still need executable evidence or explicit mapping to existing tests.

## Contract discrepancy

`events.json` and code both include `IndependenceRuleRatificationRecorded` v2, but the selected owner `events` array in `aggregates.json` lists only its two engagement-use events. The coordinator must resolve this shared catalog discrepancy with Jev and source authority. No event contract change is proposed by this owner phase. Native operation and response envelope duplication also needs independent event review.

## Evidence

The coordinator ran `dotnet test test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore --filter 'FullyQualifiedName~ImmutableSnapshotOwnerTests|FullyQualifiedName~IndependenceRuleCatalogOwnerTests|FullyQualifiedName~IndependenceRuleRatificationCatalogOwnerTests'`: 10 failed, 3 passed; the ratification invalid-causation case failed before the fix. The totals cover all three owners. Intended focused GREEN: `dotnet test test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore --filter 'FullyQualifiedName~IndependenceRuleRatificationCatalogOwnerTests'`. GREEN is pending. Separate hosted, provider and durable-store validation remain outside this slice.

## Independent review before build

The actual ratification operation constructs a root with `OwnerProvenance.For`; its causation is null. The new check also applies during replay, so historic non-root ratification facts, if any exist, would now be rejected. No inventory of persisted history was performed. The new test checks a correctly prepared first ratification and an otherwise equal child-caused one; fixtures follow the existing native boundary test but remain uncompiled and unrun after the fix. Existing historical-identity tests reject reused or incomplete proof; no new exact retry after a later fact or durable concurrent append was demonstrated.

The source-authority race remains a concrete qualification gap: `RatifyAsync` rereads the current draft and professional duty, prepares and writes content, then appends to the ratification owner. No source hold or cross-owner atomic check is visible across that interval. A source can change after reread; an owner cut checks only this catalog. Whether the signed proof must remain current at append or another explicitly frozen decision instant needs product/source authority and an appropriate coordination protocol. Do not close this as a Jev-only decision or invent cross-owner atomicity here.

Jev `jev-1.14.0` (advisory; exact request and response in `implementation-jev/independenceruleratificationcatalog-test-impl-review-2026-10-29-*.json`):

| Question | Result | Disposition |
|---|---|---|
| Recorded event advances authority | Noul 0.66 | Moderate; independently confirmed by `Apply`, which replaces `Header` and `CurrentContent` |
| Missing recorded event in owner catalog | add to owner catalog 0.90 | Report the existing v2 event for coordinator's shared catalog reconciliation; no new wire fact |
| Source authority can change between read and append | Noul 0.90 | Confirmed from visible call ordering, not a runtime race test |
| Evidence needed to preserve source authority | source authority coordination 0.78; source resolution 0.19 | Record partial until timing and coordination are justified and exercised; Jev cannot select product authority |
| Ratification is a root operation | Noul 0.92 | Consistent with actual production constructor and selected HTTP contract |
| Native operation and response envelope | retain 0.95 | Retain existing protocol; no event schema change |

No source changes were made in this review. Selected owner event list still omits the business fact until the coordinator reconciles the shared catalog.

## Coordinator validation (2026-10-29)

Release compilation and the final owner plus related snapshot/native-boundary selection passed (37 tests). Whitespace verification passed for these owners’ changed C# files. The full non-broker, non-web suite passed 7,846 tests and failed five: two ArtifactDownloadGrant coverage-map checks and three weekly-schedule original-result checks; those same five failed on hosted develop before wave 2. The suite is not green. Current checked-in producers were reviewed against the new admission checks; persisted stream contents, hosted CI, and production qualification remain unverified.
