# MethodologyTemplate events

Canonical events: **5**. Complete canonical event and available schema records: [owner event JSON](../data/events/methodology-template.json).

| Target event | Selected source name | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- | --- |
| MethodologyTemplateCreated | MethodologyTemplateCreated | business | bdgrz.methodologytemplates.methodology-template-created | 3 | [Placeholder: schema absent] | A methodology template was created with its first authored version by a named author, awaiting review and publication. |
| MethodologyTemplateDraftReviewed | MethodologyTemplateDraftReviewed | business | bdgrz.methodologytemplates.methodology-template-draft-reviewed | 3 | [Placeholder: schema absent] | A compact native MethodologyTemplateDraftReviewed delta; authored bodies and historical versions are immutable content. |
| MethodologyTemplateDraftRevised | MethodologyTemplateDraftRevised | business | bdgrz.methodologytemplates.methodology-template-draft-revised | 3 | [Placeholder: schema absent] | A compact native MethodologyTemplateDraftRevised delta; authored bodies and historical versions are immutable content. |
| MethodologyTemplateRetired | MethodologyTemplateRetired | business | bdgrz.methodologytemplates.methodology-template-retired | 3 | [Placeholder: schema absent] | An active published methodology template was retired by a named member, ending its availability while its historical versions remain. |
| MethodologyTemplateVersionPublished | MethodologyTemplateVersionPublished | business | bdgrz.methodologytemplates.methodology-template-version-published | 3 | [Placeholder: schema absent] | A compact native MethodologyTemplateVersionPublished delta; authored bodies and historical versions are immutable content. |

## Event details

### MethodologyTemplateCreated

| Canonical event detail | Value |
| --- | --- |
| Target event name | MethodologyTemplateCreated |
| Selected source event name | MethodologyTemplateCreated |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:MethodologyTemplateCreated |
| Owner selection | one exact canonical authority owner |
| Target concrete type | MethodologyTemplateCreated |
| Adopted wire | `{"discriminator":"bdgrz.methodologytemplates.methodology-template-created","version":3}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/MethodologyTemplates/Events/MethodologyTemplateCreated.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | A methodology template was created with its first authored version by a named author, awaiting review and publication. |
| Selected source meaning | A methodology template was created with its first authored version by a named author, awaiting review and publication. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/MethodologyTemplates/Events/MethodologyTemplateCreated.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### MethodologyTemplateDraftReviewed

| Canonical event detail | Value |
| --- | --- |
| Target event name | MethodologyTemplateDraftReviewed |
| Selected source event name | MethodologyTemplateDraftReviewed |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:MethodologyTemplateDraftReviewed |
| Owner selection | one exact canonical authority owner |
| Target concrete type | MethodologyTemplateDraftReviewed |
| Adopted wire | `{"discriminator":"bdgrz.methodologytemplates.methodology-template-draft-reviewed","version":3}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/MethodologyTemplates/Events/MethodologyTemplateDraftReviewed.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | A compact native MethodologyTemplateDraftReviewed delta; authored bodies and historical versions are immutable content. |
| Selected source meaning | A compact native MethodologyTemplateDraftReviewed delta; authored bodies and historical versions are immutable content. |
| Meaning source | record summary in src/Compliance.Common/Features/MethodologyTemplates/Events/MethodologyTemplateDraftReviewed.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### MethodologyTemplateDraftRevised

| Canonical event detail | Value |
| --- | --- |
| Target event name | MethodologyTemplateDraftRevised |
| Selected source event name | MethodologyTemplateDraftRevised |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:MethodologyTemplateDraftRevised |
| Owner selection | one exact canonical authority owner |
| Target concrete type | MethodologyTemplateDraftRevised |
| Adopted wire | `{"discriminator":"bdgrz.methodologytemplates.methodology-template-draft-revised","version":3}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/MethodologyTemplates/Events/MethodologyTemplateDraftRevised.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | A compact native MethodologyTemplateDraftRevised delta; authored bodies and historical versions are immutable content. |
| Selected source meaning | A compact native MethodologyTemplateDraftRevised delta; authored bodies and historical versions are immutable content. |
| Meaning source | record summary in src/Compliance.Common/Features/MethodologyTemplates/Events/MethodologyTemplateDraftRevised.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### MethodologyTemplateRetired

| Canonical event detail | Value |
| --- | --- |
| Target event name | MethodologyTemplateRetired |
| Selected source event name | MethodologyTemplateRetired |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:MethodologyTemplateRetired |
| Owner selection | one exact canonical authority owner |
| Target concrete type | MethodologyTemplateRetired |
| Adopted wire | `{"discriminator":"bdgrz.methodologytemplates.methodology-template-retired","version":3}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/MethodologyTemplates/Events/MethodologyTemplateRetired.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | An active published methodology template was retired by a named member, ending its availability while its historical versions remain. |
| Selected source meaning | An active published methodology template was retired by a named member, ending its availability while its historical versions remain. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/MethodologyTemplates/Events/MethodologyTemplateRetired.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### MethodologyTemplateVersionPublished

| Canonical event detail | Value |
| --- | --- |
| Target event name | MethodologyTemplateVersionPublished |
| Selected source event name | MethodologyTemplateVersionPublished |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:MethodologyTemplateVersionPublished |
| Owner selection | one exact canonical authority owner |
| Target concrete type | MethodologyTemplateVersionPublished |
| Adopted wire | `{"discriminator":"bdgrz.methodologytemplates.methodology-template-version-published","version":3}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/MethodologyTemplates/Events/MethodologyTemplateVersionPublished.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | A compact native MethodologyTemplateVersionPublished delta; authored bodies and historical versions are immutable content. |
| Selected source meaning | A compact native MethodologyTemplateVersionPublished delta; authored bodies and historical versions are immutable content. |
| Meaning source | record summary in src/Compliance.Common/Features/MethodologyTemplates/Events/MethodologyTemplateVersionPublished.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]




[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]
