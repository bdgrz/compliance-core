# MethodologyTemplateDraft

Partial implementation under #1176, against the locked Firm methodology context.

The separate platform draft stream owns revision history, every revision author,
independent review and third-person approval. Revisions clear acceptance. Terminal
approval/discard preserves exact terminal retry behavior and refuses changed intent.
Opening is bound to the exact committed line-start fact and immutable entry digest;
the internal handler and global reactor are System-only, with no public route.

The owner behavioral RED executed 19 cases: 18 failed and one passed. Eight opening
cases were authored before implementation and first executed at the assembled
checkpoint, following the user's deferral instruction. Source review is advisory.

Live Jev resolved `jev-1.14.0`: owner guard properties 0.95–0.98; owner adverse
statements 0.05–0.06. Opening guard properties were 0.95–0.97 and adverse statements
0.06–0.09. Original-opening retry after later revisions was 0.73; direct source
preserves the original tuple and returns without changing entries. Pinned Portia
serializer source resolves known discriminator/schema pairs directly, so retaining
known V3 alongside the new V4 does not force an upcast.

Assembled Release build: zero warnings/errors. Affected business suites: 797 passed.
All 8,327 backend units passed with zero failures/skips; whitespace validation passed. Inventory explicitly retains the
line-start producer gap and five missing human-operation routes.

Remaining: human commands/handlers; line start, publish and discard choreography;
entry materialization, parent application completion and superseded contribution
migration; explicit competing-append and complete workflow qualification. No
complete owner or context claim.
