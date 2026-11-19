# Criteria publication: ordered individual decisions

Status: **witness-pin protocol candidate; not yet qualified**, 2026-11-05. This
design removes the child roster, collection-wide publication decision and the
earlier proposal's dependency on a shared consumer checkpoint through the cut.
Its authorization and recovery prerequisites are requirements for an
implementation, not capabilities established merely by naming them. It evaluates
the parent-incarnation correction direction: removing a draft parent makes its
referencing points of focus ineffective; re-adding it creates a new incarnation
and does not automatically restore those references. The product decision and
full draft lifecycle are resolved separately.

## Decisions and ownership

| Owner | Owns | Does not own |
| --- | --- | --- |
| Edition | Draft generation, its label and one pending publication attempt; publication or cancellation of that attempt. | A criterion list, a per-criterion outcome ledger, or a traversal of criteria. |
| Entry identity `(edition, identifier)` | One identifier's revisions and incarnations, kind, one in-flight intent and its local result. A point of focus names one exact parent incarnation; a criterion can hold one publication-witness pin. | Other entry identities or the points that reference this entry. |
| Edition-label claim | One normalized label's exclusive holder and exact claim generation. | Edition entries, an edition-wide manifest, or a per-publication pin. |
| Operator authority | The authenticated subject's exact identity/session state, current operator grant, applicable vetoes and policy generation at one coherent admission instant. | An event per publication or child edit, or a roster of catalog entries. |
| Operation result | The exact request identity, actor, intent fingerprint and accepted/refused/pending outcome needed for retry and recovery. | Results for a batch or an edition. |
| Ordered timeline/read model | Indexed observations, source coverage and page-oriented catalog views. | A new aggregate that decides the edition's invariant or stores its children as one content object. |

Criterion and point-of-focus forms **share the same identity stream**, including
all incarnations, so their common `(edition, identifier)` uniqueness cannot be
defeated by writing the same identifier under two kinds or incarnations. The
kind and parent of an accepted incarnation are immutable; correction begins a
new incarnation on that same identity owner, subject to the draft lifecycle.

Each write carries small, individually addressable facts. An operator proposal
is a **pending intent**, not an accepted criterion; externally report acceptance
only after its own durable business outcome is known. One identifier can have one
in-flight mutation; its own expected-position append decides races. Distinct
identifiers can proceed independently. Exact retries use the original operation
token and intent; changed-intent reuse is conflict. If the first attempt is
refused, a later attempt can proceed only after terminal refusal or fencing is
durable; a timeout or an empty stream is not a refusal. This avoids permanently
occupying an identifier with an invalid first proposal. Expected-version losers
do not silently retry at the winner's newer version with changed meaning.

Each operation has a durable outcome addressable by its identity, or an
equivalent proven source lookup. This does not require a parent inbox. A
cross-stream failure after an intent but before a result stays pending and is
reconciled; participant acceptance wins over a late attempted cancellation. If
the owner stores its own history of successive edits, its expected churn and
Portia full-stream replay must be assessed, rather than hiding per-child events
on the edition.

## Source order and validity for individual entries

All events needed to decide individual membership at the publication boundary
must share one retained, gapless committed Fitz area or realm ordering: edition
publications, criterion intents and outcomes, and relevant parent-incarnation
transitions. A same-area read is insufficient if any necessary fact is in another
area; a realm pattern is available only inside one realm. Actor authorization is
decided separately at admission using the source-owned read policy below and its
trusted durable binding; it is not inferred from a claim supplied by the client.

An optional timeline index processes the committed order incrementally. It
retains each relevant source identity and position, the facts needed to obtain
one owner's state at an observed source boundary, and a durable coverage
checkpoint. Index changes and the checkpoint need an atomic or equivalently
recoverable protocol: on crash, replay must be idempotent and the checkpoint
cannot advance past an outcome that is neither durable nor safely recoverable.
Workers may be partitioned by criterion identity but decisions for one identity
must follow source order. A request does not claim completeness merely because a
subscription was quiet or a read returned an empty page. Unknown, missing,
filtered, retained-away or inaccessible source facts mean pending or error.

The index may be a read-side structure; it is not authority merely because it
is called an index. Its coverage, exact as-of lookups, retention, routing,
isolation and repair are preconditions. The current application's normal
Portia persistence decorator does not expose `IDomainEventProgressReader`,
and no realm head watermark is assumed. For an individual entry or a complete
view, observe the actual marker and prove the required coverage. **The edition
publication write does not wait for that index or checkpoint.** Waiting for a
single shared checkpoint through publication would still require processing all
earlier child traffic and is rejected as a hidden collection-wide gate.
Portia cursors are opaque and cursor values for a resource
and a realm are not interchangeable. A supported exact-position resolver or
qualified persisted index is required; do not compare cursor strings or infer
order from wall-clock timestamps. An index reaching C proves only source
coverage through C, not that all pre-C intents have settled.

## Publication and edits

The earlier proposal waited until the shared source index covered the publication
request. That checkpoint must process every earlier child fact, so it is withdrawn:
it is a hidden all-child dependency even if the handler reads only one witness.
The following targeted-pin alternative avoids making that wait a publication guard.

1. At admission the trusted handler authenticates the exact issuer/subject and
   obtains a coherent, current source decision for the fixed participants: its
   identity-to-user link and applicable session/identity vetoes, that user's
   operator grant, the authoritative policy generation and applicable global
   vetoes. It durably binds those exact source generations/versions, actor,
   operation ID and complete intent to `CriteriaPublicationStarted` for one
   identified attempt and the edition's exact draft generation, revision and
   confirmed current label-claim generation. A user-supplied actor, stale JWT,
   isolated operator read or Boolean cached projection is not this proof. The edition admits the start only after
   applying the exact claim confirmation; it excludes rename and abandonment
   until that attempt reaches its terminal outcome. Independently owned entries
   remain editable; there is no global child gate or manifest. Each attempt has
   a stable token, a monotonically increasing per-edition-generation attempt
   ordinal, and an exact intent fingerprint for changed-intent rejection. The
   ordinal and generation come from the committed edition start, never from a
   caller-selected or guessed value.
2. The one selected accepted, active criterion owner durably pins its exact
   incarnation to this edition generation and attempt. On that same stream, a
   removal either commits first and makes the pin refuse, or the pin commits first
   and removal is refused while it remains pinned. A point of focus is not a
   nonempty criterion witness. A timeout means the exact pin outcome is unknown;
   reconcile it rather than assuming success or releasing anything.
3. The edition verifies the trusted admission binding, exact committed witness
   pin and exact confirmed label claim for this attempt, not an advisory
   projection. A separate label pin is unnecessary: the label owner releases the
   confirmed current generation only on the exact committed edition rename-away
   or abandonment, and the pending publication attempt excludes those
   transitions. Cancellation of a not-yet-activated selection cannot release
   the current confirmed generation. No actor owner receives an event per
   publication or child edit.
4. The edition commits `CriteriaEditionPublished` using its expected revision.
   Its committed source position **C** is the **sole published-edition cut**;
   the event cannot encode its own not-yet-assigned scope position. It does not
   read, enumerate, wait for, or checkpoint other child intents or outcomes.
   The operator authorization precedes this append and is bound to the exact
   attempt; the pinned witness cannot be removed in the intervening interval.
5. A qualifying entry intent ordered before C contributes its independently
   decided state at C even when its own terminal outcome settles later. An
   intent after C is refused for that published generation. That classification
   may wait on an exact source-position resolver or qualified index **for the
   individual entry**; pending stays pending and does not delay publication. A
   read model may independently wait for source coverage to claim a complete
   catalog. The product owner selected this timing; it changes a contract that
   would require all child outcomes before publication.

The proposed coherent admission uses **two fresh linearizable collections** of
that exact fixed set of participants. Finish the first collection, sample trusted
time **T**, then collect the same participants again. Every source must report
the same non-resettable generation and monotonic version in both passes, and
every applicable veto mutation must advance its source version. Under those
conditions each source was stable across T; verify at T that the authenticated
issuer/subject still maps to the same user, the session and identity have no
applicable veto, the operator grant is active, the applicable policy generation
permits the operation, and the JWT signature, issuer and audience are valid with
`not_before ≤ T < expires_at`. This is a fixed set selected for one action, not
a collection of criteria. A changed version requires retry or refusal; missing,
unversioned, stale or unavailable authority fails closed. A time-dependent veto
that can change without a tracked version must have an equivalent proven
authoritative check, not be inferred from equal Boolean results.

Authorization occurs at this coherent **T at admission**. A revocation denies
later admissions; an already admitted exact operation may finish after
revocation. This is an explicit security/product policy choice, not a consequence
of a stale read. If a crash occurs before the
intent is durably recorded, a later attempt must first reconcile unknown commits
and, once absence is established, reauthorize; it cannot reuse an unrecorded
read. Even an exact retry requires current authority before its response can be
disclosed; that check includes current identity, session, grant, policy and veto
authority. If policy instead requires revocation to invalidate an already
admitted operation before its final edition commit, this approach is insufficient
and needs a different proven fence or atomic guard. The source currently does
not prove all these linearizable reads, non-resettable source generations, a
complete fixed veto set, a reliable trusted T or trusted admission binding; these
are implementation gates, not properties of the existing Boolean access check.
See [criteria authorization](criteria-authorization-design.md) for the full
recovery and source qualification contract.

### Abort, delayed pin and recovery

`CriteriaEditionPublished` and `CriteriaPublicationAborted` for the same pending
attempt compete on the **edition stream at the same expected revision**. The
edition can commit the exact terminal abort **before** a witness fence exists;
it must not wait for that fence as an abort guard. Exactly one terminal event
wins. The abort carries its attempt and reason and leaves the edition in
`abort-cleanup-pending`.

That committed abort authorizes the witness owner to durably fence the exact
edition generation, attempt token and ordinal on the same stream that decides
pins. The fence is a real target event even when no pin was ever observed. If a
matching pin already exists, the same target decision releases it and records
the fence; if the fence wins first, a delayed matching pin is refused. A timeout,
empty append, stale read or uncommitted outcome is not a fence. The edition
clears cleanup only after verifying the exact committed target-fence
acknowledgment from the named witness. Until then no next publication attempt
or draft abandonment may begin; its caller can still see the aborted result
with cleanup pending. Unknown target or edition outcomes are reconciled, never
assumed.

The witness may retain the greatest terminally fenced attempt ordinal for its
own edition generation, rejecting any lower or equal delayed ordinal; it need
not retain a set or roster of all earlier tokens. The edition's monotonic ordinal
and the target's fence update must themselves be replay-safe and verified from
the exact start/abort facts. A fence for an earlier token must never clear a pin
for a different, later attempt. This scalar approach is a candidate requiring
adversarial proof, not a reason to discard exact source attribution or retry
evidence.

The committed abort and a late accepted pin each trigger idempotent, exact-token
reconciliation toward the same target fence. Thus a delayed pin that commits
after the edition has aborted cannot be forgotten just because the first abort
callback saw no pin. A delayed `Publish` using the pre-abort expected edition
revision fails; a retry must not reload a later revision and resurrect the
aborted generation. A late actor authorization for that aborted token likewise
cannot reopen the edition or authorize another attempt.

After successful publication, retain the exact-generation pin or an equivalent
permanent local published fence; never unpin into an unguarded writable state. A
late removal is refused or remains pending until exact source ordering proves it
is outside C; it must never be reported accepted into the published edition.
Pin or edition unknowns are reconciled from their durable owners, and retrying a
different intent under the same attempt token is a conflict.

After abort cleanup completes, a separate edition command may rename or abandon the draft. The
label owner releases its confirmed claim only after validating that exact
committed edition transition and the holder/generation. A delayed release for a
prior generation cannot clear a newly reacquired claim. There can be no manual
or operator force-release path that bypasses those checks. If rename or abandon
commits first, its edition revision makes a stale publication start fail; if the
start commits first, rename and abandon wait for its terminal publication or
completed abort cleanup. See [label claim design](criteria-label-claim-design.md).

A point of focus can be accepted only against one same-edition, same-category
parent criterion's exact accepted incarnation, with validity and actor authority
at the required instant. Before publication, removing that parent makes those
references ineffective in the draft view without writing to each child. At C,
the published view evaluates each requested point against its exact parent
incarnation at C. Re-adding the same parent identifier creates a new incarnation;
old points do not silently reactivate. The immutable cut and existing editions
cannot be revised by later draft changes. Each point decision is local to that
point and its one parent; checking a single witness at publication does not prove
all parent relationships. An independently valid admission and the versioned
read rule must preserve that invariant for each effective point.

On a published edition, the program-creation proposal may select the exact
published edition even when unrelated individual entry outcomes remain pending.
A command referring to a particular entry waits for that entry's accepted fact
and exact parent evidence. Readiness and catalog reads expose pending/incomplete
coverage, never convert unknown into absent or accepted, and cannot claim a
complete edition until their source coverage is proved. This is a product choice,
not a guarantee implied by Fitz or by the marker.

## Read shape and performance

The current `CriteriaCatalog` constructs in-memory lists and dictionaries for
the fixed seed. Runtime editions need an indexed source-backed read model with a
stable key such as `(edition, category, identifier, incarnation)` and keyset
pagination or streaming exports. Index construction consumes already ordered
source records; it must not load all entries into memory to sort them. A page
reports its edition cut and whether coverage through that cut is qualified.
Duplicate source delivery, retries, corrections and a checkpoint rebuild must be
idempotent; page cursors must not silently mix versions or skip entries.

The publisher neither waits for every child nor holds all entries. A complete
read or index rebuild can still take time and space proportional to source
history. A single realm-wide ordered processor can become a throughput or
availability bottleneck even with bounded memory; measured throughput,
partitioning, backpressure and recovery are required design qualifications.
An index, version chain or content object that merely repackages every child as
one parent-owned list is unacceptable.

## Race and recovery cases

| Case | Required decision |
| --- | --- |
| Two intents use the same criterion and expected revision | One append wins that criterion's OCC. The other resolves conflict or exact retry; it does not append a second unexamined winner. |
| First intent is invalid or times out | Reconcile its real source decision. A definitive refusal/fence permits a later attempt; unknown remains pending. |
| Accepted incarnation A; replacement R commits before C but settles later | The index at C reports pending until R settles. If accepted, R is effective from its intent position; if refused, A remains effective. Outcome append time is not R's logical membership position. |
| Parent removal R commits before C but settles later | A referenced point remains pending at C. If R is accepted the old reference is ineffective; if refused the accepted old parent remains effective. An index checkpoint through C alone does not establish the answer. |
| Parent is removed after a point's acceptance but before C | The point's old-incarnation reference is ineffective at C. Re-adding the parent under a new incarnation does not revive it. |
| Witness removal wins before its pin | Pin refuses; choose another valid witness or add one. |
| Witness pin wins before removal | Removal is refused while the pin is held; publication may proceed without reading other entries. |
| Operator grant, linked identity, session, policy or veto changes between the two admission collections | The changed generation/version prevents that collection from admitting the operation; retry from fresh authority or fail closed. A Boolean that changes and later returns to its previous value is insufficient. |
| Actor revocation races admission | A revocation still in force at coherent admission T refuses admission; a later valid regrant is a new generation. An exact operation admitted before revocation can finish under the explicit admission policy; response disclosure on retry still requires current identity, session, grant, policy and veto authority. |
| Claim release races publication start | An exact committed rename-away/abandon wins before start, or the start wins and those transitions wait for its terminal result. Delayed release for an older generation cannot release the current claim. No separate publication pin is needed. |
| Marker is observed but one child outcome is delayed | The edition may be published if its own witness decision is accepted. That child's state remains pending to consumers until its exact outcome settles; coverage is not complete. |
| Intent, publication or outcome commit response is lost | Reconcile the same identity, exact payload and committed record. Do not create another intent, free a pending slot or infer a refusal. |
| Outcome append succeeds before checkpoint and worker crashes | Replay the same token; recover the existing outcome and safely advance the checkpoint without recording a second decision. |
| Index has not observed C or has a source gap | Publication may already be durable; individual entry classification and complete catalog claims remain pending or error. Do not infer a cut from a quiet subscription or zero projected rows. |
| Abort wins before its pin has arrived | Commit the edition abort first and show cleanup pending. The witness durably fences that exact attempt even if the pin is absent; only the verified target fence acknowledgment clears cleanup. |
| Witness pin commits after abort, before the target fence | Pin acceptance and the exact committed abort each cause idempotent reconciliation; the target atomically releases the exact pin and fences its token. |
| An old pin arrives after cleanup and a newer attempt | The witness's verified monotonic fenced-ordinal guard refuses it without clearing or changing the newer attempt's pin. |
| A removal arrives after accepted publication | Keep the permanent generation fence or resolve exact source order; never report a post-C removal as accepted into the immutable edition. |

## Evidence and remaining qualification

`Directory.Packages.props` pins Portia 0.10.0 and `compose.yml` pins the Fitz
0.4.0 image digest. Portia's tagged `FitzEventStore` appends one resource at one
expected physical stream position, treats an empty append as no fence, and its
pattern reader checks contiguous scope offsets. Fitz 0.4.0's tagged stream spec
and routing notes describe committed area/realm ordering. Those sources do not
establish atomic cross-resource compare-and-append, the application's exact
event routing, an as-of authorization index, atomic index/checkpoint persistence,
deployed retention or this protocol's crash behavior. A local newer Fitz source
suggests exact resource records may also carry area/realm offsets; Portia's
`DomainEventRecord` exact-resource API does not expose those offsets. Any
Fitz-specific resolver or upstream extension must be separately qualified against
the actual pinned client and deployed broker; do not assume it is available.

The earlier synthetic [Jev request](criteria-publication-protocol-jev-request.json) and
[response](criteria-publication-protocol-jev-response.json) test concrete races
under the stated assumptions. Jev returned 0.04 for both competing appends
committing at the same expected revision, 0.08 for a pre-marker removed witness
being valid, 0.02 for cross-realm timestamps proving authorization, 0.05 for a
replayed token requiring a second decision, 0.02 for implicit restoration under
a new parent incarnation, and 0.01 that the assumptions prove deployed behavior.
They tested the now-withdrawn shared-checkpoint candidate and are review signals,
not proof of atomicity, correctness or policy approval.

The [witness-pin request](criteria-witness-pin-jev-request.json) and
[response](criteria-witness-pin-jev-response.json) test the alternative's explicit
scenarios. Jev returned 0.82 that a shared checkpoint must process the earlier
child event, 0.06 that the pinned witness can be removed before publication,
0.06 that the pin requires checking every child, 0.10 that a delayed append at
an obsolete edition revision succeeds after cancellation, and 0.03 that a lost
pin response proves safe release. Its authorization scenario used an earlier
operator-owned authorization-event variant, which has since been replaced by
the current-read policy above; that result does not validate the selected
mechanism. Jev gave 0.07 that a stale actor read proves commit-time authorization.
The answers assume the stated owner rules and do not establish their existence,
provider behavior, idempotent recovery or acceptance of the authorization policy.

The [admission request](criteria-admission-jev-request.json) and
[response](criteria-admission-jev-response.json) test the selected hypothetical
current-read policy: Jev rated an already admitted exact operation finishing
after revocation 0.97; a new admission after revocation 0.09; a delayed
projection proving current authority 0.08; reuse of an uncommitted old read
after crash 0.05; and disclosure to an unauthorized retrying actor 0.05. Its
0.04 for the hypothetical establishing deployed linearizability reinforces the
need for source and executable qualification; none of these scores proves it.
Those examples examined an operator-only current-read sketch and do not by
themselves establish the selected coherent multi-source identity/session and
policy admission described above.

The [abort-cleanup request](criteria-abort-cleanup-jev-request.json) and
[response](criteria-abort-cleanup-jev-response.json) separately exercise
edition-abort-first recovery. Jev rated needing the target fence before the
edition abort 0.03, declaring cleanup complete without the target fence 0.04,
late-pin reconciliation 0.87, an old retry clearing a newer pin 0.04, and a
timed-out target fence proving cleanup 0.02. Its 0.42 on whether a pin could
succeed after its durable fence is inconclusive despite the stipulated owner
rule; retain the concrete race and require executable proof. The assumptions do
not establish that the application already provides this fencing.

Before implementation acceptance, demonstrate exact-scope ordering and completeness with the
pinned deployment, coherent identity/session/grant/policy/veto admission at T,
non-resettable version and trusted-clock assumptions, label
claim generation/release proof and witness pin/fence recovery, all relevant routes, per-identity expected
revisions and refusal fencing, parent-incarnation as-of behavior, edition-abort-
first cleanup, absent-pin target fencing, monotonic fence ordinals, delayed pin
and abandonment races, per-entry source-position resolution, projection
pagination without bulk sorting, duplicate and changed-intent delivery, and
unknown commit/outcome recovery. The selected product semantics allow program
creation and delayed individual outcomes as described above. The current code
and provider evidence do not yet prove this complete protocol.
