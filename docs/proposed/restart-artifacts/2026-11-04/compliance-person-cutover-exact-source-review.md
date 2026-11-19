# Person cutover exact source review

Baseline: merged baffd1badc29817d4759e3bc2d03cbc4e79617f9.
Observed root HEAD: 65fdd65e674a1329c02d4c0719836a2f22601420, with the bounded 11-file Person cutover dirty.
Review is of the file contents identified by SHA-256 below, not an immutable final PR head.
No repository files edited. No build/test/format execution. Root supplied the observed pre-patch behavioral RED evidence (7/7 retired-writer tests and 1/1 removed-route test); this review did not independently execute it.
No blockers found in the reviewed source. Final committed-head readback and coordinator validation remain required.

Scope and evidence

1. Retired internal request registration and no handler I/O.
- Core/Features/Workforce/Handlers/RevisePersonHandler.cs:7-12 has no constructor dependency and unconditionally returns nontransient Conflict with the fixed retirement message. It cannot hydrate an owner, write immutable content, replay a receipt or append.
- Core/Features/Workforce/Services/PersonOperations.cs no longer exposes ReviseAsync.
- Core/ComplianceServiceCollectionExtensions.cs:1463 retains AddRequestHandler<RevisePersonHandler>, so a registered internal request receives the intended refusal rather than becoming an unhandled contract.
- Core/ComplianceCoreJsonContext.cs:2080 retains RevisePerson; :2089 retains PersonDetailsRevised. Historical decoding/source verification remains available.
- The no-I/O claim is specifically about the retired handler. A request sent through the bus can still invoke the existing authorization pipeline, which may read membership/permissions; this is expected and is not a bypass.
- PersonLegacyMutationTests.cs:33-79 asserts the same refusal for changed, unchanged, stale, missing and invalid inputs, unchanged facts, and zero calls to throwing aggregate/content persistence doubles.
- The constant direct-handler response reveals no tenant/person existence or private values.

2. Public HTTP and MCP removal.
- App/Program.cs removes AddMcpTool<RevisePerson> and the generic PUT /people/{person_id}/revisions/{command_id} mapping.
- App/Features/Workforce/PersonHttp.cs removes ReviseBody and BindReviseAsync.
- App/ComplianceJsonContext.cs removes the now-unused ReviseBody metadata.
- App/Hosting/NativeResponses/NativeFirstResponseContracts.cs removes RevisePerson from public native response selection.
- rg over src/Compliance.App found no remaining RevisePerson, PersonHttp.Revise or BindReviseAsync reference. No assembly-wide automatic MCP tool discovery was found in App/Core source; tools are explicitly registered in Program.
- ChangePersonDetails remains an explicitly registered MCP tool and mapped POST /people/{person_id}/detail-changes/{command_id}.
- PersonNativeHttpTests.cs:129-145 asserts old route returns 404/405 and leaves business/physical versions at 1.
- MCP absence is verified from registration source in this review, not a freshly executed tools/list call.

3. Authorization and tenant scope.
- ChangePersonDetails and retained RevisePerson have the same IWorkforceRequest and IClientManagementMutationRequest marker boundaries.
- The named handler/operation path, WorkforceAuthorizer and client-management authorization are unchanged by this patch.
- App/Program.cs:2028-2037 retains ApiUser authorization on named changes.
- PersonHttp.BindChangeDetailsAsync:35-47 still gets tenant/person/command from canonical route identities. Identity:61-67 rejects empty/malformed route identities and conflicting body identities.
- PersonOperations still derives actor server-side from context and addresses new Person(request.TenantId, personId).
- No changed auth path, cross-tenant address substitution or client-supplied actor was introduced by this cutover.

4. Privacy, correlation and historical receipts.
- Person aggregate, rules, PersonReader/PersonHistory, PersonContent, CassiePersonDirectory and WorkforceAuthorizer are unchanged relative to baffd1ba.
- New PersonLegacyMutationTests.cs:82-163 directly seeds a published V2 PersonDetailsRevised fact; it does not use the retired handler to create history.
- The test serializes/deserializes it with existing metadata and reads the exact historical value through PersonReader.
- After a named change it asserts private contact/correlation preserved, named changed-field flags and authenticated actor correct, old selected receipt retains actor/time/operation, and first-response/result bytes are unchanged.
- It additionally asserts the public list has null PersonalContact and RestrictedFieldsRedacted=true, then old command refusal adds no fact.
- Current public named writes retain the existing privacy/content verification pipeline. Keeping historical command/event metadata is not re-exposing the writer.

5. Retry and mutation test adaptations.
- PersonNativeHttpTests now drives POST named detail changes while correlation stays PUT. The test still compares original/replayed HTTP response and verifies current owner did not rewind after a later mutation.
- PersonNativeIdentityTests uses ChangePersonDetails JSON metadata and retains consumed-revision exact retry/fact-count assertions.
- PersonNativeHistoryTests switches normal producing commands to named changes, including forged payload/aggregate version/realm/metadata and preparation-race cases. Integrity/refusal assertions remain; they are not changed to success or removed.
- The existing raw V1 history test still seeds actual legacy PersonRecorded/PersonMembershipCorrelated events, then checks named change preserves contact/correlation and emits truthful PersonDetailChanges.
- There are no remaining operational test constructions of RevisePerson outside the dedicated retirement/historical fixture file.

6. PersonSeeds and wider fixtures.
- PersonSeeds.cs:111-116 converts a pure-rule PersonRevised event to command kind ChangePersonDetails and runs rules.ChangeDetails with the full DisplayName/WorkEmail/PersonalContact payload.
- The same per-owner native content/metadata/provenance/fact preparation remains.
- PersonRules.ChangeDetails delegates actual changes to Revise after rejecting a canonical no-change. Revision increment and correlation preservation are otherwise unchanged.
- This is the intended default for ordinary fixtures. Raw legacy event tests and the dedicated V2 historical fixture remain distinct and are not silently rewritten.
- A fixture that intentionally attempted a no-change PersonRevised through PersonSeeds would now fail its setup instead of fabricating a new named fact; no such concrete affected call was identified in the bounded review. Coordinator full tests are the executable check for fixture consumers.
- Seed provenance is test-generated and not a substitute for transport replay tests; the preserved HTTP/native tests exercise real request intent.

Bounded limitations

- No claim of GREEN, CI pass or merge readiness is made by this source review.
- The new retirement cases call the internal handler directly; they do not claim the entire authorized request bus performs zero reads.
- No broad authorization, domain redesign or generic runtime refactor is proposed.
- Await final committed-head recheck; compare the following hashes to identify any intervening source changes.

Reviewed file SHA-256

15c385c5717324aaf87dc8c6965c3d3534641bb75fda3199a3b817450f69b3cc  src/Compliance.App/ComplianceJsonContext.cs
d1a2a703524b5d1e2915d682e340aad6134a3db30e120f3f3700f9d8e1e90137  src/Compliance.App/Features/Workforce/PersonHttp.cs
8aaa0990ea27a4676981c32253f7ecb4da8d763acb14934a3b3ca4909e6471e3  src/Compliance.App/Hosting/NativeResponses/NativeFirstResponseContracts.cs
431ac3a8cf8dc41216249f68f5f0c7c8d2b63c5f4ec916353fcadfe85da30cfd  src/Compliance.App/Program.cs
d1158e8293119d5555eb1e34fcf5e058e403edef1bd4fedc5e31405f76c4e048  src/Compliance.Core/Features/Workforce/Handlers/RevisePersonHandler.cs
70ecc8b8f3e96c6be5d1afac2594c0c5d6c21b23a823da0a3a0f51d3a1f877bf  src/Compliance.Core/Features/Workforce/Services/PersonOperations.cs
72ef7c605f4089b99b3719db5b90d16d2575727bb5e97a1881cf778d3b8bf719  test/Compliance.Tests/Features/Workforce/Tests/PersonNativeHistoryTests.cs
19d150458f21573727bdbffb6be7d539e90c1c8da70f4fcf37743e7dab1079f8  test/Compliance.Tests/Features/Workforce/Tests/PersonNativeHttpTests.cs
7c35da83f6f50b3354029c613dd0c8dd06a13c2e182e395ea80259272a560c53  test/Compliance.Tests/Features/Workforce/Tests/PersonNativeIdentityTests.cs
0b8ec916ca54d8ff70bd81a6258b944a05cc2d85d79ebfbc53359f3a62e08392  test/Compliance.Tests/Testing/PersonSeeds.cs
5d5d3deb3e548b50e9963579f9a7ad6e9df1c31b3f0dd3dcf1a9a66165348cd6  test/Compliance.Tests/Features/Workforce/Tests/PersonLegacyMutationTests.cs

Refinement review: historical V2 tamper and MCP catalog (source only)

No blocker found in the three additional test diffs.

- PersonLegacyMutationTests.cs:166-179 mutates the historical PersonDetailsRevised, not the later PersonDetailsChanged. The own-stream sequence is registration, correlation, direct V2 historical revision, then named change; corrupted[2] targets the third fact. The first variant changes only Change.ContentId while retaining metadata; the second changes only CausationId. PersonHistory.ReadAsync receives current.Owner from the real post-change read, so its full actual owner replay cut must reject the corrupted middle prefix. The source record wrapper preserves stream and resource offsets and does not mutate original fact instances; the concluding assertion rechecks real persisted facts.
- PersonNativeHttpTests.cs:149-180 discovers tools and performs/replays the named MCP mutation. Old-tool absence cannot pass through empty discovery because Assert.Single requires the named tool and its Idempotent flag. Both calls require ExpectSuccess. Assert.IsType<JsonElement>, GetProperty("result"), and EnumerateObject prevent null/missing/scalar results from passing. Matching raw result object plus physical/business owner version 2 proves an exact retry without a second append.
- RbacMcpScenarioTests.cs diff replaces the three retired writer names and adds the 24 currently registered tool names from earlier batches. It retains the explicit ExpectExactly inventory assertion; metadata, personal-transport exclusions and unauthorized-call assertions are unchanged. The added names correspond to Program.cs registrations and their command discriminators; this is fixture reconciliation, not a new public registration or reduced assertion. Coordinator reports one prior catalog-fixture failure and is rebuilding/rerunning; no new executable result is claimed by this review.

Updated reviewed test SHA-256

a9f65464b116a3bd1276b80714b95203a167470e0ed689ebcf2bb303bf21bdaa  test/Compliance.Tests/Features/Workforce/Tests/PersonLegacyMutationTests.cs
aaca6a19d329b7e6c888b6ba8daa148c0fa57e25a5e08a6908ff1bd3c4807b05  test/Compliance.Tests/Features/Workforce/Tests/PersonNativeHttpTests.cs
8a62f0d64ac32a0cb489ddce808196d43b0341426f4587eb286e78499de5b22a  test/Compliance.Tests/Features/AccessControl/Tests/RbacMcpScenarioTests.cs


Final frozen-input source review (2026-11-04)

Result: no source blocker found in the bounded Person cutover. This review covers the final 14 C#/runtime-fixture files from `/tmp/compliance-person-cutover-accepted-source-hashes.json`, including AttestClientRbacManagementWallTests. All 14 current SHA-256 values match the coordinator frozen manifest. Reviewed working-tree source is based on merged baffd1ba; current root HEAD before committing is `65fdd65e674a1329c02d4c0719836a2f22601420`. No source edits, builds, tests or formatting were performed by this reviewer. This is not a final committed-head or CI acceptance claim.

Authorization / tenant isolation / public publication

- The production diff is limited to deleting RevisePerson HTTP/MCP publication, its binder/body metadata/native first-response selection, and the obsolete PersonOperations.ReviseAsync execution; the retained typed handler now has no dependencies and returns one nontransient Conflict.
- ChangePersonDetailsHandler, PersonOperations.ChangeDetailsAsync and ExecuteAsync still derive server-side WorkforceActor, tenant-scoped Person stream and authored intent digest, then use the existing NativeOperationExecutor. PersonOperations.cs:101-130 and PersonHttp.cs:35-47,61-67 retain identity checks, canonical route tuple and conflicting-body refusal. Public named POST retains ApiUser (Program.cs:2028 onward).
- The Person aggregate/rules/history/reader/content, CassiePersonDirectory, WorkforceAuthorizer and AccessControl authorizers have no diff from merged baffd1ba. No new actor override, authorization bypass, cross-tenant stream address, projection authority substitution or private-field exposure is introduced.
- Retained Common RevisePerson ICallable/discriminator and Core handler/JSON registration are intentional historical/refusal contracts; Program contains no AddMcpTool<RevisePerson> or old revision route and App no longer describes the removed body. The new public route test checks 404/405 plus unchanged business/physical version1, rather than treating any failure as route absence. The MCP test requires exactly the named idempotent tool, old tool absent, two successful actual calls, typed non-null result objects, exact Unit result replay and version2 after both calls.

Privacy / historical V1 and V2 / receipts and retry

- PersonLegacyMutationTests.cs:98-118 directly constructs and JSON-roundtrips published V2 PersonDetailsRevised with original metadata; it does not call the now-retired writer to fabricate current history. :141-165 asserts named continuation preserves private contact/correlation and truthful changed flags, historical actor/time/provenance, exact stored first-response/result bytes, typed original Unit200 receipt and redacted public list. The subsequent old request uniformly refuses without adding a fact, intentionally distinct from reading/replaying its stored historical receipt.
- :166-179 targets fact index2 (registration, correlation, historical V2 revision, later named change), mutating payloadContentId or causation while preserving other metadata, and checks PersonHistory against the actual current owner's committed replay cut. It asserts original persisted facts remain unchanged after both negative cases. These are meaningful middle-prefix integrity checks, not an artificial older cut that skips the tampered fact.
- PersonNativeHistoryTests.cs:331-369 still seeds real V1 PersonRecorded/PersonMembershipCorrelated history directly and asserts named continuation emits PersonDetailsChanged with only DisplayName changed while retaining contact/correlation. Existing history-integrity/preparation-race and changed-intent/no-rewind assertions remain under named producing commands. PersonNativeIdentityTests.cs:101 onward still tests consumed expected revision replay using the correct ChangePersonDetails JSON contract. PersonNativeHttpTests.cs:93-124 uses the real POST named route and retains original response bytes/version3 after later change and exact retry.
- PersonSeeds.cs:111-116 now maps ordinary pure-rule PersonRevised setup to ChangePersonDetails and its real no-change refusal. It does not alter historical codecs/owners or rewrite persisted data; dedicated raw V1 and V2 fixtures remain explicit.

Final fixture repair

- AttestClientRbacManagementWallTests.cs:210-235 adds only a last-registered fixture-local singleton IPersonMemberDisplayReader returning null. Production AddCompliance registration and production native Person reader are unchanged. This fixture already owns in-memory event/KV sources, synthetic membership, active-tenant and recording-permission authorities and does not seed a Person projection.
- GetTenantMemberHandler.cs:25-32 resolves membership before optional MemberPresentationEnrichment. MemberPresentationEnrichment.cs:105-112 consults the reader only for a name and falls back to authenticated profile, verified email or user ID; its return cannot confer membership, permissions, grant eligibility or Attest independence. The replacement therefore controls an absent presentation source, rather than bypassing the subject under test.
- Existing actual/revoked Attest history HTTP/MCP denial, ordinary grant denial, mismatched returned membership tenant/user denial, trusted system bootstrap/revocation policy, fixed-role-catalog refusal, retained active team/committed position and authorized GetTenantMember-read assertions are unchanged. The formerly failing read assertion still requires read.IsSuccess and actual UserId (:93-101); there is no catch-and-ignore or weakened assertion.
- Coordinator reports all 13 fixture cases passed with Cassie configured to unreachable port1. This reviewer verified the source basis for that isolation but did not execute that result. First full run reported 8706 pass/1 fixture failure; the fresh full acceptance run is currently pending. Do not promote either source review or 13 focused cases to full-suite acceptance.

Catalog and progress honesty

- The aggregate coverage fixture now describes PersonDetailsRevised as historical V2 replay/receipt decoding and RevisePerson as registered permanent refusal with no owner/content writer; helperServices is correctly empty. Current named PersonDetailsChanged still has the pre-existing syntactic factory-detection limitation documented under #1185, not a claim of an absent runtime publisher. Historical V1 reducer-only classification remains separate from native current facts.
- RbacMcpScenarioTests retains exact inventory equality and existing permission/metadata/personal-transport exclusions. Its list reconciliation removes three retired public names and adds the currently registered tools from prior merged slices; it does not publish them. The Person-specific non-null named-call assertions independently prevent an empty or missing tool/result from passing.
- docs/proposed/reviews/person-named-writer-cutover.md distinguishes observed behavioral RED, focused selection/catalog checks, the initial full-suite fixture failure, repaired fixture-focused evidence, pending fresh full acceptance and pending committed-head CI/merge. WF-05 multi-member/reverse uniqueness and WF-08 source ordering/application remain explicitly incomplete. No whole-domain or production-readiness claim was identified in the bounded new cutover documentation.

Remaining verification

- Coordinator must finish the fresh full gate, commit the candidate, and verify the committed tree contains these exact frozen 14 inputs. Then perform exact committed-head review/readback and required final-head CI. No acceptance result is inferred while that run is pending.

Final frozen SHA-256 manifest (14 inputs)

15c385c5717324aaf87dc8c6965c3d3534641bb75fda3199a3b817450f69b3cc  src/Compliance.App/ComplianceJsonContext.cs
d1a2a703524b5d1e2915d682e340aad6134a3db30e120f3f3700f9d8e1e90137  src/Compliance.App/Features/Workforce/PersonHttp.cs
8aaa0990ea27a4676981c32253f7ecb4da8d763acb14934a3b3ca4909e6471e3  src/Compliance.App/Hosting/NativeResponses/NativeFirstResponseContracts.cs
431ac3a8cf8dc41216249f68f5f0c7c8d2b63c5f4ec916353fcadfe85da30cfd  src/Compliance.App/Program.cs
d1158e8293119d5555eb1e34fcf5e058e403edef1bd4fedc5e31405f76c4e048  src/Compliance.Core/Features/Workforce/Handlers/RevisePersonHandler.cs
70ecc8b8f3e96c6be5d1afac2594c0c5d6c21b23a823da0a3a0f51d3a1f877bf  src/Compliance.Core/Features/Workforce/Services/PersonOperations.cs
254e1702eaafc18d6b843c6247e7c683d04d6dc5bfd3ecee0c83ce8fc0153376  test/Compliance.Tests/Features/AccessControl/Tests/AttestClientRbacManagementWallTests.cs
8a62f0d64ac32a0cb489ddce808196d43b0341426f4587eb286e78499de5b22a  test/Compliance.Tests/Features/AccessControl/Tests/RbacMcpScenarioTests.cs
a9f65464b116a3bd1276b80714b95203a167470e0ed689ebcf2bb303bf21bdaa  test/Compliance.Tests/Features/Workforce/Tests/PersonLegacyMutationTests.cs
72ef7c605f4089b99b3719db5b90d16d2575727bb5e97a1881cf778d3b8bf719  test/Compliance.Tests/Features/Workforce/Tests/PersonNativeHistoryTests.cs
aaca6a19d329b7e6c888b6ba8daa148c0fa57e25a5e08a6908ff1bd3c4807b05  test/Compliance.Tests/Features/Workforce/Tests/PersonNativeHttpTests.cs
7c35da83f6f50b3354029c613dd0c8dd06a13c2e182e395ea80259272a560c53  test/Compliance.Tests/Features/Workforce/Tests/PersonNativeIdentityTests.cs
4276c177cdd293323b79f205e02f3313cc530f6ce07c4efea950f0aacb9abbc9  test/Compliance.Tests/Fixtures/aggregate-command-coverage.json
0b8ec916ca54d8ff70bd81a6258b944a05cc2d85d79ebfbc53359f3a62e08392  test/Compliance.Tests/Testing/PersonSeeds.cs


Immutable PR #1187 exact-head review (2026-11-04)

Result: CLEAR, no bounded exact-head source blockers found.

- Reviewed immutable commit `a3e501f39a8d998692ba770793e87cdb1fc71dcd` relative to merged baseline `baffd1ba`. Evidence came from git show of that SHA, independent of the currently checked-out branch.
- The commit range contains exactly two commits: `4fac5545` fixture display-name isolation and `a3e501f3` Person writer retirement. Whole-tree git diff between integration backup `1b82ee27` and PR head is empty.
- All 14 immutable committed C#/runtime-fixture file SHA-256 values match `/tmp/compliance-person-cutover-accepted-source-hashes.json` and the final frozen-input review above. Thus the source review of authorization/tenant/privacy/history/receipt/retry/public-surface/fixture assertions applies to the exact committed code, not only the former working tree. No runtime delta from the reviewed frozen source was found.
- Coordinator reports the fresh native full gate completed 8707/8707 with zero failures/skips in 12m47s and all 14 frozen input hashes unchanged. This reviewer did not rerun .NET checks. The immutable tree match binds those reported local checks to this PR's runtime inputs.
- Final committed audit wording correctly says locally qualified/assembled acceptance, preserving the initial 8706-pass/one-fixture-failure history and the repaired 13-case isolated fixture evidence separately. PR1186 final-head/post-merge 8700-unit evidence is explicitly its earlier merged revision; it is not offered as hosted qualification of this Person head.
- The Person review file :6-8 and :90-96 leaves committed-head review, exact-head CI, squash merge and post-merge readback as distinct gates tracked on #1176. delivery-reset.md:37-42 and implementation-progress.md's final section identify 8707 as local fresh acceptance and do not claim Person is merged or final-head hosted CI passed. workforce-implementation/requirement coverage retain member-cardinality/reverse-uniqueness/HRIS application gaps, and read-model migration retains 9/82 storage scope plus operational qualifications. No defect-free, deployment, client or whole-domain completion claim is introduced.
- The old preserved-work branch label and source-inventory factory limitation are historical navigation/status details; the explicit local-versus-hosted gate statements remain accurate. No extra implementation or audit rewrite is required to clear this bounded review.

Exact-head source review is complete. Required hosted CI, merge and post-merge readback remain coordinator delivery work; no success for those phases is inferred here. No repository edits, builds, tests or formatting were performed.
