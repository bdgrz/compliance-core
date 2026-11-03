# MethodologyTemplateContribution events

Canonical events: **3**. Complete canonical event and available schema records: [owner event JSON](../data/events/methodology-template-contribution.json).

| Target event | Selected source name | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- | --- |
| MethodologyTemplateContributionAccepted | MethodologyTemplateContributionAccepted | business | bdgrz.methodologytemplates.methodology-template-contribution-accepted | 3 | [Placeholder: schema absent] | Records the exact accepted contribution content without copying a retained read snapshot. |
| MethodologyTemplateContributionProposed | MethodologyTemplateContributionProposed | business | bdgrz.methodologytemplates.methodology-template-contribution-proposed | 3 | [Placeholder: schema absent] | Records the exact proposed contribution content without copying a retained read snapshot. |
| MethodologyTemplateContributionReviewed | MethodologyTemplateContributionReviewed | business | bdgrz.methodologytemplates.methodology-template-contribution-reviewed | 3 | [Placeholder: schema absent] | Records the exact reviewed contribution content without copying a retained read snapshot. |

## Event details

### MethodologyTemplateContributionAccepted

| Canonical event detail | Value |
| --- | --- |
| Target event name | MethodologyTemplateContributionAccepted |
| Selected source event name | MethodologyTemplateContributionAccepted |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:MethodologyTemplateContributionAccepted |
| Owner selection | one exact canonical authority owner |
| Target concrete type | MethodologyTemplateContributionAccepted |
| Adopted wire | `{"discriminator":"bdgrz.methodologytemplates.methodology-template-contribution-accepted","version":3}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/MethodologyTemplates/Events/MethodologyTemplateContributionAccepted.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | Records the exact accepted contribution content without copying a retained read snapshot. |
| Selected source meaning | Records the exact accepted contribution content without copying a retained read snapshot. |
| Meaning source | record summary in src/Compliance.Common/Features/MethodologyTemplates/Events/MethodologyTemplateContributionAccepted.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### MethodologyTemplateContributionProposed

| Canonical event detail | Value |
| --- | --- |
| Target event name | MethodologyTemplateContributionProposed |
| Selected source event name | MethodologyTemplateContributionProposed |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:MethodologyTemplateContributionProposed |
| Owner selection | one exact canonical authority owner |
| Target concrete type | MethodologyTemplateContributionProposed |
| Adopted wire | `{"discriminator":"bdgrz.methodologytemplates.methodology-template-contribution-proposed","version":3}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/MethodologyTemplates/Events/MethodologyTemplateContributionProposed.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | Records the exact proposed contribution content without copying a retained read snapshot. |
| Selected source meaning | Records the exact proposed contribution content without copying a retained read snapshot. |
| Meaning source | record summary in src/Compliance.Common/Features/MethodologyTemplates/Events/MethodologyTemplateContributionProposed.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### MethodologyTemplateContributionReviewed

| Canonical event detail | Value |
| --- | --- |
| Target event name | MethodologyTemplateContributionReviewed |
| Selected source event name | MethodologyTemplateContributionReviewed |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:MethodologyTemplateContributionReviewed |
| Owner selection | one exact canonical authority owner |
| Target concrete type | MethodologyTemplateContributionReviewed |
| Adopted wire | `{"discriminator":"bdgrz.methodologytemplates.methodology-template-contribution-reviewed","version":3}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/MethodologyTemplates/Events/MethodologyTemplateContributionReviewed.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | Records the exact reviewed contribution content without copying a retained read snapshot. |
| Selected source meaning | Records the exact reviewed contribution content without copying a retained read snapshot. |
| Meaning source | record summary in src/Compliance.Common/Features/MethodologyTemplates/Events/MethodologyTemplateContributionReviewed.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]




[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]
