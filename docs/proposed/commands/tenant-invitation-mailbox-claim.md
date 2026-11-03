# TenantInvitationMailboxClaim commands

Selected command contracts: **4**. Complete selected contracts and companion source evidence: [owner command JSON](../data/commands/tenant-invitation-mailbox-claim.json).

| Target command | Selected source name | Kind | Identity | Fields | HTTP routes |
| --- | --- | --- | --- | --- | --- |
| InviteTenantMember | InviteTenantMember | http | InvitationId | 5 | undefined undefined |
| InviteOrganizationMember | InviteOrganizationMember | http | InvitationId | 4 | undefined undefined |
| InviteGuestConsultant | InviteGuestConsultant | http | InvitationId | 5 | undefined undefined |
| AcceptTenantInvitation | AcceptTenantInvitation | http | TenantInvitationIds.AcceptanceOf(InvitationId) | 4 | undefined undefined |

## Selected command details

### InviteTenantMember

| Selected contract detail | Value |
| --- | --- |
| Target command name | InviteTenantMember |
| Selected source command name | InviteTenantMember |
| Name decision | The selected source name is retained. |
| Owner | TenantInvitationMailboxClaim |
| Contract record status | [Placeholder: value not recorded] |
| Kind | http |
| Logical identity | InvitationId; Uuid; required=true; client; caller-supplied immutable creation identity: an exact retry with the same id and authored intent returns the original result; the same id with changed intent is refused; a different id is a different record |
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
| InvitationId | [Placeholder: value not recorded] | true |
| TenantId | [Placeholder: value not recorded] | true |
| EmailAddress | [Placeholder: value not recorded] | true |
| Affiliation | [Placeholder: value not recorded] | true |
| Administrator | [Placeholder: value not recorded] | true |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| [Placeholder: value not recorded] | [Placeholder: value not recorded] | [Placeholder: value not recorded] |

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]

### InviteOrganizationMember

| Selected contract detail | Value |
| --- | --- |
| Target command name | InviteOrganizationMember |
| Selected source command name | InviteOrganizationMember |
| Name decision | The selected source name is retained. |
| Owner | TenantInvitationMailboxClaim |
| Contract record status | [Placeholder: value not recorded] |
| Kind | http |
| Logical identity | InvitationId; Uuid; required=true; client; caller-supplied immutable creation identity: an exact retry with the same id and authored intent returns the original result; the same id with changed intent is refused; a different id is a different record |
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
| InvitationId | [Placeholder: value not recorded] | true |
| TenantId | [Placeholder: value not recorded] | true |
| EmailAddress | [Placeholder: value not recorded] | true |
| BuiltInRole | [Placeholder: value not recorded] | true |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| [Placeholder: value not recorded] | [Placeholder: value not recorded] | [Placeholder: value not recorded] |

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]

### InviteGuestConsultant

| Selected contract detail | Value |
| --- | --- |
| Target command name | InviteGuestConsultant |
| Selected source command name | InviteGuestConsultant |
| Name decision | The selected source name is retained. |
| Owner | TenantInvitationMailboxClaim |
| Contract record status | [Placeholder: value not recorded] |
| Kind | http |
| Logical identity | InvitationId; Uuid; required=true; client; caller-supplied immutable creation identity: an exact retry with the same id and authored intent returns the original result; the same id with changed intent is refused; a different id is a different record |
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
| InvitationId | [Placeholder: value not recorded] | true |
| TenantId | [Placeholder: value not recorded] | true |
| EmailAddress | [Placeholder: value not recorded] | true |
| ProgramIds | [Placeholder: value not recorded] | true |
| ExpiresOn | [Placeholder: value not recorded] | true |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| [Placeholder: value not recorded] | [Placeholder: value not recorded] | [Placeholder: value not recorded] |

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]

### AcceptTenantInvitation

| Selected contract detail | Value |
| --- | --- |
| Target command name | AcceptTenantInvitation |
| Selected source command name | AcceptTenantInvitation |
| Name decision | The selected source name is retained. |
| Owner | TenantInvitationMailboxClaim |
| Contract record status | [Placeholder: value not recorded] |
| Kind | http |
| Logical identity | TenantInvitationIds.AcceptanceOf(InvitationId); Uuid; required=true; client; caller-supplied immutable creation identity: an exact retry with the same id and authored intent returns the original result; the same id with changed intent is refused; a different id is a different record |
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
| InvitationId | [Placeholder: value not recorded] | true |
| TenantId | [Placeholder: value not recorded] | true |
| EmailAddress | [Placeholder: value not recorded] | true |
| Token | [Placeholder: value not recorded] | true |

#### HTTP routes

| Method | Path | Policy |
| --- | --- | --- |
| [Placeholder: value not recorded] | [Placeholder: value not recorded] | [Placeholder: value not recorded] |

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]


## Source contracts

[Placeholder: no companion target-command source records match this owner.]

[Placeholder: owner-wide caller/channel authorization boundary is not consolidated in this catalog.]
