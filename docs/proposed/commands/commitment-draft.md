# CommitmentDraft commands

Selected command contracts: **5**. Complete selected contracts and companion source evidence: [owner command JSON](../data/commands/commitment-draft.json).

| Target command | Selected source name | Kind | Identity | Fields | HTTP routes |
| --- | --- | --- | --- | --- | --- |
| ApproveCommitmentDraft | ApproveCommitmentDraft | http | CommandId | 10 | PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/commitment-drafts/{draft_id}/approvals/{command_id} |
| CreateCommitmentDraft | CreateCommitmentDraft | http | DraftId | 10 | PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/commitment-drafts/{draft_id} |
| RetireCommitment | RetireCommitment | command | CommandId | 7 | [Placeholder: value not recorded] |
| ReviewCommitmentDraft | ReviewCommitmentDraft | http | CommandId | 14 | PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/commitment-drafts/{draft_id}/reviews/{command_id} |
| ReviseCommitmentDraft | ReviseCommitmentDraft | command | CommandId | 8 | [Placeholder: value not recorded] |

## Selected command details

### ApproveCommitmentDraft

| Selected contract detail | Value |
| --- | --- |
| Target command name | ApproveCommitmentDraft |
| Selected source command name | ApproveCommitmentDraft |
| Name decision | The selected source name is retained. |
| Owner | CommitmentDraft |
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
| DraftId | Uuid | true |
| ExpectedRevision | long | true |
| AcceptedReviewDecisionId | Uuid | true |
| EffectiveFrom | DateOnly | true |
| Rationale | string | true |
| ImpactDigest | string | true |
| SeparationOfDutiesWaiverId | Uuid? | false |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/programs/{program_id}/commitment-drafts/{draft_id}/approvals/{command_id} | [Placeholder: value not recorded] |

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]

### CreateCommitmentDraft

| Selected contract detail | Value |
| --- | --- |
| Target command name | CreateCommitmentDraft |
| Selected source command name | CreateCommitmentDraft |
| Name decision | The selected source name is retained. |
| Owner | CommitmentDraft |
| Contract record status | [Placeholder: value not recorded] |
| Kind | http |
| Logical identity | DraftId; Uuid; required=true; caller-supplied immutable creation identity: an exact retry with the same id and authored intent returns the original result; the same id with changed intent is refused; a different id is a different record |
| Entry point | [Placeholder: value not recorded] |
| MCP | [Placeholder: MCP availability not recorded] |
| Selected semantic events | None listed |
| Protocols | [Placeholder: value not recorded] |
| Policy preservation | [Placeholder: value not recorded] |
| Contract changes | [Placeholder: value not recorded] |
| Guard order | [Placeholder: value not recorded] |
| Response contract | `{"mode":"immediate_original_result","responseType":"CommitmentDraftRegistration","firstHttpStatus":200,"terminalResultType":"CommitmentDraftRegistration"}` |
| Expected revision scope | `{}` |
| Authorizer sources | [Placeholder: value not recorded] |
| Source contract | [Placeholder: value not recorded] |
| Primary handler | [Placeholder: value not recorded] |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| DraftId | Uuid | true |
| TenantId | Uuid | true |
| ProgramId | Uuid | true |
| ServiceId | Uuid | true |
| Kind | string | true |
| Identifier | string | true |
| Statement | string | true |
| Context | string | true |
| SourceReference | string | true |
| ProviderId | Uuid? | false |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/programs/{program_id}/commitment-drafts/{draft_id} | [Placeholder: value not recorded] |

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]

### RetireCommitment

| Selected contract detail | Value |
| --- | --- |
| Target command name | RetireCommitment |
| Selected source command name | RetireCommitment |
| Name decision | The selected source name is retained. |
| Owner | CommitmentDraft |
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
| CommitmentId | Uuid | true |
| ExpectedRevision | long | true |
| EffectiveOn | DateOnly | true |
| Rationale | string | true |

#### HTTP routes

[Placeholder: no HTTP route is recorded for this command.]

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]

### ReviewCommitmentDraft

| Selected contract detail | Value |
| --- | --- |
| Target command name | ReviewCommitmentDraft |
| Selected source command name | ReviewCommitmentDraft |
| Name decision | The selected source name is retained. |
| Owner | CommitmentDraft |
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
| DraftId | Uuid | true |
| ExpectedRevision | long | true |
| Outcome | string | true |
| Rationale | string | true |
| OwnerReference | string? | false |
| Applicability | string? | false |
| Interpretation | string? | false |
| InterpretationNote | string? | false |
| SourceVerifiedReference | string? | false |
| SourceEvidence | string? | false |
| SeparationOfDutiesWaiverId | Uuid? | false |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/programs/{program_id}/commitment-drafts/{draft_id}/reviews/{command_id} | [Placeholder: value not recorded] |

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]

### ReviseCommitmentDraft

| Selected contract detail | Value |
| --- | --- |
| Target command name | ReviseCommitmentDraft |
| Selected source command name | ReviseCommitmentDraft |
| Name decision | The selected source name is retained. |
| Owner | CommitmentDraft |
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
| DraftId | Uuid | true |
| ExpectedRevision | long | true |
| Statement | string | true |
| Context | string | true |
| SourceReference | string | true |

#### HTTP routes

[Placeholder: no HTTP route is recorded for this command.]

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]


## Source contracts

[Placeholder: no companion target-command source records match this owner.]

[Placeholder: owner-wide caller/channel authorization boundary is not consolidated in this catalog.]
