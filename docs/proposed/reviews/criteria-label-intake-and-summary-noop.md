# Criteria label intake and summary no-op follow-up

Status: **design direction; neither protocol is fully implemented**. Label
reservation fences and exact-source release handlers are authored but are not
registered as complete orchestration. A same-text summary correction currently
returns pending because no durable exact-intent receipt exists.

## Independent label signals

Each relevant committed edition or label fact needs a recoverable signal. A
source-facing reactor can create one deterministic process record for the exact
source stream and event ID, then advance its checkpoint after that record is
durable. It must not wait at the shared checkpoint for a contested label. The
existing `ProcessSweep` can discover due records in bounded pages and isolate
failures, provided the label process is registered with `ProcessCatalog` and a
receiver is supplied. These records are independent coordination state, not a
roster in an edition or claim aggregate.

`ProcessRootReaction.StartAsync` cannot be used directly: it accepts only
`INativeOperationFact` roots, while the existing criteria edition and label
participant events are ordinary domain events. A source-specific intake must
verify the trigger's actual stream, aggregate and event metadata; freeze a small
immutable payload; create a deterministic `ProcessRecord` with an exact source
reference; and confirm that any existing record has the same source and intent.
The driver requires a typed `IRequest<ReceiverOutcome>` adapter for its steps;
the current label commands return `IRequest` instead. Creation uncertainty must
not permit a checkpoint to advance unless the record is observed or a durable
create outcome is otherwise established.

Trigger both sides of each transition: edition requests and participant
reservations; edition activations or renames and participant confirmations;
and edition cancellations, refusals, abandonment or completed renames. A
participant can arrive after an edition terminal fact, so the terminal path
must establish the selected claim's target-side request fence rather than treat
an unobserved reservation as absent. If the reserve wins the claim append race,
the fence retries and releases that exact generation; if the fence wins, a
stale reservation cannot rebase across it. An unrelated holder remains intact.
For a completed rename, release the old claim only after checking the exact new
reservation, rename intent and decision, and new-label confirmation. Expired or
compacted source must remain unresolved unless retention and lineage are
qualified. Source events, not the discovery index, authorize decisions.

The source intake, receiver adaptation, and process definition are still open.
The process store's due-time paging, CAS, sync durability, unknown-commit
recovery, and source-to-record reconciliation must be qualified on the actual
provider before this is relied on. A failed signal must remain independently
retryable; a permanently pending signal must not block unrelated operations.
These requirements are not established merely by having an in-memory queue or
by registering the existing synchronous realm-wide reactors.

## Same-text summary correction

Persisting a receipt only for no-op summaries is unsafe: a concurrent structural
operation could use the same `(edition, identifier, operationId)` in the entry
stream while a different intent wins the receipt stream. Both could then appear
accepted. All entry operations must first compete to create the same
operation-scoped immutable intent, binding normalized intent, actor, target and
admission. Exact retries compare it; changed-intent reuse conflicts. This owner
contains one intent and one terminal outcome, not a list of children or retries.

For a same-text summary, first durably establish the operation intent. Then
obtain a qualified current observation of the exact entry revision and summary.
That observation can be the no-op's linearization point, after which a single
`no change` outcome is recorded in the operation owner. The outcome records the
source position and evidence required to verify the observation; it must not
claim that the summary changed. A concurrent later entry change does not
retroactively change that earlier no-op decision. A missing or unqualified
fresh-head certificate leaves the operation pending. A timed-out receipt append
is resolved by exact source replay or a target-side fence; it is never assumed to
have failed. Current authorization is still required before retry results are
disclosed.

The existing `ICriteriaFreshEntryHead` has no qualified production provider, and
the current structural and summary paths do not share an operation identity
claim. Until both are implemented and race-tested, leave the no-op pending; a
separate no-op-only receipt would introduce an idempotency hole.

## Target-fence review

The exact [request](criteria-label-fence-jev-request.json) and
[response](criteria-label-fence-jev-response.json) used `jev-1.14.0`. Jev
returned probability 0.23 that a delayed reservation should succeed after the
fence wins, 0.92 that a reservation which wins first must be released by the
fence path, 0.06 that fencing can release an unrelated holder, 0.12 that a
different terminal fact is an exact retry, 0.04 that an old confirmation
reacquires a released reservation, 0.09 that a no-op-only receipt safely shares
an identity with bypassing structural operations, 0.04 that a quiet page proves
a fresh head, 0.04 that `ProcessRootReaction` accepts a non-native source, and
0.17 that a shared checkpoint can advance while waiting on a contested claim.
These are review signals. The fence outcome still depends on the deployed
event store enforcing the stated expected-position and atomic batch premises;
the new race tests have been authored and not run.
