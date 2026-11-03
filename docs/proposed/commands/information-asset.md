# InformationAsset commands



Selected command contracts: **2**. Complete selected contracts and companion source evidence: [owner command JSON](../data/commands/information-asset.json).

| Target command | Selected source name | Kind | Identity | Fields | HTTP routes |
| --- | --- | --- | --- | --- | --- |
| RecordInformationAsset | RecordInformationAsset | http | InformationAssetId | 7 | PUT /api/v1/tenants/{tenant_id}/information-assets/{information_asset_id} |
| ReviseInformationAsset | ReviseInformationAsset | command | CommandId | 10 | [Placeholder: value not recorded] |

## Selected command details

### RecordInformationAsset

| Selected contract detail | Value |
| --- | --- |
| Target command name | RecordInformationAsset |
| Selected source command name | RecordInformationAsset |
| Name decision | The selected source name is retained. |
| Target owner | InformationAsset |
| Selected source owner | InformationAsset |
| Contract record status | [Placeholder: value not recorded] |
| Kind | http |
| Logical identity | InformationAssetId; Uuid; required=true; caller-supplied immutable creation identity: an exact retry with the same id and authored intent returns the original result; the same id with changed intent is refused; a different id is a different record |
| Entry point | [Placeholder: value not recorded] |
| MCP | [Placeholder: MCP availability not recorded] |
| Selected semantic events | None listed |
| Protocols | [Placeholder: value not recorded] |
| Policy preservation | [Placeholder: value not recorded] |
| Contract changes | [Placeholder: value not recorded] |
| Guard order | [Placeholder: value not recorded] |
| Response contract | `{"mode":"immediate_original_result","responseType":"InformationAssetRegistration","firstHttpStatus":200,"terminalResultType":"InformationAssetRegistration"}` |
| Expected revision scope | `{}` |
| Authorizer sources | [Placeholder: value not recorded] |
| Source contract | [Placeholder: value not recorded] |
| Primary handler | [Placeholder: value not recorded] |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| InformationAssetId | Uuid | true |
| TenantId | Uuid | true |
| Name | string | true |
| Classification | string | true |
| RetentionReference | string | true |
| OwnerPersonId | Uuid | true |
| Description | string? | false |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| PUT | /api/v1/tenants/{tenant_id}/information-assets/{information_asset_id} | [Placeholder: value not recorded] |

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]

### ReviseInformationAsset

| Selected contract detail | Value |
| --- | --- |
| Target command name | ReviseInformationAsset |
| Selected source command name | ReviseInformationAsset |
| Name decision | The selected source name is retained. |
| Target owner | InformationAsset |
| Selected source owner | InformationAsset |
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
| InformationAssetId | Uuid | true |
| ExpectedRevision | long | true |
| Name | string | true |
| Classification | string | true |
| RetentionReference | string | true |
| OwnerPersonId | Uuid | true |
| Lifecycle | string | true |
| Description | string? | false |

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
