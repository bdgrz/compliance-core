# Owner and reactor integration

The integration consolidates existing S22 bulk access decisions, S26 evidence/risk owners, S27 readiness owners, S28 auditor request/response/sample owners, and S30 advisor feedback work. It retains the already merged owner splits and the recovery corrections in PR #1087. This readback covers existing work; S29 and S31-S35 remain planned work.

## Resolved integration findings

- Bind auditor request terms to the persisted program before route-scoped mutations.
- Keep source-use reservations independent by first owner as well as root operation, generation and purpose. Child provenance persists the original first owner.
- Recover canceled risk and readiness decisions before assembling held proofs, then settle every held use after an interrupted cancellation.
- Distinguish unavailable immutable content from corrupt content. Receivers and process factories retry unavailable content and quarantine verified corruption.
- Replay an accepted bulk acknowledgment before checking newly changed reviewer eligibility. Keep the current restricted-application visibility check.
- Match bulk cancellation failure evidence to the exact admission and frozen selected item.
- Allocate fresh immutable IDs for feedback preparations before admission, permitting retry after an interrupted append.
- Replace removed-ledger test fixtures with actual split-owner histories and process execution; retain authorization, scope, replay, queue and failure coverage.

The internal source-use receiver catalog now names the pinned native content revision `SourceRevision` (`ulong`); the generic authorization interface reads the same value through its ignored alias. Bulk item contracts retain their owner-specific `ItemId` alongside an ignored generic source-owner alias.

## Review evidence

Independent reviews covered the recovered slices' tenant/program scope, retry identity, admission proofs, cancellation, held-use settlement and failure classification. The resulting concrete findings above have regression cases. Jev `jev-1.14.0` returned 0.91 for reservation isolation, 0.68 for cancellation factory recovery, 0.21 for a visible unresolved blocker, and 1.0 for pruning obsolete copies after verified integration. Those probabilities are advisory and are not acceptance proof.

## Validation

Final local build and unit results are recorded in the integration PR. The tooling tests, selected design validator and implementation drift checker are run on the consolidated source. Structural coverage counts are not a claim that every planned owner, process or feature is complete.
