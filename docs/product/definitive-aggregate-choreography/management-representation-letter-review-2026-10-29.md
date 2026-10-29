# ManagementRepresentationLetter aggregate review, 2026-10-29

Scope is domain events and the aggregate. RED was captured; no handler, reactor, or API contract changes are included.

## Identity, invariant and boundary

One tenant/letter stream owns the exact external letter and its fixed required signer set. Its named CEO/CISO signers each contribute one immutable distinct signed artifact; status is signed exactly when all required signers have signed. Keep the set and signatures in the same aggregate because completeness and artifact reuse must be decided atomically. Evidence artifacts and examination/package/assertion proofs belong to their own authorities and are verified by handlers; personal signing is independently authenticated. A separate sign-off owner decides later package/outcome progression.

## Lifecycle and test mapping

| State | Decision | Outcome |
|---|---|---|
| Absent | Record valid external letter and fixed unique signers | `RepresentationLetterRecorded`, revision 1 |
| Recorded | Exact creation retry | Success without a fact; changed intent or actor conflicts |
| Awaiting signatures | Named signer signs at current revision with a distinct artifact | `ManagementRepresentationLetterSigned`, revision advances |
| Awaiting signatures | Unnamed signer, repeated artifact or stale revision | Refused without a fact |
| Signed | Exact repeat signature | No new fact; changed signature conflicts |

New tests exercise creation retry after a later signature when the server clock and display changed, changed content/actor refusal, and refusal/no-fact for artifact reuse and stale signature. Existing `ManagementRepresentationLetterTests` cover all named signers, incomplete and completed status, invalid source, and valid replay. The coordinator ran `dotnet test test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-build --filter 'FullyQualifiedName~ManagementRepresentationLetterTests'` before the fix: 1 failed and 5 passed, with the exact creation retry returning `StateConflict`. `SameInitial` now binds immutable normalized content and actor identity without comparing server-generated display or time; it retains the original stored attribution and appends no event. GREEN and independent Jev review are pending.

The selected signature contract names `CommandId` and `ExpectedRevision`, but the current command lacks both and the handler supplies the just-hydrated aggregate revision. It also checks mutable artifact state before aggregate retry. Creation handler likewise checks assertion, package and artifact before consulting the existing letter. Those API/handler gaps are later-stage follow-ups; a passing owner test does not prove selected command replay or concurrent admission. Full-state event payloads repeat envelope/identity fields; independent Jev review is required before any versioned contract change.

## Independent review before build

The record handler obtains actor display from the authenticated context and `recordedAt` from its server clock; the creation request does not supply either. The revised comparison retains the original stored attribution while requiring the same actor identity and normalized authored content. `Sign` correctly treats the request's `SignedAt` as part of the immutable signature. The new tests assert stable original attribution, no new fact on retry, changed-actor/content conflict and refusal of reused artifacts and stale revisions. The reviewer strengthened the latter assertions to check `InvalidContent` and `VersionConflict` respectively. These changes are uncompiled and unrun after review.

Jev `jev-1.14.0` (advisory; request and response in `implementation-jev/managementrepresentationletter-test-impl-review-2026-10-29-*.json`):

| Question | Result | Disposition |
|---|---|---|
| Retaining original attribution while replaying same actor and intent | Noul 0.62 | Moderate; source confirms display and time come from server, and catalog excludes fresh clock selection from retry identity. Do not treat score as proof. |
| Event state duplicates identity and historical facts | Noul 0.81 | Confirmed by event and state records |
| Event duplication disposition | retain 0.44; source resolution 0.36; versioned change 0.20 | Low confidence; unresolved. No schema change in this round. |
| Tests check reused artifact and stale revision without fact | Noul 0.88 | Advisory; direct assertions inspected and strengthened |

Current command contracts and handler retry order remain later-stage discrepancies. No durable concurrent append or full host recovery is claimed by these tests.

## Coordinator validation (2026-10-29)

The Release test project compiled with zero warnings and errors. The focused owner selection passed 23 tests after the documented RED runs. The related assertion, letter and methodology selection passed 123 tests. The full non-broker, non-web suite passed 7,859 and failed five: two ArtifactDownloadGrant coverage-map tests and three weekly-schedule original-result tests. The same five failed on hosted develop before wave 2; the full suite is not green. Checked-in producers were reviewed; persisted histories and production behavior remain unqualified.

Whitespace verification passed for this batch’s changed C# files with `dotnet format whitespace Compliance.slnx --verify-no-changes --no-restore --include <files>`.
