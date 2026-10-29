# ImmutableSnapshot aggregate review, 2026-10-29

Scope: aggregate and event stage. Handler, freezer, lineage traversal and projection changes are outside this round.

## Identity, invariant and lifecycle

Keep one immutable canonical manifest, hash, attribution and lineage together on the tenant/snapshot stream. The creation identity is the selected snapshot ID. An amendment is a different stream naming its original root and exact predecessor, so it never rewrites the original. `SnapshotAmendmentLineage` and the freezer check the predecessor's actual existence, tenant, program, root and bounded chain; the owner can check local shape and canonical content, but cannot establish another owner's existence by itself.

| State | Decision | Outcome |
|---|---|---|
| Unfrozen | valid original or amendment and canonical manifest | one `SnapshotFrozen` and registration |
| Unfrozen | invalid identity, incomplete source manifest, bad hash or linkage | validation, no fact |
| Frozen | same content, lineage, reason and actor | original registration, no fact |
| Frozen | changed content, lineage, reason or actor | conflict, no fact |

The owner's valid history is one fact; concurrency relies on the event store's same-stream expected-position append and redecision. Existing `ScopeSnapshotTests.ShouldRetainOriginalGivenAttributableAmendment` covers retries, competing content, original and amendment, and tests in the same class exercise bounded lineage and manifest regeneration. Persisted competing append, source verification and end-to-end qualification remain separate.

## Identified gaps and tests

The owner permitted an empty tenant or predecessor ID, and a canonicalized manifest with invalid program revision, source hashes or boundary IDs. Those manifest constraints already define valid source history in `SnapshotAmendmentLineage.HasConsistentSource`: a fact accepted by `Freeze` could be unusable on later verification. The owner now validates these fields before appending. New owner-level tests assert validation and no fact; a valid replay test checks exact retry and changed-intent refusal without appending. Portia rejects an empty aggregate ID during construction, confirmed in the RED run; its test now asserts that exception rather than expecting `Freeze` to handle it.

An inconsistent replay previously allowed `Freeze` to acknowledge a matching retry even when the event belonged to another tenant or snapshot, or the stream held multiple freeze facts. The reducer now remembers inconsistent history and retains the first frozen state; `Freeze` returns conflict without a fact when that flag is set. Hydration still succeeds, preserving `ScopeSnapshotTests.ShouldRejectMalformedOrCyclicSourceGivenManifestRegeneration`, which expects the lineage handler to return conflict on a cross-tenant source. The explicitly targeted `ShouldRefuseRetryWithoutFactGivenInconsistentFrozenHistory` regression checks the retry boundary. The event repeats tenant and snapshot stream identity and requires independent Jev/schema review.

## Later-stage and contract gaps

The selected command contract requires a caller-supplied snapshot ID for creation and an explicit amendment command identity. Current `FreezeProgramScopeSnapshot` has no snapshot ID field, and `ScopeSnapshotFreezer` takes the new ID from `context.RequestId`; amendment command's `SnapshotId` names the predecessor. That discrepancy needs contract/handler work outside this aggregate round. Existing `Freeze` compares content, linkage, reason and actor ID on retry but ignores clock time and display; source-based review is needed before treating server-generated attribution display or time as a change of intent.

## Evidence

Coordinator RED before the guard: `dotnet test test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore --filter 'FullyQualifiedName~ImmutableSnapshotOwnerTests|FullyQualifiedName~IndependenceRuleCatalogOwnerTests|FullyQualifiedName~IndependenceRuleRatificationCatalogOwnerTests'` reported 10 failed and 3 passed across the combined selection. The empty-snapshot case threw in the constructor; the other seven invalid snapshot inputs were accepted. GREEN and independent Jev review remain pending. No event schema changed.

The coordinator subsequently ran the same combined filter with the inconsistent-history retry test before the replay fix: 3 failed (tenant, snapshot and duplicate history) and 13 passed. GREEN and broader handler compatibility remain pending.

A second RED added mismatched stored kind and manifest cases. `dotnet test test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore --filter 'FullyQualifiedName~ImmutableSnapshotOwnerTests.ShouldRefuseRetryWithoutFactGivenInconsistentFrozenHistory'` reported 2 failed and 3 passed: the two mismatches were acknowledged as retries. Retry now additionally compares the stored kind and manifest to the validated incoming manifest. GREEN is pending.

## Independent review before build

`SnapshotContentIdentity.MatchesManifest` already requires format v1 and checks the canonical JSON and hash; the new field checks align with `SnapshotAmendmentLineage.HasConsistentSource`. The actual freezer constructs a v1 manifest with the exact captured source IDs and lowercase SHA-256 values, so the new checks are compatible with its normal source path. The new direct tests assert refusal and no fact for each incomplete manifest; build and GREEN remain pending.

There is an additional concrete replay/retry gap: the reducer accepts and overwrites a second fact or a fact with a different event tenant/snapshot, and `Freeze` can acknowledge an apparent exact retry without testing `EventTenantId`, `EventSnapshotId` or the single-fact version. The lineage inspector rejects these states, but the `Freeze` retry path does not invoke that inspector. An owner-level refusal of an inconsistent hydrated state would preserve the existing regeneration handler's conflict result without throwing during hydration. A focused test for this source-backed failure is justified by the explicit one-fact/identity invariant and existing `ShouldRejectMalformedOrCyclicSourceGivenManifestRegeneration`; malformed histories are not a default test expansion. This needs RED and a coordinator decision before changing code.

The selected creation contract names caller `SnapshotId`; the live command lacks it and the freezer uses `context.RequestId`. Also the freezer checks current program and boundary sources before consulting an existing snapshot, so source failure can prevent the catalog's original-result retry. These are contract and handler gaps outside this aggregate round and remain partial, not assumptions that exact retry is fully implemented.

Jev `jev-1.14.0` is advisory; the request and complete response are saved in `implementation-jev/immutablesnapshot-test-impl-review-2026-10-29-{request,response}.json`. Live documentation was inaccessible; the request used the repository's retained working schema.

| Question | Result | Disposition |
|---|---|---|
| Reconcile inconsistent replay with existing conflict contract | refuse with conflict 1.00 | Source supports refusal before returning retry; test/fix pending |
| Missing caller SnapshotId | yes 0.94 | Confirmed in current command; contract/handler follow-up |
| Fresh source check can precede original retry | yes 0.90 | Confirmed freezer ordering; handler follow-up |
| Clock or display changed, same authored intent and actor | return original 1.00 | Consistent with server-generated fields; no change |
| Duplicated tenant/snapshot identity | yes 0.94 | Confirmed from event type |
| Event field disposition | versioned reconciliation 0.46, keep 0.42 | Near-even; unresolved; preserve current schema |

The retry guard does not qualify every reader: `VerifyProgramScopeSnapshotHandler` does not currently check duplicate source history or event tenant/snapshot identity. The lineage verifier checks version and identity. Read-side integrity handling is a later-stage follow-up; the aggregate change does not prove corrupted-history recovery across readers. The manifest is a scalar positional record, and field equality survives the JSON replay used by the focused tests.

## Coordinator validation (2026-10-29)

Release compilation and the final owner plus related snapshot/native-boundary selection passed (37 tests). Whitespace verification passed for these owners’ changed C# files. The full non-broker, non-web suite passed 7,846 tests and failed five: two ArtifactDownloadGrant coverage-map checks and three weekly-schedule original-result checks; those same five failed on hosted develop before wave 2. The suite is not green. Current checked-in producers were reviewed against the new admission checks; persisted stream contents, hosted CI, and production qualification remain unverified.
