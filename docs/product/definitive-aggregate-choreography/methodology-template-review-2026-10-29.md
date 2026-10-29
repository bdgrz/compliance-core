# MethodologyTemplate aggregate review, 2026-10-29

Scope is the aggregate and domain-event stage. The coordinator captured a focused RED before the production fix; GREEN remains pending.

## Boundary and lifecycle

A platform template has a caller-supplied immutable template ID and one current authoritative version. Keep the author, current draft, review and publication together because independent review and a distinct third approver must decide the same frozen version atomically. Retirement prevents future revisions; immutable content and publication history remain outside compact current state. Tenant contribution review and deidentification are owned by `MethodologyTemplateContribution`; the preparation rules and source reader verify an accepted contribution before including its content. The application service prepares immutable content and uses selected native acceptance for exact retry; changed authored intent conflicts. Framework OCC serializes competing saves.

| State | Decision | Outcome |
|---|---|---|
| Absent | Create | First draft |
| Active draft | Independent review | Approved review or changes requested |
| Reviewed | Third person approves exact reviewed version | Immutable publication |
| Published or changes requested | Revise | Next draft, old published content remains |
| Previously published | Retire | Terminal retirement; later revision refused |

`MethodologyTemplateTests` covers self-review refusal, independent publication, successor and valid replay; `MethodologyTemplateNativeHistoryTests` and command handler tests cover original results, stale state and retirement. They do not establish durable production behavior.

## Focused finding

`Append` applies `PreparePublish` using current state, then raises a fact from the caller's supplied version. The reducer checks version ID and content hash against current state only after assigning `_hasPublished`, `PublishedVersionNumber` and `PublishedVersionId`; an inconsistent input can throw after mutating those fields. It also stores the supplied reviewer from the publication without comparing it to the authoritative reviewed reviewer. The regular operations prepare a coherent published version from `MethodologyTemplateRules`, but the owner should refuse inconsistent publication with no fact or state mutation rather than corrupting its in-memory state or replacing reviewer identity.

`MethodologyTemplateOwnerTests.ShouldPublishOnlyExactReviewedVersionGivenPreparedPublication` builds a genuine reviewed owner via existing scenario, then supplies a valid positive publication and separate changed-version-ID, changed-content-digest and changed-reviewer cases. The negative cases assert failure, no new fact and unchanged revision/publication state. The coordinator captured RED before the fix; the valid control passed and all three inconsistent publication cases failed. This is command-level refusal coverage, not malformed-history testing.

## Contract and open evidence

The selected event catalog includes creation, revision, review, publication and retirement facts. Native facts retain version metadata, source stream identity and original response references; the independent Jev review should assess duplication before any versioned schema change. No shared contract change is proposed. GREEN, independent Jev review, focused/full tests and format checks remain pending. Exact contribution proof, host/store recovery and production qualification remain separate.

## RED and fix

`dotnet test test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore --filter 'FullyQualifiedName~ManagementAssertionTests|FullyQualifiedName~ManagementRepresentationLetterOwnerTests|FullyQualifiedName~MethodologyTemplateOwnerTests'` produced 4 failed and 7 passed across the three-owner selection. For this owner, changed version ID and content hash raised exceptions; changed reviewer was accepted.

Before raising a publication fact, the owner now compares the template identity, published status, version ID, author, content digest and reviewer to the current independently reviewed version. Replay also requires publication to preserve the reviewer. Publication and retirement state assignments now occur only after all existing lifecycle and immutable version checks succeed, preventing an invalid fact from partially modifying state before rejection. The changed replay guard would reject historical facts containing an inconsistent publication reviewer; checked-in producer constructs publication from reviewed content, but actual persisted history has not been audited. GREEN is pending.

## Independent review before build

The publication guard does not yet check the supplied review outcome or publication attribution. `MethodologyTemplateReader.VersionAsync` requires `MethodologyTemplateRules.ValidVersion`, and checks the approved member and time against the publication fact; therefore the owner can accept a version that the normal reader will reject. The positive owner fixture originally changed only the status and lacked approval fields, so it did not represent the checked-in producer. It now supplies an approver, approval time and rationale, with additional independent negative cases for review outcome, changed content body with unchanged digest, approver, approval time and rationale. These additions are written but not executed; the coordinator will capture RED before the corresponding production fix.

The checked-in approval path starts from the reviewed view, retains its review and content, and adds the independently attributed approval. Direct immutable-content integrity and full history are verified by the reader; the owner does not read external content itself. Actual persisted publication facts were not audited for compatibility with the new reviewer replay guard.

Jev `jev-1.14.0` gave 0.89 for the changed-review-outcome premise, 0.89 for the missing-rationale premise, 0.95 that an admitted publication could be rejected by the reader, and 0.65 for the test-quality question. The payload-duplication premise scored 0.76. On disposition, versioned contract change scored 0.49, source resolution 0.30 and retaining the current contract 0.21; this is diffuse and does not establish a schema change. Request and response are in `implementation-jev/methodologytemplate-test-impl-review-2026-10-29-{request,response}.json`. Jev is advisory. Live TypeSafe API OpenAPI documentation was used; the documentation page was unavailable.

Remaining: prove RED/GREEN for the newly identified admission cases, run the focused and broader tests, and qualify the existing event payload and production history separately. No shared event or schema changes were made.

## Second RED and publication content validation

Independent review added cases for changed content body with a retained digest, unapproved review outcome, another approver, changed approval time and absent rationale. `dotnet test test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore --filter 'FullyQualifiedName~ManagementAssertionTests|FullyQualifiedName~ManagementRepresentationLetterTests|FullyQualifiedName~MethodologyTemplateOwnerTests'` produced 6 failed and 17 passed across the three-owner selection; the five new invalid publication cases were accepted before this refinement.

The pre-append guard now calls the existing `MethodologyTemplateRules.ValidVersion` to verify the normalized content digest, approved review, attribution and rationale, and binds the prepared approver and approval time to the actual command actor and operation time. These checks reuse the regular preparation/reader rules. They do not impose additional checks on historical event replay, where immutable content is validated by the reader. GREEN remains pending.

Independent follow-up: the normal publication producer starts with the verified predecessor and meets the new checks. The compact owner retains the reviewer identity but not the entire reviewed snapshot, so it cannot independently compare every immutable review, attribution and contribution field with the predecessor. Source-authoritative predecessor binding remains a partial aggregate boundary; a projection check is not sufficient. New replay reviewer validation can reject inconsistent persisted facts earlier; actual stored histories have not been inspected.

## Coordinator validation (2026-10-29)

The Release test project compiled with zero warnings and errors. The focused owner selection passed 23 tests after the documented RED runs. The related assertion, letter and methodology selection passed 123 tests. The full non-broker, non-web suite passed 7,859 and failed five: two ArtifactDownloadGrant coverage-map tests and three weekly-schedule original-result tests. The same five failed on hosted develop before wave 2; the full suite is not green. Checked-in producers were reviewed; persisted histories and production behavior remain unqualified.

Whitespace verification passed for this batch’s changed C# files with `dotnet format whitespace Compliance.slnx --verify-no-changes --no-restore --include <files>`.
