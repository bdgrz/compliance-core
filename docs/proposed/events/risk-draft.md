# RiskDraft events

Canonical events: **2**. Complete canonical event and available schema records: [owner event JSON](../data/events/risk-draft.json).

| Target event | Selected source name | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- | --- |
| RiskDraftCreated | RiskDraftCreated | business | bdgrz.risk.draft.created | 2 | [Placeholder: schema absent] | A risk draft was created in a program with its authored content by a named member, awaiting evaluation. |
| RiskDraftRevised | RiskDraftRevised | business | bdgrz.risk.draft.revised | 2 | [Placeholder: schema absent] | A risk draft's authored content was revised by a named member, producing the draft's next revision. |

## Event details

### RiskDraftCreated

| Canonical event detail | Value |
| --- | --- |
| Target event name | RiskDraftCreated |
| Selected source event name | RiskDraftCreated |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:RiskDraftCreated |
| Owner selection | one exact canonical authority owner |
| Target concrete type | RiskDraftCreated |
| Adopted wire | `{"discriminator":"bdgrz.risk.draft.created","version":2}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Risks/Events/RiskDraftCreated.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | A risk draft was created in a program with its authored content by a named member, awaiting evaluation. |
| Selected source meaning | A risk draft was created in a program with its authored content by a named member, awaiting evaluation. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/Risks/Events/RiskDraftCreated.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### RiskDraftRevised

| Canonical event detail | Value |
| --- | --- |
| Target event name | RiskDraftRevised |
| Selected source event name | RiskDraftRevised |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:RiskDraftRevised |
| Owner selection | one exact canonical authority owner |
| Target concrete type | RiskDraftRevised |
| Adopted wire | `{"discriminator":"bdgrz.risk.draft.revised","version":2}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Risks/Events/RiskDraftRevised.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | A risk draft's authored content was revised by a named member, producing the draft's next revision. |
| Selected source meaning | A risk draft's authored content was revised by a named member, producing the draft's next revision. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/Risks/Events/RiskDraftRevised.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]




[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]
