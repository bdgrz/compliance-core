# Criteria seed entry and operation claim

Status: **partial**, 2026-11-05. Source and focused tests are authored but have
not been compiled or run. No seed bootstrap writer is registered.

An entry operation claim owns one `(edition, identifier, operation)` identity,
independent of the entry owner. A human claim and a seed claim cannot both own
that identity. The seed claim binds one fixed catalog intent and its edition
request; a separate proposal identifies the exact committed claim. Source
verification requires the fixed compiled release, the exact committed edition
request and operation claim, matching payloads and the common-area order
`request < claim < proposal`. A missing source fact stays pending. Point-of-focus
entries also bind their exact parent acceptance; resolution checks that acceptance
preceded the proposal and that the parent was effective at the publication cut.
No edition owner stores its children or their outcomes.

The aggregate methods do not authenticate a system principal or read source.
An internal trusted caller must do both, qualify current draft state and expected
position, and recover ambiguous outcomes before writing. A different proposal
generation from the immutable claim is pending: proving an appropriate later
target fence is not yet implemented. A fingerprint proves compatibility with a
compiled release; it does not prove complete import, a current draft, or a
qualified publication cut. The seed cannot be enabled until those gaps, label
and witness coordination, coverage, serialization, and runtime integration are
complete. Source discovery and current-head providers remain unqualified.

## Jev review

The [request](criteria-seed-entry-jev-request.json) includes the exact source
reviewed and the [response](criteria-seed-entry-jev-response.json) records the
live `jev-1.14.0` output. Jev assigned 0.10 to both human and seed claims owning
one operation, 0.80 to the exact source binding and order check, 0.96 to
leaving unobserved facts unresolved, 0.04 to a fingerprint proving current draft
state, 0.06 to these methods authenticating or loading source themselves, 0.07
to accepting a different fixed summary, 0.09 to maintaining a child roster,
0.93 to rejecting a changed immutable claim intent, and 0.90 to leaving a later
execution generation unresolved. It assigned 0.05 to a registered writer being
shown by the supplied source.

Jev assigned 0.65 to exact claim retries comparing execution generation; source
inspection disagrees. The immutable claim intentionally does not compare retry
generation, and a later execution requires a separate proven fence. Model scores
are advisory and do not prove event-store behavior, source currentness, identity
authentication, serialization or test results.
