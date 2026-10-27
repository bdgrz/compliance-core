---
name: aggregate-implementation-jev
description: Implement the approved compliance-core aggregate set with invariant-first TDD, source-backed event review with Jev, and replay, concurrency, recovery, and qualification evidence.
---

# Aggregate implementation with Jev

Use this skill when implementing or completing a domain aggregate in `compliance-core`, especially for the approved definitive set, behavior-first TDD, or Jev-assisted event naming. Apply it to one bounded aggregate slice at a time unless the user explicitly asks for parallel independent slices.

## Authority and boundaries

- `AGENTS.md` and applicable product/architecture decisions are authoritative. Follow repository rules for contracts, authorization, tenant isolation, persistence, recovery, tests, formatting, and delivery.
- Treat `docs/product/definitive-aggregate-choreography/` as the approved set of business owners, commands/events, reactors, and processes. Its inventory is a design catalog, not proof of source, wire, runtime, persistence, or implementation conformance. Inspect its decisions and evidence for the selected slice, then verify them against product requirements and code.
- This repository uses C#/.NET, `Compliance.Core`, Portia/Fitz, and xUnit. Web/mobile clients and their tests belong in their owning repositories; do not move client behavior or authorization policy into client work here.
- Jev is advisory. It can identify naming ambiguity or alternatives, but cannot establish business meaning, source authority, event ownership, aggregate boundaries, or implementation correctness.
- A fold, class, generated contract, passing structural test, or compile-only red is not proof of a proper aggregate.
- Do not introduce a command, event, field, interface, aggregate, or policy without a source-backed working behavior and tests. Negative responses and technical receipts are not automatically business events.
- Do not rebuild framework responsibilities. Inspect the pinned Portia/Fitz package API, analyzers, tests, and existing repository adapters before adding command dispatch, stream identity, hydration, metadata, OCC, persistence, authorization context, reactor delivery, process execution, or serialization plumbing. Add an adapter only where a concrete compliance behavior or framework gap requires it; document the evidence for that gap.
- Never claim host, provider, durable-store, migration, or production qualification from controlled tests. Report each evidence boundary accurately.
- The application owns business rules, lifecycle behavior and invariants. Portia/Fitz own their documented infrastructure responsibilities. Do not turn a history-size observation, advisory Jev score, or ordinary replay requirement into a new application-level framework or a speculative blocking defect. Investigate a concrete business failure against the actual framework and existing consumers first; retain any demonstrated limitation in the evidence.
- When scope is one aggregate and its tests, do not expand into other aggregates, framework plumbing or unrelated fixes. Focused review evidence and an explicitly requested status table may be updated. Keep aggregate completion distinct from repository-wide suite, CI and production qualification.
- Keep the user's requested business scope authoritative. Do not implement a technically attractive follow-on, alter acceptance tests to make an out-of-scope gap disappear, or hold an aggregate slice hostage to an unrelated CI failure. Record the blocker separately; never merge a head with failing required checks.
- Repository-specific testing preference: the user has said malformed-history tests are unnecessary. Do not add them by default. Prefer command-level refusal/no-fact tests and valid replay coverage; add a malformed-history test only when the user explicitly requests it or a source-backed acceptance criterion specifically requires that corrupted-history case.

## Efficient single-aggregate loop

- Select the next owner from the approved catalog and the highest-priority applicable backlog story, considering dependencies. A story may own more than one aggregate; finish the remaining relevant P0 owner before skipping to a later story. Catalog baseline paths can be stale: resolve the current declaration before labeling an owner missing.
- When the user says “next,” advance exactly one row in `aggregate-backlog.md`, including the remaining owner in a multi-owner group before moving to the next group. Read the row and its dependency stages instead of inferring the next owner from the last code change. If that owner already satisfies the selected invariant in source and existing tests, record the bounded verification without manufacturing a code change, then advance only on the next user instruction.
- Start with the owner invariant, identity, lifecycle and current tests. Map each rule to existing executable coverage before adding tests. Add tests for concrete gaps, not to manufacture a red run when behavior is already implemented. For a behavior defect, capture a meaningful red test before changing production code.
- Check that business lifecycle and owner-identity invariants also hold when facts are replayed. For a tenant-scoped owner, compare any tenant carried by a fact with the tenant selected when opening the aggregate; stream tenancy alone does not validate payload identity. For cross-owner references such as a name-claim holder, validate the referenced stream's tenant, area, and owner-specific identity before raising a fact. Keep those checks in the concrete owner when they depend on its domain type; do not widen a shared base without a shared invariant. Verify replay guards by inspecting the reducer and using existing valid-replay coverage; do not add malformed-history tests against the user's stated preference. Leave stream loading and persistence to the framework.
- Run the smallest relevant test selection first; run the requested or repository-required broader checks once when appropriate. Do not run builds concurrently against the same output directories. If `--no-restore` reports stale package paths, restore the existing solution without introducing lock files when the user has requested no lock files, then retry. Record actual command, scope and result; an interrupted suite has no final pass/fail total, though individual failures observed are evidence.
- Use Jev on the exact owner and focused source-backed questions, and retain the request and response. Each question must stand alone: name the specific invariant or test rather than referring to another question or “those cases.” Treat near-even scores as prompts for source review, not defects or approval. Preserve earlier worktree changes and review artifacts; do not repeat a review or successful test without a specific change, failure or open question that makes the repeat useful.
- Record brief evidence for completion: selected invariant and lifecycle, what tests establish, Jev's actual judgment, and concrete remaining limits. A local aggregate may be complete while unrelated full-suite failures remain; report both without attributing unrelated failures to that aggregate or claiming full repository qualification.

## Boundary, split, and reactor decisions

Make these decisions explicitly before writing aggregate code. The decisive question is **which owner must make which business decision atomically**; class count, entity shape, event count, and current data volume do not answer it.

### Keep facts in one aggregate when

- One named owner must decide a rule over those facts atomically, and separating them would allow an invalid intermediate or competing state.
- A bounded collection is part of one immutable manifest, balanced transaction, complete policy publication, or similarly coherent decision whose members must be validated together.
- The stream identity is the stable identity of that decision, not a caller request ID that can create another stream to evade uniqueness or contention.
- The lifecycle and history have a defensible bound or explicit rollover/requalification design. Small current state does not bound replay history.

### Split when

- Relationships, occurrences, members, or decisions have independent authority, lifecycle, correction, or concurrency and do not need one atomic decision.
- A parent would otherwise own an unbounded roster, inbox, history, payment/attempt log, or independently governed child state. Parent lists and histories are usually projections.
- Unrelated users or occurrences would contend on one stream without protecting a shared invariant.
- The aggregate is really a technical execution receipt, retry ledger, batch worker, artifact, or read model. Keep durable execution private and bounded where needed; do not turn it into a business aggregate without a source-backed business invariant.
- The proposed split preserves the original invariant through authoritative admission, a reservation/delegation owner, or explicit coordination. A projection check is not a substitute. If the invariant cannot survive the split, retain the current authoritative boundary or design the coordination protocol first.

For every split, state each resulting identity and owner, who decides the original joint rule, how membership/admission is serialized, how cancellation or release fences delayed children (including children holding nothing yet), and how OCC retry, lost acknowledgment, restart, and history growth work. Do not split merely to reduce stream size if doing so loses atomicity; do not keep facts together merely to simplify reads.

### Use a reactor when

- One owner has committed a business fact and another owner must make its own independent decision or progress its own lifecycle in response.
- Work crosses aggregate or module ownership and does not require one database/stream transaction. Each target receives an idempotent command and enforces its own invariant.
- A durable process manager is needed to coordinate ordering, reservations, multiple dependent child steps, or unknown external effects. It records progress/execution privately and calls one owner at a time.

Do not move another owner's decision into the source aggregate or have an aggregate call a platform domain service. A reactor is not an aggregate-wide workflow controller and cannot establish atomicity across streams. A source projection, notification, timer, finite sweep, or successful command dispatch is not proof that the target committed. Persist exact delegation/reservation before child admission where authority or shared budget must survive progression. Release only after the child durably acknowledges its terminal outcome or fences later admission, including empty children. On OCC conflict, reread and re-decide.

Before coding, record a short decision such as: `keep together because ...`; `split into X and Y because ...; invariant preserved by ...`; or `source fact triggers reactor R, which asks target owner T to decide ...`. If evidence cannot support one, leave the boundary unresolved and do not implement a guessed split or reactor.

## Feature directory layout

Keep capability roots under `Features/<Capability>` and favor the existing shallow layout. Introduce a subdirectory for commands, aggregates, tests or another role only when it makes a substantial feature easier to navigate; the user specifically wants fewer directories. Do not create a folder solely for symmetry.

When a requested mechanical organization pass specifies role folders, follow that structure and change namespaces to match it; do not leave aggregate classes under `Processes` or `Services` merely to preserve the old namespace. Leave no loose source files at feature roots when the requested layout assigns them a role folder. Move only files in the active scope, update current source-path inventories and generators that treat paths as data, and retain historical source locations when they document baseline evidence. Verify Core, Common and Tests independently. Do not reorganize unrelated features as drive-by cleanup. Keep shared Portia/Fitz integration in the existing hosting/composition boundary; do not create per-aggregate framework plumbing.

## Working cycle

1. **Inventory and select one bounded slice.** Check branch, worktree, existing changes, definitive catalog, product decisions, prior implementation/review evidence, and relevant issue/dependency state. Preserve unrelated work. Select by business invariant, harm, and dependency; do not equate missing event folds with missing aggregate boundaries. The goal of a full-set request is every catalogued business aggregate, with technical roles/processes handled according to their explicit disposition. Parallelize only when explicitly requested and slices have independent invariants.
2. **Qualify the aggregate boundary before coding.** Read the selected aggregate's approved definition, relevant product/source passages, current implementation, tests, event schemas, and prior reviews. Write down:
   - **Identity and scope:** what one stream represents; how its ID is derived and validated; whether a request key can fork authority; what belongs in metadata versus business facts.
   - **Named invariant:** the exact business decision this owner alone must make atomically. State explicit exclusions and which neighboring owners decide them.
   - **Boundary alternatives:** apply the [keep/split/reactor decision guide](#boundary-split-and-reactor-decisions): decide whether the invariant needs one aggregate, independently governed aggregates with coordination, a bounded private process/execution record, a reactor, or only a projection. Keep a collection only when its members must participate in the same atomic decision. Justify both lifetime history growth and write contention.
   - **Lifecycle:** legal states/transitions and their source authority; create, revise, disable/suspend, restore, close/retire, correction, and terminal cases that actually apply. Identify no-op and refusal paths. State which events each accepted decision may establish and what is not an event.
   - **Coordination:** for cross-owner work, identify the durable reservation/delegation owner, exact child/effect identity, generation/epoch, cancellation/release rule, child acknowledgment or fence, OCC re-decision, delayed append behavior, and partial/restart recovery. A sweep or projection is not exclusion proof.
   - **Framework boundary:** inspect the pinned Portia/Fitz package API, analyzers, repository usage, and available tests for identity, hydration, metadata, OCC/save, command identity, actor/authorization, execution, reactor delivery, process management, and persistence. Record what the framework supplies and what compliance invariant remains local. Do not create parallel infrastructure or wrap framework features without a demonstrated need.
   If sources do not support a boundary, lifecycle transition, policy, or naming distinction, stop that part and record the unresolved decision instead of inventing it.
3. **Establish a baseline, then write executable RED specifications.** Follow repository test conventions: xUnit, `Should<ExpectedBehavior>Given<Condition>`, block-bodied tests with Arrange/Act/Assert sections. Run the narrow existing tests or reproduce the targeted behavior before edits. Add behavior-first tests for the named invariant and applicable lifecycle transitions. Cover authorized success, invalid transition/refusal and no event, no-op, exact retry versus changed intent, replay/hydration, stale revision and competing appends, restart/lost acknowledgment, and lifetime/history bounds where applicable. For cross-owner protocols, test delayed append, empty-child cancellation/fencing, lost acknowledgment and restart before releasing the parent reservation. Prefer runtime assertions showing the defect; a compile error alone only proves a missing API. Run the tests and capture exact failing cases and command before production changes.
4. **Review event names with Jev before accepting new or changed names.** For every affected proposed fact, send a narrow request containing exact product wording/source, owner invariant, completed fact, producer command/decision, lifecycle siblings, and payload meaning when useful. Ask whether the name is precise and idiomatic and compare a few source-preserving alternatives. Ask atomic questions; if using Choice, supply criteria for each option and a companion Noul asking whether any candidate fits. Retain the exact request and response beside the aggregate review artifact. Never send credentials, secrets, protected personal data, raw vendor payloads, or unnecessary source contents. Read the local TypeSafe skill for live API schema and credential rules. Low confidence or near-even answers are unresolved; do not average scores into approval. When Jev is unavailable, malformed, or returns 422, record no judgment and continue source-based analysis without claiming Jev validation.
5. **Adjudicate boundaries, lifecycle, events, and names from sources.** Ensure each event names one completed business fact and matches the catalog's discriminator, feature, and schema-version conventions. Distinguish accepted intent, dispatch, observation, owner decision, completion, settlement, correction, and private technical execution. Verify that name, payload, producer, lifecycle, and sibling events agree. Confirm payloads do not duplicate metadata, stream identity, envelope, or execution receipts. Follow the event tooling in `tools/domain-events/README.md` and repository contract conventions when changing a schema, generated JSON context, fixture, catalog, or retirement record. Never silently rename a persisted discriminator. For an explicitly never-shipped contract, follow the recorded disposition and document it. Do not add an event for a rejection, guard, failed command, time passage, or technical outcome unless a source-backed owner decision establishes a new business fact or obligation.
6. **Implement the smallest change that turns RED to GREEN.** Preserve aggregate-private state and complete derived identity. Hydration/replay must reproduce live guards and lifecycle. On OCC conflict reload and re-decide. Preserve accepted command identity and the original committed revision/result on retry. Keep cross-owner progression in idempotent reactors and private durable execution records where specified. Do not widen the boundary, add speculative fields/interfaces, or weaken a failing test to make it green.
7. **Run focused GREEN and adversarial review.** Re-run the exact RED tests and affected aggregate tests. Add a regression for every review finding. Cross-check adjacent aggregates/reactors and event producers for ownership duplication or lifecycle gaps. Verify event generator/catalog/schema/discriminator/fixture consistency after contract changes. Re-examine lifetime growth, contention, tenant isolation, authorization, authority revocation ordering, delayed append, lost acknowledgment, restart/replay, and independent-write behavior. A green local seam does not establish host, provider, production store, migration, or external-effect qualification. For a full backend change, use the `AGENTS.md` acceptance commands when dependencies are available: locked restore, Release build, backend unit suite excluding integration categories, and formatting verification. Do not add or run tests unless the user asks to test or verify; when the active request is implementation/TDD, the requested red/green evidence is authorized. Do not commit unless asked or included in the authorized delivery objective.
8. **Record the boundary decision and evidence.** Update the focused aggregate review and definitive catalog/completion evidence only with source-pinned facts. Record identity, invariant, exclusions, lifecycle, boundary alternatives, history/contention reasoning, coordination/recovery protocol, event-name decision, exact RED and GREEN commands/results, Jev model/version and confidence, cross-review fixes, remaining gates, and unverified claims. Mark unresolved source or low-confidence naming questions explicitly. A Jev response is never a pass/fail gate by itself. Keep issue/project state authoritative for backlog status; do not imply a catalog entry is implemented until code and tests demonstrate it.

## Jev request pattern

Keep state small and judge one aggregate or a small related batch. Provide explicit source snippets or precise references and the exact fact; do not ask Jev to invent the domain. A useful shape is:

```json
{
  "model": "jev-latest",
  "state": {
    "source_fact": "Exact source-backed business rule",
    "owner_invariant": "The invariant this aggregate decides",
    "producer": "The accepted command or owner decision",
    "current_name": "CurrentEventName",
    "meaning": "Exact completed business fact",
    "siblings": ["RelatedFactA", "RelatedFactB"],
    "candidates": ["CurrentEventName", "SourcePreservingAlternative"]
  },
  "questions": {
    "clear": {
      "type": "noul",
      "question": "Does current_name precisely and naturally name meaning?",
      "criteria": {
        "true": "It clearly distinguishes the fact from sibling lifecycle facts.",
        "false": "It is materially vague, misleading, or obscures the changed subject."
      }
    }
  }
}
```

Use the exact supported request schema from `.agents/skills/typesafe-ai/SKILL.md`; Choice options require matching criteria. The repo's `scripts/backlog-jev.mjs` is for issue triage/criteria/preflight and may not support aggregate contract reviews; inspect it before reuse and do not force unrelated semantics through it. Use an established focused Jev client/wrapper if present, otherwise follow the TypeSafe skill's supported request path and repository credential handling. Preserve the exact sent payload and complete response in the selected aggregate's review evidence, but never include credentials in either artifact. Do not expose key contents in shell output, prompts, source, fixtures, logs, or docs. Treat network/API errors as no judgment, not a negative answer.

## Repository references

Read relevant sources for the selected aggregate; do not load every reference by default:

- `AGENTS.md`
- `docs/product/definitive-aggregate-choreography/README.md`
- `docs/product/definitive-aggregate-choreography/aggregates.json`, `aggregates.md`, `aggregate-details.md`, and the relevant command/event/choreography/decision JSON and Markdown artifacts
- The product requirements and source records cited by the selected catalog rows; verify citations and current status
- `docs/product/delivery-cycle.md` and linked GitHub issues/project state when selecting or reporting backlog work
- `docs/product` event contract documentation and `tools/domain-events/README.md` when changing event contracts
- `src/Compliance.Core` framework integration and the pinned Portia/Fitz APIs/analyzers
- `test/Compliance.Tests` focused aggregate tests and neighboring reactor/process tests
- `tools/domain-events/README.md`
- `.agents/skills/typesafe-ai/SKILL.md`

Use focused prior aggregate review documents and tests as evidence; confirm their facts are current before relying on them.
