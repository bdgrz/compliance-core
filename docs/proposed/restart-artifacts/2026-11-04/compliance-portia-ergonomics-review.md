# Portia ergonomics review

Reviewed baseline: merged main baffd1badc29817d4759e3bc2d03cbc4e79617f9.
Observed root HEAD during review: 65fdd65e674a1329c02d4c0719836a2f22601420; git diff baffd1ba..HEAD -- src Directory.Packages.props was empty. Production examples below therefore describe the requested merged baseline.
Initial dirty scope: PersonNativeHistoryTests.cs, PersonNativeHttpTests.cs, PersonNativeIdentityTests.cs, and untracked PersonLegacyMutationTests.cs. These are preparation/source assertions, not executed RED/GREEN evidence.
No repository files changed, no .NET/format/test commands, no upstream edits/network work.
Packages: Directory.Packages.props:13-21 pins the used Portia family to 0.10.0.
Framework implementation was read from the local Portia repository with git show v0.10.0, not inferred from its current working tree.

Overall judgment

Portia makes the core event-sourced patterns small and legible. The overall application is not uniformly simple yet. The strongest new slices visibly use it well; substantial complexity comes from Compliance's generic native-admission/source-proof protocols, retained legacy workflows and its own process runtime. A short public handler can hide considerable application machinery. Neither current test counts nor existing type names establish full proposed-domain completion.

Three clean patterns

1. One business owner, thin command handler, explicit commit/discard.
- src/Compliance.Core/Features/MethodologyTemplates/Handlers/ReviewMethodologyTemplateDraftHandler.cs:9-21 checks the personal request boundary and invokes IAggregateExecutor on one draft with AggregateOutcome.CommitOnSuccess(owner.Review(...)).
- Aggregates/MethodologyTemplateDraft.cs:88-124 states the actual rule: reviewer authored no revision, approval requires the accepted current revision and a third person. It raises business facts without persistence code.
- Aggregates/TemplateVersionApplication.cs:46-80 is similarly direct: open the exact entry set, record one immutable outcome per entry, complete only when all have answers.
- Portia v0.10.0 src/Portia.Core/Aggregates/AggregateExecutor.cs:9-35 owns hydration, invocation and applying the requested disposition; :54-68 owns save/discard.
- Existing MethodologyTemplateDraftTests and TemplateVersionApplicationTests assert refusal without fact, exact retry/changed intent, lifecycle and replay. No independent execution was performed by this review.
This is the best pattern to copy for simple local decisions.

2. Typed choreography with returned failure Results and framework-owned checkpoints.
- MethodologyTemplates/Reactors/MethodologyTemplateDraftConclusionReactor.cs:16-31 returns SendReactionAsync directly for publication/closure. Application code defines the source/target and deterministic command identity.
- Source-bound business handlers verify the committed approval/discard and let the line decide whether the draft is current.
- MethodologyTemplateDraftConclusionTests.cs:16-66 runs ReactorScenario with an actual refused send, asserts the original RequestError, checkpoint held at Start and the identical replayed command; after success it asserts progress.
- Portia v0.10.0 src/Portia.Core/Reactions/ReactorRunner.cs:141-157 turns failed Result into processor failure and advances checkpoint only on success.
This avoids application-written reaction loops and makes failure sequencing reviewable. It does not make effects and checkpoints atomic or downstream work exactly-once.

3. Application SQL inside Portia's projection transaction.
- Workforce/Readers/CassieWorkforceRepository.cs:10 derives from CassieProjectionRepository. :24-67 uses CreateCommand/CurrentProjectionKey for model SQL; :70-90 uses OpenReadAsync for ordinary reads.
- CassiePersonDirectory.cs:38-63 applies business-specific person transitions and keeps native private values out of discovery rows.
- Portia v0.10.0 src/Portia.Cassie/Projections/CassieProjectionRepository.cs:105-132 owns begin/checkpoint concurrency; :224-240 conditionally updates checkpoint and commits the SQL transaction.
- CassiePersonDirectoryTests.cs:76 onward uses BeginAsync/CommitAsync and checks correlation, tenant isolation and ambiguity. Other assembled native fixtures cover rollback/generation behavior; this review did not rerun them.
No application transaction/checkpoint implementation was found in this feature adapter. Business tables, filtering, paging and privacy remain application code. This statement applies to the migrated directories, not every repository.

Three concrete friction points

1. A simple named mutation traverses a large application protocol.
- ChangePersonDetailsHandler.cs:8-11 is thin, but PersonOperations.cs:80-87 then :109-138 runs Rules, verified historical reads, provenance, content freezing and NativeOperationExecutor.
- PersonContent.cs:58-98 replays the business operation against before/after values and verifies several content references; PersonReader.cs:77-104 verifies the historical mutation and named field flags again.
- NativeOperationExecutor.cs:85-165 adds hydration/preparation/rechecks, a second executor hydration and up to four OCC-race attempts. :198-205 reads the frozen original response.
These support real application promises: exact original response replay, authored-intent collision detection, immutable large/private content and exact historical provenance. Portia's AggregateExecutor does not provide that receipt protocol. Nevertheless, there are multiple representations and repeated semantic checks for one mutation. That is our application cost, not evidence that Portia requires every aggregate to use this architecture.
- The recent corrective-action fix (ChangeCorrectiveActionHandler.cs:34-50) is a practical example: current authorization must precede accepted receipt replay because prepare callbacks are skipped for retries. The generic wrapper makes that ordering easy to miss. Preserve authorization as a visible command boundary.

2. Parallel legacy and named paths make change cost exceed business-rule cost.
- Current main still maps both RevisePerson and ChangePersonDetails (Program.cs:272-274, :2029-2046); RevisePersonHandler delegates to PersonOperations.ReviseAsync.
- PersonDirectoryProjector.cs:6-11 and PersonReader.cs:59-75 support legacy and native event families.
- Dirty Person tests move existing history/HTTP/replay assertions to ChangePersonDetails; add a refusal for the generic revisions route; and propose zero-persistence-call refusal for the old handler.
- PersonLegacyMutationTests.cs:82 onward preserves historical receipt/privacy/correlation behavior after a named change.
These are meaningful cleanup specifications, not observed passing implementation. They demonstrate that the current source complexity is partly migration debt. Retiring old writers can reduce future paths while historical decoding remains where required. Portia is not choosing or enforcing these product cutovers.

3. A second generic workflow runtime lives inside Compliance.
- Processes/Processes/ProcessDriver.cs:65-90 defines 30-second worker leases and record-CAS progression; :108-149 schedules retries/quarantine and dispatches receivers.
- ProcessSweep.cs:98-112 polls with a PeriodicTimer and retries failed sweeps.
- FitzKvProcessRecordStore.cs:77-110 owns KV durability/CAS storage and due-index maintenance; :153-170 retries isolation conflicts.
- ProviderReviewProcess, ProviderNameChangeProcess and RiskActionCompletionReviewProcess use this runtime. The approved proposals explicitly remove several of these old processes.
This is generic execution/recovery infrastructure in application code, overlapping the class of work Portia runners already perform. It is not a drop-in duplicate of a Portia saga API: Portia 0.10 does not promise a multi-stream transaction, transactional request outbox or a complete business saga engine. Some durable business coordination may remain necessary. Still, copying this runtime into new simple slices increases the application's framework surface and is the clearest boundary to tighten.

Responsibility assessment

| Responsibility | Current owner / assessment |
| --- | --- |
| Aggregate hydration, event append, commit/discard, stream OCC | Portia; application uses its reader/executor/writer. NativeOperationExecutor wraps this rather than replacing the event store. |
| Typed reaction dispatch, failure propagation, checkpoint advancement and framework retry | Portia on the clean reactor paths. Do not add feature-level checkpoint loops. |
| Projection SQL transaction, model/checkpoint commit and stale writer rejection | Portia.Cassie on migrated adapters; feature code supplies business SQL and key predicates. |
| Lease/due-index/CAS scheduling and recovery sweep for generic processes | Application currently owns these; overlaps runtime concerns and warrants simplification as legacy flows are retired. |
| Original HTTP/MCP result replay, authored command identity and collision policy | Application contract; no equivalent receipt API established by this review. |
| Current actor authority, tenant scope, private-field disclosure | Application; Portia transports typed context and composes authorizers but cannot infer product policy. |
| Authoritative source outcomes, reject/compensate/incomplete business decisions | Application. Portia validation-authority.md:15-23 and :65-80 explicitly distinguish these from framework processing success. |
| Business freshness fences and approved source-cut semantics | Application. Cassie transaction/checkpoint correctness does not by itself provide read-your-writes or business authority. |

Recommended answer to the user

"Portia is helping: the best aggregate, reactor and Cassie paths are small and clear. The repo overall still carries too much application scaffolding and legacy overlap. We should use the direct one-owner/typed-reaction pattern for new slices, finish retiring superseded writers, and stop expanding our generic process/admission machinery unless a named business invariant requires it. Portia should continue owning execution, persistence, concurrency and processor retry; we own business outcomes and authorization."

This is an architectural assessment, not a defect-free certification, test result or whole-domain completion claim. No unsupported upstream API, saga, outbox, automatic authorization or exactly-once guarantee is assumed.

Historical memory was used only to orient the review toward small aggregate boundaries and explicit validation roles (MEMORY.md:29-36, thread 01a1a261-11a9-7393-8fdd-d40b77161b42). All claims about the current application and pinned framework above were verified against the specified source.

Concurrent-work boundary: final readback showed root had begun Person production cutover edits (Program/JSON/HTTP/native response contracts, RevisePersonHandler, PersonOperations and PersonSeeds). Those were not edited by this reviewer and are outside the immutable-main assessment above. Statements that the generic writer remains live describe baffd1ba, not the in-progress cutover.
