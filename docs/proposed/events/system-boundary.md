# SystemBoundary events

Canonical events: **8**. Complete canonical event and available schema records: [owner event JSON](../data/events/system-boundary.json).

| Event | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- |
| BoundaryApproved | business | bdgrz.boundary.approved | 2 | [Placeholder: schema absent] | A system boundary draft version was approved as the boundary effective from a stated date, on the strength of an accepted review and by a named approver. |
| BoundaryDraftCreated | business | bdgrz.boundary.draft.created | 2 | [Placeholder: schema absent] | A system boundary draft was created for a program with its authored content by a named author, awaiting review. |
| BoundaryDraftDiscarded | business | bdgrz.boundary.draft.discarded | 2 | [Placeholder: schema absent] | A system boundary draft version was discarded by a named member, ending it without approval. |
| BoundaryDraftRevised | business | bdgrz.boundary.draft.revised | 2 | [Placeholder: schema absent] | A system boundary draft version's content was revised by a named author, producing its next draft revision for review. |
| BoundaryReviewed | business | bdgrz.boundary.reviewed | 2 | [Placeholder: schema absent] | A system boundary draft revision was reviewed with a decision outcome by a named reviewer, optionally under a separation-of-duties waiver. |
| BoundarySuccessorProposed | business | bdgrz.boundary.successor.proposed | 2 | [Placeholder: schema absent] | A successor draft version of a system boundary was proposed from one exact approved predecessor version, with new content by a named author. |
| ResponsibilityAssigned | business | bdgrz.responsibility.assigned | 1 | [Placeholder: schema absent] | A member was assigned a responsibility type over a scoped record for an effective period by a named assigner, together with any separation-of-duties waivers that allowed it. |
| ResponsibilityRevoked | business | bdgrz.responsibility.revoked | 1 | [Placeholder: schema absent] | A member's responsibility assignment over a scoped record was revoked with a stated reason by a named member, removing the authority it granted. |

## Event details

### BoundaryApproved

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:BoundaryApproved |
| Owner selection | one exact canonical authority owner |
| Target concrete type | BoundaryApproved |
| Adopted wire | `{"discriminator":"bdgrz.boundary.approved","version":2}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Boundaries/Events/BoundaryApproved.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | A system boundary draft version was approved as the boundary effective from a stated date, on the strength of an accepted review and by a named approver. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/Boundaries/Events/BoundaryApproved.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [S037](../reactors/boundary-risk-reassessment-reactor.md#s037) | physical root, logical prerequisite | SystemBoundary | RaiseRiskReassessmentTriggers | RiskGovernance |

### BoundaryDraftCreated

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:BoundaryDraftCreated |
| Owner selection | one exact canonical authority owner |
| Target concrete type | BoundaryDraftCreated |
| Adopted wire | `{"discriminator":"bdgrz.boundary.draft.created","version":2}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Boundaries/Events/BoundaryDraftCreated.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | A system boundary draft was created for a program with its authored content by a named author, awaiting review. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/Boundaries/Events/BoundaryDraftCreated.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### BoundaryDraftDiscarded

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:BoundaryDraftDiscarded |
| Owner selection | one exact canonical authority owner |
| Target concrete type | BoundaryDraftDiscarded |
| Adopted wire | `{"discriminator":"bdgrz.boundary.draft.discarded","version":2}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Boundaries/Events/BoundaryDraftDiscarded.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | A system boundary draft version was discarded by a named member, ending it without approval. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/Boundaries/Events/BoundaryDraftDiscarded.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### BoundaryDraftRevised

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:BoundaryDraftRevised |
| Owner selection | one exact canonical authority owner |
| Target concrete type | BoundaryDraftRevised |
| Adopted wire | `{"discriminator":"bdgrz.boundary.draft.revised","version":2}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Boundaries/Events/BoundaryDraftRevised.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | A system boundary draft version's content was revised by a named author, producing its next draft revision for review. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/Boundaries/Events/BoundaryDraftRevised.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### BoundaryReviewed

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:BoundaryReviewed |
| Owner selection | one exact canonical authority owner |
| Target concrete type | BoundaryReviewed |
| Adopted wire | `{"discriminator":"bdgrz.boundary.reviewed","version":2}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Boundaries/Events/BoundaryReviewed.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | A system boundary draft revision was reviewed with a decision outcome by a named reviewer, optionally under a separation-of-duties waiver. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/Boundaries/Events/BoundaryReviewed.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### BoundarySuccessorProposed

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:BoundarySuccessorProposed |
| Owner selection | one exact canonical authority owner |
| Target concrete type | BoundarySuccessorProposed |
| Adopted wire | `{"discriminator":"bdgrz.boundary.successor.proposed","version":2}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Boundaries/Events/BoundarySuccessorProposed.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | A successor draft version of a system boundary was proposed from one exact approved predecessor version, with new content by a named author. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/Boundaries/Events/BoundarySuccessorProposed.cs |

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
