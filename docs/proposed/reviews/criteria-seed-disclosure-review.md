# Criteria seed disclosure owner

Status: **partial**, 2026-11-05. The source owner and focused tests are authored;
they have not been compiled or run. The trusted seed bootstrap, source-bound
admission and event-serialization registrations remain necessary before this
owner can be used as durable seed authority.

One `(edition, category, code)` identifies one immutable, independently stored
coverage disclosure. Its own event binds a seed-release version and fingerprint,
the exact edition-request event ID, operation identity, note and actor. The
owner records one fact; an exact retry is a no-op and changed intent conflicts.
Its platform stream is in the edition's `criteria-{editionId}` area, with a
separate resource per disclosure. It stores no list of gaps and never puts
per-disclosure outcomes on the edition.

An event ID and syntactically valid fingerprint are **not** authority. The
owner does not read another stream and cannot verify that the referenced edition
request actually exists or binds that version and fingerprint. A trusted,
system-only seed handler must read the exact committed edition request, verify
the seed binding and legitimate system origin, and only then invoke the owner.
The fingerprint is a compatibility check against mixed seed releases, not proof
that every seed entry or disclosure was imported. An unknown append remains
pending until exact source reconciliation; competing first writes rely on the
event store's expected-position append.

The focused tests cover identity separation, no-op exact retry, changed-intent
refusal without another fact, invalid input, replay and competing appends.
They still require the shared event/JSON registration. Integration must add
tests showing a forged or mismatched edition-request proof is rejected before
any disclosure fact is written, plus seed coverage and recovery tests.

## Jev review

The live `jev-1.14.0` [request](criteria-seed-disclosure-jev-request.json) and
[response](criteria-seed-disclosure-jev-response.json) include the exact owner
and event source. Jev rated an exact retry raising another event 0.04, changed
note refusal 0.96, acceptance of an unverified nonempty source ID 0.96, direct
source verification by this owner 0.03, separate edition identities 0.97,
foreign-edition replay refusal 0.93, two expected-zero writes both committing
0.09, fingerprint syntax proving actual seed content 0.03, and a parent roster
in this owner 0.05. These are review signals, not proof of provider atomicity,
serialization, trusted admission or runtime correctness.
