# ServiceIdentity: retirement freezes changed terms

Date: 2026-11-03. Issue: [#1172](https://github.com/bdgrz/compliance-core/issues/1172).
Status: **partial**. Retirement hardening is implemented; the complete target
owner and named-event migration remain separate work.

## Identity and invariant

The existing
[`ServiceIdentity`](../../../src/Compliance.Core/Features/Workforce/Aggregates/ServiceIdentity.cs)
stream identifies one tenant and ServiceIdentityId. The constructor, stream
address and event identity checks are unchanged.

The [approved ServiceIdentity rule](../contexts/workforce-assurance.md#serviceidentity)
says a retired identity accepts no further changes. Its recorded and revised
terms already carry lifecycle status and canonical content. Keep this rule in
the same owner: it decides whether this identity has retired and whether new
terms differ, atomically from its own history. No cross-owner read is needed.

Before the fix, a current-revision update after retirement could reactivate the
identity or change its owner, purpose or review date. Existing tests retired an
identity but did not attempt a subsequent current-revision change.

## Behavior and event contract

Both observers retain a monotonic fact that the identity has retired, after
their existing tenant, identity, ordering and canonical-content validation.
Historical active terms cannot erase an earlier retirement.

`Revise` keeps the existing missing-record, revision, content-validation, cleanup
and canonical equality gates. An unchanged canonical no-op remains event-free.
After those gates, changed terms are refused with `StateConflict`, unchanged
revision and no additional fact. This covers changes that keep the status
retired as well as reactivation.

Original registration retries still compare against the original terms, even
when their review date has since passed. Changed valid registration intent
still conflicts. Valid pre-retirement disable/enable/owner changes and the first
retirement remain permitted.

Legacy retired-to-active histories remain readable. Their historical terms and
projections are not rewritten: the latch constrains future changed decisions.
Correcting a legacy active projection is separate migration work. A current-term
no-op still adds no fact. Retirement of this record does not change access at
the outside service.

Existing recorded/revised V1 events, discriminators, payloads, serializers and
public request contracts are unchanged. No handler, reactor, projector or route
is added or changed, and no persisted snapshot field or data migration is added.

## TDD and review evidence

Base: `69043e8a715067ea38af90ceea102b7b6fd6794d`, after the WorkRelationship
terminal-state slice merged. The preceding full backend suite had 8,008 passing
tests.

- Focused behavioral RED: all **8** direct/replayed reactivation cases accepted
  the forbidden update and returned no failure.
- Whole-owner RED before production changes: **23 failed, 38 passed**, 61 total.
  Additional failures covered changed owner/purpose/review content and legacy
  reactivation histories.
- Focused GREEN: all **61 owner cases** passed. They cover both retirement event
  origins, JSON replay, canonical no-ops, original registration retries after
  expiry, changed registration intent, legacy history, error precedence and
  allowed pre-retirement lifecycle changes.
- Independent source/test review found no further required preservation case.
- All **269 workforce tests** passed. Release solution build had **0 warnings,
  0 errors**; global whitespace, diff whitespace, all **6 Node tests** and both
  Spec commands passed. Full backend qualification: **8,042 passed, 0 failed,
  0 skipped**.

Commands:

```sh
# RED first: eight current-revision reactivations were accepted.
dotnet test test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore --filter 'FullyQualifiedName~ShouldRefuseReactivationWithoutFactGivenRetiredServiceIdentity' -m:1 -v:minimal
# Whole-owner RED, then GREEN with the guard.
dotnet test test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore --filter 'FullyQualifiedName~ServiceIdentityTests' -m:1 -v:minimal
dotnet test test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-build --no-restore --filter 'FullyQualifiedName~Bdgrz.Compliance.Tests.Features.Workforce' -m:1 -v:minimal
dotnet build Compliance.slnx -c Release --no-restore -m:1 -v:minimal
dotnet format whitespace Compliance.slnx --verify-no-changes --no-restore
dotnet test Compliance.slnx -c Release --no-build --no-restore --filter 'Category!=BrokerIntegration&Category!=WebIntegration' -m:1 -v:minimal
```

Jev was requested as `jev-latest`; both responses resolved `jev-1.14.0`.
[Plan request](service-identity-retirement-jev-plan-request.json) and
[response](service-identity-retirement-jev-plan-response.json): local ownership
0.96, preserving unchanged no-ops 0.95, retaining historical retirement 0.94,
and bounded test plan 0.93.

The concrete-test review included the actual tests, current owner and JSON
serialization helper: [request](service-identity-retirement-jev-tests-request.json),
[response](service-identity-retirement-jev-tests-response.json). Judgments were
0.87 for the retirement matrix, 0.85 for no-op/retry preservation, 0.91 for
legacy/error precedence and 0.83 for the allowed lifecycle. These are advisory;
source review and observed RED/GREEN results establish the reported behavior.

Final-head review, hosted CI and merge evidence are recorded on the
implementation PR linked from #1172. Local results do not establish hosted,
broker or deployment qualification.

## Remaining work

- Reconcile the generic revised contract with the target's named lifecycle,
  owner/purpose and attributable review facts.
- Complete the shared-account kind, accountable person and justification model
  and authoritative owner qualification without advisory projection checks.
- Legacy active projections need their own explicit correction/migration plan;
  this guard preserves historical readability rather than rewriting facts.
- Current revision/error tests do not establish new physical competing-append,
  host, broker or durable-store qualification. Retiring a record is not provider
  access revocation.
