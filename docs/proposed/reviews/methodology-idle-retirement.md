# Methodology idle-line retirement

Tracking: #1176. Implements the locked Firm methodology line's retirement command
through `RetireMethodologyTemplateV3`, its handler and a personal current-operator
authorizer. The existing owner decides the idle-only invariant atomically; no
publication is required. Exact operator/reason retries append nothing; changed
terminal intent conflicts. An open draft refuses retirement without a fact.

The test commit precedes implementation. Its eight authored cases cover idle
retirement, retry and changed intent, open-draft refusal, system/nonoperator/removed
operator refusal (including retry after authority loss), exact identities and
required reason. They have **not been compiled or executed**; the coordinator owns
the serialized validation lane. This is source preparation, not observed RED/GREEN.

Platform access defines the operator as the platform super administrator. The
current `IPlatformOperatorAccess` reads that authoritative roster; it has no scoped
operator permission distinction. Both existing draft authoring and approval use
this seam. The new command names `methodology.approve` and retains that current
super-administrator meaning rather than inventing tenant role access.

Shared integration requires command JSON metadata, handler and authorizer
registration, and HTTP/MCP replacement of the old retirement request. Existing V2
retirement history remains decodable. No deployment, full methodology application
or whole-context acceptance is claimed.
