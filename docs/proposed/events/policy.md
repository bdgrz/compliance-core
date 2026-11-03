# Policy events

Canonical events: **8**. Complete canonical event and available schema records: [owner event JSON](../data/events/policy.json).

| Target event | Selected source name | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- | --- |
| PolicyApproved | PolicyApproved | business | bdgrz.policy.approved | 2 | [Placeholder: schema absent] | Activates the exact reviewed draft revision as an immutable version identified by ContentSha256. A major version requires every audience to re-acknowledge. |
| PolicyDraftCreated | PolicyDraftCreated | business | bdgrz.policy.draft.created | 2 | [Placeholder: schema absent] | A policy draft was created for a program with its identifier and authored content by a named member, awaiting review. |
| PolicyDraftDiscarded | PolicyDraftDiscarded | business | bdgrz.policy.draft.discarded | 2 | [Placeholder: schema absent] | A policy draft was discarded with a stated rationale by a named actor, ending it without approval. |
| PolicyDraftRevised | PolicyDraftRevised | business | bdgrz.policy.draft.revised | 2 | [Placeholder: schema absent] | A new draft revision. PredecessorVersion is set when the revision opens a successor draft from that approved version. |
| PolicyPeriodicReviewConfirmed | PolicyPeriodicReviewConfirmed | business | bdgrz.policy.periodic_review.confirmed | 2 | [Placeholder: schema absent] | A periodic review that confirmed the current version without changing it. |
| PolicyRetired | PolicyRetired | business | bdgrz.policy.retired | 2 | [Placeholder: schema absent] | An approved policy version was retired effective until a stated date, on the strength of an accepted retirement review and a stated rationale. |
| PolicyRetirementProposed | PolicyRetirementProposed | business | bdgrz.policy.retirement.proposed | 2 | [Placeholder: schema absent] | Retirement of a policy version effective until a stated date was proposed with a rationale by a named member, awaiting review. |
| PolicyReviewed | PolicyReviewed | business | bdgrz.policy.reviewed | 2 | [Placeholder: schema absent] | A policy draft revision was reviewed with a decision outcome and rationale by a named reviewer. |

## Event details

### PolicyApproved

| Canonical event detail | Value |
| --- | --- |
| Target event name | PolicyApproved |
| Selected source event name | PolicyApproved |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:PolicyApproved |
| Owner selection | one exact canonical authority owner |
| Target concrete type | PolicyApproved |
| Adopted wire | `{"discriminator":"bdgrz.policy.approved","version":2}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Policies/Events/PolicyApproved.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | Activates the exact reviewed draft revision as an immutable version identified by ContentSha256. A major version requires every audience to re-acknowledge. |
| Selected source meaning | Activates the exact reviewed draft revision as an immutable version identified by ContentSha256. A major version requires every audience to re-acknowledge. |
| Meaning source | record summary in src/Compliance.Common/Features/Policies/Events/PolicyApproved.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### PolicyDraftCreated

| Canonical event detail | Value |
| --- | --- |
| Target event name | PolicyDraftCreated |
| Selected source event name | PolicyDraftCreated |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:PolicyDraftCreated |
| Owner selection | one exact canonical authority owner |
| Target concrete type | PolicyDraftCreated |
| Adopted wire | `{"discriminator":"bdgrz.policy.draft.created","version":2}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Policies/Events/PolicyDraftCreated.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | A policy draft was created for a program with its identifier and authored content by a named member, awaiting review. |
| Selected source meaning | A policy draft was created for a program with its identifier and authored content by a named member, awaiting review. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/Policies/Events/PolicyDraftCreated.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### PolicyDraftDiscarded

| Canonical event detail | Value |
| --- | --- |
| Target event name | PolicyDraftDiscarded |
| Selected source event name | PolicyDraftDiscarded |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:PolicyDraftDiscarded |
| Owner selection | one exact canonical authority owner |
| Target concrete type | PolicyDraftDiscarded |
| Adopted wire | `{"discriminator":"bdgrz.policy.draft.discarded","version":2}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Policies/Events/PolicyDraftDiscarded.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | A policy draft was discarded with a stated rationale by a named actor, ending it without approval. |
| Selected source meaning | A policy draft was discarded with a stated rationale by a named actor, ending it without approval. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/Policies/Events/PolicyDraftDiscarded.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### PolicyDraftRevised

| Canonical event detail | Value |
| --- | --- |
| Target event name | PolicyDraftRevised |
| Selected source event name | PolicyDraftRevised |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:PolicyDraftRevised |
| Owner selection | one exact canonical authority owner |
| Target concrete type | PolicyDraftRevised |
| Adopted wire | `{"discriminator":"bdgrz.policy.draft.revised","version":2}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Policies/Events/PolicyDraftRevised.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | A new draft revision. PredecessorVersion is set when the revision opens a successor draft from that approved version. |
| Selected source meaning | A new draft revision. PredecessorVersion is set when the revision opens a successor draft from that approved version. |
| Meaning source | record summary in src/Compliance.Common/Features/Policies/Events/PolicyDraftRevised.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### PolicyPeriodicReviewConfirmed

| Canonical event detail | Value |
| --- | --- |
| Target event name | PolicyPeriodicReviewConfirmed |
| Selected source event name | PolicyPeriodicReviewConfirmed |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:PolicyPeriodicReviewConfirmed |
| Owner selection | one exact canonical authority owner |
| Target concrete type | PolicyPeriodicReviewConfirmed |
| Adopted wire | `{"discriminator":"bdgrz.policy.periodic_review.confirmed","version":2}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Policies/Events/PolicyPeriodicReviewConfirmed.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | A periodic review that confirmed the current version without changing it. |
| Selected source meaning | A periodic review that confirmed the current version without changing it. |
| Meaning source | record summary in src/Compliance.Common/Features/Policies/Events/PolicyPeriodicReviewConfirmed.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### PolicyRetired

| Canonical event detail | Value |
| --- | --- |
| Target event name | PolicyRetired |
| Selected source event name | PolicyRetired |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:PolicyRetired |
| Owner selection | one exact canonical authority owner |
| Target concrete type | PolicyRetired |
| Adopted wire | `{"discriminator":"bdgrz.policy.retired","version":2}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Policies/Events/PolicyRetired.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | An approved policy version was retired effective until a stated date, on the strength of an accepted retirement review and a stated rationale. |
| Selected source meaning | An approved policy version was retired effective until a stated date, on the strength of an accepted retirement review and a stated rationale. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/Policies/Events/PolicyRetired.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### PolicyRetirementProposed

| Canonical event detail | Value |
| --- | --- |
| Target event name | PolicyRetirementProposed |
| Selected source event name | PolicyRetirementProposed |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:PolicyRetirementProposed |
| Owner selection | one exact canonical authority owner |
| Target concrete type | PolicyRetirementProposed |
| Adopted wire | `{"discriminator":"bdgrz.policy.retirement.proposed","version":2}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Policies/Events/PolicyRetirementProposed.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | Retirement of a policy version effective until a stated date was proposed with a rationale by a named member, awaiting review. |
| Selected source meaning | Retirement of a policy version effective until a stated date was proposed with a rationale by a named member, awaiting review. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/Policies/Events/PolicyRetirementProposed.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### PolicyReviewed

| Canonical event detail | Value |
| --- | --- |
| Target event name | PolicyReviewed |
| Selected source event name | PolicyReviewed |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:PolicyReviewed |
| Owner selection | one exact canonical authority owner |
| Target concrete type | PolicyReviewed |
| Adopted wire | `{"discriminator":"bdgrz.policy.reviewed","version":2}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Policies/Events/PolicyReviewed.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | A policy draft revision was reviewed with a decision outcome and rationale by a named reviewer. |
| Selected source meaning | A policy draft revision was reviewed with a decision outcome and rationale by a named reviewer. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/Policies/Events/PolicyReviewed.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]




[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]
