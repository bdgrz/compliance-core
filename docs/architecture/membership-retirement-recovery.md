# Membership retirement recovery

[#1040](https://github.com/bdgrz/compliance-core/issues/1040) replaces projected
membership-retirement discovery with native source coverage and exact receiving
scope. It is a Member continuation slice of CHOREO-03/06 under
[#1022](https://github.com/bdgrz/compliance-core/issues/1022). Administrator
withdrawal/restoration fencing, invitation admission, and the remaining public
Member/TeamMember retry contracts retain their separate pending acceptance.

`MemberDeprovisioned` identifies the exact membership episode. An authoritative
member fold selects at most one original retirement and its completion, including
after a later registration. It creates an immutable value proof containing tenant,
member, user, episode, native source event/revision/position, concrete-payload
SHA-256, and original actor. Receivers consume values; no live aggregate is passed
to another owner. A new episode requires the predecessor's cleanup completion.

The initial `CompleteMemberRetirement` command has required typed identity derived
from its retirement source, member owner and episode. It scans both native team
assignment and grant source histories from the start, through each read's ending
cut. It executes every matching exact source fact in canonical target order.
Directory rows and moving projection checkpoints cannot omit source relationships.
Source cuts are saved only after receiving effects succeed, in technical checkpoint
storage. Discovery checkpoints never authorize skipping unfinished receivers; every
interrupted recovery rescans. Domain events contain business retirement/completion
authority, not scan cursors, stages, attempts or request-receipt events.

Three independent tenant reactors consume retirement, team assignment and member
grant facts. They retain their checkpoints through dependency failures and use
sustained bounded-backoff recovery rather than faulting the host after ten failed
passes. Existing facts through the retirement discovery cut are covered by
the initial scan. Later facts are checked against the permanently committed
retirement of their own episode. An assignment observed before retirement can be
acknowledged while active, because retirement discovery starts after the parent
retirement commits. A fact beyond that cut has its own independent consumer.

`CleanupRetiredMembershipRelationship` carries the exact source position/event,
kind, target, episode and exact retirement event. Reactors dispatch this command
only after authoritative retirement proof exists; active observations create no
mutation command whose retry could later change meaning. Its typed identity includes source event,
canonical target stream, episode, native source position and retirement event;
stream identity distinguishes effect
kind and tenant. System authorization precedes lookup. The receiver verifies the
exact native source and selects authoritative parent retirement. Missing or
unknown source authority is transient; it never grants permission to remove or
complete. A current active episode needs no retirement effect.

Team removal consumes the exact native assignment event and episode. Grant
revocation consumes its immutable issuance and member episode. A mismatch is
superseded work with no append. Replaying an old operation cannot remove a newer
assignment, even in the same episode. The new source fact has its own effect
identity. An active team assignment cannot be overwritten by another episode;
assignment admission settles only a proven retired predecessor. Compensation
targets the assignment fact selected by its own execution, preserving newer
authority. Ordinary current authorization and administrator availability checks
remain in the public handlers.

Completion binds the original termination, episode and operation. It recognizes
its selected native completion before current lifecycle state, so an exact old
retry after registration returns its original success without appending. Unknown
receiving writes, coverage saves and reactor checkpoints recover from native
facts. Original grant revocation attribution comes from the proven retirement;
execution-attempt and logical effect identities remain separate. No expiry or
missing read releases authority.

The member retains current lifecycle state and at most one selected retirement
and completion flag. Team assignments retain their current source identity; grants
retain their immutable issuance identity. There is no lifetime receipt map or
relationship cap. Physical member/relationship history still grows across
lifecycles. Initial retirement currently scans tenant relationship history for
complete coverage; this cost is separate from domain retained state and can later
use a proved consistent directory snapshot plus authoritative tail.

Retirement, cleanup-completion and team-removal facts are direct pre-release v2
contracts. Superseded shapes and projected cleanup paths are removed; no migration
fallback is added. Public HTTP/MCP contracts are unchanged in this slice, and the
new recovery commands are system-only internal contracts. No queue listener,
deployment or environment reset is introduced.

The first RED deleted newer membership authority from a delayed cleanup. The
second RED overwrote an active assignment with another membership episode. Tests
cover complete unprojected source discovery beyond 1,000 assignments, late facts
beyond a saved cut, exact source retries, changed scope, native serialization,
unknown receiving/coverage outcomes and autonomous recovery. Jev selected native
source cuts plus late receivers, exact receiving scope and technical progress
storage with probability 1.00 each; source and executable tests govern acceptance.
