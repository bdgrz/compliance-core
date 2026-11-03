# CommitmentDraft events

Canonical events: **7**. Complete canonical event and available schema records: [owner event JSON](../data/events/commitment-draft.json).

| Target event | Selected source name | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- | --- |
| CommitmentApproved | CommitmentApproved | business | bdgrz.commitment.approved | 2 | [Placeholder: schema absent] | A commitment draft was approved as a numbered commitment version effective from a stated date, on an accepted review, by a named approver, with any separation-of-duties waiver. |
| CommitmentDraftCreated | CommitmentDraftCreated | business | bdgrz.commitment.draft.created | 2 | [Placeholder: schema absent] | A commitment draft was created for a program service with its kind, identifier, content and source reference by a named member. |
| CommitmentDraftRevised | CommitmentDraftRevised | business | bdgrz.commitment.draft.revised | 2 | [Placeholder: schema absent] | A pending commitment draft was revised with new content and source reference by a named member, starting a new draft revision awaiting review. |
| CommitmentRetired | CommitmentRetired | business | bdgrz.commitments.commitment-retired | 2 | [Placeholder: schema absent] | An approved commitment was retired effective on a stated date by a named actor, ending it for obligations from that date. |
| CommitmentReviewed | CommitmentReviewed | business | bdgrz.commitment.reviewed | 2 | [Placeholder: schema absent] | A commitment draft revision was reviewed with a decision outcome, either accepting it with applicability, version and effective date or requesting changes, by a named reviewer. |
| ResponsibilityAssigned | ResponsibilityAssigned | business | bdgrz.responsibility.assigned | 1 | [Placeholder: schema absent] | A member was assigned a responsibility type over a scoped record for an effective period by a named assigner, together with any separation-of-duties waivers that allowed it. |
| ResponsibilityRevoked | ResponsibilityRevoked | business | bdgrz.responsibility.revoked | 1 | [Placeholder: schema absent] | A member's responsibility assignment over a scoped record was revoked with a stated reason by a named member, removing the authority it granted. |

## Event details

### CommitmentApproved

| Canonical event detail | Value |
| --- | --- |
| Target event name | CommitmentApproved |
| Selected source event name | CommitmentApproved |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:CommitmentApproved |
| Owner selection | one exact canonical authority owner |
| Target concrete type | CommitmentApproved |
| Adopted wire | `{"discriminator":"bdgrz.commitment.approved","version":2}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Commitments/Events/CommitmentApproved.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | A commitment draft was approved as a numbered commitment version effective from a stated date, on an accepted review, by a named approver, with any separation-of-duties waiver. |
| Selected source meaning | A commitment draft was approved as a numbered commitment version effective from a stated date, on an accepted review, by a named approver, with any separation-of-duties waiver. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/Commitments/Events/CommitmentApproved.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### CommitmentDraftCreated

| Canonical event detail | Value |
| --- | --- |
| Target event name | CommitmentDraftCreated |
| Selected source event name | CommitmentDraftCreated |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:CommitmentDraftCreated |
| Owner selection | one exact canonical authority owner |
| Target concrete type | CommitmentDraftCreated |
| Adopted wire | `{"discriminator":"bdgrz.commitment.draft.created","version":2}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Commitments/Events/CommitmentDraftCreated.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | A commitment draft was created for a program service with its kind, identifier, content and source reference by a named member. |
| Selected source meaning | A commitment draft was created for a program service with its kind, identifier, content and source reference by a named member. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/Commitments/Events/CommitmentDraftCreated.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### CommitmentDraftRevised

| Canonical event detail | Value |
| --- | --- |
| Target event name | CommitmentDraftRevised |
| Selected source event name | CommitmentDraftRevised |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:CommitmentDraftRevised |
| Owner selection | one exact canonical authority owner |
| Target concrete type | CommitmentDraftRevised |
| Adopted wire | `{"discriminator":"bdgrz.commitment.draft.revised","version":2}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Commitments/Events/CommitmentDraftRevised.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | A pending commitment draft was revised with new content and source reference by a named member, starting a new draft revision awaiting review. |
| Selected source meaning | A pending commitment draft was revised with new content and source reference by a named member, starting a new draft revision awaiting review. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/Commitments/Events/CommitmentDraftRevised.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### CommitmentRetired

| Canonical event detail | Value |
| --- | --- |
| Target event name | CommitmentRetired |
| Selected source event name | CommitmentRetired |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:CommitmentRetired |
| Owner selection | one exact canonical authority owner |
| Target concrete type | CommitmentRetired |
| Adopted wire | `{"discriminator":"bdgrz.commitments.commitment-retired","version":2}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Commitments/Events/CommitmentRetired.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | An approved commitment was retired effective on a stated date by a named actor, ending it for obligations from that date. |
| Selected source meaning | An approved commitment was retired effective on a stated date by a named actor, ending it for obligations from that date. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/Commitments/Events/CommitmentRetired.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### CommitmentReviewed

| Canonical event detail | Value |
| --- | --- |
| Target event name | CommitmentReviewed |
| Selected source event name | CommitmentReviewed |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:CommitmentReviewed |
| Owner selection | one exact canonical authority owner |
| Target concrete type | CommitmentReviewed |
| Adopted wire | `{"discriminator":"bdgrz.commitment.reviewed","version":2}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Commitments/Events/CommitmentReviewed.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | A commitment draft revision was reviewed with a decision outcome, either accepting it with applicability, version and effective date or requesting changes, by a named reviewer. |
| Selected source meaning | A commitment draft revision was reviewed with a decision outcome, either accepting it with applicability, version and effective date or requesting changes, by a named reviewer. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/Commitments/Events/CommitmentReviewed.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### ResponsibilityAssigned

| Canonical event detail | Value |
| --- | --- |
| Target event name | ResponsibilityAssigned |
| Selected source event name | ResponsibilityAssigned |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:ResponsibilityAssigned |
| Owner selection | ResponsibilityScope.RecordType/RecordId + exact owner authority header selects one of three listed scope owners |
| Target concrete type | ResponsibilityAssigned |
| Adopted wire | `{"discriminator":"bdgrz.responsibility.assigned","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Responsibilities/Events/ResponsibilityAssigned.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | A member was assigned a responsibility type over a scoped record for an effective period by a named assigner, together with any separation-of-duties waivers that allowed it. |
| Selected source meaning | A member was assigned a responsibility type over a scoped record for an effective period by a named assigner, together with any separation-of-duties waivers that allowed it. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/Responsibilities/Events/ResponsibilityAssigned.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### ResponsibilityRevoked

| Canonical event detail | Value |
| --- | --- |
| Target event name | ResponsibilityRevoked |
| Selected source event name | ResponsibilityRevoked |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:ResponsibilityRevoked |
| Owner selection | ResponsibilityScope.RecordType/RecordId + exact owner authority header selects one of three listed scope owners |
| Target concrete type | ResponsibilityRevoked |
| Adopted wire | `{"discriminator":"bdgrz.responsibility.revoked","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Responsibilities/Events/ResponsibilityRevoked.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | A member's responsibility assignment over a scoped record was revoked with a stated reason by a named member, removing the authority it granted. |
| Selected source meaning | A member's responsibility assignment over a scoped record was revoked with a stated reason by a named member, removing the authority it granted. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/Responsibilities/Events/ResponsibilityRevoked.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]




[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]
