# ManagementAssertion aggregate review, 2026-10-29

Scope: aggregate and domain-event stage only. Handler, reactor, authorization and shared schema changes are outside this round.

## Identity and boundary

One tenant/assertion stream owns authored versions and at most one immutable independent named CEO/CISO decision for each version, with the program and examination scope fixed. Keep authored versions and their decisions together so one owner decides exact-version eligibility and one-decision uniqueness atomically. The version binds exact package, source snapshot, description and supporting content by identity and digest. Package validation, actual executive authority, and the assertion/letter/outcome ordering are source-proof and process responsibilities; the owner must not use projections as authority.

## Lifecycle and coverage

| State | Decision | Outcome |
|---|---|---|
| Empty | Valid creation | One recorded version; draft |
| Existing | Exact creation retry | Original success, no fact |
| Draft/current or previously decided | Revision with changed content and exact revision | New version, draft; prior decision retained |
| Draft/current version | Independent named CEO/CISO decision | One approved or rejected decision fact |
| Any | Missing source bindings, prohibited signer, stale revision, or different decision for a decided version | Refusal, no fact |

Existing `ManagementAssertionTests` cover attribution, exact package inputs, CEO/CISO independence, invalid source bindings, valid replay and a same-decision retry. A focused new test creates, revises, then retries the exact original creation and compares the pending fact count; changed creation intent remains a conflict. `SameInitial` previously read `CurrentVersion` instead of the retained first version, so the exact creation retry conflicted after revision. It now compares the retained first version.

## Contract discrepancies and limits

The selected `ApproveManagementAssertion` contract requires `CommandId` and `ExpectedRevision`, but the current command has neither. The handler reads the current revision and current signer list before the aggregate is invoked, so historical receipt replay and its authorization order require separate handler/contract qualification. `RecordManagementAssertionHandler` also resolves mutable source state before reaching `Create`; direct aggregate retry cannot prove handler retry semantics. No handler or event changes are made here.

Events retain full state snapshots, including tenant/subject/revision and historical versions/approvals. The duplication and versioned event-contract disposition need independent Jev review; shared schemas are not changed here. Payload and version limits bound per-stream aggregate state. Existing valid replay tests do not establish durable-store OCC, restart or production qualification.

## Evidence

Coordinator RED before the fix: `dotnet test test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore --filter 'FullyQualifiedName~ManagementAssertionTests|FullyQualifiedName~ManagementRepresentationLetterOwnerTests|FullyQualifiedName~MethodologyTemplateOwnerTests'` yielded 4 failed and 7 passed across the combined selection; the new assertion test saw a `StateConflict` for the exact creation retry. Independent Jev review and focused/full GREEN are pending.

## Independent review before build

The first-version comparison correctly preserves the original creation intent after later revisions; unchanged actor and content remain no-op. The earlier comparison also checked `actorDisplay`, which `RecordManagementAssertionHandler` derives from the current authenticated session rather than the authored request. A changed session display for the same member rejected an exact retry. `ShouldReturnOriginalCreationWithoutFactGivenSameActorWithChangedSessionDisplay` asserts success without a new fact and preserves the original stored display/time; the owner now compares stable actor identity rather than display.

`Approve` locates the exact historical version and existing decision before stale/current checks; a matching member, role, outcome and rationale is a no-op while a changed decision conflicts. Existing tests cover a decision retry and valid replay, but a retry after a later version was not newly tested here. Current handler/command source does not expose the selected `CommandId` and expected revision, and mutable source checks precede the creation aggregate, so this review does not qualify end-to-end selected retry semantics.

Jev `jev-1.14.0` is advisory; exact input and complete response are in `implementation-jev/managementassertion-test-impl-review-2026-10-29-{request,response}.json`. Live TypeSafe documentation was inaccessible, so the retained repository request schema was used.

| Question | Result | Disposition |
|---|---|---|
| First retained version supports creation retry after revision | yes 0.76 | Consistent with selected creation contract and test |
| Same actor and intent with changed session display | return original 0.74, conflict 0.22 | Source establishes display is session-derived; fixed after RED |
| Existing prior-decision lookup preserves decision after later version | yes 0.56 | Uncertain; code inspection supports no-op; additional focused proof desirable |
| State event repeats tenant, subject, revision | yes 0.90 | Confirmed from event and view contracts |
| Event payload disposition | source resolution 0.61, keep 0.27, versioned reconciliation 0.12 | Unresolved; preserve current event schema until consumer and migration evidence |

Coordinator second RED: `dotnet test test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore --filter 'FullyQualifiedName~ManagementAssertionTests|FullyQualifiedName~ManagementRepresentationLetterTests|FullyQualifiedName~MethodologyTemplateOwnerTests'` reported 6 failed and 17 passed across the combined selection. The changed-session-display retry failed before the adjustment. GREEN remains pending.

## Coordinator validation (2026-10-29)

The Release test project compiled with zero warnings and errors. The focused owner selection passed 23 tests after the documented RED runs. The related assertion, letter and methodology selection passed 123 tests. The full non-broker, non-web suite passed 7,859 and failed five: two ArtifactDownloadGrant coverage-map tests and three weekly-schedule original-result tests. The same five failed on hosted develop before wave 2; the full suite is not green. Checked-in producers were reviewed; persisted histories and production behavior remain unqualified.

Whitespace verification passed for this batch’s changed C# files with `dotnet format whitespace Compliance.slnx --verify-no-changes --no-restore --include <files>`.
