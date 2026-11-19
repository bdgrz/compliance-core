# Criteria seed metadata owner

Status: **source written; tests not executed**, 2026-11-05. This is one
independent owner for the initial metadata of a platform seed edition. It does
not represent implementation or end-to-end qualification of seed publication.

- **Identity:** edition ID; the source address is
  `stream://bdgrz/criteria-{editionId}/seed-metadata-{editionId}`.
- **Invariant:** at most one initial metadata fact is recorded for that edition.
  It binds one exact operation, seed version and fingerprint, committed edition
  request event ID, framework, original publication time, coverage note, source,
  content rights, numbered-criteria completeness and actor. It carries no
  criteria or support-gap collection.
- **Lifecycle:** unrecorded → recorded. The exact retry is a no-op; a different
  operation, request, actor or metadata intent conflicts. Replay rejects a
  duplicate fact, wrong edition, invalid fingerprint or malformed attribution.
  Concurrent first writes contend on the one expected-empty source stream.
- **Authority:** the aggregate validates the supplied values, not their origin.
  A trusted, non-public system caller must read and verify the exact committed
  edition request and its seed version/fingerprint, and must verify its system
  principal before calling the owner. A nonempty event ID or actor string is not
  that proof. A fingerprint is a release-compatibility binding, not evidence that
  every entry or disclosure exists or that source coverage is complete.

Focused tests are authored in `CriteriaSeedMetadataTests`: identity, exact retry,
changed intent, invalid source binding, replay, and competing appends. The user
requested deferring compilation and test execution until the full feature is
assembled; those tests have not been run. Shared serializer and event-catalog
registration, a source-verifying bootstrap caller, native provider behavior,
and the cross-owner publication workflow remain integration requirements.

The exact [Jev request](criteria-seed-metadata-jev-request.json) and
[response](criteria-seed-metadata-jev-response.json) use `jev-1.14.0`. On the
provided source, Jev returned 0.92 for an exact retry succeeding without a
second fact, 0.06 for accepting a changed source event as an exact retry, 0.03
for accepting foreign-edition replay, and 0.05 for accepting a second replayed
fact. It returned 0.03 that the aggregate itself verifies a supplied source
event, 0.02 that an actor string proves a trusted system caller, and 0.02 that
the metadata fact proves complete seed coverage. These are advisory signals;
the caller obligations and executable qualification remain necessary.
