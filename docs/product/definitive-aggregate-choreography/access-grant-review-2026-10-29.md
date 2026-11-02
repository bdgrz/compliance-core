# AccessGrant aggregate review, 2026-10-29

Scope: domain-event/aggregate stage only. Handlers, reactors and projectors are untouched. Evidence is written, not executed; build and run are pending.

## Boundary decision

**Keep together (retain).** One grant (`AccessGrant`, stream `access-grants/{grantId}` under the tenant) decides atomically that a stable grant identity has one immutable set of principal/role/scope/effective terms and, once revoked, can never be reissued.

- Identity: tenant + caller-supplied immutable `GrantId`. The stream address derives from those only; the issuing episode binds to the grant at creation.
- Invariant: one immutable terms-and-episode set per grant id; revocation is terminal.
- Exclusions: wider-scope coverage of narrower scopes (authorizer), member/role/source current state, clock selection, membership retirement orchestration (`DeprovisionMember` reactor/handler), guest provisioning, authorization.
- Splitting terms from revocation would allow reissue or episode mismatch (catalog rationale); no split.

## Lifecycle

| State | Command | Result | Event |
|---|---|---|---|
| none | Issue (valid) | issued | `AccessGrantIssued` |
| none | Issue (invalid, empty episode id) | validation refusal | none |
| none | Revoke | not found | none |
| issued/revoked | Issue, exact terms, actor and episode | success, no event | none |
| issued/revoked | Issue, changed terms, actor or episode | conflict | none |
| issued | Revoke (valid) | revoked | `AccessGrantRevoked` |
| issued | Revoke (invalid actor/time) | validation refusal | none |
| revoked | Revoke, same actor | success (original result), no event | none |
| revoked | Revoke, different actor | conflict | none |
| revoked | RevokeForRetirement | no-op success | none |

## Changes (real gaps)

- Revoke exact retry: the catalog's selected replay rule says an exact retry returns the original result and treats the "Revoke exact retry conflicts" observation as an old replay defect. Revoke of an already revoked grant now succeeds with no event for the revoking actor (clock timestamp ignored) and conflicts for any other actor. The aggregate stores the revoking actor on replay. Validation is checked before the revoked state.
- Issue retry ignored the membership episode: same id and terms with a different or dropped `MembershipEpisodeId` returned success. It now conflicts (catalog: "Issue compares terms/episode"; revoked grants never rebind). `ReplayIssue` (handler receipt path) is unchanged.
- An empty (non-null) `MembershipEpisodeId` is now refused as validation.

## Test-to-criterion table

| Criterion | Test |
|---|---|
| Valid issue, event payload | `ShouldIssueGrantWithScopeAndProvenanceGivenValidTerms` |
| Invalid decisions append nothing | interval, foreign-tenant, missing resource type, mismatched resource type, empty episode, invalid revoke actor/time tests |
| Changed-intent refusal | `ShouldRejectChangedTermsGivenExistingGrantId`, `ShouldRejectChangedMembershipEpisodeGivenExistingGrantId` |
| Exact retry, including after later facts | `ShouldReplayOriginalCreationWithoutRestoringAuthorityGivenPreviouslyRevokedGrantId`, episode retry test |
| Revoke no-op and competing actor | `ShouldReturnOriginalRevocationWithoutEventGivenExactRetryByRevokingActor`, `ShouldRejectRevocationGivenGrantRevokedByAnotherActor` |
| Refusal with no fact | `ShouldRejectRevocationWithoutIssuedGrantGivenUnknownGrant` |
| Replay of valid history | the Given-seeded tests above |
| Same-stream sequence | `ShouldIssueAndRevokeGivenFreshGrantAcrossCommands` |
| Stale revision / competing appends | Portia expected-position append OCC (framework); the handler resolves via `ReplayIssue` |

Malformed-history tests were not added, by user preference. `ShouldRejectRepeatedRevocationGivenPreviouslyRevokedGrant` was replaced by the two revoke retry tests.

## Event contract status

- `AccessGrantIssued` (TenantId, GrantId, Terms, MembershipEpisodeId) and `AccessGrantRevoked` (TenantId, GrantId, RevokedBy, RevokedAt) are business facts. No add, rename, remove or payload change requested.
- Catalog `eventAuthority` is "requires schema reconciliation"; that stays with the shared schema owner.

## Later-stage follow-ups (not aggregate gaps)

- `RevokeAccessGrantHandler`: now yields success for a same-actor repeat; the selected CommandId receipt and original-result binding are handler work. A different-actor repeat still conflicts.
- `GrantAccessHandler` replay path should also pass the episode if guest provisioning needs it; `ProvisionGuestProgramAccess` and retirement reactors own that wiring.
- HTTP routes and authorization precedence before replay.

## Evidence

- Jev: no new request this round; decisions follow the catalog's selected replay rule and invariant text.
- Tests: written, not executed; build and run pending. Intended: `dotnet test test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~AccessGrantTests"`, then the unit suite.
- RED: none captured; not claimed.
- Residual risk: other tests that revoke twice and expect Conflict (none found by search) and the handler suite are unverified.

## Review before build

Independent review of source, tests and this file (not the implementer summary).

### Findings and fixes

- Implementation: no correctness gap found. Revoke retry (same actor success, other actor conflict, validation first, NotFound before both), episode-bound issue retry, empty episode refusal and post-revocation exact issue replay all enforce the invariant; no event is appended on any retry. Payloads carry no metadata duplicates.
- Test gap fixed: no test that a revoked grant cannot be reissued with changed terms. Added `ShouldRejectReissueGivenRevokedGrantAndChangedTerms`.
- Style fix: `Admin` static field moved above the tests (declared after use-sites in the file bottom; ordering made consistent).
- Compile read: usings, `Uuid?` lifted comparisons (same pattern as existing `RevokeForRetirement`), `with` on records, `Assert.Collection`/`Assert.Single`, `PendingEvents` used as a list, no Attach on pending events. Nothing found. Not compiled.
- Not covered here: `RevokeForRetirement` (internal, retirement stage) has no test in this file; belongs to the retirement/cleanup follow-up.

### Jev (model jev-1.14.0, advisory), request/response in `implementation-jev/accessgrant-test-impl-review-2026-10-29-*.json`

- Revoke retry handling: same actor success, other actor conflict, 1.00 (decisive). Applied (already the code).
- Premise that "Revoke exact retry conflicts" is a superseded baseline observation: Noul 0.95. Consistent with the catalog identityReadingRule.
- Changed or dropped episode conflicts: conflict_is_correct 0.82 (confidence 0.73), clear lead. Applied (already the code).
- Tests assert behavioral outcome and can fail: Noul 0.95.
- Discrepancies: no event or command naming conflicts; no catalog change needed.

### Remaining risks

- Nothing built or run. Handler `RevokeAccessGrantHandler` now returns success for same-actor repeat; CommandId receipt binding is later-stage work.


## Coordinator build and test evidence (2026-10-29, wave 3)

- `dotnet build test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore`: 0 warnings, 0 errors after fixing blind-written test compile errors.
- `dotnet test ... --no-build -c Release --filter "Category!=BrokerIntegration&Category!=WebIntegration"`: 7231 passed, 5 failed of 7236.. The five failures predate this wave: two AggregateCommandCoverageTests (map lacks ArtifactDownloadGrant) and three WeeklyScheduleOriginalResultTests.
- `dotnet format whitespace ... --verify-no-changes --include <changed files>`: clean.
- New tests were written before the build; RED was not observed.
