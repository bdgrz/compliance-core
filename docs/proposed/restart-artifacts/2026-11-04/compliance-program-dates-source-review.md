# Program present-target ordering source review

Result: **CLEAR, no source blockers within the authorized invariant repair.** Reviewed root working tree at `1deb9cf7121f9d49ab1ee0e60bda4ea52f223455`: only ProgramRules.cs (six added lines / three comparisons) and new ProgramTargetDateTests.cs. No edits, .NET execution or formatting by reviewer. Coordinator reports actual behavioral RED: 35 total / 29 pass / 6 fail (nonadjacent pairs on create and revise); GREEN selection65/65 plus strict build zero warnings/errors. These executions were supplied by coordinator, not rerun here. This does not qualify canonical Program workflow, criteria admission, named stages or identity changes.

## Invariant and complete comparison coverage

Locked `docs/proposed/contexts/program-and-scope.md:45-50`: present readiness <= TypeI-as-of <= TypeII-start <= TypeII-end; missing values stay unknown. Four positions require six ordered pairs. `ProgramRules.cs:161-172` now checks all six exactly:

| Pair | Guard | Test refusal cases |
| --- | --- | --- |
| readiness / TypeI | existing :161 | target tests InlineData(0,1) on create :16 and revise :43 |
| readiness / TypeII start | added :167 | (0,2) :17/:44, intermediate null |
| readiness / TypeII end | added :169 | (0,3) :18/:45, both intermediates null |
| TypeI / TypeII start | existing :165 | (1,2) :19/:46 |
| TypeI / TypeII end | added :171 | (1,3) :20/:47, intermediate null |
| TypeII start / TypeII end | existing :163 | (2,3) :21/:48 |

The tests deliberately populate only the compared pair, so existing transitive adjacent comparisons cannot conceal a missing nonadjacent guard. Every reversal is later calendar date at the earlier stage and earlier calendar date at the later stage. This is behavioral coverage of missing-data ordering, not a mirror that merely asserts the number of comparisons.

Nullable DateOnly relational `>` yields false when either operand is null. All checks use strict `>`, so equality remains accepted. Unknown-mask tests :73-111 cover all16 subsets, including no targets, each single target, every two/three-target combination and all four, on both creation and revision. They compare exact ProgramPlan values in both appended histories, proving nulls are preserved instead of filled. Equality tests :113-139 cover all six isolated equal pairs plus all four equal, on create/revise. Those cases cover equality across every hole and the simultaneous chain; no requirement for strictly increasing dates was introduced.

## Refusal, mutation and validation precedence

- Creation path `ProgramRules.cs:95-109` calls Validate before Add. Tests :35-39 require InvalidContent, uncreated owner, revision0 and empty applied/pending changes. No local fact is appended on refusal.
- Revision path :112-125 keeps missing owner then stale revision before content validation, and calls Add only after success. Tests :66-70 require original revision1, unchanged full applied-change sequence, original Plan retained and no pending change. No replacement or silent correction occurs.
- New comparisons are appended after the three original comparisons. Name required remains first (:157-158), missing Plan second (:159-160), then existing error ordering and messages unchanged (:161-166). Existing stale/missing/existing-creation retry precedence is unchanged. Only plans previously missed by the old comparisons receive one of the three new messages.
- Positive revision tests deliberately change the program name so a real attributed revision is recorded; :109-110 checks original actor display and supplied timestamp. Tests do not introduce a no-op receipt or infer transport/durability proof from the plain rule object.

## Current native writer, history and receipt effects

- Both real current CreateProgram/ReviseProgram handlers still call ProgramOperations; its only applicable content validation is the same ProgramRules.Validate at `ProgramOperations.cs:125-130`. Validation occurs before NativeContent.Write/Freeze and aggregate append, so this bounded repair reaches the native writers and refuses invalid NEW intent without creating authored content or a business fact.
- Existing compact-owner scope/revision preparation stays ahead of plan validation (`ProgramOperations.cs:107-124`). NativePreparedCut check and owner Create/Revise selection remain unchanged (:137-149). `ComplianceProgram.cs:105-117` checks native cut and original revision before appending, independent of authored historical body reads.
- Normal correction writes provide a complete new valid ProgramContent; ProgramOperations does not call ProgramReader.RulesAsync or decode the old authored plan while preparing it. A valid correction can therefore append through current owner/native verification even if an older plan is malformed by the strengthened rule. This does not promise that an already stalled projection/rebuild will advance past that old fact.
- Accepted-operation replay still uses NativeOperationExecutor's acceptance branch and stored FirstResponse; ProgramOperations.cs:152-167 returns the original schema/status/body and never revalidates the request plan on that replay path. Exact actor/intent/expected-revision identity conflict semantics remain unchanged. Existing `ProgramNativeHistoryTests.ShouldRetainOriginalResponseGivenCreationReplayAfterRevisionAndUnavailableAuthoredBody` :151-174 asserts original response, no authored reads and two facts; `ProgramNativeHttpTests.ShouldReplayOriginalCreationAndUnitBytesGivenLaterRevisionAndCurrentAuthorization` :98 covers actual original HTTP bytes/current reauthorization. Neither file nor native executor, history metadata verification, actor derivation, intent digest, wire contracts or first-response schemas changed.
- Valid ordered/null/equal ProgramContent continues to pass the same shared validation used by `ProgramReader.ReadContentAsync:99-107`. RulesAsync replays valid content via Create/Revise (:63-73), with unchanged lineage/source/metadata verification in ProgramHistory.cs:54-87. No source candidate was found that regresses a previously ordered supported plan or its accepted receipt.

## Explicit breaking malformed-history qualification

An old accepted plan such as [2028-04-01, null, 2028-03-01, null] passed the prior three nullable adjacent comparisons but violates the approved invariant. The stronger shared Validate now rejects that content through ProgramReader.ReadContentAsync:105; RulesAsync also reconstructs using Create/Revise and will refuse it. FitzProgramDirectory.ApplyAsync:61,81,108 calls ReadContentAsync, so a fresh projection/rebuild encounters that historical invalid fact and stops, including if a later valid correction exists. An already-materialized old view is not automatically repaired by this code.

The coordinator explicitly confirms the user's compatibility/rollback waiver: this is accepted breaking malformed-history behavior, not a blocker for this repair. Such data may require fresh data or an explicit conversion; neither conversion nor grandfathering nor a migration/retry framework is implemented or claimed. Native compact history/accepted receipt readback above is separate from semantic authored-content reconstruction. No claim that valid correction alone repairs the old replay prefix is made.

## Reviewed file hashes

c49c33594b34c96d8171c7f481d700a38a0bd1f944c36706034c6a345559fec6  src/Compliance.Core/Features/Programs/Rules/ProgramRules.cs
a1532c67864613e11503892cd63276caf5f0f99114decd2ef35997543e478f7e  test/Compliance.Tests/Features/Programs/Tests/ProgramTargetDateTests.cs
