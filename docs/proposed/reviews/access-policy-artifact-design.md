# Canonical access-policy artifact, version 1

Status: **proposed format; no policy value or provider is activated**. This
specifies the immutable bytes referenced by the [policy generation owner](access-policy-generation-design.md)
and addresses the [evidence migration](access-admission-policy-content-migration.md).
The source-backed evaluator and its credential, route, content and freshness
providers remain unregistered or unqualified. This format does not establish
their guarantees.

## Small closed artifact

The reference uses schema `bdgrz.access-policy.v1` and media type
`application/json`. Content is one JSON object with exactly these eight required
properties; booleans cannot be null or omitted:

| Property | Type and allowed value | Meaning |
| --- | --- | --- |
| `account_required` | boolean | The applicable complete fixed veto set includes the exact account-veto participant. |
| `allow_bdgrz_identity_only` | boolean | The specifically certified Bdgrz identity-only credential path may be considered, subject to all conditions below. |
| `allow_development_identities` | boolean | Whether identities whose source provider is `bdgrz-development` may pass the evaluator's additional development-identity check. |
| `evaluator_profile` | exactly `bdgrz.access-admission.v1` | Selects the complete fixed rules in this note, including identity, operator, program and scope semantics. |
| `fixed_veto_set_complete` | boolean | Declares that the named policy-required veto set is exhaustive for this credential contract. Only `true` can support admission. |
| `global_veto_required` | boolean | The complete fixed veto set includes the exact global-veto participant. |
| `schema` | exactly `bdgrz.access-policy.v1` | Identifies the artifact's closed schema; it must match the immutable reference schema. |
| `session_required` | boolean | The complete fixed veto set includes the exact session participant. |

No generation is embedded in the artifact: the activation source binds one
generation to this immutable reference. A rollback may reuse identical bytes
only at a new higher generation. No artifact embeds users, roles, grants,
operators, tenant memberships, session IDs, client secrets or deployment nodes.
An example below illustrates syntax only; it does **not** select the platform's
security policy:

```json
{"account_required":true,"allow_bdgrz_identity_only":false,"allow_development_identities":false,"evaluator_profile":"bdgrz.access-admission.v1","fixed_veto_set_complete":true,"global_veto_required":true,"schema":"bdgrz.access-policy.v1","session_required":true}
```

## Exact canonical bytes

The canonical content is the UTF-8 encoding of one JSON object with the eight
properties in the exact order shown in the example, with no whitespace between
tokens, no byte-order mark, no trailing newline and no other bytes. Property
names, schema and profile values are the exact unescaped ASCII strings shown;
booleans are lowercase `true` or `false`. No arrays, numbers, nulls, aliases,
escapes, case folding, Unicode normalization or alternative string spellings
are accepted. Hash the **exact canonical byte sequence** using SHA-256 and encode
its 32 bytes as exactly 64 lowercase hexadecimal characters. The reference's
byte length must equal the actual bytes and be from 1 through the provisional
16 KiB limit inclusive. Reject an oversized declared reference before fetching;
bound the actual fetch or stream to at most the limit plus one byte, then reject
excess before parsing. Do not trust a declared reference length alone.

Reject malformed UTF-8, duplicate keys at any nesting level, unknown or missing
properties, unsupported schema/profile/media type, values of the wrong type and
noncanonical wire encodings. A parser must detect duplicates rather than silently
choosing the first or last value, validate the complete object and then require
byte-for-byte equality with the canonical serialization; it must not accept a
hash of a reserialized variant in place of the stored bytes. The reference's
schema, media type, length and digest must be bound by the exact committed policy
activation and independently matched to the retained immutable bytes. Missing,
conflicting, corrupt, truncated or unverifiable bytes remain unavailable.

This is a deliberately restricted format, not a claim that arbitrary JSON has a
canonical representation. A schema extension, new field, new credential mode or
change of fixed semantics requires a new schema and/or evaluator profile, an
independently defined canonical form, a higher policy generation, and a
qualified compatible-writer rollout. Old binaries fail closed on unknown fields
or unsupported versions; ignoring a field would silently change authority.

## Fixed evaluator profile

`bdgrz.access-admission.v1` is a proposed semantic contract for the current
`AccessAdmissionSemanticEvaluator`, not an assertion that it is fully wired or
qualified. A binary may claim support only if it implements and tests all of the
following rules, derives these declarations from the same verified artifact,
and supports the exact active schema, profile and digest:

* Every path requires an unexpired cryptographically validated credential, its
  exact registered, nonrevoked `UserIdentity` and user mapping, the current
  policy and every applicable veto at one qualified admission instant. The
  credential resolver, not the policy JSON or caller body, proves the signed
  identity binding, issuer, subject and validity window. Unknown applicability
  or an incomplete veto set is unavailable.
* Criteria-operator admission requires the exact active per-user platform
  operator grant. Program create and criteria-edition selection require the
  exact active tenant and eligible client-personnel membership episode, plus
  either the matching protected-primary designation or a complete exact
  team-member, team, team-role, role and `program.manage` role-permission path.
  Advisor and attest roles do not satisfy that team path. Resolve all alternate
  paths authoritatively before treating a rejected candidate as a definitive
  denial; the artifact does not contain the grants themselves.
* A non-primary creation grant must have organization scope for the same tenant;
  a program-scoped grant cannot create another program. A non-primary selection
  grant must have either same-tenant organization scope or the exact target
  program scope. A resource-type scope is unsupported by this evaluator. The
  primary path uses the exact primary designation, rather than inventing a team
  scope. Product changes to these rules require a new supported profile.
* The current `FullParticipants` mode requires all three veto booleans to be
  true and observes the matching exact session, account and global-veto owners.
  The current `BdgrzIdentityOnly` mode requires
  `allow_bdgrz_identity_only=true`, all three veto booleans false, the exact
  Bdgrz issuer and a separately certified signed identity binding. A policy whose
  booleans describe any other combination is not supported by the current
  evaluator; it must not be silently weakened. These booleans are global in
  the current evaluator, so one active artifact cannot enable both of those
  modes simultaneously. Supporting independently declared per-scheme veto sets
  requires a separately designed and qualified evaluator/profile.
* `allow_development_identities=true` only controls the evaluator's additional
  check; it does not create an identity, authenticate a principal or override
  the host's development-only setup. With `false`, a `bdgrz-development`
  identity cannot pass admission even if a previously issued signed token has
  the `bdgrz` issuer. Production enablement, any environment-specific policy
  activation and existing development identities require explicit security and
  deployment decisions; never infer safety solely from token issuer or current
  identity-creation configuration.

There are no implicit defaults. A syntactically valid artifact can still be
unsupported, incomplete or select no usable credential path. Treat the
resulting admission as unavailable; activation preflight and the product must
decide whether such a configuration is permitted and how lockout is recovered.
Enabling identity-only requires independent security evidence that session,
account and global-veto authority really is inapplicable to that scheme.
External identity schemes and additional veto sources are not implicitly covered.

## Activation, reads and independent verification

Before activation, a trusted component verifies canonical bytes, reference,
semantic support, permitted policy choices and the independently qualified
deployment writer barrier. The small policy head records only the generation and
reference. Publication of bytes, a matching hash, the head event, and a local
configuration value each fail to prove the other prerequisites.

At admission, a qualified source adapter binds the exact current head generation
and non-reused source position to the immutable reference, fetches and validates
those exact retained bytes, and supplies the parsed policy to the two-pass
evaluator. Every relevant policy change advances the source position; the node
requires exact supported generation and content at the common instant. A policy
change across the two reads forces retry. The resulting evidence binds the
generation, exact source identity and content digest to the target operation.
The generation is **not** derived from the digest and identical bytes at a
later generation do not imply an identical policy observation.

An independent verifier resolves that historical source identity and position,
rechecks the event/reference, reads retained bytes, enforces canonical form,
recomputes the digest and checks the evaluator profile and artifact declarations.
It separately verifies the credential, grants, scope, applicable vetoes and the
admission-time/route guarantees; a digest alone does not prove them. Missing
history or an unavailable provider is unknown. Historical format-0 evidence
retains replay compatibility and never acquires a fabricated digest.

The active-generation change must be fenced or routed so every writer making a
new admission supports the exact active artifact and profile. Mixed-version
writers, in-flight decisions, unknown appends, partitions, rollback, source
lineage and content retention need qualification beyond this format; no list of
nodes is stored on the policy owner. See the generation design for the
alternative of a genuinely equivalent qualified deployment authority.

## Decisions and validation gates

Security and product must choose actual boolean values, the complete veto set,
identity-only eligibility, development-identity treatment and activation
authority; this note deliberately does not choose them. Infrastructure must
qualify credential and path resolution, immutable storage, historical and
current reads, a trusted clock, all writer barriers and rollout/recovery. The
16 KiB maximum is provisional. Profile and canonical format names in this note
are proposed identifiers, not a claim that a production parser exists.

Focused qualification should cover golden byte/hash vectors; exact maximum and
oversize; whitespace, BOM, escapes, duplicate, unknown, reordered, missing and
wrong-type properties; noncanonical references and corrupt retained bytes;
scheme/veto combinations; both program scopes and protected-primary path;
development identity carrying a valid Bdgrz-issued token; policy changes
between reads; a stale evaluator during rollout; higher-generation rollback with
identical bytes; source reset; lost activation acknowledgement; and legacy event
replay. Tests must demonstrate unavailable versus definitive denial without
using a projection or a missing provider as proof.

## Bounded Jev review

The [request](access-policy-artifact-jev-request.json) and
[response](access-policy-artifact-jev-response.json) preserve one focused
`jev-1.14.0` review. Probabilities of **yes** were: 0.05 for accepting duplicate
keys, 0.04 for an old evaluator ignoring unknown fields and authorizing, 0.03
for identical bytes at different generations being the same generation, 0.08
for one current veto triple satisfying both credential modes, 0.04 for an
otherwise valid Bdgrz token bypassing the disabled development-identity check,
0.03 for an existing-program-scoped grant authorizing creation of another
program, 0.04 for an unbound digest proving historical bytes, 0.94 for replaying
a legacy event without claiming current policy proof, 0.03 for the unqualified
rollout already being proved, and 0.08 for requiring a user or grant roster.
These are advisory signals about the supplied scenarios, not parser, evaluator,
source or deployment qualification. The unresolved product/security and
infrastructure decisions above remain open.
