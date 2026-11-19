# Criteria edition framework review

The draft command and the requested, activated and drafted source facts carry an
explicit framework. The only currently supported value is `tsc`; it is checked
on admission, seed binding and replay and cannot change between those facts.
The canonical draft intent, and therefore operator admission digest, includes
the framework. The runtime index derives it from the original request and
rejects a mismatched seed metadata fact. The draft status view exposes the
observed value, or null when no draft request has been observed.

This changes the provisional HTTP/MCP request contract: a caller must send
`framework: "tsc"`. The three new event payload contracts also require the
field. Missing fields do not default to `tsc`; if any provisional source
events were persisted without it, a deliberate migration is required before
replay. No claim is made here that such persisted events do or do not exist.
Source field checks do not establish a fresh broker head.

The exact [Jev request](criteria-edition-framework-jev-request.json) and
[response](criteria-edition-framework-jev-response.json) used `jev-1.14.0`.
Probability of yes: the framework is carried through the three facts 0.97;
an unsupported framework is newly accepted 0.07; the draft admission digest
includes framework 0.74; replay accepts a changed framework 0.08; missing
history is silently fabricated as `tsc` 0.17; a seed can bind another
framework 0.09; the runtime index derives framework from the source request
0.95; seed metadata may silently rewrite it 0.09; these checks establish a fresh
broker head 0.05; and the required request field changes wire compatibility 0.75.
These are advisory review signals, not build, replay, migration or provider
evidence. Focused tests were authored, not run.
