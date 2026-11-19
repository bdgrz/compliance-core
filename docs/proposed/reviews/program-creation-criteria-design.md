# Program creation with a published criteria edition

Status: **product behavior selected; criteria publication authority still
requires qualification**, 2026-11-05. This review specifies the creation
contract and its dependency on publication. It does not establish that the
deployment, authorization framework, or source reader provides the required
guarantees. The selected behavior is reflected in the parent context; it is not
a claim that the implementation is complete.

## Decision

An authorized organization user may create a program as soon as its chosen
edition has an **authoritative, irreversible published fact**, even if outcomes
for unrelated criteria entries are pending. Program creation decides one fact:
which published edition this program selects. It does not enumerate the edition
or assert that all entries, framework coverage, or downstream readiness inputs
are complete. It can start the user's compliance effort while individual
criterion outcomes settle.

This decision is conditional on publication itself being valid and final: the
publication owner must have actually accepted the edition, its nonempty witness
and immutable-membership invariant must hold, and its published status must not
be retractable. A raw marker whose membership, parent validation, authorization,
or source ordering remains ambiguous is **not** an authoritative published
fact. The current CR-09 candidate has not yet proved those conditions.

This intentionally replaces the current static-catalog selection gate. Current
`SelectProgramCriteriaEditionHandler` rejects `!edition.IsComplete`. The seed
sets `IsComplete` based on its numbered criteria while separately disclosing
partial points-of-focus coverage and unsupported Privacy lifecycle. Neither the
current flag nor publication alone establishes full framework coverage. Retain
the seed disclosures and expose incomplete or unknown coverage independently.

## Command, authority, and outcomes

- The caller supplies `TenantId`, stable `ProgramId`, `EditionId`, name and plan.
  The canonical HTTP endpoint remains a client-identified PUT under the tenant
  and program IDs; the MCP tool accepts the same typed identities and behavior.
  The body and route identity must agree. Edition is platform-level. The
  organization authorization decision must coherently include active tenant,
  current membership and organization-wide program-management permission. A
  program-scoped grant cannot create another program.
- The handler reads the **exact edition owner source**, not a catalog projection
  or a caller-supplied assertion. Verify a committed, valid publication for that
  exact `EditionId` and retain its immutable publication fact identity/version
  as the selection evidence. Because publication is irreversible, that positive
  fact remains true while the program owner appends; this is a monotonic
  reference, not a general license for cross-aggregate read guards. Resource
  stream positions and area/realm positions are different coordinates. Do not
  invent or compare a scope cursor from the direct edition read.
- A current source-confirmed draft or absent publication is a refusal of this
  attempt, with no `ProgramCreated` fact. Later publication can make the same
  still-uncreated program ID and intent eligible for a new attempt. A read
  timeout, unknown commit, storage failure, or uncertain source result is pending
  or retryable, never proof that the edition is unpublished. Do not turn absence
  into a permanent cached refusal under the caller's ID.
- Create the tenant/program owner under the exact expected stream position:
  initially zero; after a confirmed creation-attempt fence, the successor
  position. `ProgramCreated` records tenant, program, exact edition and
  publication evidence, normalized name/plan, creation actor, attempt generation,
  and the original response or durable response reference. A successful response
  means the program fact committed; a fence alone is not success. Its list
  projection may still lag. Immediate reads use the source-backed owner or a
  proven catch-up path and remain tenant-authorized.
- A currently authorized exact retry of `(TenantId, ProgramId)` with the same
  normalized authored intent returns the original response and writes no new
  creation fact. The normalized intent includes `EditionId` as well as name and
  plan. Once a `ProgramCreated` or creation-attempt fence has durably bound the
  ID, changing edition, name, or plan conflicts. A transient admission with no
  durable program-stream fact is not such a binding. Concurrent first requests
  use the program stream's optimistic concurrency: the loser reloads the actual
  committed winner and either returns that original response or conflicts on
  changed intent. An unknown append is reconciled against source; no new program
  ID is silently minted. Current authorization is checked before exposing the
  original result on a retry.

### Authorization admission and revocation

The selected rule is **authorization at admission**. After verifying the exact
irreversible publication fact, the request obtains one fresh, coherent,
source-authoritative allow for this actor and this creation intent. At the
decision instant the authenticated issuer/subject remains attached to the exact
platform `UserId`, any applicable identity or session has not been revoked, the
tenant is active, the actor's exact membership episode is eligible, and the actor
has effective organization-wide `program.manage`. An already-issued token does
not bypass source identity or session revocation for a new admission. The
current product excludes guest and firm-staff memberships from this creation
path. A grant scoped only to an existing program is insufficient. The allow
records actor, tenant, episode, full normalized intent and the exact bounded
source evidence used for the decision.

The admission instant is the authorization service's linearized decision, not a
prior HTTP authentication check or whichever source read finishes last. A
revocation committed and ordered before that decision prevents admission. If a
revocation is ordered **after** an allow, the same uninterrupted in-flight
attempt may append `ProgramCreated`; the later revocation blocks new admissions
but does not retroactively cancel this attempt. `ProgramCreated` remains the
single durable admitted-intent and final creation fact. There is no separate
durable admission receipt, child ledger, or `ProgramCreationRequested` stream.
The transient allow authorizes an attempt; it does not reserve the ProgramId or
guarantee success against a competing creation or recovery fence.

If the process crashes before a known program commit, its transient allow is not
reused: a new attempt must obtain current authorization. If the append outcome
is unknown, reconcile the exact tenant/program source and any durable intent
binding first. A request's current intent is not proof of an unrecorded prior
attempt's lost intent.
A committed `ProgramCreated` stands. An empty read alone does not establish
that an earlier append cannot still commit; a new append is permitted only after
definitive settlement or a committed same-stream attempt fence as described
below. A caller must pass **current** authorization before receiving the original
response, even though a committed creation remains valid after a later revocation.
Internal recovery may reconcile the outcome without disclosing it to an
unauthorized caller.

### Resolving a possibly in-flight creation

An old admitted append can remain in flight after its caller crashes or times
out, including after an authoritative read returned an empty stream. Returning
`not created` or starting a replacement based only on that read is unsafe. The
recovery process uses the client-supplied `(TenantId, ProgramId)` and the
recovering request's normalized intent; it never allocates a replacement program
ID. A prior request's intent or attempt ID can be treated as known only when a
trusted retained record proves it. Without such a record, no uncommitted intent
has bound the ID, even if a different prior append may still be in flight.

1. Read the exact program stream and reconcile any committed `ProgramCreated`.
   A matching successful creation returns its original response only after
   current authorization; changed intent conflicts. A source read that is
   missing, stale or unavailable is not settlement.
2. If an earlier append's outcome remains unknown, obtain provider-confirmed
   definitive settlement **or** race all potentially in-flight writers at that
   generation's exact expected stream position with an internal
   `ProgramCreationAttemptFenced` fact. The authenticated, currently authorized
   recovering request supplies its own normalized intent. The fact binds tenant,
   program, the closed generation/expected position, that **presented** intent's
   digest and recovery provenance. The recovery identity must be deterministically
   recoverable from the client program ID, generation and presented intent; the
   client is not asked to invent an additional ID. For an initially empty stream,
   every old `ProgramCreated` at position zero and the fence compete at position
   zero. A prior attempt ID or its intent may be named as fact only when supported
   by trustworthy durable evidence; the recovery payload alone does not prove it.
3. If `ProgramCreated` wins, the fence conflicts; reload and compare that
   committed creation against the recovering request. Same intent may recover
   the original response after current authorization; different intent conflicts.
   If the fence wins, every old append at that expected position must be rejected.
   **An old attempt may never rebase** to the new position on conflict. It is
   terminally fenced even if it later wakes up, its transport retries or its
   original authorization was valid. The fence establishes no program and must
   not produce the creation response.
4. A committed fence binds this program ID to the recovering request's recorded
   normalized intent; it does not assert the content of an unknown prior attempt.
   A changed-intent request conflicts. An exact-intent attempt can use the same
   client ID in a new generation, but must verify the publication evidence and
   obtain fresh coherent authorization before appending at the fence's successor
   position. A no-fact refusal before admission, by contrast, creates no ID
   binding; it does not prove a concurrent old append cannot still commit. Bind
   every new attempt to its exact generation and expected
   position; no automatic expected-version refresh may revive a fenced attempt.
5. If the fence append itself has an unknown outcome, remain pending and
   reconcile its exact attempt identity. Another empty read does not prove the
   fence cannot still commit. Deterministic retries at the same expected position
   can compete, but neither a new generation nor a success response is reported
   until source confirms which fact won. Repeated retries of one generation do
   not append repeated fence facts.

Fitz 0.4.0 specifies expected-offset conflicts for competing commits, and Portia
0.10.0 passes expected physical positions to its Fitz append. The application
must still enforce the generation and no-rebase rules in every writer and qualify
the configured provider's conflict, durable-read, retention and unknown-commit
behavior. The owner retains its own creation/fence lifecycle facts, not a list of
children. This recovery contract does not itself prove provider deployment or
authorization semantics.

A fence-only stream is **not a created program**. The program owner's replay,
native operation preparation and history verification must accept a fence before
`ProgramCreated` while keeping `IsCreated` false; source-backed scope resolution,
directory projections and reads must not create or expose a program merely
because its stream is nonempty. Physical source offsets advance for fence facts;
they are not automatically program business revisions. Serializer, event
registration and initial/retry response handling must support this distinction.

For example, attempt A for intent A may still be in flight while an authorized
recovery request for the same ID supplies different intent B. With no earlier
durable fact, the system cannot infer A's contents. If A's `ProgramCreated`
commits first, its fact binds A and B conflicts. If B's fence commits first, it
binds B and fences **all** old position-zero writers; B may then retry after
fresh authorization, while A cannot rebase. B's fence proves neither that A
existed nor that A carried B's intent. If either outcome is unknown, both remain
unresolved until the actual committed winner is read or provider settlement is
otherwise definitive. Only a durable intake protocol, if separately designed
and implemented, could bind or recover A before either program-stream fact; no
such intake or durable admission receipt is assumed here.

The required **program-create authority service contract** is narrowly scoped:

1. `AdmitProgramCreate` receives authenticated actor, tenant, program, and a
   digest of the exact normalized creation intent, and returns allow, definitive
   refusal, or unknown/unavailable. It cannot substitute another intent or actor.
2. For allow, it proves a single coherent source-current decision point across
   all facts that can invalidate access: authenticated issuer/subject-to-UserId
   binding, sign-in identity and applicable session revocation, tenant activity,
   user/member eligibility and episode, and a complete qualifying organization-
   wide grant path. It orders that decision with every relevant identity or
   session change, grant, revocation, suspension, team/role deletion, permission
   change and tenant-state change. Every writer of those
   facts must participate. A current-looking collection of separately read owner
   versions is not automatically a coherent or fresh point; the two-pass
   candidate below gives a precise conditional way to establish one.
3. A bounded positive path is sufficient when the access policy's applicable
   global restrictions also hold. A primary-admin path binds the current Member
   mark to the matching confirmed `PrimaryOrgAdmins` designation. A team path
   binds the current episode and TeamMember, active Team, organization-scoped
   TeamRole assignment, active Role and the `program.manage` RolePermission.
   The exact permitted alternatives and any overriding deny must come from the
   access contract, not be guessed by the program. Built-in role permission maps
   are authoritative only under a pinned, provable access-policy version. This
   validates **one** path;
   it does not collect or load every role, grant or team onto Program.
4. A read index may find candidate paths in bounded pages. Each chosen path must
   be verified by the coherent authority at the decision point. If a lagging
   index finds no path, report proof unavailable or continue discovery; absence
   from that index cannot prove denial. A definitive no-grant decision requires
   a separately qualified complete source of relevant paths. No program write
   scans or sorts all grants in memory to establish its invariant.
5. Return bounded immutable evidence for the exact allow and its source decision
   point; bind that evidence to `ProgramCreated` so replay and audit can verify
   the original admission. A transient allow that never yields a committed
   ProgramCreated is not a reusable durable authorization. Unknown or incomplete
   source ordering fails closed; do not persist an authorization claim merely
   because a projection or dispatch succeeded.

### Candidate: two-pass coherent positive path

An authority service can test one bounded candidate path without an atomic
multi-stream transaction **if** its exact-source reader provides the fresh,
linearizable latest-version contract below. The candidate path can be discovered
from a paged advisory index; the index itself supplies no authority.

1. Enumerate the **fixed** owner set for this path and every applicable global
   veto: the active Tenant and exact eligible Member episode; the authenticated
   issuer/subject's current `SignInIdentity` bound to the same
   `UserId`; each applicable token/session-revocation authority; either the
   matching primary designation or one TeamMember, active Team, organization-
   scoped TeamRole assignment, active Role and `program.manage` RolePermission;
   the authoritative current built-in access-policy generation; and every other
   fixed veto owner the access policy requires. Token signature, issuer, audience,
   validity time and applicable session constraints must also be verified for T;
   a valid signature alone is not proof of current identity or session authority.
   The veto-owner set must be authoritative, closed and bounded for this actor
   and operation. If the policy allows an unenumerated or potentially unbounded
   set of independent veto sources, this proof is incomplete; define an
   authoritative per-actor/per-session veto owner or another qualified bounded
   authority first.
   A path-specific owner that is absent or unavailable does not silently authorize.
2. First pass: fresh, linearizable source-read every exact owner, retaining its
   identity, physical stream position, source generation and relevant state. A
   source read must establish the latest committed state at its own read instant,
   including when the owner is absent; a cached projection does not qualify. If a
   read is unavailable or a proof cannot be obtained, fail closed or retry.
3. After **all** first-pass reads have completed, record trusted time `T`, then
   begin the second pass. Fresh-read the same owners again. Every owner must have
   the identical physical position and source generation as in its first read.
   Evaluate identity, session, tenant, membership, scope, grant and all veto
   predicates, including time-dependent expirations, **at T**. Membership's
   current date rule uses
   `DateOnly.FromDateTime(T.UtcDateTime)`; a local-time date or a date sampled
   before T is not equivalent. Same boolean state is insufficient: a revoke and
   regrant between reads is detected by the advanced position.
4. If every comparison succeeds, every owner's first read completed before T and
   its unchanged second read occurred after T; with monotonic non-resettable
   positions, each required fact held simultaneously at T. The allow is certified
   only **after all** second reads succeed, while T remains the admission instant.
   All path joins must agree: token issuer/subject → exact live SignInIdentity →
   UserId → same-tenant Member and current episode; TeamMember → that Member and
   Team; TeamRole → that same Team, active Role and tenant-wide scope; and
   RolePermission → that Role and `program.manage`. For a primary-admin path,
   `UserId`, Member episode and designation generation must match the confirmed
   primary seat. Facts that are individually valid but belong to different
   actors, tenants, episodes, teams, roles or scopes cannot be spliced into a
   proof. Retain the small evidence tuple and access-policy version with
   `ProgramCreated`.
   The candidate works across realms under these explicit real-time, linearizable
   read assumptions; a shared cursor is not needed for this fixed path.
5. A mismatch, timeout, unknown generation or unavailable veto is not an allow;
   retry with a new path and T or report pending/unavailable. A revocation before
   T is observed or changes a position. A revocation after T but before its
   owner's second read causes a mismatch and requires retry; one after that
   owner's second read may still coexist with a successful proof of the earlier
   T. Neither case changes the selected rule: after a successful certification,
   the admitted attempt is authorized **at T**, not at the eventual append.

The authority service must attest the actual pass ordering, T, exact owner
identities, positions/generations, actor binding, intent and evaluator policy
version as one bounded proof bound to the request. `ProgramCreated` retains that
trusted service attestation or an exact immutable verifiable reference; it does
not create a separate admission stream. Historical event versions and a recorded
timestamp alone cannot prove that the two fresh reads were really performed in
the required order. The service and its attestation trust boundary, integrity,
recovery and source guarantees require qualification; a signature does not prove
an incorrect implementation performed the checks.

This is a conditional algorithm, not a claim about current Portia behavior.
Qualify genuinely fresh linearizable latest reads for every involved source,
monotonic physical positions and immutable source identities with no reset or
recreated-stream ABA, complete fixed veto enumeration, a trusted T for expiry,
all writer paths, and retained/verifiable proof. Every authority-changing fact
must advance the physical position being compared or be immutable content whose
exact identity and bytes are verified and bound by that position. A revocation
in an untracked audit stream, a mutable external content object, or an out-of-band
permission change can invalidate equal-position reasoning. A lagging checkpoint
or repeated stale reads can likewise produce matching positions while a
revocation already committed.

The current built-in role/permission policy generation is a **fixed two-pass
participant**, read from an authoritative version owner or equivalently qualified
source along with the other owners. Bind its generation and immutable content
identity into the evidence. Recording only the local binary's version is not
sufficient: the authoritative policy could have advanced from V1 to V2 before T
while a stale node still evaluates V1. The admission service must implement and
verify the exact authoritative generation at T; a mismatch, unknown generation
or unverifiable policy content fails closed. This includes the direct
primary-admin permission path.

A rolling deployment needs a writer barrier or an equivalent qualified routing
mechanism: do not activate an authoritative new policy generation until all
instances that may decide under it can evaluate its exact immutable content.
Otherwise route exclusively to compatible instances and fail closed on any
unsupported generation. A transition between the two reads changes the
authoritative policy owner's position and forces retry; a transition after T is
handled by the selected admission-at-T rule. Whether such an authoritative
version owner and cutover mechanism exist is an implementation qualification,
not an assumption about the current in-memory role tables.
The fixed number of owners prevents an all-grant write decision, but each owner's
own replay cost still depends on its history; qualify the actual reader and
bounded candidate discovery rather than assuming constant work.

An implementation may satisfy the contract with this qualified two-pass service
or another proved source-consistent ordering mechanism. Ordinary separately timed
re-reads, a lagging projection with an unqualified checkpoint, or an invented
cross-stream transaction do not satisfy it. Current `ProgramManagementAuthorizer`
and `TeamRolePermissionAuthorizer` read several sources; the latter currently
materializes team-role assignment history while finding paths. They do not by
themselves establish the required common decision instant or bounded discovery.
Portia's single-stream optimistic append does not itself establish the required
fresh-read contract. The authority service, coherent freshness, evidence retention,
bounded lookup and recovery remain implementation qualification gates.

`SelectProgramCriteriaEdition` uses the same exact published-source check for
an explicit later edition change. Its independently client-supplied command ID,
expected program revision and authored intent govern retries and conflicts; it
records the selected edition and publication evidence on the existing program.
An exact accepted retry returns the original result. Selecting the same edition
does not fabricate a new selection or invalidate its prior evidence; stale
revision, changed intent and ambiguous append follow the program's documented
operation rules. It is not a way to upgrade an old program silently.

## Pending entries and downstream behavior

The publication contract may decide a valid pre-marker participant as a member
before its individual outcome is materialized. A downstream command that acts on
one criterion needs that participant's durable accepted outcome and authority
that its exact winning proposal belongs to this edition's publication boundary.
A point of focus additionally needs its exact accepted same-edition, same-category
parent incarnation to be effective at the same published cut. An accepted parent
removed before the cut is not enough. `CriterionChanged` observed on its own is
insufficient if the accepted proposal's ordering relative to the publication
marker is not established. The earlier synthetic Jev request calls this outcome
`CriterionAdded`; that name is historical request text, not the canonical event.

For the exact item, distinguish accepted, refused/nonmember, and pending/unknown.
An unavailable or lagging projection is not evidence of absence. Catalog
enumeration exposes a qualified coverage frontier and incomplete status; a
missing item cannot be presented as inapplicable. Applicability, controls, and
readiness must not claim complete criteria coverage while source discovery or
required individual outcomes remain unresolved. A committed ProgramCreated may
be used for unrelated setup, but no dependent decision may bypass its own
criterion proof. A page or cursor alone does not prove coverage.

## Existing records and migration

Current `CreateProgram` has no edition parameter; `ProgramCreated` records no
edition and `SelectProgramCriteriaEdition` is a later operation. Preserve old
event schemas and historical digests. An old creation fact must replay as it
actually occurred: if no later selection exists, its edition remains
`legacy_unknown` and edition-dependent work stays blocked. Existing authorized
selection is an explicit, attributed transition to a verified published edition;
do not insert a default into an old fact or silently claim it was always chosen.
If a later historical selection exists, replay that recorded selection under its
historical contract and explicitly reconcile its publication evidence; do not
invent a missing proof. Preserve the old creation retry's original response and
intent under its historical request version; it cannot be retroactively compared
to a newly mandatory `EditionId`. The cutover needs an explicit wire/API version
or compatibility adapter so existing callers and histories are not silently
reinterpreted.

The new creation shape changes authored intent and affects `CreateProgram`,
`ProgramCreated`, owner/replay/history verification, directory projection and
view, native first response, HTTP/OpenAPI, MCP and contract tests. The current
name normalization must remain consistent on initial creation and retry. The
legacy nullable edition read state must remain explicit until selection. Program
creation must not automatically select a default edition for the user.

Current-source pointers: [CreateProgram](../../../src/Compliance.Common/Features/Programs/Commands/CreateProgram.cs),
[ProgramOperations](../../../src/Compliance.Core/Features/Programs/Services/ProgramOperations.cs),
[ComplianceProgram](../../../src/Compliance.Core/Features/Programs/Aggregates/ComplianceProgram.cs),
[ProgramReader](../../../src/Compliance.Core/Features/Programs/Readers/ProgramReader.cs),
[ProgramManagementAuthorizer](../../../src/Compliance.Core/Features/Programs/Authorizers/ProgramManagementAuthorizer.cs),
[SelectProgramCriteriaEditionHandler](../../../src/Compliance.Core/Features/Programs/Handlers/SelectProgramCriteriaEditionHandler.cs),
and [PlatformCriteriaCatalog](../../../src/Compliance.Core/Features/Criteria/Services/PlatformCriteriaCatalog.cs).

## Example outcomes

| Situation | Result |
| --- | --- |
| Exact published edition, one unrelated entry pending | Create the program; report the pending entry and incomplete coverage separately. |
| Draft edition, then later published | Refuse the first attempt without creating a program; a new attempt using the same uncreated program ID may succeed. |
| Edition source times out | Pending/retryable; do not create based on a guessed state and do not permanently reserve the ID as failed. |
| Same ID, same normalized name/plan/edition after a lost response | Return the first committed response after current authorization; no second event. |
| Same ID, different edition | Conflict; do not return the first response as if it applied to the new intent. |
| Two concurrent creates for one tenant/program ID | One append wins; compare the loser's full normalized intent against the committed winner. |
| Empty read while an earlier create is still in flight | Outcome remains unknown; settle or race a same-position fence before any new attempt. |
| Original creation wins the expected-position race | Reconcile its committed intent and original response; the competing fence cannot commit at the old position. |
| Same-position fence wins first | The old attempt must never rebase; an authorized exact-intent new generation may create using the same client program ID. |
| A's intent and attempt ID were lost; authorized B supplies a different intent | Do not infer A's intent. If A commits first, B conflicts; if B's fence wins, it binds B and closes all old writers at that position. |
| Fence response is unknown and the source currently reads empty | Stay pending and reconcile the fence; do not infer it will not commit. |
| Changed intent after a fence has bound the program ID | Conflict; the fence is not a successful creation or a license to reuse that ID for different intent. |
| Revocation commits before authorization admission | Refuse; a stale grant or mixed-time path cannot authorize. |
| Revocation or revoke/regrant occurs between first read and that owner's second read | The physical position differs; the two-pass proof fails even if the boolean state matches. Retry with a new T or refuse. |
| Token remains signed and unexpired, but its exact sign-in identity or session was revoked before T | Refuse or remain pending until the authoritative identity/session state is proven; an already-issued token alone cannot authorize. |
| Candidate combines a valid member with another user's team membership, another tenant's role, or a different membership episode | Reject the spliced path; all identity, tenant, episode, team, role, scope and permission joins must match. |
| Persisted evidence names old source versions and T but no trusted attestation of the two-pass ordering | Do not claim that the live two-pass admission was performed; historical positions alone are insufficient. |
| Member expires after its first read but before T | Evaluate eligibility at T and refuse; matching positions do not extend time-based access. |
| UTC midnight passes between first read and T | Evaluate `DateOnly` from trusted T in UTC; a membership expiring before that date is not extended by an unchanged stream position. |
| A permission changes only out of band or a rollout changes its unpinned role table | The compared versions do not prove the authorization decision; fail closed until the authority-changing state and applicable policy version are bound. |
| Authoritative built-in policy changes V1→V2 before T while a node still runs V1 | Reject or route to an instance implementing the exact authoritative V2 generation; matching tenant and role versions do not make V1 current. |
| A required owner times out, is stale, resets its position, or a global veto is not checked | No certified allow; fail closed or remain pending until the service can establish the complete source-current proof. |
| A qualifying grant exists but the paged discovery index misses it | Do not assert that no grant exists; continue qualified discovery or report proof unavailable. |
| Revocation commits after a coherent allow, before an uninterrupted append | Permit that exact admitted attempt; subsequent admissions use current authorization. |
| Crash before known commit, then revocation | Reconcile any unknown append; if no ProgramCreated committed, discard the old allow and reauthorize before a new append. |
| Successful original commit, then caller is revoked and retries | Keep the committed fact; do not expose its original response without current authorization. |
| Legacy creation has no selection event | Replay `legacy_unknown`; block edition-dependent decisions until an explicit published-edition selection. |
| Child outcome exists but its proposal may be after publication | Pending/unqualified for mapping until membership relative to the exact marker is established. |
| A relevant entry is pending while calculating readiness | Incomplete or unknown, never absent or not applicable. |

## Adversarial evidence and remaining gates

The live TypeSafe request and response are
[program-creation-criteria-jev-request.json](program-creation-criteria-jev-request.json)
and [program-creation-criteria-jev-response.json](program-creation-criteria-jev-response.json).
For the explicitly conditional synthetic cases, Jev 1.14.0 returned 0.93 that a
program can bind an irrevocably published edition despite unrelated pending
outcomes, and 0.08 that a prior read remains safe if publication can be
retracted. It returned 0.08 that pending means absent, 0.05 that mapping can
proceed without required proof, 0.04 that a child outcome alone proves ordering,
and 0.11 that changing the edition is an exact retry. It returned 0.05 that an
old event can be given a default edition, 0.04 that a prior authorization read
orders a later append before revocation, 0.03 that numbered criteria imply full
points-of-focus/Privacy coverage, and 0.05 that a source timeout proves absence.
These numbers are model signals about the stated premises, not proof of the
premises, runtime storage semantics, authorization, or correctness.

The separate [authorization request](program-creation-auth-jev-request.json)
and [response](program-creation-auth-jev-response.json) explicitly test the
selected admission policy. Jev returned 0.97 that a continuously running attempt
may finish after a later revocation, 0.02 that a restarted precommit attempt can
reuse the old allow, and 0.97 for reconciling an unknown append first. It returned
0.01 for using a stale permission after an earlier revocation, 0.87 that one
coherent complete positive path suffices under the stated assumptions, 0.02 that
mixed-time reads prove coherence, and 0.02 that a program-scoped grant authorizes
creation. It gave 0.20 to returning a retry response without current
authorization; the product rule requires current authorization regardless of
the model. These scenarios assume the authority contract; they do not prove it
exists or that any particular implementation meets it.

The focused [two-pass request](program-create-authority-two-pass-jev-request.json)
and [response](program-create-authority-two-pass-jev-response.json) tested the
conditional positive-path proof. Jev returned 0.07 that comparing boolean states
can safely replace positions after revoke/regrant, 0.05 that equally stale reads
establish fresh authority, 0.04 that an expired member can be admitted at T,
0.04 that an omitted veto is safe, 0.04 that a lagging index miss proves no grant,
and 0.03 that unrelated grants must all be read. Its 0.78 answer for the
cross-realm case is an inconclusive model signal. The overlap argument depends
on the stated linearizable-read, monotonic-position and trusted-time premises;
the model does not establish any of them.

The [identity/path request](program-creation-identity-jev-request.json) and
[response](program-creation-identity-jev-response.json) test the additional
actor and evidence requirements. Jev returned 0.03 that an unexpired token alone
authorizes after identity or session revocation, 0.03–0.06 that paths spliced
across users, teams, tenants or episodes authorize, 0.04 that unchecked vetoes
are safe, and 0.03 that old positions plus T prove the reads occurred. It gave
0.94 to an actual trusted service attestation recording its checks under the
explicit correctness assumptions, but 0.05 that the attestation itself proves
those assumptions. The source and executable authority contract remain decisive.

The [unknown-append fence request](program-creation-fence-jev-request.json) and
[response](program-creation-fence-jev-response.json) tested the recovery cases.
Jev returned 0.08 that an empty read proves an old append cannot commit, 0.08
that a fence means creation succeeded, 0.86 for returning a confirmed original
result, 0.88 that a newly authorized same-intent successor can use the same ID,
0.06 that an old attempt may safely rebase, and 0.96 that changed intent after a
binding fence conflicts. It returned 0.07 that an empty read proves an unknown
fence settled. Its 0.49 answer on whether an old expected-zero append could
commit after a position-zero fence is inconclusive. The Fitz expected-offset
contract, actual provider behavior and competing-append tests—not that score—are
the authority for the fence mechanism.

The separate [lost-intent request](program-creation-lost-intent-jev-request.json)
and [response](program-creation-lost-intent-jev-response.json) cover a prior
attempt A whose identity and intent were not durably recorded. Jev rated a B
fence as proof that A carried B's intent at 0.03, and a B fence binding B without
knowing A at 0.93. It rated a stale old append committing after the fence at 0.05,
a stale rebase as safe at 0.04, and an empty read proving no prior in-flight
intent at 0.10. It rated B conflicting after a different durable A fence at 0.90,
but gave an inconclusive 0.57 to B conflicting after A's `ProgramCreated` won.
The committed binding and normalized-intent comparison define that result; the
model's answer does not replace the source contract or race tests.

Before full implementation acceptance, prove CR-09's immutable publication,
deterministic identity/membership, precise source evidence, accepted parent and
late-writer behavior on the actual configured provider; preserve exact program
ID retries, same-position fence/no-rebase recovery, and native commit-unknown
behavior; implement and qualify the
coherent authorization service and its all-writer ordering;
version legacy contracts and replay; and cover the table's failure and competing
append cases in focused tests plus API/MCP/read tests. Exercise a revocation
before and after T, revoked sign-in identity/session despite an unexpired token,
actor/tenant/episode/path-splicing, revoke/regrant ABA, expiry at T, cross-realm
owners, an omitted veto, absent trusted pass-order attestation, stale latest
reads, unknown results, a lagging discovery index, UTC midnight, out-of-band
mutable authority state and a mixed-version role-policy rollout. Fitz
0.4.0's source spec
describes commit-time area/realm ordering and Portia 0.10.0 validates contiguous
pattern offsets, but exact runtime routing, retention, durability, and recovery
still need evidence. The publication mechanism remains the main dependency.
