# Risk control treatment: independent review

Date: 2026-11-03. Issue: [#1164](https://github.com/bdgrz/compliance-core/issues/1164).
Status: **partial**. The bounded reviewer-independence change is implemented;
control-retirement coordination and the complete proposed-owner migration remain
follow-ups.

## Identity and invariant

The current source owner is
[`RiskTreatmentAssertion`](../../../src/Compliance.Core/Features/Risks/Aggregates/RiskTreatmentAssertion.cs).
One stream represents one tenant, program, risk and control. Its deterministic
identity remains unchanged.

Keep the proposal and its review together because this owner must decide from
its own pending proposal whether the reviewer is the proposer. A new stream,
projection check or reactor is unnecessary for this rule. The proposal records
`ProposerMemberId`; the review supplies the reviewer's stable member identity.
Names and display values do not determine independence.

The approved rule is in [Risk control treatment](../contexts/risk-and-provider-oversight.md#riskcontroltreatment)
and [CV-06](../contexts/conventions.md#guards-and-reviews): a proposer cannot
review their own assertion unless a separation-of-duties waiver covers them.
The existing HTTP handler already checked this rule. The public owner method
did not, so this is an aggregate-invariant repair rather than evidence of an
HTTP authorization bypass.

## Change and event contract

`Review` checks the hydrated pending proposal before raising its review fact.
Both `accept` and `reject` refuse with `Forbidden` and no new fact when the
reviewer is the proposer and the waiver reference is absent or the empty UUID.
The refusal leaves the proposal, revision and accepted treatment unchanged.
Stale-revision checks retain precedence.

An independent reviewer remains permitted. A proposer naming a nonempty waiver
reference remains permitted; resolving the approved waiver's exact beneficiary,
subject, revision and validity remains the existing handler's responsibility.
A reference alone is not proof of approval.

The existing `RiskControlTreatmentReviewed` fact retains its discriminator,
schema version, accept/reject outcome and waiver reference. No event contract
or stream identity changes. The proposed context's separate accepted/rejected
event names remain a migration concern; this slice preserves the current wire
contract.

## TDD and review evidence

Source baseline: `fcfb053c1670f94ad6860876e7b089baddbe2000`. Before behavioral
tests could run, [#1161](https://github.com/bdgrz/compliance-core/issues/1161)
repaired 67 optional-result expression arms in commit `4978eaea`. These are
explicit nullable defaults preserving the existing no-decision branch.

- Behavioral RED: four cases in
  [`RiskTreatmentAssertionAggregateTests`](../../../test/Compliance.Tests/Features/Risks/Tests/RiskTreatmentAssertionAggregateTests.cs)
  failed because `Review` returned success. Cases cover accept/reject with an
  absent or empty waiver reference, after valid serialized proposal replay.
- Initial GREEN: the same four cases passed after the owner guard was added.
- Preservation coverage adds independent accept/reject, proposer accept/reject
  with a nonempty waiver reference, and stale self-review refusal.
- Independent source review found no new bypass. Existing event/provenance
  checks still bind the reviewer to the operation actor. Waiver validity is an
  explicit upstream evidence boundary.
- Final focused validation: all **9 owner cases** passed as part of **212 risk
  tests**, including existing treatment retry and native-history tests.
- Release solution build: **0 warnings, 0 errors**. Solution whitespace
  verification and `git diff --check` passed.
- Full backend units after the change: **7,940 passed, 25 failed**, out of
  7,965 tests. The failure names match the pre-change baseline exactly: **no new
  failures**. This is not repository-wide green or hosted CI qualification.

Commands:

```sh
# RED: 4 behavioral failures; initial GREEN: the same 4 passed.
dotnet test test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore --filter 'FullyQualifiedName~RiskTreatmentAssertionAggregateTests' -m:1 -v:minimal
# Final focused GREEN: 212 passed, including all 9 owner cases.
dotnet test test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore --filter 'FullyQualifiedName~Bdgrz.Compliance.Tests.Features.Risks' -m:1 -v:minimal
dotnet build Compliance.slnx -c Release --no-restore -m:1 -v:minimal
dotnet format whitespace Compliance.slnx --verify-no-changes --no-restore
dotnet test Compliance.slnx -c Release --no-build --no-restore --filter 'Category!=BrokerIntegration&Category!=WebIntegration' -m:1 -v:minimal
```

Jev review used `jev-1.14.0`: local ownership of the proposer/reviewer comparison
0.90; preserving independent review 0.96; the existing past-tense event name
0.90. These are advisory judgments, not correctness proof. The
[exact request](risk-control-treatment-jev-request.json) and
[complete response](risk-control-treatment-jev-response.json) are retained.

## Remaining work

- Admission or invalidation of pending treatments when a referenced control
  retires is not implemented by this reviewer guard. Retiring accepted
  treatments and requiring risk reassessment must preserve the cross-owner
  invariant rather than rely on an advisory read.
- The complete proposed owner still needs its source-to-target identity,
  command and event mapping reconciled. This bounded change does not mark the
  aggregate or the overall audit complete.
- Actual waiver qualification, authenticated caller authority and physical
  competing appends remain supplied by the existing workflow and Portia. The
  focused owner tests do not establish new host, broker or durable-store
  qualification.
- Baseline full units after the compiler repair: **7,931 passed, 25 failed**.
  The failures preceded this aggregate change: retired design and coverage
  inputs ([#1162](https://github.com/bdgrz/compliance-core/issues/1162)), nine
  persistence-frame cases ([#1163](https://github.com/bdgrz/compliance-core/issues/1163)),
  and five work scheduling/policy queue cases recorded on
  [#1161](https://github.com/bdgrz/compliance-core/issues/1161#issuecomment-6612324394).
  Required CI still invokes retired validation scripts. No checks were removed
  or weakened; merge qualification remains outstanding.
