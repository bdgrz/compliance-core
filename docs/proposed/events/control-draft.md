# ControlDraft events

Canonical events: **12**. Complete canonical event and available schema records: [owner event JSON](../data/events/control-draft.json).

| Event | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- |
| ControlApproved | business | bdgrz.control.approved | 2 | [Placeholder: schema absent] | Activates an immutable control version from one exact reviewed draft revision. |
| ControlDraftCreated | business | bdgrz.control.draft.created | 2 | [Placeholder: schema absent] | A control draft was created in a program with its identifier and authored content by a named member, awaiting review. |
| ControlDraftDiscarded | business | bdgrz.control.draft.discarded | 2 | [Placeholder: schema absent] | A control draft was discarded with a stated rationale by a named member, ending it without approval. |
| ControlDraftRevised | business | bdgrz.control.draft.revised | 2 | [Placeholder: schema absent] | A control draft's authored content was revised by a named member, producing the draft's next revision for review. |
| ControlOwnerPersonDesignated | business | bdgrz.control.owner_person.designated | 2 | [Placeholder: schema absent] | Designates, or clears with a null PersonId, the governed workforce person who owns one exact pending control revision. The person need not sign in. The signed-in recording actor is kept separate, and CorrelatedMemberId is snapshotted only to apply owner-based separation of duties; it never grants access. |
| ControlProposalWithdrawn | business | bdgrz.control.proposal.withdrawn | 2 | [Placeholder: schema absent] | Withdraws a pending successor draft or retirement proposal at its exact revision. The current approved version stays effective; the ControlDraftRevised raised in the same commit restores its content on a new revision so stale decisions cannot apply. |
| ControlRetired | business | bdgrz.control.retired | 2 | [Placeholder: schema absent] | Ends future use of one exact approved control version. Prior versions, decisions, and relationships are retained and stay readable. |
| ControlRetirementProposed | business | bdgrz.control.retirement.proposed | 2 | [Placeholder: schema absent] | Proposes ending future use of one exact approved control version. |
| ControlReviewed | business | bdgrz.control.reviewed | 2 | [Placeholder: schema absent] | A control version was reviewed with a decision outcome and rationale by a named reviewer, recorded against the exact draft revision reviewed. |
| ControlSuccessorProposed | business | bdgrz.control.successor.proposed | 2 | [Placeholder: schema absent] | A successor version line was opened for a control from one exact approved predecessor version by a named member, with the successor's content to be authored separately. |
| ResponsibilityAssigned | business | bdgrz.responsibility.assigned | 1 | [Placeholder: schema absent] | A member was assigned a responsibility type over a scoped record for an effective period by a named assigner, together with any separation-of-duties waivers that allowed it. |
| ResponsibilityRevoked | business | bdgrz.responsibility.revoked | 1 | [Placeholder: schema absent] | A member's responsibility assignment over a scoped record was revoked with a stated reason by a named member, removing the authority it granted. |

## Event details

### ControlApproved

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:ControlApproved |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ControlApproved |
| Adopted wire | `{"discriminator":"bdgrz.control.approved","version":2}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Controls/Events/ControlApproved.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | Activates an immutable control version from one exact reviewed draft revision. |
| Meaning source | record summary in src/Compliance.Common/Features/Controls/Events/ControlApproved.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### ControlDraftCreated

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:ControlDraftCreated |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ControlDraftCreated |
| Adopted wire | `{"discriminator":"bdgrz.control.draft.created","version":2}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Controls/Events/ControlDraftCreated.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | A control draft was created in a program with its identifier and authored content by a named member, awaiting review. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/Controls/Events/ControlDraftCreated.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### ControlDraftDiscarded

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:ControlDraftDiscarded |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ControlDraftDiscarded |
| Adopted wire | `{"discriminator":"bdgrz.control.draft.discarded","version":2}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Controls/Events/ControlDraftDiscarded.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | A control draft was discarded with a stated rationale by a named member, ending it without approval. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/Controls/Events/ControlDraftDiscarded.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### ControlDraftRevised

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:ControlDraftRevised |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ControlDraftRevised |
| Adopted wire | `{"discriminator":"bdgrz.control.draft.revised","version":2}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Controls/Events/ControlDraftRevised.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | A control draft's authored content was revised by a named member, producing the draft's next revision for review. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/Controls/Events/ControlDraftRevised.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### ControlOwnerPersonDesignated

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:ControlOwnerPersonDesignated |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ControlOwnerPersonDesignated |
| Adopted wire | `{"discriminator":"bdgrz.control.owner_person.designated","version":2}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Controls/Events/ControlOwnerPersonDesignated.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | Designates, or clears with a null PersonId, the governed workforce person who owns one exact pending control revision. The person need not sign in. The signed-in recording actor is kept separate, and CorrelatedMemberId is snapshotted only to apply owner-based separation of duties; it never grants access. |
| Meaning source | record summary in src/Compliance.Common/Features/Controls/Events/ControlOwnerPersonDesignated.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### ControlProposalWithdrawn

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:ControlProposalWithdrawn |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ControlProposalWithdrawn |
| Adopted wire | `{"discriminator":"bdgrz.control.proposal.withdrawn","version":2}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Controls/Events/ControlProposalWithdrawn.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | Withdraws a pending successor draft or retirement proposal at its exact revision. The current approved version stays effective; the ControlDraftRevised raised in the same commit restores its content on a new revision so stale decisions cannot apply. |
| Meaning source | record summary in src/Compliance.Common/Features/Controls/Events/ControlProposalWithdrawn.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### ControlRetired

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:ControlRetired |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ControlRetired |
| Adopted wire | `{"discriminator":"bdgrz.control.retired","version":2}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Controls/Events/ControlRetired.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | Ends future use of one exact approved control version. Prior versions, decisions, and relationships are retained and stay readable. |
| Meaning source | record summary in src/Compliance.Common/Features/Controls/Events/ControlRetired.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### ControlRetirementProposed

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:ControlRetirementProposed |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ControlRetirementProposed |
| Adopted wire | `{"discriminator":"bdgrz.control.retirement.proposed","version":2}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Controls/Events/ControlRetirementProposed.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | Proposes ending future use of one exact approved control version. |
| Meaning source | record summary in src/Compliance.Common/Features/Controls/Events/ControlRetirementProposed.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### ControlReviewed

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:ControlReviewed |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ControlReviewed |
| Adopted wire | `{"discriminator":"bdgrz.control.reviewed","version":2}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Controls/Events/ControlReviewed.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | A control version was reviewed with a decision outcome and rationale by a named reviewer, recorded against the exact draft revision reviewed. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/Controls/Events/ControlReviewed.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### ControlSuccessorProposed

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:ControlSuccessorProposed |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ControlSuccessorProposed |
| Adopted wire | `{"discriminator":"bdgrz.control.successor.proposed","version":2}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Controls/Events/ControlSuccessorProposed.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | A successor version line was opened for a control from one exact approved predecessor version by a named member, with the successor's content to be authored separately. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/Controls/Events/ControlSuccessorProposed.cs |

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
| Canonical semantic key | event:ResponsibilityAssigned |
| Owner selection | ResponsibilityScope.RecordType/RecordId + exact owner authority header selects one of three listed scope owners |
| Target concrete type | ResponsibilityAssigned |
| Adopted wire | `{"discriminator":"bdgrz.responsibility.assigned","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Responsibilities/Events/ResponsibilityAssigned.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | A member was assigned a responsibility type over a scoped record for an effective period by a named assigner, together with any separation-of-duties waivers that allowed it. |
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
| Canonical semantic key | event:ResponsibilityRevoked |
| Owner selection | ResponsibilityScope.RecordType/RecordId + exact owner authority header selects one of three listed scope owners |
| Target concrete type | ResponsibilityRevoked |
| Adopted wire | `{"discriminator":"bdgrz.responsibility.revoked","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Responsibilities/Events/ResponsibilityRevoked.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | A member's responsibility assignment over a scoped record was revoked with a stated reason by a named member, removing the authority it granted. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/Responsibilities/Events/ResponsibilityRevoked.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]




[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]
