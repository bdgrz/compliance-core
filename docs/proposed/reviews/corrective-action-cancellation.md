# CorrectiveAction cancellation and Finding severity

Partial implementation under #1176, against the locked Assurance work context.

Finding admission/replay uses low, moderate and high; Work priority retains its own
vocabulary. Severity RED executed 22 cases: 14 failed and eight passed; the earlier
focused GREEN passed 65 cases.

Cancellation admits a reasoned Compliance Lead decision on an open action, with
revision and finding-closure use fences. The existing Portia execution path retains
exact frozen response/retry behavior. Cancellation attribution and reason remain
separate from completion actor/evidence. Finding closure treats cancellation as an
outcome and excludes the canceller from independent review absent the existing exact
waiver. Work consumers remove cancelled open work; baseline history captures the
terminal fact with its reason. Incident follow-up remains completion-only.

Cancellation owner, workflow, Work and baseline tests were authored before
implementation. They were first executed at the assembled checkpoint, following
the user's deferral instruction; no observed pre-implementation RED is claimed.
Live Jev resolved `jev-1.14.0`, adverse statements 0.03–0.20. Direct source verified
the three largest candidates: cancellation reaches baseline capture (0.20),
canceller exclusion remains (0.19), and the existing Work projector receives the
new fact and recomputes open work (0.18).

Assembled Release build: zero warnings/errors. Affected business suites: 797 passed.
Full units and whitespace validation: pending.

Remaining: AW-09 finding-owned action-outcome reporting and proposed closure
lifecycle, ChangeCorrectiveAction and the rest of the approved domain. Coordinated
Finding source vocabulary/producers remain tracked separately in #1180. No complete
owner, workflow or context claim.
