# Runtime criteria metadata and disclosure design gap

Status: **partially selected and implemented**, 2026-11-05. The user selected
bounded set/correct operations with the current catalog contract, framework
`tsc`, separate disclosure owners and no publication prerequisites. The
source-authoritative process, cut-aware reads, recovery and provider
qualification are still open. This review records the original design gap and
the proposal; current domain-source evidence is in the
[edition value review](criteria-edition-value-review.md).

The operator can also set `content_rights` as a bounded, independently owned
edition field. Its accepted source value is a declaration supplied by the
operator, not a grant of rights or a platform-wide policy. Published catalog
responses use that recorded value; an edition with no value remains pending in
the unpaged detail contract. The original design discussion below predates
this choice.

## Proposed bounded design for review

This is the design proposed for the missing path. The set/correct subset was
subsequently selected; it is not yet an implemented end-to-end authority.
Framework remains the edition's immutable
`tsc` value, recorded in the draft request and checked through activation. The
committed `CriteriaEditionPublished` source event supplies publication time and
the one edition cut. The seed's historical catalog timestamp is kept as a
separately named, source-attributed value rather than replacing that time. The
platform content-rights restriction is derived from M0-D02. An operator's
edition-specific content-rights declaration records what the edition ships;
it does not grant rights to licensed text.

Each independently correctable metadata field has identity `(editionId,
fieldKey)`: initially `coverage_note`, `source_url`, and
`numbered_criteria_complete`, and `content_rights`. The typed value is one bounded string, URL or
boolean, not a general JSON document. Each field owner has one effective draft
value, its revision, and at most one pending change. Each support disclosure has
identity `(editionId, category, code)` and independently owns one bounded note,
revision and pending change. The disclosure records a scoped support limitation,
not a member of an edition-owned list. Raw operation IDs are scoped to the exact
field or disclosure owner. A separate immutable create-once operation owner
`(target identity, operationId)` binds one normalized intent and actor across
set and clear/withdraw command kinds; a different target is a different scope.
These identifiers and names are design candidates, not new wire contracts.

For each target, an operator may propose a set or correction against its exact
current revision while the edition is an active draft. A clear of optional
metadata and a withdrawal of a disclosure are proposed explicit operations,
not deletion of source history. Their effective result is **unknown/no current
declaration**, never proof that the relevant capability is supported. Whether
the product should allow clear and withdrawal and which fields must be authored
before publication remain open product decisions. Until settled, no clear,
withdraw, or publication prerequisite should be exposed as implemented behavior.
The same operation ID with the same normalized intent returns its original
receipt after current read authorization; changed intent or a different actor
conflicts. A target refuses a different proposal while its current one is
unresolved. Operators get a retryable pending result for an unknown append or
decision, not an inferred refusal or success.

Admission is server-derived from the currently authenticated platform operator,
bound to the exact target, normalized intent and execution generation. The
handler verifies the immutable edition draft-generation source and the target
operation claim. The proposed event and each accepted or refused outcome refer
to those exact committed facts; accepting an event ID from a request is not a
source check. The edition's effective publication or abandonment boundary is
established using the same qualified common-area ordering and source-backed
participant protocol selected for criteria entries. A proposal before the
committed publication cut may settle after it, retaining its intent's position;
until the exact outcome is known its field or disclosure is pending. A proposal
after the cut cannot change the published value. An abandoned draft cannot gain
an effective change. No publication write reads or counts all metadata fields
or disclosures, and publication can finish while unrelated pre-cut outcomes
remain pending; read coverage represents that uncertainty explicitly.

Unknown writes are reconciled from exact committed source. If no winner can be
observed, only an authoritative expected-position fence for the same target,
operation and attempt can close the old execution before a newer execution is
admitted. An absent page, checkpoint or local timeout is never such a fence.
Fenced attempts cannot later settle as a newer generation. The immutable
operation identity survives generation changes, and a qualified target-head
mechanism must preserve exact historical receipts and reject changed intent.
Retaining one in-memory pending item does not bound Portia's full aggregate
event-stream load. Evaluate expected write frequency and lifetime for each owner;
if its lifecycle can grow beyond acceptable replay costs, qualify a rollover or
equivalent source-loading mechanism before relying on an unrestricted history.
Do not add rollover machinery merely because a low-churn owner has history.
One immutable operation owner per request keeps its own stream bounded; it is
not an edition-level operation roster. Retained history, source lineage,
tombstone and archive lookup must protect the promised retry window.

The current proposed individual limits are 2,000 UTF-8 bytes for a coverage or
disclosure note, 1,024 for a source URL, 128 for a disclosure code, and 8 KiB
for the complete serialized business event, including admission evidence; these
match the current `CriteriaContentBounds` checks and are per-record engineering
limits rather than edition size limits. Reject invalid UTF-8 and check the actual
escaped serialized payload before raising a fact. Whether these particular
limits meet product needs is open; they must not be presented as a limit on the
number of independent disclosures or criteria. Licensed source text remains
outside platform content.

Read models materialize each effective scalar and disclosure independently.
They bind edition and draft generation, exact source event identities, the
publication cut, projection generation and ordered discovery coverage. A
particular field reports pending if its pre-cut outcome is unresolved; the
disclosure list remains pending until a qualified source-discovery-through-cut
certificate and individual outcomes establish the traversed snapshot. Filtered
or paged reads cannot claim absence merely because the index is quiet. The
nullable `RuntimeCriteriaEdition` shape currently cannot distinguish every
unknown, explicitly cleared, and pending state; a versioned API/read contract
must settle those distinctions before enabling the write path. Completion of a
disclosure walk is independent of the operator's coverage declarations.

Qualification remains open for current source heads, expected-position append
after ambiguous commits, common-area order, retention/lineage, source discovery
and projection/checkpoint atomicity. The target-only proof and bounded owner
mechanism must be implemented and tested; an edition projection read, a cached
head or a fingerprint is not an authority. Do not register or expose these
proposed commands until the decisions and those source capabilities are resolved.

The approved [criteria context](../contexts/criteria.md) gives an edition one
framework and makes its published catalog immutable. It also requires the
existing seed's support gaps as independent small facts and says absence is
unknown. It defines a single structural publication cut and independent
pre-cut entry outcomes. It does not define an equivalent runtime-authoring
protocol for metadata or disclosures. [M0-D02](../decisions/m0-d02-criteria-content.md)
names `framework`, `edition_label` and `published_at`; the current runtime
draft command and its source event do not carry the documented framework.

The current `CriteriaSeedMetadataRecorded` and
`CriteriaSeedDisclosureRecorded` facts represent one fixed release and refer to
its exact version, fingerprint and system seed request. They cannot establish
operator-authored facts for other editions. `RuntimeCriteriaEdition` permits
framework, coverage note, source URL, content-rights declaration and numbered
criteria completeness to be absent. `PublishedAt` on that read model comes from
the committed publication event's source metadata, whereas the seed metadata
also records its historical catalog timestamp. Those timestamps have different
provenance and must not be interchanged. A declaration that numbered criteria
are complete is not proof of source discovery, settled entry outcomes, complete
points of focus, or full framework support.

The user selected the current catalog contract as the default and declined to
make stream rollover a prerequisite for low-churn owners. Whether those owners'
actual history or provider limits warrant rollover remains an open operational
qualification, not a reason to block their local aggregate decisions.

Generic editions may now record their own operator-authored `content_rights`
declaration with the same source-bound value protocol as other metadata. The
legacy `CriteriaCatalogEdition` detail remains pending if that value has not
been recorded; no value is synthesized from the seed or a current policy.

## Decisions needed

1. **Metadata ownership and editing.** Decide whether framework belongs in the
   initial edition request as an immutable value, and define the owner, identity,
   operation semantics and editable fields for any other metadata. Decide which
   fields are required, whether unavailable fields are nullable, whether URLs
   are references rather than licensed content, and whether rights are fixed by
   M0-D02 or have a controlled vocabulary. Decide whether a draft correction is
   allowed, including exact retry, changed intent, optimistic revision and
   historical source-fact requirements. A create-once metadata owner would
   arbitrarily prohibit draft corrections; a mutable per-edition owner without
   generation rollover would load unbounded history in Portia.
2. **Disclosure lifecycle.** Define an independent identity, for example
   `(edition, category, code)`, and the meaning of one disclosure. Specify
   whether the same code can be corrected, withdrawn or replaced during draft,
   whether the original is retained for audit, and what a published disclosure
   means. Category and code validation, the original product-written content
   boundary, and small per-record limits must be explicit. No edition aggregate
   may retain a roster or make an all-disclosures decision.
3. **Authorization and operation identity.** Specify the platform-operator
   command and non-null, server-derived admission evidence binding the exact
   actor, edition, operation identity, payload and execution generation. Define
   create-once operation ownership across each target's command kinds; a caller
   supplied event ID or projection status is not authorization. Any system seed
   import remains a distinct trusted source-verified path.
4. **Publication ordering and recovery.** Decide whether and how the edition's
   published event is also the cut for metadata and individual disclosures.
   If it is, define each proposal and its accepted or refused outcome, exact
   source-order comparison to the cut, publication-versus-abandonment races,
   target expected-position fences and recovery of unknown appends. A late
   outcome for a pre-cut proposal cannot be treated as absent; a post-cut
   proposal must not mutate a published edition. A same-area source position is
   discovered after commit rather than guessed in a new event. The existing
   entry protocol does not automatically admit another type of proposal.
5. **Source and read completeness.** Define which metadata fact or outcome is
   authoritative at the cut, and how disclosure discovery and all relevant
   pending outcomes are proven for a paged snapshot. Qualified contiguous
   discovery and cut coverage are required before reporting a complete list;
   a projection checkpoint, count, fingerprint or quiet page cannot prove
   completeness. Readers must represent missing, unobserved and unresolved
   fields as pending or unknown, not synthesize defaults or full support.
   Seed-only and runtime-authored facts need distinct provenance, and a
   projection must validate the exact edition, draft generation and source
   binding before applying either.

Any eventual implementation needs focused refusal-with-no-fact, exact retry,
changed intent, replay and competing append tests for each bounded owner, plus
source ordering, late outcome, authority and paged-coverage tests. Provider
freshness, expected-position behavior after ambiguous commits, source retention
and checkpoint atomicity must be qualified separately. The existing fixed-seed
writer likewise remains disabled: it lacks qualified current-head and
expected-position guarantees, a source-authorized system execution path,
source-verified claim and participant outcomes, label/witness publication
coordination, later-generation fence recovery and qualified import/disclosure
coverage. No count or release fingerprint substitutes for those proofs.

## Jev review

The live `jev-1.14.0` [request](criteria-runtime-metadata-jev-request.json)
includes this proposal as reviewed, the approved criteria context and content
decision; the [response](criteria-runtime-metadata-jev-response.json) preserves
the typed answers. Jev assigned 0.05 to the design requiring an edition-owned
roster, 0.04 to one in-memory pending item bounding a fully loaded unrestricted
stream, 0.96 to pre-cut changes settling after publication while pending, 0.03
to post-cut intents changing the published metadata, 0.02 to a missing
disclosure proving support, and 0.04 to publication requiring an all-disclosure
scan. It assigned 0.03 to the proposal proving the current provider guarantees,
0.08 to an old successful operation authorizing a receipt after read access is
lost, 0.17 to allowing an unfenced later execution after an unknown append, and
0.03 to numbered-criteria completeness proving full framework support.

These are advisory judgments about the supplied proposal. They do not settle
the open product decisions, prove authority and storage guarantees, or validate
an implementation. In particular, the generation and recovery design needs
source-level race analysis and executable provider qualification before use.
