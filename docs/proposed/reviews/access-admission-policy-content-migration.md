# Access admission policy-content migration

Status: **partially implemented; source authority and rollout remain unqualified**. This note
refines the existing [criteria authorization](criteria-authorization-design.md)
and [program creation](program-creation-criteria-design.md) designs; it does not
activate admission or select a production policy owner.

## Existing facts

`AccessAdmissionSourceVersion` records a stream, its non-reused source generation,
and a next physical position. The stable-read source contract assigns version-to-
state binding to its provider; there is no production provider or policy owner
that currently proves that the position binds immutable policy content. The
provisional evaluator compares a policy observation's `ContentSha256` to a
configured supported digest but emits only the policy generation and source
version in `AccessAdmissionEvidence`. A later reader therefore cannot establish
which policy bytes were evaluated from current code alone.

Existing `ProgramCreated` and `ProgramCriteriaEditionSelected` records can
predate publication and admission evidence. Their replay accepts the legacy
absence of both publication proof and admission; partial pairs are rejected.
Provisional admission-bearing program and criteria facts can also lack a policy
digest. The same admission binding helpers are currently used by command
decisions and replay, so simply adding a required check would make old streams
unreadable.

## Evidence and replay

The provisional implementation adds an explicit evidence format version and a
`PolicyContentSha256` field to `AccessAdmissionEvidence`. An absent version and digest identify legacy format
0; do not manufacture a digest from the node's current policy or the historical
generation string. Format 1 carries a lowercase 64-hex SHA-256 digest of the
exact immutable canonical access-policy content. Reject malformed, unsupported,
or partially populated version/field combinations; a digest in format 0 must
not be silently treated as format 1. Keep this bounded field in payload size
accounting and generated JSON serialization.

Use **separate** validation at the two boundaries:

* New admission and every new or recovered target append require format 1,
  exact actor, tenant/scope, operation, target, generation and normalized intent
  binding, as well as the qualified authority decision. Admission evidence is
  created by trusted server code and is never accepted from the caller. Unknown
  policy content, unsupported generation or missing source proof is unavailable,
  not permission or a definitive denial.
* Replay validates the historical format that was actually stored. Format 0
  admission-bearing facts retain their existing structural checks and remain
  explicitly **legacy, without policy-content proof**. The two existing program
  events with neither admission nor publication retain their existing legacy
  replay behavior. Format 1 replay validates all format-1 fields and existing
  event invariants. Neither old event type may gain a synthesized admission or
  publication proof. This applies to aggregate replay, program history, criteria
  lifecycle and source verifiers, not just JSON deserialization.

Keep the original exact event bytes, event identity, operation intent and first
response on retry. A committed legacy outcome can be replayed as history; do
not silently reinterpret it as a format-1 authorization. An already committed
pending proposal or participant claim needs its own source-verified recovery
rule: preserve its original evidence and do not replace it with a different
admission or fabricate acceptance. Decide explicitly whether an independently
proved existing decision can finish under its original historical contract;
otherwise keep it pending. An absent original append requires a new, qualified
format-1 admission and the existing generation/expected-position fence.

The proposed extension is compatible only if the deployed serializer ignores
unknown fields and old binaries cannot write a digestless new admission after
the cutover. Qualify an upgrade order and writer barrier before enforcing it;
do not infer compatibility from default C# values. Existing event discriminators
may remain unchanged only if real historical wire fixtures, source-generated
JSON and mixed-version readers pass; otherwise use explicit event-version
upcasting or a new event version. Inspect the actual retained streams before
asserting whether provisional admission-bearing facts exist. In particular,
verify whether serializing a newly added null/default property changes any
historical canonical digest, operation receipt, immutable content hash or
replay comparison; preserve the original representation when it matters.

## Source authority and rollout

The future provider must expose a canonical policy owner or equivalently
qualified source whose immutable event/version identifies retained policy bytes
and their canonical content digest. Its exact stream generation and position
must be non-reused, complete and current at each two-pass observation; every
policy change advances the compared version. The provider checks the retained
bytes against the source-bound digest, parses only the declared canonical
format, and derives the supported credential, permission and veto declarations
from those same bytes. The deciding implementation verifies it supports that
exact content and generation, and copies the resulting digest and exact source
version into evidence. A configured hash, a DTO's unverified string, a stable
projection, or an image digest is not a source proof.

An independent verifier resolves the recorded source generation and position,
establishes the same immutable policy event and content identity, fetches the
retained bytes, recomputes their digest, and compares it with both the event and
the admission. Missing retained source or bytes, lineage mismatch, hash mismatch,
unsupported evaluator or unavailable continuity remains unknown. The digest
does not prove identity, grant, revocation, time, or that the two-pass decision
was correct; those separate authority and freshness guarantees still apply.

Policy changes during the two reads force retry. A policy change after their
common admission instant follows the existing admission-at-that-instant rule.
Qualify the deployment barrier or routing that prevents stale writers from
deciding new work with an unsupported current generation, including mixed
versions, rollback and recovery of unknown appends. This design does not assume
a new session or account owner: scheme-specific veto requirements must come from
the qualified policy and credential contract.

## Focused tests before activation

* Deserialize and replay genuine legacy program creation and selection fixtures
  with neither admission nor publication; preserve existing rejection of partial
  pairs. Replay historical criteria/program format-0 admission-bearing facts,
  exact retries and fenced recovery without changing facts or inventing proof.
* Round-trip format 1 through generated JSON and each affected domain event;
  assert bounded payload accounting, exact digest preservation and valid replay.
  Reject missing, uppercase, malformed, partially populated and unsupported
  versions for new admissions; assert no resulting fact. A format-0 new command
  must fail even though its historical event can replay.
* Change immutable policy bytes under the same claimed digest, point to a
  different generation or owner position, remove content, and simulate reset,
  stale read or retention gap. Each must remain unavailable. Assert that a
  matching string supplied by an unqualified fake is not source proof.
* Race policy V1 to V2 between passes, test stale V1 and compatible V2 nodes,
  route/barrier failures, rollback and changes after the admitted instant. Test
  lost append acknowledgement, exact committed retry, fenced absent append and
  delayed legacy participant outcomes without substituting new evidence.
* Verify identity-only and full-participant policies against their exact content,
  including unknown veto applicability, and test program primary/team paths and
  platform-operator paths. Changes in source policy declarations must advance
  its version and invalidate a two-pass capture.

Open decisions: the canonical policy format and source owner; how immutable
bytes and historical lineage are retained; the production barrier/routing and
supported binaries; the exact policy for pending legacy participant processes;
and whether actual stored histories require wire-version migration. None is
resolved by adding the evidence field alone.
