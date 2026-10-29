# IndependenceRuleCatalog aggregate review, 2026-10-29

Scope is the aggregate and domain-event stage. Build and focused tests are pending.

## Identity, invariant and boundary

One fixed platform catalog ID owns consecutive authored draft rule versions. Keep allocation and the current source-use holds together so a competing revision cannot advance the catalog while a personal decision holds its exact version. Immutable predecessor content retains old versions. Professional ratification is a separate `IndependenceRuleRatificationCatalog` decision; `ValidRule` refuses content claiming ratification. Evaluations keep the version they referenced and are separate owners. Reactors coordinate rule-source use with an engagement; holding a draft is not itself proof of ratification.

| State | Decision | Outcome |
|---|---|---|
| No active hold, current expected sequence | Append valid authored revision with exact immutable predecessor and prepared cut | `IndependenceRulesRevised`, next consecutive rule version |
| Active use, stale sequence or changed native/physical cut | Revise | conflict, no revision fact |
| Existing selected logical command | Exact retry through native operation executor | Original response; changed binding conflicts |
| Current revision | Acquire use | Holds exact version; business sequence unchanged |
| Active or absent use | Settle use | Settlement or fence, native sequence advances |

## Coverage and finding

The aggregate validates actor, content digest, owner scopes, expected sequence, catalog episode, immutable predecessor and selected acceptance before raising a fact. Normal `IndependenceRuleCatalogOperations.ReviseAsync` constructs a root with null causation. `Validate` previously accepted a non-null causation on that authored root; the new `IndependenceRuleCatalogOwnerTests.ShouldRequireRootProvenanceGivenPreparedRuleRevision` supplies otherwise valid immutable content and compares a normal root with a root carrying a foreign cause. It asserts a refusal and no fact for the latter. The coordinator ran `dotnet test test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore --filter 'FullyQualifiedName~ImmutableSnapshotOwnerTests|FullyQualifiedName~IndependenceRuleCatalogOwnerTests|FullyQualifiedName~IndependenceRuleRatificationCatalogOwnerTests'` before the fix: 10 failed and 3 passed across the combined selection; the catalog test confirmed foreign causation was accepted. `Validate` now rejects causation for this root on both append and replay. Green verification remains pending. Exact parent/source proof is not needed for a root operation.

Existing `IndependenceRuleCatalogHttpTests` establish original-result retry after later state and current authorization; `IndependenceCatalogNativeBoundaryTests` cover immutable authored intent mismatch; `IndependenceRuleCatalogLifetimeTests` cover more than 100 versions, held use, settlement and a late-grant fence. These prior tests have not been rerun in this pass. Storage failure and hosted CI remain separate evidence.

The selected `events.md` and `aggregates.md` assign `IndependenceRulesRevised` to this catalog alongside the use events, matching code, but `businessOwners[].events` lists only the use events and carries the revision in `baselineEvents`. This shared catalog discrepancy needs the independent Jev review and coordinator resolution. No event schema changed in this phase.

## Independent review

The RED test varies the causation field alone and uses the checked-in immutable content writer and result freeze path; its accepted control shows that the same prepared revision can be appended as an ordinary root. `IndependenceRuleCatalogOperations.ReviseAsync` and `IndependenceCatalogSeeds` are the only checked-in revision event producers found, and both derive operation provenance using `OwnerProvenance.For`, whose causation is null. The new check also runs during replay, so a preexisting persisted fact with non-null causation would now make hydration fail. The checked-in producer is evidence of intended semantics, not a production-history audit; inspect existing store history or expressly qualify this compatibility boundary before deployment. No evidence of prior persisted facts with non-null causation was found or ruled out here. No source/test change was needed during independent review.

Jev `jev-1.14.0` (advisory; exact request and response in `implementation-jev/independencerulecatalog-test-impl-review-2026-10-29-*.json`): checked-in producer null-causation premise 0.87; existing production-history proof 0.06 (not established); selected list omission premise 0.97 and include existing `IndependenceRulesRevised` in the selected owner list 0.97 (remove runtime event 0.01, source resolution 0.02). Coordinator should reconcile the owner list with the existing v2 event; no schema change is proposed. Native envelope address disposition was diffuse: keep 0.34, versioned change 0.33, source resolution 0.33; leave unresolved and retain the contract. Live TypeSafe docs were unavailable in the browser; this request used the retained repository request schema. Build and GREEN verification remain pending.

## Coordinator validation (2026-10-29)

Release compilation and the final owner plus related snapshot/native-boundary selection passed (37 tests). Whitespace verification passed for these owners’ changed C# files. The full non-broker, non-web suite passed 7,846 tests and failed five: two ArtifactDownloadGrant coverage-map checks and three weekly-schedule original-result checks; those same five failed on hosted develop before wave 2. The suite is not green. Current checked-in producers were reviewed against the new admission checks; persisted stream contents, hosted CI, and production qualification remain unverified.
