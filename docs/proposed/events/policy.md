# Policy events

Canonical events: **8**. Complete canonical event and available schema records: [owner event JSON](../data/events/policy.json).

| Event | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- |
| PolicyApproved | business | bdgrz.policy.approved | 2 | [Placeholder: schema absent] | Activates the exact reviewed draft revision as an immutable version identified by ContentSha256. A major version requires every audience to re-acknowledge. |
| PolicyDraftCreated | business | bdgrz.policy.draft.created | 2 | [Placeholder: schema absent] | A policy draft was created for a program with its identifier and authored content by a named member, awaiting review. |
| PolicyDraftDiscarded | business | bdgrz.policy.draft.discarded | 2 | [Placeholder: schema absent] | A policy draft was discarded with a stated rationale by a named actor, ending it without approval. |
| PolicyDraftRevised | business | bdgrz.policy.draft.revised | 2 | [Placeholder: schema absent] | A new draft revision. PredecessorVersion is set when the revision opens a successor draft from that approved version. |
| PolicyPeriodicReviewConfirmed | business | bdgrz.policy.periodic_review.confirmed | 2 | [Placeholder: schema absent] | A periodic review that confirmed the current version without changing it. |
| PolicyRetired | business | bdgrz.policy.retired | 2 | [Placeholder: schema absent] | An approved policy version was retired effective until a stated date, on the strength of an accepted retirement review and a stated rationale. |
| PolicyRetirementProposed | business | bdgrz.policy.retirement.proposed | 2 | [Placeholder: schema absent] | Retirement of a policy version effective until a stated date was proposed with a rationale by a named member, awaiting review. |
| PolicyReviewed | business | bdgrz.policy.reviewed | 2 | [Placeholder: schema absent] | A policy draft revision was reviewed with a decision outcome and rationale by a named reviewer. |

## Event details

### PolicyApproved

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:PolicyApproved |
| Owner selection | one exact canonical authority owner |
| Target concrete type | PolicyApproved |
| Adopted wire | `{"discriminator":"bdgrz.policy.approved","version":2}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Policies/Events/PolicyApproved.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | Activates the exact reviewed draft revision as an immutable version identified by ContentSha256. A major version requires every audience to re-acknowledge. |
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
| Canonical semantic key | event:PolicyDraftCreated |
| Owner selection | one exact canonical authority owner |
| Target concrete type | PolicyDraftCreated |
| Adopted wire | `{"discriminator":"bdgrz.policy.draft.created","version":2}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Policies/Events/PolicyDraftCreated.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | A policy draft was created for a program with its identifier and authored content by a named member, awaiting review. |
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
| Canonical semantic key | event:PolicyDraftDiscarded |
| Owner selection | one exact canonical authority owner |
| Target concrete type | PolicyDraftDiscarded |
| Adopted wire | `{"discriminator":"bdgrz.policy.draft.discarded","version":2}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Policies/Events/PolicyDraftDiscarded.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | A policy draft was discarded with a stated rationale by a named actor, ending it without approval. |
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
| Canonical semantic key | event:PolicyDraftRevised |
| Owner selection | one exact canonical authority owner |
| Target concrete type | PolicyDraftRevised |
| Adopted wire | `{"discriminator":"bdgrz.policy.draft.revised","version":2}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Policies/Events/PolicyDraftRevised.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | A new draft revision. PredecessorVersion is set when the revision opens a successor draft from that approved version. |
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
| Canonical semantic key | event:PolicyPeriodicReviewConfirmed |
| Owner selection | one exact canonical authority owner |
| Target concrete type | PolicyPeriodicReviewConfirmed |
| Adopted wire | `{"discriminator":"bdgrz.policy.periodic_review.confirmed","version":2}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Policies/Events/PolicyPeriodicReviewConfirmed.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | A periodic review that confirmed the current version without changing it. |
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
| Canonical semantic key | event:PolicyRetired |
| Owner selection | one exact canonical authority owner |
| Target concrete type | PolicyRetired |
| Adopted wire | `{"discriminator":"bdgrz.policy.retired","version":2}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Policies/Events/PolicyRetired.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | An approved policy version was retired effective until a stated date, on the strength of an accepted retirement review and a stated rationale. |
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
| Canonical semantic key | event:PolicyRetirementProposed |
| Owner selection | one exact canonical authority owner |
| Target concrete type | PolicyRetirementProposed |
| Adopted wire | `{"discriminator":"bdgrz.policy.retirement.proposed","version":2}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Policies/Events/PolicyRetirementProposed.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | Retirement of a policy version effective until a stated date was proposed with a rationale by a named member, awaiting review. |
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
| Canonical semantic key | event:PolicyReviewed |
| Owner selection | one exact canonical authority owner |
| Target concrete type | PolicyReviewed |
| Adopted wire | `{"discriminator":"bdgrz.policy.reviewed","version":2}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Policies/Events/PolicyReviewed.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | A policy draft revision was reviewed with a decision outcome and rationale by a named reviewer. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/Policies/Events/PolicyReviewed.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]




[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]
