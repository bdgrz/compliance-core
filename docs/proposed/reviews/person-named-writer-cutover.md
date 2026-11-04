# Person named-writer cutover

Specification: [Workforce assurance](../contexts/workforce-assurance.md), Person
commands and WF-02. Tracking: [#1176](https://github.com/bdgrz/compliance-core/issues/1176).

Status: **locally qualified source checkpoint**. Exact-head CI/merge are tracked
in the delivery record on #1176. This retires one obsolete writer;
it does not complete Person member linkage, source application or the context.

## Authority and scope

Person remains identified by tenant and person for life, with no ending lifecycle.
ChangePersonDetails accepts differing details and records exact changed fields.
The old generic revision path accepted unchanged details and recorded unconditional
revisions. This cutover removes that request's execution and public HTTP/MCP
publication. Its registered typed handler returns the same permanent Conflict,
without owner/content dependencies. Bus authorization can still read current
membership and permissions before reaching that handler.

Historical V1/V2 facts, operation kinds, immutable content, original receipts and
Core JSON contracts remain readable. No event or stored data is renamed. Named
mutation authorization, canonical route identities, private-field policy and
Portia execution are unchanged. Ordinary fixture detail changes now produce named
facts; the deliberate V2 history fixture directly appends a historical fact.

## Behavioral evidence

Before production changes, strict compilation passed. Seven
PersonLegacyMutationTests failed at their intended retirement assertions: changed,
unchanged, stale, missing and invalid requests; zero owner/content execution; and
refusal of a previously recorded generic request. The historical continuation
reached its receipt/privacy/correlation assertions before the expected refusal
failed. The old HTTP revision-route check also failed because it returned 200.
Compilation/scaffolding corrections preceding that run are not behavioral RED.

The assembled selection passed 97 cases and exposed one stale exact MCP catalog
fixture. Its expected tool names were refreshed against the actual public catalog;
the corrected catalog and coverage-export checks then passed 2/2. Existing
authorization and tool-metadata assertions remain. The first full backend run used the pinned native Cassie service with ordinary collection
parallelism and frozen compiler/runtime-fixture inputs. It finished with 8,706
passing and one failing case; all 13 recorded input hashes stayed unchanged.

That first full run exposed an unisolated RBAC fixture:
AttestClientRbacManagementWallTests.ShouldPreserveAdministrationReadsButDenyTeamDeletionGivenHistoricalAttestAssignment
uses in-memory membership/events but its display-name enrichment reaches a real
Cassie Person directory without schema initialization (`workforce_people` absent).
This is an acceptance failure, not a reason to weaken production reads or the
team-deletion/readability assertions. Independent source review supports controlling that optional presentation seam
inside this already isolated fixture. Bounded Jev-1.14.0 signals were .08 for
bypassing authority, .12 for needing native Person behavior, and .89 for the
scoped repair. The fixture-local null roster-name reader then passed all 13 RBAC wall cases,
including with Cassie configured to an unreachable port. Every original
authorization/readability assertion remains. The next fresh full run passed **8,707/8,707**, zero failures/skips, in 12m47s;
all 14 recorded compiler/runtime-fixture inputs stayed unchanged.

Meaningful checks include:

- Uniform nontransient refusal and unchanged owner facts for generic requests.
- A persistence tripwire requiring zero owner/content calls from the old handler.
- Named HTTP and MCP mutation/retry producing an actual empty Unit response and
  one append, with the old route/tool absent.
- Historical V1 continuation and V2 JSON round-trip retaining private contact,
  member correlation, original receipt/result bytes and historical actor/time.
- Rejection of a corrupted V2 middle-prefix payload or causation metadata after
  a later named fact, verified against the actual current owner's replay cut.
- Existing named changed-field, no-change refusal, tenant isolation, current
  authorization, malformed history and native projection checks.

## Source and Jev review

Independent source review found no blocker in the bounded production cutover or
the subsequent test/catalog refinements. It verified that the public registration
was removed rather than relying on the retained historical ICallable marker, and
that the old handler has no dependency through which to write.

Live requested model `jev-latest` resolved to `jev-1.14.0`. Preparation signals
favored uniform refusal and service-writer removal (.97 each), historical decoding
(.94), and privacy/correlation/receipt preservation (.95). On assembled source,
adverse signals were .06 for a remaining generic writer, .05 for retained public
publication and .11 for authorization/tenant regression. The actual MCP contract
assertions scored .92. Historical fixture judgments remained uncertain (.40/.69);
manual source review and passing focused/full executable checks supply evidence
for the covered cases without turning those signals into model clearance.
These probabilities are review signals, not acceptance or defect-free proof.

The local bounded requests/responses are preserved as
`/tmp/compliance-person-writer-cutover-jev-*` and
`/tmp/compliance-person-cutover-assembled-jev-*`; no API key is in these artifacts.

## Remaining delivery

Local full backend acceptance passed. Final committed-head review, exact-head CI,
squash merge and post-merge readback are separate delivery gates tracked on #1176. No deployment or client qualification is
claimed. WF-05 still needs several links per Person, authoritative reverse
uniqueness, Org Admin-only named link/unlink operations and coordination. WF-08
still needs accepted-HRIS application and source-revision ordering.
