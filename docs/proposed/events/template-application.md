# TemplateApplication events

Canonical events: **4**. Complete canonical event and available schema records: [owner event JSON](../data/events/template-application.json).

| Target event | Selected source name | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- | --- |
| MethodologyTemplateApplied | MethodologyTemplateApplied | business | bdgrz.methodologytemplates.methodology-template-applied | 3 | [Placeholder: schema absent] | A methodology template was applied to a program by a named member, recording the template version and content the program now follows. |
| TemplateApplicationDraftCreated | TemplateApplicationDraftCreated | business | bdgrz.methodologytemplates.template-application-draft-created | 1 | [Placeholder: schema absent] | Records an owning-workflow record creation receipt that remains a draft. |
| TemplateSuccessorAdoptionProposed | TemplateSuccessorAdoptionProposed | business | bdgrz.methodologytemplates.template-successor-adoption-proposed | 3 | [Placeholder: schema absent] | Adoption of a newer successor of an applied methodology template was proposed for a program by a named member, awaiting a disposition. |
| TemplateUpdateDecisionRecorded | TemplateSuccessorDispositionRecorded | business | bdgrz.methodologytemplates.template-successor-disposition-recorded | 3 | [Placeholder: schema absent] | A named program member recorded whether the application would adopt the proposed new template version. |

## Event details

### MethodologyTemplateApplied

| Canonical event detail | Value |
| --- | --- |
| Target event name | MethodologyTemplateApplied |
| Selected source event name | MethodologyTemplateApplied |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:MethodologyTemplateApplied |
| Owner selection | one exact canonical authority owner |
| Target concrete type | MethodologyTemplateApplied |
| Adopted wire | `{"discriminator":"bdgrz.methodologytemplates.methodology-template-applied","version":3}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/MethodologyTemplates/Events/MethodologyTemplateApplied.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | A methodology template was applied to a program by a named member, recording the template version and content the program now follows. |
| Selected source meaning | A methodology template was applied to a program by a named member, recording the template version and content the program now follows. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/MethodologyTemplates/Events/MethodologyTemplateApplied.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### TemplateApplicationDraftCreated

| Canonical event detail | Value |
| --- | --- |
| Target event name | TemplateApplicationDraftCreated |
| Selected source event name | TemplateApplicationDraftCreated |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:TemplateApplicationDraftCreated |
| Owner selection | one exact canonical authority owner |
| Target concrete type | TemplateApplicationDraftCreated |
| Adopted wire | `{"discriminator":"bdgrz.methodologytemplates.template-application-draft-created","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/MethodologyTemplates/Events/TemplateApplicationDraftCreated.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | Records an owning-workflow record creation receipt that remains a draft. |
| Selected source meaning | Records an owning-workflow record creation receipt that remains a draft. |
| Meaning source | record summary in src/Compliance.Common/Features/MethodologyTemplates/Events/TemplateApplicationDraftCreated.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### TemplateSuccessorAdoptionProposed

| Canonical event detail | Value |
| --- | --- |
| Target event name | TemplateSuccessorAdoptionProposed |
| Selected source event name | TemplateSuccessorAdoptionProposed |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:TemplateSuccessorAdoptionProposed |
| Owner selection | one exact canonical authority owner |
| Target concrete type | TemplateSuccessorAdoptionProposed |
| Adopted wire | `{"discriminator":"bdgrz.methodologytemplates.template-successor-adoption-proposed","version":3}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/MethodologyTemplates/Events/TemplateSuccessorAdoptionProposed.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | Adoption of a newer successor of an applied methodology template was proposed for a program by a named member, awaiting a disposition. |
| Selected source meaning | Adoption of a newer successor of an applied methodology template was proposed for a program by a named member, awaiting a disposition. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/MethodologyTemplates/Events/TemplateSuccessorAdoptionProposed.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### TemplateUpdateDecisionRecorded

| Canonical event detail | Value |
| --- | --- |
| Target event name | TemplateUpdateDecisionRecorded |
| Selected source event name | TemplateSuccessorDispositionRecorded |
| Name decision | Names the recorded decision about whether this application adopts the proposed new template version. |
| Canonical semantic key | event:TemplateSuccessorDispositionRecorded |
| Owner selection | one exact canonical authority owner |
| Target concrete type | TemplateSuccessorDispositionRecorded |
| Adopted wire | `{"discriminator":"bdgrz.methodologytemplates.template-successor-disposition-recorded","version":3}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/MethodologyTemplates/Events/TemplateSuccessorDispositionRecorded.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | A named program member recorded whether the application would adopt the proposed new template version. |
| Selected source meaning | A decision on a proposed adoption of a methodology template successor was recorded for a program by a named member. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/MethodologyTemplates/Events/TemplateSuccessorDispositionRecorded.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]




[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]
