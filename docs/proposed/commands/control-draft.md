# ControlDraft commands

Selected command contracts: **10**. Complete selected contracts and companion source evidence: [owner command JSON](../data/commands/control-draft.json).

| Target command | Selected source name | Kind | Identity | Fields | HTTP routes |
| --- | --- | --- | --- | --- | --- |
| ApproveControl | ApproveControl | http | CommandId | 10 | PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/draft/approvals/{command_id} |
| CreateControlDraft | CreateControlDraft | http | ControlId | 5 | PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id} |
| DesignateControlOwnerPerson | DesignateControlOwnerPerson | command | CommandId | 7 | [Placeholder: value not recorded] |
| DiscardControlDraft | DiscardControlDraft | http | CommandId | 6 | PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/draft/discards/{command_id} |
| ProposeControlRetirement | ProposeControlRetirement | http | CommandId | 8 | PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/retirement-proposals/{command_id} |
| ProposeControlSuccessor | ProposeControlSuccessor | http | CommandId | 7 | PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/successors/{command_id} |
| RetireControl | RetireControl | http | CommandId | 9 | PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/retirements/{command_id} |
| ReviewControl | ReviewControl | http | CommandId | 8 | PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/draft/reviews/{command_id} |
| ReviseControlDraft | ReviseControlDraft | command | CommandId | 6 | [Placeholder: value not recorded] |
| WithdrawControlProposal | WithdrawControlProposal | http | CommandId | 6 | PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/proposal-withdrawals/{command_id} |

## Selected command details

### ApproveControl

| Selected contract detail | Value |
| --- | --- |
| Target command name | ApproveControl |
| Selected source command name | ApproveControl |
| Name decision | The selected source name is retained. |
| Owner | ControlDraft |
| Contract record status | [Placeholder: value not recorded] |
| Kind | http |
| Logical identity | CommandId; Uuid; required=true; CommandId binds one immutable authored intent to the exact owner identity and expected revision: an exact retry returns the original result, a changed intent under the same CommandId is refused |
| Entry point | [Placeholder: value not recorded] |
| MCP | [Placeholder: MCP availability not recorded] |
| Selected semantic events | None listed |
| Protocols | [Placeholder: value not recorded] |
| Policy preservation | [Placeholder: value not recorded] |
| Contract changes | [Placeholder: value not recorded] |
| Guard order | [Placeholder: value not recorded] |
| Response contract | `{"mode":"immediate_original_result","responseType":"Unit","firstHttpStatus":200,"terminalResultType":"Unit"}` |
| Expected revision scope | `{}` |
| Authorizer sources | [Placeholder: value not recorded] |
| Source contract | [Placeholder: value not recorded] |
| Primary handler | [Placeholder: value not recorded] |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| TenantId | Uuid | true |
| ProgramId | Uuid | true |
| ControlId | Uuid | true |
| ExpectedRevision | long | true |
| AcceptedReviewDecisionId | Uuid | true |
| EffectiveFrom | DateOnly | true |
| Rationale | string | true |
| SeparationOfDutiesWaiverId | Uuid? | false |
| ImpactDigest | string? | false |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/draft/approvals/{command_id} | [Placeholder: value not recorded] |

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]

### CreateControlDraft

| Selected contract detail | Value |
| --- | --- |
| Target command name | CreateControlDraft |
| Selected source command name | CreateControlDraft |
| Name decision | The selected source name is retained. |
| Owner | ControlDraft |
| Contract record status | [Placeholder: value not recorded] |
| Kind | http |
| Logical identity | ControlId; Uuid; required=true; caller-supplied immutable creation identity: an exact retry with the same id and authored intent returns the original result; the same id with changed intent is refused; a different id is a different record |
| Entry point | [Placeholder: value not recorded] |
| MCP | [Placeholder: MCP availability not recorded] |
| Selected semantic events | None listed |
| Protocols | [Placeholder: value not recorded] |
| Policy preservation | [Placeholder: value not recorded] |
| Contract changes | [Placeholder: value not recorded] |
| Guard order | [Placeholder: value not recorded] |
| Response contract | `{"mode":"immediate_original_result","responseType":"ControlRegistration","firstHttpStatus":200,"terminalResultType":"ControlRegistration"}` |
| Expected revision scope | `{}` |
| Authorizer sources | [Placeholder: value not recorded] |
| Source contract | [Placeholder: value not recorded] |
| Primary handler | [Placeholder: value not recorded] |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| ControlId | Uuid | true |
| TenantId | Uuid | true |
| ProgramId | Uuid | true |
| Identifier | string | true |
| Content | ControlDraftContent | true |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id} | [Placeholder: value not recorded] |

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]

### DesignateControlOwnerPerson

| Selected contract detail | Value |
| --- | --- |
| Target command name | DesignateControlOwnerPerson |
| Selected source command name | DesignateControlOwnerPerson |
| Name decision | The selected source name is retained. |
| Owner | ControlDraft |
| Contract record status | [Placeholder: value not recorded] |
| Kind | command |
| Logical identity | CommandId; Uuid; required=true; CommandId binds one immutable authored intent to the exact owner identity and expected revision: an exact retry returns the original result, a changed intent under the same CommandId is refused |
| Entry point | [Placeholder: value not recorded] |
| MCP | [Placeholder: MCP availability not recorded] |
| Selected semantic events | None listed |
| Protocols | [Placeholder: value not recorded] |
| Policy preservation | [Placeholder: value not recorded] |
| Contract changes | [Placeholder: value not recorded] |
| Guard order | [Placeholder: value not recorded] |
| Response contract | `{"mode":"immediate_original_result","responseType":"Unit","firstHttpStatus":200,"terminalResultType":"Unit"}` |
| Expected revision scope | `{}` |
| Authorizer sources | [Placeholder: value not recorded] |
| Source contract | [Placeholder: value not recorded] |
| Primary handler | [Placeholder: value not recorded] |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| TenantId | Uuid | true |
| ProgramId | Uuid | true |
| ControlId | Uuid | true |
| ExpectedRevision | long | true |
| PersonId | Uuid? | true |
| Rationale | string | true |

#### HTTP routes

[Placeholder: no HTTP route is recorded for this command.]

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]

### DiscardControlDraft

| Selected contract detail | Value |
| --- | --- |
| Target command name | DiscardControlDraft |
| Selected source command name | DiscardControlDraft |
| Name decision | The selected source name is retained. |
| Owner | ControlDraft |
| Contract record status | [Placeholder: value not recorded] |
| Kind | http |
| Logical identity | CommandId; Uuid; required=true; CommandId binds one immutable authored intent to the exact owner identity and expected revision: an exact retry returns the original result, a changed intent under the same CommandId is refused |
| Entry point | [Placeholder: value not recorded] |
| MCP | [Placeholder: MCP availability not recorded] |
| Selected semantic events | None listed |
| Protocols | [Placeholder: value not recorded] |
| Policy preservation | [Placeholder: value not recorded] |
| Contract changes | [Placeholder: value not recorded] |
| Guard order | [Placeholder: value not recorded] |
| Response contract | `{"mode":"immediate_original_result","responseType":"Unit","firstHttpStatus":200,"terminalResultType":"Unit"}` |
| Expected revision scope | `{}` |
| Authorizer sources | [Placeholder: value not recorded] |
| Source contract | [Placeholder: value not recorded] |
| Primary handler | [Placeholder: value not recorded] |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| TenantId | Uuid | true |
| ProgramId | Uuid | true |
| ControlId | Uuid | true |
| ExpectedRevision | long | true |
| Rationale | string | true |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/draft/discards/{command_id} | [Placeholder: value not recorded] |

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]

### ProposeControlRetirement

| Selected contract detail | Value |
| --- | --- |
| Target command name | ProposeControlRetirement |
| Selected source command name | ProposeControlRetirement |
| Name decision | The selected source name is retained. |
| Owner | ControlDraft |
| Contract record status | [Placeholder: value not recorded] |
| Kind | http |
| Logical identity | CommandId; Uuid; required=true; CommandId binds one immutable authored intent to the exact owner identity and expected revision: an exact retry returns the original result, a changed intent under the same CommandId is refused |
| Entry point | [Placeholder: value not recorded] |
| MCP | [Placeholder: MCP availability not recorded] |
| Selected semantic events | None listed |
| Protocols | [Placeholder: value not recorded] |
| Policy preservation | [Placeholder: value not recorded] |
| Contract changes | [Placeholder: value not recorded] |
| Guard order | [Placeholder: value not recorded] |
| Response contract | `{"mode":"immediate_original_result","responseType":"ControlRetirementRegistration","firstHttpStatus":200,"terminalResultType":"ControlRetirementRegistration"}` |
| Expected revision scope | `{}` |
| Authorizer sources | [Placeholder: value not recorded] |
| Source contract | [Placeholder: value not recorded] |
| Primary handler | [Placeholder: value not recorded] |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| TenantId | Uuid | true |
| ProgramId | Uuid | true |
| ControlId | Uuid | true |
| ExpectedApprovedVersionId | Uuid | true |
| EffectiveUntil | DateOnly | true |
| Rationale | string | true |
| ExpectedRevision | long | true |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/retirement-proposals/{command_id} | [Placeholder: value not recorded] |

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]

### ProposeControlSuccessor

| Selected contract detail | Value |
| --- | --- |
| Target command name | ProposeControlSuccessor |
| Selected source command name | ProposeControlSuccessor |
| Name decision | The selected source name is retained. |
| Owner | ControlDraft |
| Contract record status | [Placeholder: value not recorded] |
| Kind | http |
| Logical identity | CommandId; Uuid; required=true; CommandId binds one immutable authored intent to the exact owner identity and expected revision: an exact retry returns the original result, a changed intent under the same CommandId is refused |
| Entry point | [Placeholder: value not recorded] |
| MCP | [Placeholder: MCP availability not recorded] |
| Selected semantic events | None listed |
| Protocols | [Placeholder: value not recorded] |
| Policy preservation | [Placeholder: value not recorded] |
| Contract changes | [Placeholder: value not recorded] |
| Guard order | [Placeholder: value not recorded] |
| Response contract | `{"mode":"immediate_original_result","responseType":"ControlSuccessorRegistration","firstHttpStatus":200,"terminalResultType":"ControlSuccessorRegistration"}` |
| Expected revision scope | `{}` |
| Authorizer sources | [Placeholder: value not recorded] |
| Source contract | [Placeholder: value not recorded] |
| Primary handler | [Placeholder: value not recorded] |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| TenantId | Uuid | true |
| ProgramId | Uuid | true |
| ControlId | Uuid | true |
| ExpectedApprovedVersionId | Uuid | true |
| Content | ControlDraftContent | true |
| ExpectedRevision | long | true |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/successors/{command_id} | [Placeholder: value not recorded] |

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]

### RetireControl

| Selected contract detail | Value |
| --- | --- |
| Target command name | RetireControl |
| Selected source command name | RetireControl |
| Name decision | The selected source name is retained. |
| Owner | ControlDraft |
| Contract record status | [Placeholder: value not recorded] |
| Kind | http |
| Logical identity | CommandId; Uuid; required=true; CommandId binds one immutable authored intent to the exact owner identity and expected revision: an exact retry returns the original result, a changed intent under the same CommandId is refused |
| Entry point | [Placeholder: value not recorded] |
| MCP | [Placeholder: MCP availability not recorded] |
| Selected semantic events | None listed |
| Protocols | [Placeholder: value not recorded] |
| Policy preservation | [Placeholder: value not recorded] |
| Contract changes | [Placeholder: value not recorded] |
| Guard order | [Placeholder: value not recorded] |
| Response contract | `{"mode":"immediate_original_result","responseType":"Unit","firstHttpStatus":200,"terminalResultType":"Unit"}` |
| Expected revision scope | `{}` |
| Authorizer sources | [Placeholder: value not recorded] |
| Source contract | [Placeholder: value not recorded] |
| Primary handler | [Placeholder: value not recorded] |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| TenantId | Uuid | true |
| ProgramId | Uuid | true |
| ControlId | Uuid | true |
| ExpectedRevision | long | true |
| AcceptedReviewDecisionId | Uuid | true |
| ImpactDigest | string | true |
| Rationale | string | true |
| SeparationOfDutiesWaiverId | Uuid? | false |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/retirements/{command_id} | [Placeholder: value not recorded] |

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]

### ReviewControl

| Selected contract detail | Value |
| --- | --- |
| Target command name | ReviewControl |
| Selected source command name | ReviewControl |
| Name decision | The selected source name is retained. |
| Owner | ControlDraft |
| Contract record status | [Placeholder: value not recorded] |
| Kind | http |
| Logical identity | CommandId; Uuid; required=true; CommandId binds one immutable authored intent to the exact owner identity and expected revision: an exact retry returns the original result, a changed intent under the same CommandId is refused |
| Entry point | [Placeholder: value not recorded] |
| MCP | [Placeholder: MCP availability not recorded] |
| Selected semantic events | None listed |
| Protocols | [Placeholder: value not recorded] |
| Policy preservation | [Placeholder: value not recorded] |
| Contract changes | [Placeholder: value not recorded] |
| Guard order | [Placeholder: value not recorded] |
| Response contract | `{"mode":"immediate_original_result","responseType":"Unit","firstHttpStatus":200,"terminalResultType":"Unit"}` |
| Expected revision scope | `{}` |
| Authorizer sources | [Placeholder: value not recorded] |
| Source contract | [Placeholder: value not recorded] |
| Primary handler | [Placeholder: value not recorded] |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| TenantId | Uuid | true |
| ProgramId | Uuid | true |
| ControlId | Uuid | true |
| ExpectedRevision | long | true |
| Outcome | string | true |
| Rationale | string | true |
| SeparationOfDutiesWaiverId | Uuid? | false |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/draft/reviews/{command_id} | [Placeholder: value not recorded] |

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]

### ReviseControlDraft

| Selected contract detail | Value |
| --- | --- |
| Target command name | ReviseControlDraft |
| Selected source command name | ReviseControlDraft |
| Name decision | The selected source name is retained. |
| Owner | ControlDraft |
| Contract record status | [Placeholder: value not recorded] |
| Kind | command |
| Logical identity | CommandId; Uuid; required=true; CommandId binds one immutable authored intent to the exact owner identity and expected revision: an exact retry returns the original result, a changed intent under the same CommandId is refused |
| Entry point | [Placeholder: value not recorded] |
| MCP | [Placeholder: MCP availability not recorded] |
| Selected semantic events | None listed |
| Protocols | [Placeholder: value not recorded] |
| Policy preservation | [Placeholder: value not recorded] |
| Contract changes | [Placeholder: value not recorded] |
| Guard order | [Placeholder: value not recorded] |
| Response contract | `{"mode":"immediate_original_result","responseType":"Unit","firstHttpStatus":200,"terminalResultType":"Unit"}` |
| Expected revision scope | `{}` |
| Authorizer sources | [Placeholder: value not recorded] |
| Source contract | [Placeholder: value not recorded] |
| Primary handler | [Placeholder: value not recorded] |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| TenantId | Uuid | true |
| ProgramId | Uuid | true |
| ControlId | Uuid | true |
| ExpectedRevision | long | true |
| Content | ControlDraftContent | true |

#### HTTP routes

[Placeholder: no HTTP route is recorded for this command.]

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]

### WithdrawControlProposal

| Selected contract detail | Value |
| --- | --- |
| Target command name | WithdrawControlProposal |
| Selected source command name | WithdrawControlProposal |
| Name decision | The selected source name is retained. |
| Owner | ControlDraft |
| Contract record status | [Placeholder: value not recorded] |
| Kind | http |
| Logical identity | CommandId; Uuid; required=true; CommandId binds one immutable authored intent to the exact owner identity and expected revision: an exact retry returns the original result, a changed intent under the same CommandId is refused |
| Entry point | [Placeholder: value not recorded] |
| MCP | [Placeholder: MCP availability not recorded] |
| Selected semantic events | None listed |
| Protocols | [Placeholder: value not recorded] |
| Policy preservation | [Placeholder: value not recorded] |
| Contract changes | [Placeholder: value not recorded] |
| Guard order | [Placeholder: value not recorded] |
| Response contract | `{"mode":"immediate_original_result","responseType":"Unit","firstHttpStatus":200,"terminalResultType":"Unit"}` |
| Expected revision scope | `{}` |
| Authorizer sources | [Placeholder: value not recorded] |
| Source contract | [Placeholder: value not recorded] |
| Primary handler | [Placeholder: value not recorded] |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| TenantId | Uuid | true |
| ProgramId | Uuid | true |
| ControlId | Uuid | true |
| ExpectedRevision | long | true |
| Rationale | string | true |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/proposal-withdrawals/{command_id} | [Placeholder: value not recorded] |

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]


## Source contracts

[Placeholder: no companion target-command source records match this owner.]

[Placeholder: owner-wide caller/channel authorization boundary is not consolidated in this catalog.]
