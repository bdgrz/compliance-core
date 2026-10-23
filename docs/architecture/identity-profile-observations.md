# Identity profile observations

[#1038](https://github.com/bdgrz/compliance-core/issues/1038) addresses ACCESS-17.
Provider display text is presentation, so it is retained in Portia audit sessions
and projected into the platform-user directory. It is never an identity, account
association, permission, or source of access authority.

`UserIdentity` retains provider association and terminal revocation. Its domain
stream contains registration and, when retired, revocation. Profile observations
and authentication audits use separate physical sessions in the same source
area. They neither advance the authority stream's position nor compete with
revocation for that position. The profile replay handler has been removed.
There is no additional profile aggregate that consults another live owner.

The continuation preserves authoritative authentication and rehydrates the identity
before admitting provider presentation. An identity already revoked at that read
cannot admit an observation. Admission proves the active identity at that revision;
revocation after admission can commit before the audit append. Such an audit
records historical admission and cannot reactivate the identity. Authentication
and session revocation checks continue to read current identity authority.

Each observation carries a server-created immutable registration proof: identity,
source event, source revision, zero-based physical source position, and SHA-256
of the concrete generated registration payload. The observed user is the registered
account, not provider display text. Audit metadata preserves the admitted aggregate
revision and execution/actor provenance. No route or public command accepts an
observation proof. Invalid/control-character names normalize to null; null clears
the current projected name.

The platform-user projector consumes one global cursor over the identity source
area, including its audit sessions. Registration is committed before profile
admission and persistence, so its directory row precedes observation projection.
The same cursor covers updates, null clearing, restart and rebuild. The directory
retains the latest admission time and event identity; delayed older audits cannot
overwrite a newer name or name removal. Equal admission times use ordinal event
identity as a deterministic tie breaker. This presentation ordering grants no
security authority. Directory existence still comes from registration. Display
text cannot create a platform user or change its account identity.

Audit delivery can repeat after an unknown append outcome. A repeated login can
therefore produce another audit, without another registration, security revision,
or authority replay record. This is observational history, not business mutation
deduplication or a durable request-receipt collection. No automatic external send
is involved. The concrete audit serialization is covered by persistence tests.

Current authority retains only scalar association/revocation state and one
registration proof. Audit storage and its projection replay grow with observation
traffic, independently from the at-most-two-record identity authority replay.
Existing histories containing profile state facts need the accepted pre-release
cutover; changing retained fields does not shorten those histories. The profile
contract is now version 2 with required source proof. No legacy replay path,
deployment or environment reset is added here.

The focused RED wrote 128 observations and found 129 authority records. GREEN
retains one registration record, independently persists observations, preserves
revocation through admission/append races, and rebuilds the projected name through
its global source cursor. Jev selected audit admission and historical proof race
semantics with probability 1.00 for both; these are advisory design signals.
ACCESS-12's recovery operation and Member/TeamMember episode work remain open.
