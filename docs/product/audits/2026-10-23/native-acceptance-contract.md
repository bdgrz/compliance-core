# Native operation acceptance and result lookup

This is the exact prerequisite for [#1042](https://github.com/bdgrz/compliance-core/issues/1042). Current Portia/Fitz per-stream append does not provide it. The selected implementation must supply this contract before enabling the affected replacement paths.

## Binding and append

`AcceptedOperationBinding` contains Realm, LogicalId, CommandKind, FirstOwner, AffectedOwner, ExpectedRevisionOwner, Episode, OriginalActor, OriginalExpectedRevision and AuthoredIntentSha256. FirstOwner is the stream appending the native admission. AffectedOwner is the public business resource; ExpectedRevisionOwner identifies the logical counter the caller supplied. They differ for campaign/batch, finding/action, request/response and administrator guard/member transitions. For commands with multiple expected tokens, ExpectedRevisionOwner names the primary public counter; every additional expected epoch/version is a required authored field and participates in canonical comparison. Compare all these fields on reuse; never treat a hash as the only owner/actor check.

`ValueTask<AcceptedAppendResult> AppendAcceptedOperationAsync(EventStreamAddress firstOwner, ulong expectedPhysicalPosition, AcceptedOperationBinding binding, IReadOnlyList<DomainEvent> fullyAttributedNativeFacts, CancellationToken ct)` is atomic with unique `(Realm, LogicalId) -> first native fact locator`. The whole batch and uniqueness assertion commit together. Preflight every concrete attributed envelope before opening the append session. No separate KV-before/after append can implement this.

`AcceptedAppendResult` is a closed result:

- `Committed(FirstFact, NextPhysicalPositionExclusive)`: batch committed and the binding is visible; the next position is the exclusive append frontier, not the final event offset.
- `Replayed(FirstFact)`: comparison fields equal; append nothing and load the original response descriptor from that fact.
- `Conflict`: the key is already bound differently; append nothing. Public error exposes no other owner's routing/result details.

FirstFact is `SourceFactLocator` with exact resource/global positions, metadata aggregate version, event ID and digest. The proposed candidate EventId, AcceptedAt, server-resolved intent, response URL and generated output bytes are **not** binding comparison fields. Concurrent equal authored intent can produce different candidates; only the winner's facts/response survive, and the loser returns the winner's original response.

Root native acceptance carries `FirstResponse: ImmutableContentRef`. It references `FirstResponseDescriptor { HttpStatus, ContentType, ResultSchema, Body: ResponseBodyReference }`. Body is a verified standard immutable result or a protected response-content reference. Native terminal facts can carry `OperationResult: ImmutableContentRef` for the exact original typed business outcome. These are fields on native business facts, not generic receipt events or aggregate lifetime dictionaries. The unique index contains locators/binding routing data only and is rebuilt from those native facts without TTL.

## Authoritative lookup and retry order

`ReadAcceptedOperationAsync(Realm, LogicalId)` is a linearizable read of the atomic acceptance constraint, returning binding routing data and FirstFact or NotFound. This index differs from a projector/search index. A projector miss cannot prove absence. NotFound is only absence at that read instant; the atomic append assertion handles a concurrent first acceptance.

1. Authenticate and apply current top-level tenant/platform/account policy.
2. Bind required route/typed IDs and normalize authored input, including presence flags and selected-source variants.
3. Look up routing binding as an authorization input; authorize the concrete current resource scope before loading prior intent/result. Never disclose routing metadata to an unauthorized caller.
4. For an existing accepted binding, compare kind, all owner scopes, canonical actor, episode, authored input digest and original expected revision. Return exact first response or Conflict. Do not regenerate clocks, tokens, source selections or expected revisions.
5. For no binding, resolve current sources, freeze concrete intent/result content, validate logical source/resource revision and local invariant, then perform the atomic native append at the hydrated physical tail.
6. On OCC, busy source or guard failure, recheck accepted binding before failing/retrying: an identical concurrent request may already have committed. An unknown append outcome always resumes by lookup with the same logical ID.

Rebuild verifies the first batch and all subsequent operation/cause links, including native terminal facts. A duplicate key with different binding is corruption, never a last-write-wins repair. Derived effects have their own LogicalId and CommandKind; RootOperationId and CausationEventId link them to the root. The root's original binding is not compared against a child command kind. A no-append condition proof (e.g. an already-associated replacement identity) is a verified existing native fact; it must still resume the waiting process without a receipt event.

## Status/result authorization

Queries are specified in `operation-query-contracts.json`. A binding lookup may provide **minimal internal authorization routing**, but no authored business input, response or decrypted capability before current owner policy passes. Routing scope is a server-created closed record containing realm, policy key from a static registry, concrete tenant/program/resource selectors, original actor and any native selected-source selector needed by that policy.

Tenant creation pending bootstrap authorizes its original authenticated creator. Platform identity recovery authorizes the frozen replacement provider identity or the recovered account; platform realm does not mean every status query requires a platform operator. Protected download results require their original personal actor plus current artifact/handoff disclosure authority. A UUID alone grants no status/result access. Missing and unauthorized operations use existing indistinguishable visibility errors where required.

## Required proof

Race the same UUID at different owners/kinds and assert exactly one native batch. Race equal intent with different candidate EventId/clock/content-response pointers and assert one original response. Crash between every internal persistence action and prove no index without native fact, no native acceptance without index, and no partial batch. Rebuild from native history and repeat both cases. Test current authorization denial before any result load, owner-versus-parent revision scopes, child-versus-root binding, and exact retry after later edits/new episodes.
