# Firm services boundaries review

Status: **authority unresolved**, 2026-11-05. The small-record structure below
is a proposal; it does not establish that the product can already enforce the
required cross-owner decisions. M0-D26 and M0-D27 remain the policy authority.

## Findings and design

- An evaluation must not copy the full service revision set into one
  `IndependenceEvaluated` event, nor append one event to an evaluation for each
  service. An independent exact service-revision witness can establish a positive
  impairment finding. A streamed fold avoids loading all services into memory,
  but using that fold to authorize an aggregate write still makes the decision
  depend on all children and is withdrawn. The five service kinds are a fixed
  policy vocabulary and may remain together in a rule-set version.
- An attest record package previously embedded its manifest and a single content
  reference. Independent entries and incremental output avoid a package-sized
  aggregate or one per-child event on a long-lived parent. M0-D17's ZIP and
  `manifest.json` remain the required delivery format.
- A read of service history or an impairment projection cannot protect a later
  append. An impairing service can commit between the read and acceptance,
  assignment or audit outcome; a later gap cannot reverse that earlier decision.
  A recording must not be refused simply because it is impairing. Protected
  actions need authority and ordering against source writes and ratification;
  unknown outcomes are pending and retries use stable identity. No satisfactory
  local protocol is established by this review.

## Explicit policy conflict and local alternatives

M0-D26 requires a **platform-enforced** refusal if any service in a client's
look-back period is impairing under the active rule. Its acceptance guarantee
is not equivalent to a later displayed gap, an eventually consistent `clear`
projection, or a partner's signed declaration. The former bounded-window chain
is also withdrawn: collectively, its per-service and per-action attempts are a
client-wide ledger, even if each window is individually small. A scalar counter
whose owner appends for every service change merely hides the same growth.

Per-service classification and per-assignment outcomes remain useful local
facts. A single relevant impairment is a sufficient witness to deny or flag;
finding no witness in a partial read is not sufficient to permit. The first
person/client practice can be committed once in that specific compartment;
each later assignment of the same practice needs no compartment history event.
These facts reduce ownership but do **not** prove the global negative requirement
or resolve a service write racing an attest acceptance. Service revision, void,
look-back dates and rule-set ratification can change which witness matters.

One potential product alternative is partner-attested acceptance with separate
independent service findings and immediate per-finding gaps; it moves the
no-impairment determination to the signer and weakens the accepted strict
platform guard. Another is to keep the current policy and investigate a
genuinely local enforcement rule or provider capability, without a child-wide
write decision or hidden client ledger. The former is **not an approved product
change**, and no workable authority for the latter is yet proven. Do not ship
or describe FS-03/04/11 as satisfied based on either idea alone.

## Qualification blockers

1. **Policy and ordering:** resolve the all-service negative rule without a
   child-wide write decision, client-wide attempt ledger or unauthorized policy
   change. Establish what commits first when a service, ratification and
   protected action race. Participant outcomes must handle ambiguous commits,
   changed intent, delayed operations, and honest service corrections. No
   particular event-store fencing or cross-stream atomicity is assumed.
2. **Read and delivery completeness:** independently of the write decision,
   determine how the M0-D27 package obtains a repeatable and complete source
   boundary for read-side streaming. Prove treatment of concurrent writes,
   version retention, missing records, partial reads and retry. A cursor,
   watermark, computed digest or completion flag cannot by itself establish
   that all source writes were covered. Package delivery must not claim complete
   evidence if the required source coverage has not been established.
3. **Compound admissions:** an assignment needs both compartment admission and
   independence ordering; prove they remain valid through the assignment append,
   including close, retries, and compensation where one participant succeeds and
   another refuses. A proposed coordinator must not accidentally create one
   lifetime stream containing all assignments or service changes.

The event-store and cross-context proof is future work; Jev cannot provide it.
No record in this proposal is permitted to hold all entries or all service
revisions. A source-boundary reference must not be a renamed manifest or an
aggregate containing a hidden unbounded collection. No supported complete
boundary is established merely by stating that its reference is compact.

## Adversarial cases required before implementation is qualified

Exercise service commit before and after a protected append; unknown append
outcome; duplicate versus changed-intent retries; ratification racing acceptance;
two simultaneous advisory/attest acceptances; void and correction races;
compartment success followed by independence refusal; participant timeout; and
source cut formation while a producer is still writing. Validate streamed empty,
single, and large collections against the same independently reconstructed source
cut, including missing and duplicated entries and retention failure. Verify the
complete M0-D17 manifest and delivered ZIP, not only an aggregate status flag.

## Jev review

The request and raw response are preserved alongside this review:
[request](firm-services-boundaries-jev-request.json),
[response](firm-services-boundaries-jev-response.json). The live API returned
`jev-1.14.0`. Values are probabilities of the stated yes/no question, not proofs.

| Observation | P(yes) | Interpretation |
| --- | ---: | --- |
| Acceptance can commit after a racing impairing service | 0.85 | Supports the identified race. |
| Assignment can commit after a racing impairing service | 0.79 | Supports further review of the race; the prior 0.8 triage threshold is not proof or a calibrated safety measure. |
| An eventual gap prevents an already committed acceptance | 0.15 | Does not support relying on gap detection as a guard. |
| Pending protocol rejects service truth merely because it impairs | 0.05 | Compatible with recording the truth. |
| The candidate protocol establishes that the mechanism already exists | 0.03 | Does not establish implementation or storage guarantees. |
| A projection watermark alone proves all service revisions are included | 0.03 | Does not establish completeness. |
| Evaluation aggregate must store a growing revision list | 0.10 | Supports independent contributions. |
| Package aggregate must store a growing entry list | 0.07 | Supports independent entries. |
| Package completeness requires coverage of every relevant source write | 0.97 | Supports the source-cut prerequisite. |

Each judgment is evaluated against the concrete counterexamples and accepted
decisions above. Scores reflect the supplied cases, not implementation evidence,
and do not resolve the blockers.

## Local-decision Jev review, 2026-11-05

The later request and raw response are [here](firm-services-local-decisions-jev-request.json)
and [here](firm-services-local-decisions-jev-response.json). Jev `jev-1.14.0`
assigned 0.96 to an acceptance violating policy after a prior impairing service,
0.85 to a linked per-service attempt sequence retaining a client-wide ledger,
and 0.98 to one relevant service demonstrating impairment. It rated 0.03 for a
partial search's absence proving no impairment, 0.02 for a partner declaration
alone preserving platform enforcement, and 0.04 for a lagging clear projection
proving no impairment. It rated a new owner event for each same-practice
assignment 0.06. These synthetic-case signals support the counterexamples; they
do not prove a replacement authority, source completeness or provider behavior.
