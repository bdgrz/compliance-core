# WorkRelationship: an ending is terminal

Date: 2026-11-03. Issue: [#1170](https://github.com/bdgrz/compliance-core/issues/1170).
Status: **partial**. The bounded terminal-status rule is implemented; full target
identity/lifecycle and event migration remain separate work.

## Identity and invariant

The source owner is
[`WorkRelationship`](../../../src/Compliance.Core/Features/Workforce/Aggregates/WorkRelationship.cs).
The existing `IdFor` helper derives its ID from the tenant and normalized
source worker identifier. The constructor, normalization, stream address, person
and original registration comparison are unchanged.

The approved [WorkRelationship rule, WF-01](../contexts/workforce-assurance.md#workrelationship)
requires rehire to use a new work relationship: an ended relationship stays
ended. This owner already receives its complete recorded/revised terms. Keep
this rule here because whether this relationship has ended and its proposed
status can be decided atomically from one stream. No projection or cross-owner
read is needed.

Before the fix, active registration followed by an ended revision could be
revised back to active at the current revision. The previous leaver test only
refused an outdated expected revision, so it did not establish the terminal
lifecycle rule.

## Behavior and event contract

The owner accumulates the fact that it has ended from both
`WorkRelationshipRecorded` and `WorkRelationshipRevised`. A later legacy fact
with active terms cannot erase that fact. After the existing missing-record,
revision and content checks, a new non-ended revision is refused with
`StateConflict`, unchanged logical revision and no additional fact.

Valid ended-to-ended date corrections remain permitted. Original registration
retries still compare against the original terms, so retrying an active
registration after ending returns the same relationship without reactivating
it; changed registration intent still conflicts. Valid pre-ending transitions
and existing validation-error precedence remain supported.

Historical events, including a legacy ended-to-active sequence, remain
replayable. The guard controls new decisions; it does not rewrite historical
terms or projected status. Existing V1 events, discriminators, payloads,
identities, serializers and public request contracts are unchanged. No handler,
reactor, projector or route is added or changed.

## TDD and review evidence

Base: `a9c0be74b0ebf0d3d83b98eda70d0d791595058f`, after the baseline, risk and
validation-performance PRs merged. The preceding full backend suite had 7,982
passing tests.

- Focused behavioral RED: all **12** direct/replayed combinations accepted a
  forbidden revision and returned no failure. They cover pending, active and
  on-leave intent after ending through either recorded or revised terms.
- Whole-owner RED before production changes: **15 failed, 18 passed**, 33 total.
  Three further failures showed that legacy reactivated history also permitted
  a new reactivation.
- Independent review found two preservation gaps in the initial test plan:
  content-validation precedence after ending, and allowed pre-ending lifecycle
  transitions. Three cases were added before accepting GREEN.
- Focused GREEN: all **36 owner cases** passed. All **235 workforce tests**
  passed, including JSON-replayed relationship history, date corrections, original
  registration retries, legacy reactivation history and stale decision handling.
- Release solution build: **0 warnings, 0 errors**. Global whitespace and diff
  whitespace checks passed.
- Full backend qualification: **8,008 passed, 0 failed, 0 skipped**.
  All **6 Node tests** and both Spec commands passed.

Commands:

```sh
# RED first: 12 forbidden current-revision transitions were accepted.
dotnet test test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore --filter 'FullyQualifiedName~ShouldRefuseRevisionWithoutFactGivenEndedWorkRelationship' -m:1 -v:minimal
# Whole-owner RED, then GREEN after the guard and preservation cases.
dotnet test test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore --filter 'FullyQualifiedName~WorkRelationshipTests' -m:1 -v:minimal
dotnet test test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-build --no-restore --filter 'FullyQualifiedName~Bdgrz.Compliance.Tests.Features.Workforce' -m:1 -v:minimal
dotnet build Compliance.slnx -c Release --no-restore -m:1 -v:minimal
dotnet format whitespace Compliance.slnx --verify-no-changes --no-restore
dotnet test Compliance.slnx -c Release --no-build --no-restore --filter 'Category!=BrokerIntegration&Category!=WebIntegration' -m:1 -v:minimal
```

Jev was requested as `jev-latest`; both responses resolved `jev-1.14.0`.
[Plan request](work-relationship-terminal-jev-plan-request.json) and
[response](work-relationship-terminal-jev-plan-response.json): local ownership
0.86, retaining terminal history 0.93, broad planned matrix 0.67. The last
judgment remained provisional rather than being treated as clearance.

The follow-up supplied the concrete tests and serialization helper:
[request](work-relationship-terminal-jev-tests-request.json),
[response](work-relationship-terminal-jev-tests-response.json). Judgments were
0.89 for the refusal matrix, 0.94 for corrections, 0.95 for registration retry,
and 0.90 for legacy/stale handling. Independent review then added the two
preservation areas above. These bounded model judgments are advisory; actual
RED/GREEN results and source checks establish the reported behavior.

Final-head adversarial review, hosted CI and merge evidence are recorded on the
implementation PR linked from #1170. Local results are not hosted or deployment
qualification.

## Remaining work

- Migrate the generic recorded/revised contract to the approved named lifecycle
  facts and reconcile the complete target identity and rehire mapping.
- Complete the remaining worker-type, pre-ending lifecycle, manager/sponsor and
  correction rules; this guard does not claim the entire target write model.
- Sponsor qualification is cross-owner work and needs authoritative admission
  or explicit coordination rather than an advisory active-status read.
- The stale-decision tests prove owner error precedence and refusal after
  rehydration. They do not establish new physical competing-append, broker or
  durable-store qualification. No new revision-command retry or no-op semantics
  are claimed by this terminal-status slice.
