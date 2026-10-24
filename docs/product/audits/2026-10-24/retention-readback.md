# Retention owner split readback

The combined source-wide retention aggregate is replaced by an immutable exact-content basis and one independent owner per HoldId. Commands retain their original actor, intent, expected revision and frozen first response through native acceptance. Different holds do not contend on a shared revision. Queries and the Type I source contributor compose authoritative owners. Generated population exports use the same native basis admission.

Behavioral RED: two regressions failed against the old owner. GREEN: strict Release build passed with zero warnings/errors; 140 focused tests and all 5,453 backend unit tests passed. All 22 Node tooling tests passed. The canonical design validator and implementation coverage scanner passed with no drift. Three bounded escaped fact payload cases remain below 48 KiB. Jev1.14.0 reviewed22 current source/dependency-hash sets; source/test review found no additional actionable defect in this bounded slice.

The optional web smoke class passed17 and failed11: digest-origin test-host configuration and stale application/advisor/header assertions remain locally recorded. This is not a web or deployment qualification claim.

The three retention mutation routes now use identified PUT paths. Basis recording and release require CommandId; placement uses HoldId. Current personal HTTP/admin authorization remains required. MCP mutation exposure stays disabled.

Existing combined retention history fails closed until a verified migration is selected and implemented; no data was deleted and no deployment/reset ran. Disposition remains disabled. Protected download/crypto and the other S31/S35 owners remain open work. Bare-operation routes/cross-route uniqueness remain blocked on the previously recorded Portia capability; per-first-owner writes are independently supported. No GitHub issues were created.
