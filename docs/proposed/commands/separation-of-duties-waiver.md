# SeparationOfDutiesWaiver commands



Selected command contracts: **2**. Complete selected contracts and companion source evidence: [owner command JSON](../data/commands/separation-of-duties-waiver.json).

| Target command | Selected source name | Kind | Identity | Fields | HTTP routes |
| --- | --- | --- | --- | --- | --- |
| ApproveSeparationOfDutiesWaiver | ApproveSeparationOfDutiesWaiver | http | CommandId | 4 | PUT /api/v1/tenants/{tenant_id}/separation-of-duties-waivers/{waiver_id}/approvals/{command_id} |
| RecordSeparationOfDutiesWaiver | RecordSeparationOfDutiesWaiver | http | WaiverId | 6 | PUT /api/v1/tenants/{tenant_id}/separation-of-duties-waivers/{waiver_id} |

## Selected command details

### ApproveSeparationOfDutiesWaiver

| Selected contract detail | Value |
| --- | --- |
| Target command name | ApproveSeparationOfDutiesWaiver |
| Selected source command name | ApproveSeparationOfDutiesWaiver |
| Name decision | The selected source name is retained. |
| Target owner | SeparationOfDutiesWaiver |
| Selected source owner | SeparationOfDutiesWaiver |
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
| Response contract | `{"mode":"immediate_original_result","responseType":"SeparationOfDutiesWaiverView","firstHttpStatus":200,"terminalResultType":"SeparationOfDutiesWaiverView"}` |
| Expected revision scope | `{}` |
| Authorizer sources | [Placeholder: value not recorded] |
| Source contract | [Placeholder: value not recorded] |
| Primary handler | [Placeholder: value not recorded] |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| TenantId | Uuid | true |
| WaiverId | Uuid | true |
| ExpectedRevision | long | true |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/separation-of-duties-waivers/{waiver_id}/approvals/{command_id} | [Placeholder: value not recorded] |

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]

### RecordSeparationOfDutiesWaiver

| Selected contract detail | Value |
| --- | --- |
| Target command name | RecordSeparationOfDutiesWaiver |
| Selected source command name | RecordSeparationOfDutiesWaiver |
| Name decision | The selected source name is retained. |
| Target owner | SeparationOfDutiesWaiver |
| Selected source owner | SeparationOfDutiesWaiver |
| Contract record status | [Placeholder: value not recorded] |
| Kind | http |
| Logical identity | WaiverId; Uuid; required=true; caller-supplied immutable creation identity: an exact retry with the same id and authored intent returns the original result; the same id with changed intent is refused; a different id is a different record |
| Entry point | [Placeholder: value not recorded] |
| MCP | [Placeholder: MCP availability not recorded] |
| Selected semantic events | None listed |
| Protocols | [Placeholder: value not recorded] |
| Policy preservation | [Placeholder: value not recorded] |
| Contract changes | [Placeholder: value not recorded] |
| Guard order | [Placeholder: value not recorded] |
| Response contract | `{"mode":"immediate_original_result","responseType":"SeparationOfDutiesWaiverView","firstHttpStatus":200,"terminalResultType":"SeparationOfDutiesWaiverView"}` |
| Expected revision scope | `{}` |
| Authorizer sources | [Placeholder: value not recorded] |
| Source contract | [Placeholder: value not recorded] |
| Primary handler | [Placeholder: value not recorded] |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| WaiverId | Uuid | true |
| TenantId | Uuid | true |
| Scope | SeparationOfDutiesWaiverScope | true |
| BeneficiaryUserId | Uuid | true |
| Rationale | string | true |
| ExpiresAt | DateTimeOffset | true |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/separation-of-duties-waivers/{waiver_id} | [Placeholder: value not recorded] |

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]


## Source contracts

[Placeholder: no companion target-command source records match this owner.]

[Placeholder: owner-wide caller/channel authorization boundary is not consolidated in this catalog.]
