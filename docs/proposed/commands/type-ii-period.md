# TypeIIPeriod commands



Selected command contracts: **10**. Complete selected contracts and companion source evidence: [owner command JSON](../data/commands/type-ii-period.json).

| Target command | Selected source name | Kind | Identity | Fields | HTTP routes |
| --- | --- | --- | --- | --- | --- |
| AmendTypeIIPeriodClose | AmendTypeIIPeriodClose | command | CommandId | 7 | [Placeholder: value not recorded] |
| ApproveTypeIIPeriodClose | ApproveTypeIIPeriodClose | http | CommandId | 7 | PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/observation-periods/{period_id}/close-approvals/{command_id} |
| FreezeTypeIIPeriodClose | FreezeTypeIIPeriodClose | http | CommandId | 6 | PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/observation-periods/{period_id}/close-snapshots/{close_snapshot_id}/freezes/{command_id} |
| OpenDueControlOccurrences | GenerateDueControlOccurrences | creation | GenerationKey | 5 | [Placeholder: value not recorded] |
| StartTypeIIObservationPeriod | StartTypeIIObservationPeriod | http | PeriodId | 7 | {"method":"PUT","path":"/api/v1/tenants/{tenant_id}/programs/{program_id}/type-ii-periods/{period_id}"} |
| AmendTypeIIObservationPeriod | AmendTypeIIObservationPeriod | http | CommandId | 8 | {"method":"PUT","path":"/api/v1/tenants/{tenant_id}/programs/{program_id}/type-ii-periods/{period_id}/amendments/{command_id}"} |
| CancelTypeIIPeriodAmendment | CancelTypeIIPeriodAmendment | http | CommandId | 7 | {"method":"PUT","path":"/api/v1/tenants/{tenant_id}/programs/{program_id}/type-ii-periods/{period_id}/amendments/{amendment_id}/cancellations/{command_id}"} |
| RejectTypeIIPeriodOpening | RejectTypeIIPeriodOpening | abort | CommandId | 6 | [Placeholder: route/channel not recorded] |
| RejectTypeIIPeriodAmendment | RejectTypeIIPeriodAmendment | abort | CommandId | 6 | [Placeholder: route/channel not recorded] |
| ApplyTypeIIPeriodRange | ApplyTypeIIPeriodRange | receiver | CommandId | 8 | [Placeholder: value not recorded] |

## Selected command details

### AmendTypeIIPeriodClose

| Selected contract detail | Value |
| --- | --- |
| Target command name | AmendTypeIIPeriodClose |
| Selected source command name | AmendTypeIIPeriodClose |
| Name decision | The selected source name is retained. |
| Target owner | TypeIIPeriod |
| Selected source owner | TypeIIPeriod |
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
| PeriodId | Uuid | true |
| CloseSnapshotId | Uuid | true |
| ExpectedRevision | long | true |
| AmendmentReason | string | true |

#### HTTP routes

[Placeholder: no HTTP route is recorded for this command.]

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]

### ApproveTypeIIPeriodClose

| Selected contract detail | Value |
| --- | --- |
| Target command name | ApproveTypeIIPeriodClose |
| Selected source command name | ApproveTypeIIPeriodClose |
| Name decision | The selected source name is retained. |
| Target owner | TypeIIPeriod |
| Selected source owner | TypeIIPeriod |
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
| PeriodId | Uuid | true |
| ExpectedRevision | long | true |
| Rationale | string | true |
| AcknowledgedMaterialItemIds | IReadOnlyList<Uuid> | true |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/programs/{program_id}/observation-periods/{period_id}/close-approvals/{command_id} | [Placeholder: value not recorded] |

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]

### FreezeTypeIIPeriodClose

| Selected contract detail | Value |
| --- | --- |
| Target command name | FreezeTypeIIPeriodClose |
| Selected source command name | FreezeTypeIIPeriodClose |
| Name decision | The selected source name is retained. |
| Target owner | TypeIIPeriod |
| Selected source owner | TypeIIPeriod |
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
| PeriodId | Uuid | true |
| CloseSnapshotId | Uuid | true |
| ExpectedRevision | long | true |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/programs/{program_id}/observation-periods/{period_id}/close-snapshots/{close_snapshot_id}/freezes/{command_id} | [Placeholder: value not recorded] |

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]

### OpenDueControlOccurrences

| Selected contract detail | Value |
| --- | --- |
| Target command name | OpenDueControlOccurrences |
| Selected source command name | GenerateDueControlOccurrences |
| Name decision | Opens the control work due under the exact accepted plan and period. |
| Target owner | TypeIIPeriod |
| Selected source owner | TypeIIPeriod |
| Contract record status | [Placeholder: value not recorded] |
| Kind | creation |
| Logical identity | GenerationKey; Uuid; required=true; caller-supplied immutable creation identity: an exact retry with the same id and authored intent returns the original result; the same id with changed intent is refused; a different id is a different record |
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
| TenantId | Uuid | true |
| ProgramId | Uuid | true |
| PeriodId | Uuid | true |
| ThroughDate | DateOnly | true |
| GenerationKey | string | true |

#### HTTP routes

[Placeholder: no HTTP route is recorded for this command.]

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]

### StartTypeIIObservationPeriod

| Selected contract detail | Value |
| --- | --- |
| Target command name | StartTypeIIObservationPeriod |
| Selected source command name | StartTypeIIObservationPeriod |
| Name decision | The selected source name is retained. |
| Target owner | TypeIIPeriod |
| Selected source owner | TypeIIPeriod |
| Contract record status | [Placeholder: value not recorded] |
| Kind | http |
| Logical identity | PeriodId; Uuid; required=true; caller-supplied immutable creation identity: an exact retry with the same id and authored intent returns the original result; the same id with changed intent is refused; a different id is a different record |
| Entry point | [Placeholder: value not recorded] |
| MCP | [Placeholder: MCP availability not recorded] |
| Selected semantic events | None listed |
| Protocols | [Placeholder: value not recorded] |
| Policy preservation | [Placeholder: value not recorded] |
| Contract changes | [Placeholder: value not recorded] |
| Guard order | [Placeholder: value not recorded] |
| Response contract | `{"mode":"async_fixed_ack","responseType":"OperationAcceptedResponse","firstHttpStatus":202,"terminalResultType":"Unit"}` |
| Expected revision scope | `{}` |
| Authorizer sources | [Placeholder: value not recorded] |
| Source contract | [Placeholder: value not recorded] |
| Primary handler | [Placeholder: value not recorded] |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| TenantId | Uuid | true |
| ProgramId | Uuid | true |
| PeriodId | Uuid | true |
| ExaminationId | Uuid | true |
| PlanId | Uuid | true |
| PeriodStart | DateOnly | true |
| PeriodEnd | DateOnly | true |

#### HTTP routes

[Placeholder: no HTTP route is recorded for this command.]

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]

### AmendTypeIIObservationPeriod

| Selected contract detail | Value |
| --- | --- |
| Target command name | AmendTypeIIObservationPeriod |
| Selected source command name | AmendTypeIIObservationPeriod |
| Name decision | The selected source name is retained. |
| Target owner | TypeIIPeriod |
| Selected source owner | TypeIIPeriod |
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
| Response contract | `{"mode":"async_fixed_ack","responseType":"OperationAcceptedResponse","firstHttpStatus":202,"terminalResultType":"Unit"}` |
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
| PeriodId | Uuid | true |
| ExpectedRevision | long | true |
| PeriodStart | DateOnly? | true |
| PeriodEnd | DateOnly? | true |
| AmendmentReason | string | true |

#### HTTP routes

[Placeholder: no HTTP route is recorded for this command.]

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]

### CancelTypeIIPeriodAmendment

| Selected contract detail | Value |
| --- | --- |
| Target command name | CancelTypeIIPeriodAmendment |
| Selected source command name | CancelTypeIIPeriodAmendment |
| Name decision | The selected source name is retained. |
| Target owner | TypeIIPeriod |
| Selected source owner | TypeIIPeriod |
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
| Response contract | `{}` |
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
| PeriodId | Uuid | true |
| AmendmentId | Uuid | true |
| ExpectedRevision | long | true |
| Reason | string | true |

#### HTTP routes

[Placeholder: no HTTP route is recorded for this command.]

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]

### RejectTypeIIPeriodOpening

| Selected contract detail | Value |
| --- | --- |
| Target command name | RejectTypeIIPeriodOpening |
| Selected source command name | RejectTypeIIPeriodOpening |
| Name decision | The selected source name is retained. |
| Target owner | TypeIIPeriod |
| Selected source owner | TypeIIPeriod |
| Contract record status | [Placeholder: value not recorded] |
| Kind | abort |
| Logical identity | CommandId; Uuid; required=true; abort receiver identity bound to the exact admitted operation and generation: exact redelivery resolves the original rejected result; an abort after a terminal outcome is a no-op |
| Entry point | [Placeholder: value not recorded] |
| MCP | [Placeholder: MCP availability not recorded] |
| Selected semantic events | None listed |
| Protocols | [Placeholder: value not recorded] |
| Policy preservation | [Placeholder: value not recorded] |
| Contract changes | [Placeholder: value not recorded] |
| Guard order | [Placeholder: value not recorded] |
| Response contract | `{}` |
| Expected revision scope | `{}` |
| Authorizer sources | [Placeholder: value not recorded] |
| Source contract | [Placeholder: value not recorded] |
| Primary handler | [Placeholder: value not recorded] |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| OperationId | Uuid | true |
| Target | EventStreamAddress | true |
| Generation | long | true |
| ExpectedRevision | long | true |
| FailureProof | ImmutableContentRef | true |

#### HTTP routes

[Placeholder: no HTTP route is recorded for this command.]

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]

### RejectTypeIIPeriodAmendment

| Selected contract detail | Value |
| --- | --- |
| Target command name | RejectTypeIIPeriodAmendment |
| Selected source command name | RejectTypeIIPeriodAmendment |
| Name decision | The selected source name is retained. |
| Target owner | TypeIIPeriod |
| Selected source owner | TypeIIPeriod |
| Contract record status | [Placeholder: value not recorded] |
| Kind | abort |
| Logical identity | CommandId; Uuid; required=true; abort receiver identity bound to the exact admitted operation and generation: exact redelivery resolves the original rejected result; an abort after a terminal outcome is a no-op |
| Entry point | [Placeholder: value not recorded] |
| MCP | [Placeholder: MCP availability not recorded] |
| Selected semantic events | None listed |
| Protocols | [Placeholder: value not recorded] |
| Policy preservation | [Placeholder: value not recorded] |
| Contract changes | [Placeholder: value not recorded] |
| Guard order | [Placeholder: value not recorded] |
| Response contract | `{}` |
| Expected revision scope | `{}` |
| Authorizer sources | [Placeholder: value not recorded] |
| Source contract | [Placeholder: value not recorded] |
| Primary handler | [Placeholder: value not recorded] |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| OperationId | Uuid | true |
| Target | EventStreamAddress | true |
| Generation | long | true |
| ExpectedRevision | long | true |
| FailureProof | ImmutableContentRef | true |

#### HTTP routes

[Placeholder: no HTTP route is recorded for this command.]

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]

### ApplyTypeIIPeriodRange

| Selected contract detail | Value |
| --- | --- |
| Target command name | ApplyTypeIIPeriodRange |
| Selected source command name | ApplyTypeIIPeriodRange |
| Name decision | The selected source name is retained. |
| Target owner | TypeIIPeriod |
| Selected source owner | TypeIIPeriod |
| Contract record status | [Placeholder: value not recorded] |
| Kind | receiver |
| Logical identity | CommandId; Uuid; required=true; reactor; deterministic receiver identity derived from the admitted root fact, episode and generation; exact redelivery resolves the original result |
| Entry point | [Placeholder: value not recorded] |
| MCP | [Placeholder: MCP availability not recorded] |
| Selected semantic events | None listed |
| Protocols | [Placeholder: value not recorded] |
| Policy preservation | [Placeholder: value not recorded] |
| Contract changes | [Placeholder: value not recorded] |
| Guard order | [Placeholder: value not recorded] |
| Response contract | `{}` |
| Expected revision scope | `{}` |
| Authorizer sources | [Placeholder: value not recorded] |
| Source contract | [Placeholder: value not recorded] |
| Primary handler | ApplyTypeIIPeriodRangeHandler |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| Source | SourceFactProof<TypeIIPeriodRangeAuthorized> | true |
| Target | EventStreamAddress | true |
| OperationId | Uuid | true |
| Generation | long | true |
| ExpectedRevision | long? | true |
| Intent | ImmutableContentRef | true |
| PeriodId | Uuid | true |

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
