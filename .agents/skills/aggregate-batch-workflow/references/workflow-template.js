// Workflow script template for the aggregate-batch-workflow skill.
// Invoke with the Workflow tool: pass this file's text as `script`, and the batch as `args`:
//   args = [{ name, src, tests: [..], review, hint }, ...]
//   name   aggregate name, e.g. "Company"
//   src    path to the aggregate source file
//   tests  the aggregate's own test file path(s)
//   review repo-relative path of the aggregate review file (Jev artifacts go in an implementation-jev/ folder beside it)
//   hint   known-failing tests, uncommitted prior work, or other context for the agents
//
// Coordinator notes for the commit step (not part of the script):
//   - Stage explicit paths per aggregate: src file, its tests, its review file, its
//     implementation-jev/<name>-test-impl-review-*-{request,response}.json, and its tracker row.
//   - The tracker (aggregate-backlog.md) is one shared file. Build each commit's version
//     as: HEAD tracker with rows replaced by the final rows for the aggregates committed so far.
//   - Commit messages follow AGENTS.md (feat(scope): summary (#issue)).

export const meta = {
  name: 'aggregate-batch',
  description: 'Implement and Jev-review a batch of aggregates in parallel without building; build and test happen afterward',
  phases: [
    { title: 'Implement', detail: 'qualify each aggregate, fix gaps, write tests (no build/test)' },
    { title: 'Jev review', detail: 'adversarial review of impl+tests with Jev, fix findings' },
  ],
}

const ITEMS = args

const RULES = `
HARD RULES for this session (a parallel multi-agent run; other agents are editing OTHER aggregates at the same time):
- Do NOT run dotnet build, dotnet test, dotnet format, dotnet restore, or any command that compiles or runs tests. The user will build and test once, after review. Write code carefully so it compiles on the first try.
- Do NOT commit, stage, push, or touch git state. Do not run git checkout/stash/reset.
- Edit ONLY: your aggregate's source file, its own test file(s) listed below, and your own review file (the review path below). You may add NEW files that belong only to your aggregate (new test classes, new value types used only by it).
- Do NOT edit shared or other owners' files: aggregate-backlog.md (the tracker), tools/domain-events/** schemas and generated code, ComplianceCoreJsonContext.cs, event catalogs under docs/product/definitive-aggregate-choreography, Support/Fakes/shared test helpers, handlers, reactors, projectors, other aggregates. If your aggregate needs an event added/renamed/removed or a payload change, DO NOT apply it; report it in eventChangesNeeded with the exact change and reason.
- Scope is the aggregate stage only (no handlers, reactors or projectors unless the user said otherwise) (read .agents/skills/aggregate-implementation-jev/SKILL.md "Working cycle"; follow it, except that you cannot execute RED/GREEN; record evidence honestly as "written, not executed").
- Never print, log, echo or commit the Jev/TypeSafe API key.
- Business facts come from the approved catalog docs/product/definitive-aggregate-choreography/ (aggregates.json, aggregate-details.md, decisions.json for your aggregate) and the product requirements it cites, never from guesses.
- Do not change analyzer rules, .editorconfig severities or build enforcement.
Build gotchas: the build treats warnings as errors with strict analyzers; implicit usings and style rules follow .editorconfig and the repo AGENTS.md (read them, and the analyzer diagnostics already enforced, before writing). Test names must be Should<Outcome>Given<State>[When<Action>] (CNTRYL0001) and every test needs ordered // Arrange, // Act, // Assert comments (CNTRYL0002). Check every id, tenant and version string a test builds against the validation in the type it constructs; helper factories validate inputs and throw before the aggregate under test runs. Known compile and runtime pitfalls from earlier waves (avoid them): (1) AggregateScenario.PendingEvents is a list, so use the .Count property, never .Count() (CA1826/CA1829); a helper returning an array must not use .Count either. (2) Events taken from PendingEvents already carry metadata; never pass them to DomainEventSeed.Attach (throws 'metadata can only be attached once'). Seed replay history with NativeFacts.Raised(owner) (clones and attaches) and NativeFacts.Replay, or JSON round-trip a copy first. (3) After a replayed aggregate accepts a further command in Act, its state has moved on: capture any state you assert on before that command. (4) Add the exact using for every type: ImmutableContentRef lives in Bdgrz.Compliance.Features.Proofs.Models, RecordNameChangeIntent in Bdgrz.Compliance.Features.NamedRecords.Models, RequestErrorKind via Cntryl.Portia, NativeFacts in Bdgrz.Compliance.Tests.Testing. (6) NativeFacts.Raised(owner) returns only facts raised since that owner object was opened; an owner built by Replay has none, so build the history on one seed owner (materialize, then act) and call Raised once. (7) Test helper methods that build one concrete event type must return that concrete type (CA1859), not DomainEvent. (8) RequestError exposes IsTransient (not Retryable/Transient); Assert.Equal(1, x.Count) is xUnit2013, use Assert.Single; use ! on Error when asserting .Error!.Kind after Assert.False(IsSuccess) (nullable warnings are errors); a nullable struct result needs .Value before .IsSuccess. (9) Avoid inline constant array arguments (CA1861): assign to a local first. (10) Break object initializers onto separate lines when they wrap (IDE0055). (5b) Prefer private helper return types the analyzer accepts (CA1859: return the concrete array rather than IReadOnlyList).
Do not add malformed-history tests (user preference); cover valid replay and command-level refusal instead.
`

const IMPL_SCHEMA = {
  type: 'object',
  properties: {
    aggregate: { type: 'string' },
    identityAndInvariant: { type: 'string' },
    gapsFound: { type: 'array', items: { type: 'object', properties: { description: { type: 'string' }, kind: { type: 'string' }, fixed: { type: 'boolean' } }, required: ['description', 'fixed'] } },
    changedFiles: { type: 'array', items: { type: 'string' } },
    testsAdded: { type: 'array', items: { type: 'string' } },
    eventChangesNeeded: { type: 'array', items: { type: 'string' } },
    laterStageFollowUps: { type: 'array', items: { type: 'string' } },
    unresolved: { type: 'array', items: { type: 'string' } },
    trackerRowSuggestion: { type: 'string' },
  },
  required: ['aggregate', 'gapsFound', 'changedFiles', 'testsAdded', 'eventChangesNeeded', 'unresolved'],
}

const REVIEW_SCHEMA = {
  type: 'object',
  properties: {
    aggregate: { type: 'string' },
    verdict: { type: 'string', enum: ['ready-for-build', 'needs-attention'] },
    findings: { type: 'array', items: { type: 'object', properties: { description: { type: 'string' }, severity: { type: 'string' }, fixed: { type: 'boolean' } }, required: ['description', 'fixed'] } },
    compileRisks: { type: 'array', items: { type: 'string' } },
    jevQuestions: { type: 'array', items: { type: 'object', properties: { question: { type: 'string' }, answer: { type: 'string' }, probability: { type: 'number' } }, required: ['question', 'answer'] } },
    jevArtifacts: { type: 'array', items: { type: 'string' } },
    remaining: { type: 'array', items: { type: 'string' } },
  },
  required: ['aggregate', 'verdict', 'findings', 'compileRisks', 'remaining'],
}

const results = await pipeline(
  ITEMS,
  (item) => agent(`You are completing the ${item.name} aggregate (domain-event/aggregate stage) for the current scope.

Files: source ${item.src}; tests ${item.tests.join(', ')}; your review file ${item.review} (update it, or create it if absent, following the structure of an existing aggregate review in this repo).
Context: ${item.hint}
${RULES}
Do this:
1. Read the catalog entry for ${item.name} (identity, invariant, lifecycle, command->event table), AGENTS.md, the current source, the current tests, and the latest review. State identity, named invariant and exclusions; decide keep-together/split/reactor per the skill.
2. Adversarially qualify the aggregate against the skill's done criteria: stable identity (no caller key forks a stream), named invariant, owned lifecycle, event contracts (payload carries only business facts, no metadata duplication), invalid decisions append no events, no-op and exact retry (including retry of a committed fact after later facts), changed-intent refusal, replay/hydration of valid history, stale revision / competing decisions, bounded history. Find REAL gaps only; do not invent speculative fields or behavior.
3. For each real gap: write the failing test first (in the existing test file or a new aggregate-only test file), then the minimal fix in the aggregate. Add the missing coverage for criteria that lack a test. If a known-failing test is listed above, resolve it.
4. Update your review file: boundary decision, lifecycle table, changes, a test-to-criterion table, event contract status, later-stage follow-ups (handlers/reactors/projectors, never aggregate gaps), and an honest evidence section stating tests are "written, not executed; build and run pending".
5. Return the structured summary.`,
    { label: `impl:${item.name}`, phase: 'Implement', schema: IMPL_SCHEMA }),

  (impl, item) => agent(`You are the independent reviewer of the ${item.name} aggregate work just done by another agent. Your job: find what is wrong with the tests and implementation BEFORE anything is built, using Jev as an advisory second opinion, and fix it.

Implementer summary (JSON): ${JSON.stringify(impl)}
Files: source ${item.src}; tests ${item.tests.join(', ')}; review ${item.review}.
${RULES}
Do this:
1. Read the catalog entry for ${item.name}, then the actual current source and test files (use git diff -- <path> for what changed this session; the source may also carry earlier uncommitted work). Do not trust the implementer summary.
2. Review as a skeptic. (a) Implementation: does every accepted decision enforce the named invariant; can a stale/duplicate/competing/replayed input append a wrong or duplicate fact; are retries of committed facts acknowledged without a new event; any payload field duplicating metadata. (b) Tests: does each assert a real behavioral outcome (events appended or not, state after replay) rather than structure; any test that cannot fail; any criterion without a test; names/phases follow CNTRYL0001/0002; every fixture value (realm, id, version) valid for the type that validates it. (c) Compile risk: read every changed line as a compiler and the strict analyzers would (usings, types, nullability, var, expression bodies, blank lines after blocks, culture overloads, Assert.Single, Portia/Fitz API). List anything you could not confirm.
3. Ask Jev (advisory only; it never overrides the approved catalog or sources). Read .agents/skills/typesafe-ai/SKILL.md for the exact request schema and .agents/skills/jev-prioritization for how to ask. Send 2-5 narrow ATOMIC questions with small state (exact rule text, decision, expected outcome), e.g. 'Given this rule and this test scenario, does the test assert the invariant?' or 'Is this retry outcome consistent with the rule?'. Include event-name precision questions ONLY if event names are in question (they should not be changed). Run the call with the key only in that command's environment: TYPESAFE_API_KEY="$(tr -d '[:space:]' < ~/.config/typesafe/jev.key)" node <your script>.mjs  — write the script and payload under the scratchpad directory, never print the key, and save the exact request and complete response (no credentials) as the implementation-jev/ folder beside the review file: ${item.name.toLowerCase()}-test-impl-review-2026-10-28-request.json and -response.json (update the date for later runs). If Jev is unavailable, malformed, or returns an error, record NO judgment and continue; low or near-even confidence is unresolved, not approval.
3b. RESOLVE EVERY DISCREPANCY WITH JEV; do not leave it as an open question. A discrepancy is: an event or command in code that the approved catalog lacks (or the reverse), a naming conflict with sibling events, a payload field that repeats the stream identity, or an owner decision the model is silent on. Policy: a fact that records an owner decision gating a business outcome is a domain event and a catalog gap to register; technical execution state stays in private durable records; a negative outcome alone creates no event; payloads carry only business facts not already carried by stream identity or metadata. For each discrepancy send Jev a Choice (criteria for every option, e.g. add_to_model / remove / needs_source_resolution) plus a Noul that the underlying premise holds, citing the exact model text and the exact code behavior. Apply a decisive answer (a clear lead, not near-even) to this aggregate's own code/tests; for anything that changes the approved catalog, schemas, generated code or event names, do NOT edit shared files: list it under eventChangesNeeded/remaining with the Jev disposition and probabilities. Record every discrepancy, the probabilities and the disposition in your review file. Near-even or low-confidence answers stay flagged unresolved.
4. Fix every real finding you can confirm (edit source/tests/review). Do not weaken a test to make it pass. If a finding needs a shared-file/event change, report it in remaining instead.
5. Append a 'Review before build' section to the review file with findings, fixes, Jev questions/answers/probabilities (model version too) and remaining risks. Return the structured result. verdict is 'ready-for-build' only if you found no unresolved correctness gap and no known compile risk.`,
    { label: `jev:${item.name}`, phase: 'Jev review', schema: REVIEW_SCHEMA }),
)

return ITEMS.map((it, i) => ({ aggregate: it.name, result: results[i] }))
