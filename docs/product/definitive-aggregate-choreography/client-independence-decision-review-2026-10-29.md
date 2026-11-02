# ClientIndependenceDecision aggregate review, 2026-10-29

Scope: aggregate and domain-event stage. Tests are written, not executed; build and run are pending. No handler, reactor, projection, or shared contract was changed.

## Identity, invariant and boundary

The tenant/evaluation tuple selects one permanent preview stream; the selected command ID does not alter that identity. It holds exactly one computed preview with its frozen rule/service snapshot and never authorizes a production engagement. Keep this independent, immutable artifact separate from `ClientServiceIndependence`, which admits the evaluation, and `ServiceEngagementIndependence`, which decides engagement acceptance. The process consumes the admission and materializes the preview; the handler verifies source facts, frozen content and complete source cuts. The owner stores references and does not read external immutable content.

The stream has at most one event, so history is bounded. Different preview attempts compete on the same stream under framework OCC; an exact accepted retry is resolved by selected `NativeAcceptance`, with changed binding refused. The handler verifies and computes content before calling the owner. A different materialization of a claimed evaluation is refused without a new fact.

## Lifecycle and changes

| State | Decision | Outcome |
|---|---|---|
| Empty, valid owner and admitted child | Materialize | `ClientIndependenceEvaluated`, revision 1 |
| Empty, empty identity or provenance inconsistent with own evaluation and client source | Materialize | Validation, no event |
| Evaluated | Materialize again | Conflict, no event |
| Evaluated, selected original operation | Compare accepted binding | Original binding replayed; changed binding conflicts |

The owner previously checked only the provenance owner before raising its fact. The history verifier (`ClientServiceNativeHistory.VerifyDecisionAsync`) later rejects facts whose root, cause, kind, first owner, affected owner or expected-revision owner do not match the admitted client evaluation. The owner now enforces those source-backed provenance requirements before append, and rejects empty tenant, evaluation and logical IDs as required by the materialization receiver. This does not independently verify the causation fact: that is the source receiver's admission responsibility.

The new `ClientIndependenceDecisionTests` cover a valid child, empty tenant/evaluation, eight incorrect provenance fields, immutable one-fact behavior, replayed exact versus changed operation binding and tenant/evaluation identity isolation. The coordinator captured RED before this fix; focused GREEN and full-suite validation are pending. Existing `ClientServiceIndependenceTests` exercise frozen intent failure and exact receipt replay through the process; they do not prove these new owner-level checks or external persistence.

## Catalog and event contract discrepancies

The selected catalog event `ClientIndependenceEvaluated` matches the event raised; no event schema, wire discriminator or shared file was changed. Its tenant and operation IDs repeat stream identity while the native operation and receipt accompany the immutable content. Independent Jev review should evaluate the duplication and report any versioned contract recommendation to the coordinator.

`aggregates.json` and `aggregate-details.md` assign product rule D26-06 to this owner and require partner evaluation when conditional and management acknowledgement. `docs/product/decisions/m0-d26-independence.md` places those fields under the engagement acceptance record, whereas `IndependenceEvaluationView` and `ClientIndependencePreviewEvaluator` expressly describe a draft-rule preview that cannot grant production acceptance and contain no such references. The conflict remains unresolved: the preview must not be marked fully done for D26-06 without source-backed model/product reconciliation. Do not invent references in this aggregate or treat the preview as an acceptance. The catalog materialization command's `effectKind` says `claim_professional_assertion_identity`, a naming discrepancy for coordinator/Jev review. Downstream acceptance and handler validation remain outside this aggregate round.

## Evidence and limits

Coordinator RED: `dotnet test test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore --filter 'FullyQualifiedName~ClientServiceIndependenceOwnerTests|FullyQualifiedName~ClientIndependenceDecisionTests|FullyQualifiedName~EngagementManagementAcknowledgementOwnerTests'` yielded 14 failed and 8 passed across the three-owner selection. Empty owner identities and invalid child provenance were accepted before the fix. Intended focused GREEN: `dotnet test test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore --filter 'FullyQualifiedName~ClientIndependenceDecisionTests'`. Independent Jev review, build and focused/full unit runs remain pending. Valid in-memory replay does not establish durable-store, production or end-to-end qualification.

## Independent review before build

The new validation matches the deterministic checks in `ClientServiceNativeHistory.VerifyDecisionAsync`; the actual materialization receiver constructs the same child shape from the source admission. Reviewed the new tests and their fixture by reading; they check behavioral refusal and selected binding but remain uncompiled and unrun after the fix. Exact event-source and content proof are checked in the receiver, not by the aggregate. Durable concurrent appends are not directly proved by these owner-level tests.

Jev `jev-1.14.0` (advisory; exact request and response in `implementation-jev/clientindependencedecision-test-impl-review-2026-10-29-*.json`):

| Question | Result | Disposition |
|---|---|---|
| Catalog requires preview fields omitted by source | Noul 0.89 | Confirmed by catalog and preview model |
| D26-06 discrepancy | reconcile owner mapping 0.82; needs source resolution 0.13 | Report for coordinator's source-backed catalog reconciliation. Do not claim the acceptance record is complete without verifying all required fields and proofs. |
| Materialization effect kind describes different action | Noul 0.78 | Confirmed by command and receiver names |
| Effect kind discrepancy | correct catalog effect kind 0.98 | Report for coordinator's shared-file correction; no shared edit here. The same value appears on 17 selected command contracts, so other rows need separate source review. |
| Tenant and operation IDs repeat stream identity | Noul 0.75 | Confirmed by fact and stream construction |
| Payload disposition | keep 0.50; version 0.30; source resolution 0.20 | Low confidence: unresolved; no event contract change proposed without further source evidence. |
| Tests check invalid provenance refusal without fact | Noul 0.78 | Advisory; assertions independently inspected |

The owner remains partial against the catalog's D26-06 assertion until its mapping and acceptance evidence are reconciled. No source or event changes were made in this review.

## Coordinator validation (2026-10-29)

Release build and the focused three-owner selection passed (30 tests). Related client independence, acknowledgement and engagement tests passed (129 tests). The full non-broker, non-web suite passed 7,828 tests and failed five: the two ArtifactDownloadGrant coverage-map checks and three weekly-schedule original-result checks; those same five failed on hosted develop before wave 2. The suite is not green. Independent review checked current production operation builders; acknowledgement historical stream contents have not been inspected, and its replay now enforces child provenance earlier. Hosted CI and production qualification remain open.

Whitespace verification passed for the three owners’ changed C# files using `dotnet format whitespace Compliance.slnx --verify-no-changes --no-restore --include <files>`.
