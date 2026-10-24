# Finding closure content recovery

Six behavioral regression rows first failed on the existing receiver/verifier: closure intent, authored decision, admitted child manifest, child admission intent, authored action, and derived action facts were each temporarily withheld. Each failure was the permanent-error classification; original closures were otherwise resumable.

The receiver and verifier now catch ImmutableContentUnavailableException before the integrity base class and return transient conflicts. Genuine corrupt content retains its permanent classification. The six new cases prove no action grant or parent cancellation while unavailable, followed by a successful grant and completed closure after the same bytes return. The related focused suite passes18 tests; strict Release build has zero warnings/errors. The full backend unit suite passes all5,459 tests; whitespace/design/implementation checks pass.

Jev1.14.0 directly reviewed both current implementation hashes and dependency excerpts. The acquire receiver incomplete-path signal0.75 was checked against the named Placeholder path: it is returned only with SupersededReason for an already-closed parent, and the generic receiver returns that terminal no-op before any requirement can be pinned/granted. The successful recovery tests exercise the real grant path. No unfinished grant path was demonstrated. Verifier signals were below0.65. No GitHub issues were created.

This bounded repair does not classify every other integrity catch in the repository. A local inventory is queued for further semantic review; count alone is not evidence of defects.
