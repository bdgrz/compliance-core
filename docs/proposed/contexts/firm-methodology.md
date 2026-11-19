# Firm methodology

Status: **refinement in progress**, 2026-11-05. This source replaces the earlier
FM-05 boundary; the authority and product questions below remain open. It follows
the [context conventions](conventions.md).

## Purpose

Firm methodology keeps the firm's starting content for clients, such as controls,
policies and risks, as platform-level templates that never contain client data,
and records each time a client applies one (M0-D25, F1-04).

**Owns:** methodology templates (platform level) and template applications
(per tenant).

| Concern | Owner |
| --- | --- |
| The drafts an application creates, and their approval | Their own contexts ([Control environment](control-environment.md), [Policies](policies.md), [Risk and provider oversight](risk-and-provider-oversight.md)) |
| Criteria editions | [Criteria](criteria.md) |
| Platform operators | [Platform access](platform-access.md) |

## Candidate: independently published entries

The design direction is to make each write decision about one template or one
entry, never the complete set of entries. This is a **provisional alternative**,
not an implementation-ready or approved change to the product contract. It
changes the previously accepted whole-version semantics described below.

| Owner | Independent decision |
| --- | --- |
| Template metadata | Create, rename, or retire the template; optionally publish a metadata revision. Metadata has no entry roster and publishing it does not assert that any collection is complete. |
| Template entry line | Own one stable `(TemplateId, EntryKey)`, serialize competing successors for that key and point to its current immutable published revision. It does not own other entry keys. |
| Template entry revision | Own the draft, review and approval of exactly one entry revision; publication identifies that immutable revision and its entry line. |
| Tenant entry application | Apply an exact published entry revision to one program, using its own operation, previous applied revision and target outcome. It progresses independently of applications for other keys. |

An entry revision includes only that entry's kind, content, criteria references,
authors and provenance. A proposed per-entry review policy would require a
reviewer who authored none of **that entry revision's** changes and an approver
who is neither any of its authors nor the reviewer; stale edits and review/approval compete
on the entry revision, so the approved content is the content that was reviewed.
This preserves independence for each entry but does **not** satisfy the former
whole-draft rule that a reviewer authored no revision of any entry in the draft.
The policy change requires a product decision; it cannot be disguised as the
same invariant. Client content is still excluded from templates.

### Inclusion, reads and application

- A template identifies metadata and individually published entries. An exact
  entry reference means `(TemplateId, EntryKey, EntryRevisionId)` and resolves to
  that immutable published content. A newer published revision for the same key
  does not mutate the older one.
- A directory or search projection can list currently discoverable published
  entries, page by page. It may lag or change between pages. No read claims an
  atomic template snapshot, complete immutable version membership, or global
  "all entries applied" result without a separately specified authoritative
  mechanism; none is assumed here. A published metadata revision is not a
  membership boundary, a manifest, or an implicit frozen source cut.
- A Compliance Lead selects an exact entry revision for a program. Its target
  owner creates a draft on first application or considers a revision proposal
  on later application; it never overwrites approved client content. The entry
  application records the target's exact durable `created`, `proposed`, or
  fenced `refused` outcome. Unknown outcomes stay pending; exact retries reuse
  the same identity and intent. A new revision for entry B does not wait for an
  unrelated entry A to complete.
- Ordering is per tenant, program and entry key. A later revision of the same
  key is considered against the last applied revision of that key; it cannot
  race an unresolved prior application of that same key. Exact successor and
  replay rules need qualification. No template-wide completed-version fact or
  next-version barrier exists in this alternative.

The former contract offered a published immutable set and one application that
created or proposed work for **every** applicable entry, then recorded overall
completion before the next version. This candidate instead exposes independent
entry publication and application, with partial visibility and progress. A UI
may offer batch selection or request independent operations as a convenience,
but that does not prove all entries were found, accepted, or completed, and
cannot present a global completion claim. If users require an atomic release,
whole-template reviewer independence, or authoritative all-entry application,
those are separate business requirements with a collective decision; this
candidate does not implement them. Whether this product change is acceptable is
open. Removal and withdrawal semantics also remain open; removing a source entry
does not by itself delete an approved client record.

### Candidate: an immutable release defined by source order

A second, unqualified alternative retains the useful **whole-version read
semantics** without putting every entry into a publication decision. Each entry
revision is independently accepted by its own owner and names the exact draft,
editing epoch and base release. A review freeze marker in the same durably
ordered scope defines the approved draft's source frontier. Publication names
that exact draft, approved freeze frontier and base release. For each stable key,
the release reads the latest accepted revision or withdrawal **in that draft**
across all its editing epochs through the approved freeze, or, if unchanged, the
effective key in its accepted immutable base release. Publication does not
enumerate entries or collect per-entry outcomes. A complete, retained,
historical reader can page that exact version.

For example, suppose release 1 contains `A1` and `B1`. Draft D2 starts from
release 1 and accepts `B2` before its approved freeze. Release 2 reads as
`A1, B2`; release 1 still reads as `A1, B1`. A revision accepted for discarded
draft D0 is never imported into D2 merely because its source position is older.
An edit accepted in a later editing epoch cannot enter an already reviewed cut;
review and approval must bind the same frontier. An edit proposal is not an
accepted revision merely because its proposal event precedes the freeze: an
owner refusal excludes it. A proposal still undecided at freeze cannot be
acknowledged as accepted into that release without a qualified ordering rule.
Later acceptance must be refused for the frozen epoch or explicitly admitted
to a subsequent one, with truthful retry outcomes. This example describes the
candidate's semantics, not a claim of current implementation behavior.

The earlier draft contract says all edits admitted before a freeze are resolved
or fenced before review. Defining inclusion only by an acceptance fact committed
before the marker can instead exclude an earlier *requested* edit that remains
undecided. Unless a protocol proves this cannot happen for an admitted edit,
that is an unapproved behavior change: the editor must receive a truthful
pending or refused outcome and cannot be told their change was published. The
product semantics of admission and how to avoid this race remain open.

The append is only a release *request* until the owner can establish acceptance
under the relevant lifecycle rules. Same-scope order, accepted facts rather than
unresolved requests, exact-identity outcomes and retained history are essential.
An independently checked mutable template state is not a fence. If retirement
or a competing release races this request, an actual ordered authority must
determine which is effective; a handler's earlier read or timeout does not.
Similarly, release identifiers and any expected predecessor must be made unique
without a long-lived template aggregate appending one fact for every entry.

This preserves the existence of an immutable, whole-version *view* only if the
chosen provider and read model establish all of the following:

1. Every effective entry acceptance, withdrawal and review-freeze marker uses
   the same durable ordered scope, with the correct draft and epoch provenance,
   scope generation and retention. Observing an append or a transient
   notification is not a complete frontier. Approved review and publication
   must bind the same exact freeze and base release.
2. A version reader can resolve the effective state of one key at the approved
   draft freeze over its pinned base, and enumerate all keys incrementally. It
   excludes unrelated and discarded drafts and retains the exact historical
   result after future edits, removals and retries. Base-lineage traversal and
   index-rebuild costs may grow and need measurement; a compacted index needs
   proof. Pages must seek from a stable unique key against the same immutable
   cut, including across rebuilds or concurrent later edits; a token alone does
   not establish that guarantee. Pagination or a hash alone is not that proof,
   nor does this require sorting an entire dataset in application memory.
3. An indexed read states **not yet complete** until its writes through the
   marker and the associated checkpoint are durably coupled. A checkpoint saved
   before its indexed records may outlive a crash and falsely advertise a
   complete version. A lagging index cannot certify an absent entry or author.
4. The deployed provider's ordering, durability, filtering, retention and
   recovery contracts are demonstrated, including scope resets and ambiguous
   writes. Source-order read capacity and lag are measured; a growing feed still
   has storage and catch-up costs even though no aggregate owns its entries.

Local Fitz documentation describes commit-time area and realm offsets and the
local Portia Fitz reader checks consecutive pattern offsets. These source facts
are encouraging but do not prove the deployed configuration, historical index,
retention, durable checkpoint coupling or domain admission protocol. The
[ordered-release review](../reviews/methodology-ordered-release-review.md)
records the evidence and counterexamples. If a required guarantee cannot be
established, the release remains unqualified; do not fall back to a parent
roster or an unverified complete-snapshot claim.

### Reviewer independence and applying a release

An immutable release view alone does **not** preserve the existing whole-draft
review rule. One possible design checks only the proposed reviewer's own
participation: an exact `(draft, actor, freeze-position)` query must prove that
actor authored no accepted draft revision in any editing epoch. The third-person
approver must likewise be neither an author nor the reviewer. Such an
actor-specific query need not load other entries, but requires an authoritative
index proven complete at the exact freeze position and a protocol that prevents
the decision until every accepted pre-freeze author event is reflected. A claim
written before a subsequently refused or never-sent edit does not prove accepted
authorship; a claim written after an accepted edit without atomic coordination
can be absent during review. These facts and retries must be modeled explicitly.
The old rule remains in force unless product changes it; neither per-entry
review nor an eventually consistent author lookup satisfies it automatically.

Likewise, selecting a release does not itself mean all entries have been
applied. Individual destination owners retain their own creation or revision
proposal decisions and terminal outcomes. A read may show the progress of a
particular release, but it may say **complete** only when the precise release
membership and matching terminal outcomes are actually covered by an
authoritative, current read; it must distinguish refused entries from accepted
ones and show pending or unknown instead of guessing. Proving all-entry
completion for display is still a potentially large read and needs its own
coverage, paging and lag semantics. No write decision in this alternative scans,
settles or stores all children.

The accepted rule that a later version starts only after the previous
application has completed is a separate **write guard**. Deriving a dashboard
status does not enforce that guard. Replacing it with independent per-key
sequencing allows release 2's entry `A2` to proceed while release 1's unrelated
entry `B1` is pending. That is a product behavior change, not an equivalent
implementation. For one key, two concurrent applications still require a shared
atomic decision or demonstrable equivalent, exact intent identities and durable
target outcomes. The complete-version guard remains unresolved unless product
revises it or a qualifying design satisfying the no-all-child rule is proven.

### Remaining authority and growth questions

The entry line must arbitrate competing successors atomically, including first
publication, retries and changed intent; merely reading the last revision can
fork. Publication versus template retirement is another cross-owner race: a
mutable parent read alone cannot forbid a later entry publication. Choose and
prove an authority or a deliberate policy that allows the race; do not invent a
hidden child ledger or an assumed atomic global cut. Likewise, Program activity,
authorization and target-side refusal need their respective authoritative
outcomes and recovery behavior. A refusal must fence delayed writes even at an
absent target; a lost acknowledgment remains pending.

An entry-line stream may still grow with every published successor, and each
per-entry application ordering stream may grow with repeated applications.
Portia rehydrates whole streams, so a lifecycle or bound remains to be designed
and measured for each long-lived owner. A per-revision stream being small does
not by itself bound the line. Discovery, target acceptance, durability and
concurrent edit/publication all require source and executable qualification.

## Earlier whole-version candidate: rejected under the current constraint

The remaining sections preserve the earlier candidate and its business contract
for comparison. Its review, source-cut verification and completion each require
reasoning over all entries. Those write decisions do not meet the current
constraint and must not be implemented as the new design. In particular, neither
the source-cut fence nor the segmented window fallback below is an assumed
capability of the independent-entry candidate.

## Language

| Term | Meaning |
| --- | --- |
| Template | A named line of methodology versions, kept at platform level. |
| Template version | One immutable, published set of independently stored entries. Each has a stable key and the content for one control, policy or risk. |
| Source cut | An exact reference to a published base and the complete set of accepted draft changes, established by an authoritative source. Its representation and completeness guarantee remain to be proven; it must not be a list or manifest of entries. |
| Application | A tenant's use of one template version for one program. Created drafts and revision proposals carry version and entry provenance. |

## Aggregates

### MethodologyTemplate

**Identity:** `TemplateId` (platform level).

**Invariant:** one template has a line of published versions and at most one
open draft. The line publishes only the open draft's approval. A retired template
takes no new drafts, and a template with an open draft is not retired.

| Command | Who | Accepts when | Emits |
| --- | --- | --- | --- |
| CreateMethodologyTemplate | Platform operator with `methodology.author` | `TemplateId` is new; name unused | MethodologyTemplateCreated |
| StartMethodologyTemplateDraft | Platform operator with `methodology.author` | Not retired; no open draft; starts from latest version (or empty) | MethodologyTemplateDraftStarted |
| PublishMethodologyTemplateVersion | Reactor on MethodologyTemplateDraftApproved | Approval names the open draft and exact sealed source cut; an exact repeat is a no-op | MethodologyTemplateVersionPublished |
| CloseMethodologyTemplateDraft | Reactor on MethodologyTemplateDraftDiscarded | The draft is the open one; an exact repeat is a no-op | MethodologyTemplateDraftClosed |
| RetireMethodologyTemplate | Platform operator with `methodology.approve` | Not retired; no open draft; reason given | MethodologyTemplateRetired |

The following payloads describe the rejected historical candidate only; they
are neither approved contracts nor facts asserted to exist in the implementation.

| Event | Carries |
| --- | --- |
| MethodologyTemplateCreated | template, name, purpose, creator |
| MethodologyTemplateDraftStarted | template, draft, based-on version, actor |
| MethodologyTemplateVersionPublished | template, version, draft, approved source-cut reference, approver |
| MethodologyTemplateDraftClosed | template, draft, reason |
| MethodologyTemplateRetired | template, reason, actor |

`MethodologyTemplateVersionPublished` carries template, version, draft, the
approved source-cut reference and approver, never entry content or keys. Published
source facts are immutable and retained for source verification. The line makes
only a small number of decisions per version, but still grows with every version.
Portia rehydrates the full stream; a bounded lifecycle or another qualified
approach is needed before claiming this long-lived owner is bounded.

### MethodologyTemplateDraft

**Identity:** `TemplateId` + `DraftId`, opened from the line's start fact.

**Invariant:** approval names exactly the sealed cut accepted by a reviewer who
authored no accepted revision, and an approver distinct from every author and
the reviewer (FM-01). Approved or discarded drafts accept no further edits.
Entry content contains no client data (FM-02).

The draft references its published base; it does not copy its entries or own a
collection of child entries. An edit of one stable key belongs to an independent
`MethodologyTemplateDraftEntry`. The draft controls review and edit epochs,
not every individual entry. A revision after an accepted review first opens a
new editing epoch on the draft and invalidates that review. Opening that epoch
and approval contend on the same draft revision: if approval wins no new epoch
opens; if the epoch wins approval must be reconsidered. A return also identifies
the reviewed cut; resuming edits uses the same admission rules.

| Command | Who | Accepts when | Emits |
| --- | --- | --- | --- |
| OpenMethodologyTemplateDraft | Reactor on MethodologyTemplateDraftStarted | New; exact repeat is a no-op; references the based-on version | MethodologyTemplateDraftOpened |
| BeginMethodologyTemplateDraftEditing | Platform operator with `methodology.author` | Draft not approved or discarded; prior review, if any, is invalidated atomically | MethodologyTemplateDraftEditingStarted |
| ReviewMethodologyTemplateDraft | Platform operator with `methodology.author` | The exact editing epoch is sealed with no unresolved admitted edit; candidate authored no accepted revision in the complete history | MethodologyTemplateDraftAccepted or MethodologyTemplateDraftReturned |
| ApproveMethodologyTemplateDraft | Platform operator with `methodology.approve` | The accepted cut is still current; candidate authored no accepted revision and is not its reviewer | MethodologyTemplateDraftApproved |
| DiscardMethodologyTemplateDraft | Platform operator with `methodology.author` | Not approved; reason given; admission is stopped and every admitted edit resolved or fenced | MethodologyTemplateDraftDiscarded |

| Event | Carries |
| --- | --- |
| MethodologyTemplateDraftOpened | template, draft, based-on version |
| MethodologyTemplateDraftEditingStarted | template, draft, editing epoch, actor, prior review invalidated if any |
| MethodologyTemplateDraftAccepted | template, draft, sealed source-cut reference, reviewer |
| MethodologyTemplateDraftReturned | template, draft, sealed source-cut reference, comments, reviewer |
| MethodologyTemplateDraftApproved | template, draft, accepted source-cut reference, approver |
| MethodologyTemplateDraftDiscarded | template, draft, reason, actor |

Approval carries the source-cut reference and approver, not entries. To check a
specific reviewer or approver, the complete sealed history can be traversed with
a single candidate-matched boolean, retaining no author roster. The author set
covers every accepted revision of the draft, including previous editing epochs.
A traversal that cannot establish a complete history cannot authorize review.

### MethodologyTemplateDraftEntry and the source cut

**Identity:** `TemplateId` + `DraftId` + stable `EntryKey`.

**Invariant:** one key has one independently decided sequence of draft
changes. Each fact contains one entry's content and references, its author and
its exact admitted operation. An edit against a stale entry revision is refused;
an exact retry returns the original outcome. No fact contains the full template.
Unchanged entries remain in the immutable base; additions, changes and removal
markers belong to their individual keys.

The preferred direction is independently decided edits, with a real
**source-owned admission fence** that prevents new accepted edits after a draft
epoch is sealed. Review can proceed only after every edit admitted before the
fence has either durably committed into the exact cut or been durably refused or
fenced at its target. Unknown outcomes remain pending; a timeout or a projection
showing no work cannot establish that the epoch is drained. A source cut must
identify the exact base and accepted changes and support complete authoritative
paged enumeration without collecting the entire dataset. It must also allow a
candidate's authorship to be checked over all accepted draft revisions, including
earlier epochs. Opening the next epoch and invalidating any prior review remain
draft-owner decisions; discard needs the same drain-or-fence guarantee.

This is a requirement on source authority, not a claim that an appropriate
Portia storage primitive exists. The source, the atomic fence/acceptance
boundary, how an absent target is fenced, source retention, enumeration
completeness, concurrency and crash recovery all need a concrete design and
provider-backed proof. An independently read mutable gate followed by an entry
append does not meet this requirement. Resolving each key over its base and
accepted changes may use paging or rescans with bounded working memory; an
unqualified projection, an inferred negative, a giant manifest or an in-memory
sort is not a completeness proof.

One considered fallback is a chain of finite **draft change windows** that
serialize edit admission and record each operation's identity, target and exact
outcome before admitting another. Each individual window can be bounded, but
their combined history is a distributed child ledger and its one-at-a-time
admission serializes edits. It does not demonstrate that the design smell is
removed, nor is it an approved solution. Retain it only as an unresolved
fallback for comparison; do not implement it without an explicit boundary
decision and proof of freeze, replay, capacity and recovery.

### TemplateApplication

**Identity:** `TenantId` + `ProgramId` + `TemplateId`.

**Invariant:** a program starts a later version only after the previous
application has durably completed. The first application creates one client
draft per entry; subsequent applications propose revisions for changed entries
and never overwrite approved client content (FM-03). Only the tenant's
Compliance Lead applies a template (FM-04).

| Command | Who | Accepts when | Emits |
| --- | --- | --- | --- |
| ApplyMethodologyTemplate | Compliance Lead | Program active; version published; template not retired; later than last applied; previous application completed; required authority participation succeeds | MethodologyTemplateApplied |
| RecordTemplateApplicationCompleted | Reactor on TemplateVersionApplicationCompleted | Exact active version, source cut and operation match; exact repeat is a no-op | TemplateApplicationCompleted |

| Event | Carries |
| --- | --- |
| MethodologyTemplateApplied | tenant, program, template, selected version and source-cut reference, previous applied version and source-cut reference, operation, actor |
| TemplateApplicationCompleted | tenant, program, template, exact version, source-cut reference, operation, outcome counts |

`MethodologyTemplateApplied` carries the exact published cut, previous applied
version and its cut, initiating operation and actor. It does not carry entries
to create or propose. Completion records only bounded summary and provenance;
per-entry outcomes remain independent. This owner grows with version decisions,
not with entry count, but Portia's full-stream rehydration means its long-lived
per-application history also requires a separately qualified bound or lifecycle.

Program activity and template retirement are mutable facts owned elsewhere.
Independent reads before append cannot enforce their simultaneous truth. The
application must not claim these guards are authoritative until the Program and
Template owners' exact admission, refusal and lifecycle-race handling are
specified and qualified. An unknown participant result remains pending.

### TemplateVersionApplication

**Identity:** `TenantId` + `ProgramId` + `TemplateId` + version.

**Invariant:** completion identifies one exact published cut and, for a
later application, the exact *last applied* cut, and occurs only after every
expected entry has one authoritative immutable terminal outcome. It does not
store or append each entry outcome. A zero-entry comparison may complete after
the same source verification.

| Command | Who | Accepts when | Emits |
| --- | --- | --- | --- |
| OpenTemplateVersionApplication | Reactor on MethodologyTemplateApplied | New; exact repeat is a no-op | TemplateVersionApplicationOpened |
| CompleteTemplateVersionApplication | Reactor verifying the pinned cuts and exact participant outcomes | Complete authoritative enumeration and all expected terminal outcomes verified | TemplateVersionApplicationCompleted |

| Event | Carries |
| --- | --- |
| TemplateVersionApplicationOpened | tenant, program, template, version, selected and prior source-cut references, operation |
| TemplateVersionApplicationCompleted | tenant, program, template, version, selected and prior source-cut references, operation, outcome counts |

### TemplateEntryApplication

**Identity:** `TenantId` + `ProgramId` + `TemplateId` + version + `EntryKey`.

**Invariant:** the exact source entry and operation have one immutable terminal
created, proposed or target-fenced refused outcome. Its operation binds the
source cut, exact source entry,
previous cut where applicable, destination, intended effect and deterministic
operation ID. The target context owns its own create or proposal decision. Its
accepted fact or durable target-fenced refusal yields exactly one immutable
`created`, `proposed`, or `refused` outcome (with reason and created record where
applicable). If a target result is unknown, the entry remains pending. Replays
of the same intent return its outcome; changed intent cannot reuse its identity.
A durable refusal must prevent a delayed operation with that token from later
creating a record, including against an initially absent target.

A worker enumerates the complete immutable source cut directly and derives the
expected entry identities. The first version selects every current entry. For a
later version it compares the selected source with the **last applied** cut,
not merely the immediately preceding published version: otherwise skipping a
version can lose its still-effective changes. It checks each expected entry's
matching terminal source fact. Immutable cuts and terminal outcomes make an
exhaustive proof remain true between verification and the completion append;
missing facts, gaps, mismatches or unverified source completeness leave the
application pending. The completion decision cannot be based on counters, a
caller-supplied flag or an eventually consistent projection alone. Counts may be
reported after verification and per-entry results remain individually readable.
Enumeration may page or rescan with bounded memory; it must not materialize or
sort the entire version in memory.

| Command | Who | Accepts when | Emits |
| --- | --- | --- | --- |
| ApplyTemplateEntry | Reactor deriving an expected entry from the pinned cut | Source and exact operation match; destination decides the effect | TemplateEntryApplicationOutcomeRecorded |

| Event | Carries |
| --- | --- |
| TemplateEntryApplicationOutcomeRecorded | tenant, program, template, version, entry key, source and operation references, destination outcome, refusal reason or created record where applicable |

The ordering owner records that exact completion before a later version can
start. Delayed delivery or restart retries the same operation; neither dispatch
nor an acknowledgment by itself constitutes a business outcome.

## Flows

```mermaid
flowchart LR
    A[/Start draft/] --> B([Draft started]) --> C[/Open draft referencing base/]
    C --> D[/Open editing epoch/]
    D --> E[[Independent entry edits, subject to source authority]] --> F[/Fence admission and resolve or fence admitted edits/]
    F --> G[/Establish complete source cut: mechanism unresolved/] --> H[/Independent review/] --> I[/Third-person approval/]
    I --> J([Published version references sealed cut]) --> K[/Apply version/]
    K --> L([Application pins selected and last-applied cuts]) --> M[[Enumerate expected entries in bounded memory]]
    M --> N[[Independent entry applications and target outcomes]] --> O[/Verify every exact terminal outcome/]
    O --> P([Version application completed]) --> Q([Ordering owner records completion])
```

## Authorization contract

- `methodology.author` and `methodology.approve` are platform-operator
  permissions. Templates are readable by every tenant and contain no client data.
- A tenant Compliance Lead initiates application; destination contexts enforce
  their own authorization and business invariants.

## Disagreement and replay

| Case | Outcome |
| --- | --- |
| A template control's identifier is already used in the program | Its owner durably refuses or fences that operation; the entry outcome records `refused` (`identifier_in_use`). |
| Application, entry dispatch, or completion delivered twice | Exact operation identities replay the same result; changed intent is rejected. |
| A target result is unknown or an admitted edit is unresolved | It remains pending; neither review nor version completion advances on a timeout. |
| An admitted edit arrives after review freeze | A resolved acceptance is included in the sealed cut; a fenced token cannot later succeed. An unfenced request prevents sealing. |
| Review and a new edit race | Approval and starting an editing epoch contend on the draft revision; only an approved, unchanged cut can publish. |
| Version 3 is selected after version 1 | Compare version 3 with version 1, including changes first introduced in version 2 that remain in version 3. |
| `MethodologyTemplateDraftApproved` delivered twice | Publish exact same draft and cut once; a changed cut is not an exact repeat. |
| An entry result arrives before its version application opens | Retry against exact identities; no completion is inferred from delivery order. |

## Decisions

The following are historical decisions and review evidence; the independent
entry proposal above is provisional and does not change accepted product policy.
Existing FM-01 through FM-04 remain the business decisions: independent reviewer
and third-person approver, no client content in firm templates, revisions for
changed entries without overwriting approved content, and Compliance Lead
initiation. The original FM-05 whole-draft and whole-version outcome boundaries
must be replaced. The candidate at the top of this document would change FM-01's
scope and FM-03's whole-version application behavior; those are not silently
approved by this refinement. Segmented admission windows are a fallback with the
child ledger smell, not a settled replacement. The first Jev review evaluated
that earlier window-based candidate, not the independent-entry alternative.

[Jev request](../reviews/methodology-boundaries-jev-request.json),
[response](../reviews/methodology-boundaries-jev-response.json), and
[scenario request](../reviews/methodology-boundaries-jev-scenarios-request.json)
with [response](../reviews/methodology-boundaries-jev-scenarios-response.json)
record `jev-1.14.0` advisory judgments on 2026-11-05. It favored complete sealed
source plus immutable independent outcomes (0.86), authoritative streamed
verification (0.88), target-side fencing (0.88), and a candidate-author scan
(0.91). It rejected projection-based completeness (0.13), a required parent
roster (0.12), and independent mutable-guard reads as an atomic guarantee (0.07).
The skipped-version counterexample scored 0.75; the unfenced-freeze scenario
scored 0.61. These are unresolved model signals, not proof; the concrete missed
change and delayed-write interleavings require source and executable validation.

The follow-up [simplicity request](../reviews/methodology-boundaries-jev-simplicity-request.json)
and [response](../reviews/methodology-boundaries-jev-simplicity-response.json)
favored identifying the window chain as a per-entry ledger (0.89), recognizing
that one in-flight operation serializes independent edits (0.93), and that
per-version full replay grows without a lifecycle bound (0.96). It rejected an
eventually consistent listing as proof of a complete cut (0.06). The delayed
write after an unfenced read scored 0.59 and remains an unresolved model signal;
the described interleaving must be addressed regardless of that score.

For the independent-entry candidate, the separate
[request](../reviews/methodology-boundaries-jev-independent-request.json) and
[response](../reviews/methodology-boundaries-jev-independent-response.json)
favored that independent publication changes whole-version semantics (0.87),
that per-entry review weakens the whole-draft author rule (0.89), and that
unrelated entry application can progress independently (0.89). It rejected
claims of a complete immutable listing or global completion based on an
eventually consistent directory (both 0.07). The retirement race scored 0.51;
this uncertain judgment does not resolve the explicit read-retire-write race.
The responses are review signals, not proofs or approval of the product changes.

## Open questions

For the independent-entry candidate, resolve whether entry-level review and
incremental visibility/application may replace the accepted whole-draft and
whole-version guarantees. Specify the single-entry successor decision,
publication versus retirement, per-entry application ordering, and retention
and replay bounds before implementation. Global atomic release or completion,
if required, would need a different product and domain decision.

The following questions describe what the earlier, whole-version candidate
would have required; its collective decisions conflict with the current
constraint and are not the chosen implementation:

1. Specify authoritative Program and Template participation for application
   admission, including retirement/offboarding races, refusal, retry, fencing and
   recovery. Until then the mutable prerequisites above are not guaranteed.
2. Decide removal semantics and exactly which entry changes require a proposal.
   Removal of a source entry alone does not authorize deletion of approved client
   content. Do not silently interpret this as a complete removal policy.
3. Design and prove source-owned admission fencing and an authoritative complete
   cut with source retention and bounded traversal, including concurrent edits,
   empty cuts, overwritten keys, late arrivals and restart. Assess the segmented
   window fallback as a child ledger, not evidence of an accepted solution.
   The draft root's review/epoch history, template line's version history, and
   application ordering history each need their own full-replay bound or
   lifecycle design; few events per version is not a finite bound.
4. Qualify destination-side durable token fences, including an absent target and
   delayed requests; exact source/intent matching; concurrent review/edit and
   approval; skipped versions; terminal outcome immutability; and completion vs.
   the next version. The providers and contexts must actually support these
   guarantees. Until then this design is not locked or implementation-ready.

## Superseded owners

| Old owner | Becomes |
| --- | --- |
| MethodologyTemplate | Proposed line, draft lifecycle and independent draft entries; source admission and completeness remain unresolved |
| MethodologyTemplateContribution | Removed (FM-02) |
| TemplateApplication and TemplateVersionApplication with per-entry parent outcomes | Ordering owner, version completion owner and independent entry outcomes; successor adoption is a later application (FM-03) |
| `ProposeTemplateSuccessorAdoption`, `DecideWhetherToAdoptNewTemplateVersion` | Removed: not applying a version is the decision |
