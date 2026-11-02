# TeamMember commands

Selected command contracts: **6**. Complete selected contracts and companion source evidence: [owner command JSON](../data/commands/team-member.json).

| Command | Kind | Identity | Fields | HTTP routes |
| --- | --- | --- | --- | --- |
| AssignTeamMember | http | CommandId | 7 | {"method":"PUT","path":"/api/v1/tenants/{tenant_id}/teams/{team_id}/members/{member_id}/{command_id}"} |
| RemoveTeamMember | http | CommandId | 7 | {"method":"PUT","path":"/api/v1/tenants/{tenant_id}/members/{member_id}/team-removals/{command_id}"} |
| ApplyAdministratorAssignmentRestoration | receiver | CommandId | 11 | [Placeholder: value not recorded] |
| ApplyAdministratorAssignmentWithdrawal | receiver | CommandId | 10 | [Placeholder: value not recorded] |
| ApplyNeutralAdministratorAssignmentMutation | receiver | CommandId | 10 | [Placeholder: value not recorded] |
| CleanupRetiredMembershipRelationship | reactor | CommandId | 9 | [Placeholder: value not recorded] |

## Selected command details

### AssignTeamMember

| Selected contract detail | Value |
| --- | --- |
| Owner | TeamMember |
| Contract record status | [Placeholder: value not recorded] |
| Kind | http |
| Logical identity | CommandId; Uuid; required=true; CommandId binds one immutable authored intent to the exact owner identity and expected revision: an exact retry returns the original result, a changed intent under the same CommandId is refused |
| Entry point | [Placeholder: value not recorded] |
| MCP | [Placeholder: MCP availability not recorded] |
| Selected semantic events | [Placeholder: value not recorded] |
| Protocols | [Placeholder: value not recorded] |
| Policy preservation | [Placeholder: value not recorded] |
| Contract changes | [Placeholder: value not recorded] |
| Guard order | [Placeholder: value not recorded] |
| Response contract | `{"mode":"conditional_async_guarded","responseType":"AdministratorTransitionResponse","firstHttpStatus":"202 for administrator guard choreography; 200 for a direct non-administrator source mutation","terminalResultType":"Unit"}` |
| Expected revision scope | `{}` |
| Authorizer sources | [Placeholder: value not recorded] |
| Source contract | [Placeholder: value not recorded] |
| Primary handler | [Placeholder: value not recorded] |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| TenantId | Uuid | true |
| TeamId | Uuid | true |
| MemberId | Uuid | true |
| MemberEpisodeId | Uuid | true |
| ExpectedAssignmentRevision | long | true |
| ExpectedAssignmentGeneration | long | true |

#### HTTP routes

[Placeholder: no HTTP route is recorded for this command.]

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]

### RemoveTeamMember

| Selected contract detail | Value |
| --- | --- |
| Owner | TeamMember |
| Contract record status | [Placeholder: value not recorded] |
| Kind | http |
| Logical identity | CommandId; Uuid; required=true; CommandId binds one immutable authored intent to the exact owner identity and expected revision: an exact retry returns the original result, a changed intent under the same CommandId is refused |
| Entry point | [Placeholder: value not recorded] |
| MCP | [Placeholder: MCP availability not recorded] |
| Selected semantic events | [Placeholder: value not recorded] |
| Protocols | [Placeholder: value not recorded] |
| Policy preservation | [Placeholder: value not recorded] |
| Contract changes | [Placeholder: value not recorded] |
| Guard order | [Placeholder: value not recorded] |
| Response contract | `{"mode":"conditional_async_guarded","responseType":"AdministratorTransitionResponse","firstHttpStatus":"202 for administrator guard choreography; 200 for a direct non-administrator source mutation","terminalResultType":"Unit"}` |
| Expected revision scope | `{}` |
| Authorizer sources | [Placeholder: value not recorded] |
| Source contract | [Placeholder: value not recorded] |
| Primary handler | [Placeholder: value not recorded] |

#### Request fields

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| TenantId | Uuid | true |
| TeamId | Uuid | true |
| MemberId | Uuid | true |
| MemberEpisodeId | Uuid | true |
| ExpectedAssignmentRevision | long | true |
| ExpectedAssignmentGeneration | long | true |

#### HTTP routes

[Placeholder: no HTTP route is recorded for this command.]

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]

### ApplyAdministratorAssignmentRestoration

| Selected contract detail | Value |
| --- | --- |
| Owner | TeamMember |
| Contract record status | [Placeholder: value not recorded] |
| Kind | receiver |
| Logical identity | CommandId; Uuid; required=true; internal; deterministic receiver identity derived from the admitted root fact, episode and generation: exact redelivery resolves the original result, a changed admission is a new identity |
| Entry point | [Placeholder: value not recorded] |
| MCP | [Placeholder: MCP availability not recorded] |
| Selected semantic events | [Placeholder: value not recorded] |
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
| CommandId | [Placeholder: value not recorded] | true |
| Source | [Placeholder: value not recorded] | true |
| Target | [Placeholder: value not recorded] | true |
| OperationId | [Placeholder: value not recorded] | true |
| Generation | [Placeholder: value not recorded] | true |
| ExpectedRevision | [Placeholder: value not recorded] | true |
| Intent | [Placeholder: value not recorded] | true |
| MemberId | [Placeholder: value not recorded] | true |
| MemberEpisodeId | [Placeholder: value not recorded] | true |
| AssignmentId | [Placeholder: value not recorded] | true |
| ExpectedAssignmentRevision | [Placeholder: value not recorded] | true |

#### HTTP routes

[Placeholder: no HTTP route is recorded for this command.]

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]

### ApplyAdministratorAssignmentWithdrawal

| Selected contract detail | Value |
| --- | --- |
| Owner | TeamMember |
| Contract record status | [Placeholder: value not recorded] |
| Kind | receiver |
| Logical identity | CommandId; Uuid; required=true; internal; deterministic receiver identity derived from the admitted root fact, episode and generation: exact redelivery resolves the original result, a changed admission is a new identity |
| Entry point | [Placeholder: value not recorded] |
| MCP | [Placeholder: MCP availability not recorded] |
| Selected semantic events | [Placeholder: value not recorded] |
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
| CommandId | [Placeholder: value not recorded] | true |
| Source | [Placeholder: value not recorded] | true |
| Target | [Placeholder: value not recorded] | true |
| OperationId | [Placeholder: value not recorded] | true |
| Generation | [Placeholder: value not recorded] | true |
| ExpectedRevision | [Placeholder: value not recorded] | true |
| Intent | [Placeholder: value not recorded] | true |
| MemberId | [Placeholder: value not recorded] | true |
| MemberEpisode | [Placeholder: value not recorded] | true |
| AssignmentId | [Placeholder: value not recorded] | true |

#### HTTP routes

[Placeholder: no HTTP route is recorded for this command.]

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]

### ApplyNeutralAdministratorAssignmentMutation

| Selected contract detail | Value |
| --- | --- |
| Owner | TeamMember |
| Contract record status | [Placeholder: value not recorded] |
| Kind | receiver |
| Logical identity | CommandId; Uuid; required=true; internal; deterministic receiver identity derived from the admitted root fact, episode and generation: exact redelivery resolves the original result, a changed admission is a new identity |
| Entry point | [Placeholder: value not recorded] |
| MCP | [Placeholder: MCP availability not recorded] |
| Selected semantic events | [Placeholder: value not recorded] |
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
| CommandId | [Placeholder: value not recorded] | true |
| Source | [Placeholder: value not recorded] | true |
| Target | [Placeholder: value not recorded] | true |
| OperationId | [Placeholder: value not recorded] | true |
| Generation | [Placeholder: value not recorded] | true |
| ExpectedRevision | [Placeholder: value not recorded] | true |
| Intent | [Placeholder: value not recorded] | true |
| MemberId | [Placeholder: value not recorded] | true |
| MemberEpisode | [Placeholder: value not recorded] | true |
| AssignmentId | [Placeholder: value not recorded] | true |

#### HTTP routes

[Placeholder: no HTTP route is recorded for this command.]

#### Handler source evidence

[Placeholder: handler source evidence is not recorded.]

#### Test evidence recorded by the target contract

Existing tests: [Placeholder: existing tests not recorded]

Required tests: [Placeholder: required tests not recorded]

### CleanupRetiredMembershipRelationship

| Selected contract detail | Value |
| --- | --- |
| Owner | TeamMember |
| Contract record status | [Placeholder: value not recorded] |
| Kind | reactor |
| Logical identity | CommandId; Uuid; required=true; internal; deterministic receiver identity derived from the admitted root fact, episode and generation: exact redelivery resolves the original result, a changed admission is a new identity |
| Entry point | [Placeholder: value not recorded] |
| MCP | [Placeholder: MCP availability not recorded] |
| Selected semantic events | [Placeholder: value not recorded] |
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
| TenantId | [Placeholder: value not recorded] | true |
| MemberId | [Placeholder: value not recorded] | true |
| MembershipEpisodeId | [Placeholder: value not recorded] | true |
| Kind | [Placeholder: value not recorded] | true |
| TargetId | [Placeholder: value not recorded] | true |
| SourceEventId | [Placeholder: value not recorded] | true |
| SourcePosition | [Placeholder: value not recorded] | true |
| TerminationEventId | [Placeholder: value not recorded] | true |
| CommandId | [Placeholder: value not recorded] | true |

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
