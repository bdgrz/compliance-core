# Program target-date ordering

Specification: [Program and scope](../contexts/program-and-scope.md), Program
invariant: readiness ≤ Type I as-of ≤ Type II start ≤ Type II end for dates that
are present. Missing dates remain unknown. Tracking: [#1176](https://github.com/bdgrz/compliance-core/issues/1176).

Status: **paused checkpoint; focused acceptance passed, full gate interrupted**. This is an invariant
repair in the existing ProgramRules path, not completion of the canonical Program
commands, criteria-at-creation or stage lifecycle.

## Change and evidence

The old validator compared the three adjacent pairs only. A missing intermediate
date let a reversed nonadjacent pair through. Three additional comparisons cover
readiness/Type II start, readiness/Type II end, and Type I as-of/Type II end. Existing
validation order/messages remain first; equality and nulls keep their meaning.
No execution, persistence, concurrency, receipt or public wire code changes.

The preparation branch authored 35 cases without running .NET. Coordinator
compilation first corrected a missing namespace import and two style diagnostics;
those were not behavioral RED. The clean focused run then passed 29 and failed
six at the intended refusal assertions: each of the three missing pairs on
creation and revision. After the repair, 65 focused/native/history/HTTP cases
passed, including all 35 new cases. Strict Release build had zero warnings/errors;
whole-solution whitespace verification passed. The full run was canceled when the
user paused implementation; it is not a completed full gate.

Tests cover all six pair inversions on creation/revision, all sixteen present/null
patterns, all six equal-date pairs and all-four equality. Refusal leaves identity,
revision, exact prior plan and pending changes unchanged. Valid revisions retain
unknown dates, attribution and time. Existing native tests retain current
authorization and original response bytes across consumed revisions.

Independent source review found no blocker for valid ordered, nullable or equal
plans and their supported retry paths. Live Jev requested `jev-latest` and resolved
to `jev-1.14.0`: adverse signals were .26 for an uncovered pair, .13 for equal/null
regression, .09 for mutation on refusal and .05 for execution/receipt changes.
The test-proof question remained inconclusive at .49; the finite six-pair source
map and executed RED/GREEN checks are separate evidence, not model clearance.
Requests/responses are preserved locally in
`/tmp/compliance-program-dates-assembled-jev-*` and the preparation review artifacts.

## Accepted historical-data qualification

The validator is also used by ProgramReader.ReadContentAsync and RulesAsync.
Previously stored reversed-across-null content is therefore rejected during
historical reads/reconstruction. Fresh projection replay can stop at an old invalid
fact even if a later valid correction exists. Native correction accepts a complete
new valid plan without decoding the older body; accepted receipt recovery remains
frozen. These path observations are source evidence, not a production data audit.

The user explicitly waived backward compatibility and rollback. Invalid historical
plans are not grandfathered. Fresh data or explicit stream conversion may be
needed; no conversion or deployment is claimed. This qualification does not waive
correct handling of valid historical plans.

## Remaining acceptance

The assembled full backend gate, final committed-head review, exact-head CI, merge
and post-merge readback remain separate delivery gates. Named target changes,
criteria edition at creation, boundary workflows and the rest of the approved
Program/scope context remain partial.
