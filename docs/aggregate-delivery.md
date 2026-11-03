# Aggregate delivery and evidence

Use this guide with [CONTRIBUTING](../CONTRIBUTING.md) and the repository's
aggregate rules. One round implements one named invariant in one aggregate and
its domain events. Handlers, reactors, projections and API routes require their
own explicit scope. A locked context document establishes a design decision;
it does not establish implemented behavior.

## Establish the baseline

Record the target base commit and inspect its hosted Backend result before
choosing a slice. A successful result on that exact base is reusable baseline
evidence. When it is absent or failed, run the configured checks and inventory
the failures before feature implementation. Repair delivery blockers in a
separate prerequisite PR, then rebase the aggregate slice. Preserve the initial
failure names and distinguish them from any regression the slice introduces.

The current Backend gates are:

```sh
node --test tools/test/*.test.mjs
node tools/validate-aggregate-spec.mjs
node tools/check-implementation-coverage.mjs
dotnet restore Compliance.slnx
dotnet format whitespace Compliance.slnx --verify-no-changes --no-restore
dotnet build Compliance.slnx -c Release --no-restore
dotnet test Compliance.slnx -c Release --no-build --no-restore --filter 'Category!=BrokerIntegration&Category!=WebIntegration'
```

Use the selected SDK and configured registry credentials. Never print, commit
or include credentials in review input. Run builds and tests serially in the
shared checkout. Read-only review can continue while CI runs on a frozen head.

## Use Jev at decision checkpoints

Call the Jev API at these checkpoints and retain its actual responses. Use the
repository wrapper when present; otherwise use the configured TypeSafe API
with the same credential-handling rules. A probability is an advisory judgment,
not an acceptance test or a promise of defect freedom.

1. **Before coding:** provide the approved rule, current source owner, identity,
   lifecycle and candidate invariant. Ask typed questions about local ownership,
   keeping or splitting facts, and any required authoritative coordination.
   Cross-owner projection reads cannot establish an atomic invariant.
2. **Before accepting the test plan:** provide the behavioral matrix and test
   helpers. Check refusal without a fact, no-op behavior, exact retry versus
   changed intent, replay and competing appends where applicable. Include
   serialization, identity and fixture helpers rather than assuming their
   behavior. Write and run a focused behavioral RED, then make it GREEN.
3. **Before merge:** provide the immutable head SHA, exact diff, relevant source,
   event contracts, tests, helpers and actual results. Ask separate adversarial
   questions about authorization, tenant scope, replay, failure and event
   compatibility. Include the existing execution path when a guarantee is
   supplied upstream; do not present a four-line patch as the entire system.

Ask one concrete property per typed question. If a broad question returns an
uncertain judgment, record it, inspect the missing evidence, and split the
question with an explicit reason. Retain the original result and the follow-up;
do not repeatedly ask an unchanged question until a favorable answer appears.
Near-even or low-confidence design judgments remain unresolved. Candidate
findings need source verification and a failing regression before changing
behavior. A demonstrated counterexample takes precedence over model confidence.

Compiler, formatter, test, Git and CI outcomes are verified directly through
those tools. Jev reviews the decisions and possible gaps; it does not replace
these checks. Independent source review remains part of the final review.

## Keep one evidence record

Use `docs/proposed/reviews/<aggregate>.md` and the linked issue/PR as one
navigable record. Link the review from `docs/proposed/README.md`:

- Base and final head commits; source and target owner, identity and invariant.
- Initial RED command and observed failure; focused GREEN and preserved cases.
- Exact Jev questions, input authority, requested model alias, resolved model
  version when returned, responses and probabilities, including uncertainty and
  source-backed disposition. Mark unavailable metadata explicitly.
- Focused and full unit results, formatter/build results, and any named gaps.
- Final-head hosted check, adversarial review disposition and merged commit.

Mark an aggregate partial whenever a named completion condition remains open.
Keep local tests, runtime compatibility, proposed-model conformance, hosted CI,
broker qualification and deployment evidence distinct. Generated inventories
and test-file references are navigation evidence, not behavior proof.

## Finish on the reviewed head

Stage explicit paths and keep the PR focused. Freeze the head while required CI
runs. If source changes after review, review the new head and rerun affected
checks; the final PR head still needs hosted Backend success. Squash-merge only
the reviewed head with passing required checks, then read back the merged state
and synchronize local, tracking and live target refs. Close only the acceptance
actually proved and preserve remaining aggregate or workflow work explicitly.

For validation performance changes, measure before and after with the same
checks and assertions. Timing is diagnostic; avoid wall-clock pass/fail tests.
Reduce redundant analysis rather than changing analyzer rules, classifications,
test selection or timeout enforcement.
