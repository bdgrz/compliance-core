# ReadinessGapPlan events

Canonical events: **4**. Complete canonical event and available schema records: [owner event JSON](../data/events/readiness-gap-plan.json).

| Target event | Selected source name | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- | --- |
| ReadinessGapAnnotated | ReadinessGapAnnotated | business | bdgrz.readiness.gap.annotated | 1 | 7 | Advisor feedback was recorded on a gap as the root native fact of its operation. Revision is the gap plan revision at the annotation, which an annotation never advances. |
| ReadinessGapPlanReadinessDecisionAuthorized | ReadinessGapPlanReadinessDecisionUseAuthorized | reservation | bdgrz.readiness-gap-plan.readiness-gap-plan-readiness-decision-use-authorized | 1 | 8 | The exact gap plan is held for a readiness decision. The use does not advance the gap plan revision: Revision is the held revision, and Generation is the adoption generation the decision seals. |
| ReadinessGapPlanReadinessDecisionAuthorizationClosed | ReadinessGapPlanReadinessDecisionUseSettled | reservation | bdgrz.readiness-gap-plan.readiness-gap-plan-readiness-decision-use-settled | 1 | 8 | A readiness decision use of a gap plan ended: released after the irreversible decision, or cleared after the decision was canceled. A settlement before any grant fences the key so a delayed acquisition cannot recreate the authority. The disposition is recorded in the settlement intent. |
| ReadinessGapPlanned | ReadinessGapPlanned | business | bdgrz.readiness.gap.planned | 1 | 7 | A gap plan was recorded as the root native fact of its operation. Revision is the gap plan revision after this plan and PlanContent the complete ReadinessGapPlanView. |

## Event details

### ReadinessGapAnnotated

| Canonical event detail | Value |
| --- | --- |
| Target event name | ReadinessGapAnnotated |
| Selected source event name | ReadinessGapAnnotated |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:ReadinessGapAnnotated |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ReadinessGapAnnotated |
| Adopted wire | `{"discriminator":"bdgrz.readiness.gap.annotated","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Readiness/Events/ReadinessGapAnnotated.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | Advisor feedback was recorded on a gap as the root native fact of its operation. Revision is the gap plan revision at the annotation, which an annotation never advances. |
| Selected source meaning | Advisor feedback was recorded on a gap as the root native fact of its operation. Revision is the gap plan revision at the annotation, which an annotation never advances. |
| Meaning source | record summary in src/Compliance.Common/Features/Readiness/Events/ReadinessGapAnnotated.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | ReadinessGapAnnotated |
| Schema discriminator | bdgrz.readiness.gap.annotated |
| Schema discriminatorVersion | 1 |
| Schema owner | ReadinessGapPlan |
| Schema existingContract | src/Compliance.Common/Features/Readiness/ReadinessGapAnnotated.cs |
| Schema status | selected target schema; implementation pending |
| Schema reason | source command accepted result |
| Schema changes | Externalize structured ReadinessAnnotationView including nested collections; a former item cap cannot become an event-size rejection for valid larger content. |
| Schema reducerRule | Validate owner/generation and local predecessor; apply only this owner delta or immutable content reference. Never hydrate a foreign aggregate or mutate based on a projection. |
| Schema serializer | Register concrete target event and every proof/content variant in ComplianceCoreJsonContext and attributed persistence preflight coverage. |
| Schema nativeResponseRule | FirstResponse is non-null only on this logical command first native accepting fact; it references FirstResponseDescriptor. OperationResult is non-null on the declared terminal business fact and references MutationResultContent. Both use verified content and static concrete result serializers; neither is reconstructed from current state. |

#### Event fields

| Field | Type | Required |
| --- | --- | --- |
| TenantId | Uuid | true |
| ProgramId | Uuid | true |
| Revision | long | true |
| AnnotationContent | ImmutableContentRef | true |
| Operation | OperationProvenance | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

#### Nested content schemas

#### Content schema: ReadinessGapAnnotated.AnnotationContent.v1

| Property | Value |
| --- | --- |
| field | AnnotationContent |
| schema | ReadinessGapAnnotated.AnnotationContent.v1 |
| contentType | ReadinessAnnotationView |
| rule | Complete original typed structured value, including nested collections, written/verified before this event. No cumulative parent view or mutable latest pointer. |


[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### ReadinessGapPlanReadinessDecisionAuthorized

| Canonical event detail | Value |
| --- | --- |
| Target event name | ReadinessGapPlanReadinessDecisionAuthorized |
| Selected source event name | ReadinessGapPlanReadinessDecisionUseAuthorized |
| Name decision | Authorization is already the completed fact; Use adds no stakeholder meaning. |
| Canonical semantic key | event:ReadinessGapPlanReadinessDecisionUseAuthorized |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ReadinessGapPlanReadinessDecisionUseAuthorized |
| Adopted wire | `{"discriminator":"bdgrz.readiness-gap-plan.readiness-gap-plan-readiness-decision-use-authorized","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Readiness/Events/ReadinessGapPlanReadinessDecisionUseAuthorized.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | The exact gap plan is held for a readiness decision. The use does not advance the gap plan revision: Revision is the held revision, and Generation is the adoption generation the decision seals. |
| Selected source meaning | The exact gap plan is held for a readiness decision. The use does not advance the gap plan revision: Revision is the held revision, and Generation is the adoption generation the decision seals. |
| Meaning source | record summary in src/Compliance.Common/Features/Readiness/Events/ReadinessGapPlanReadinessDecisionUseAuthorized.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | ReadinessGapPlanReadinessDecisionUseAuthorized |
| Schema discriminator | bdgrz.readiness-gap-plan.readiness-gap-plan-readiness-decision-use-authorized |
| Schema discriminatorVersion | 1 |
| Schema owner | ReadinessGapPlan |
| Schema status | selected target schema; implementation pending |
| Schema reason | typed reactor receiver accepted business fact |
| Schema changes | None listed |
| Schema reducerRule | Validate owner/generation and local predecessor; apply only this owner delta or immutable content reference. Never hydrate a foreign aggregate or mutate based on a projection. |
| Schema serializer | Register concrete target event and every proof/content variant in ComplianceCoreJsonContext and attributed persistence preflight coverage. |
| Schema nativeResponseRule | FirstResponse is non-null only on this logical command first native accepting fact; it references FirstResponseDescriptor. OperationResult is non-null on the declared terminal business fact and references MutationResultContent. Both use verified content and static concrete result serializers; neither is reconstructed from current state. |

#### Event fields

| Field | Type | Required |
| --- | --- | --- |
| Owner | EventStreamAddress | true |
| Revision | long | true |
| Generation | long | true |
| Intent | ImmutableContentRef | true |
| SourceProofs | ImmutableContentRef? | true |
| Operation | OperationProvenance | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

#### Nested content schemas

#### Content schema: source-use-authorization-intent.v1

| Property | Value |
| --- | --- |
| field | Intent |
| schema | source-use-authorization-intent.v1 |
| recordType | SourceUseAuthorizationIntent |
| rule | A use fact points to the immutable original workflow intent plus this exact source-specific requirement/token. No arbitrary command snapshot serves as policy proof. Terminal settlement before grant fences the same key; neither lease expiry nor missing read releases it. |

##### Fields

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| ParentIntent | ImmutableContentRef | true |
| Reservation | ReservationKey | true |
| Requirement | SourceUseRequirement | true |
| Grant | ReservationGrant? | true |
| Disposition | SourceUseSettlementDisposition? | true |

##### Variant: SourceUseAuthorizationIntent

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| ParentIntent | ImmutableContentRef | true |
| Reservation | ReservationKey | true |
| Requirement | SourceUseRequirement | true |
| Grant | ReservationGrant? | true |
| Disposition | SourceUseSettlementDisposition? | true |


#### Resolved intent schema

SourceUseAuthorizationIntent

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [T065](../reactors/record-program-readiness-and-type-i-decisions-reactor.md#t065) | emitted semantic fact | ProgramReadiness | AuthorizeReadinessGapPlanReadinessDecision | ReadinessGapPlan |

### ReadinessGapPlanReadinessDecisionAuthorizationClosed

| Canonical event detail | Value |
| --- | --- |
| Target event name | ReadinessGapPlanReadinessDecisionAuthorizationClosed |
| Selected source event name | ReadinessGapPlanReadinessDecisionUseSettled |
| Name decision | The exact authorization ended; the recorded outcome still distinguishes use, cancellation, or a late fence. |
| Canonical semantic key | event:ReadinessGapPlanReadinessDecisionUseSettled |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ReadinessGapPlanReadinessDecisionUseSettled |
| Adopted wire | `{"discriminator":"bdgrz.readiness-gap-plan.readiness-gap-plan-readiness-decision-use-settled","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Readiness/Events/ReadinessGapPlanReadinessDecisionUseSettled.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | A readiness decision use of a gap plan ended: released after the irreversible decision, or cleared after the decision was canceled. A settlement before any grant fences the key so a delayed acquisition cannot recreate the authority. The disposition is recorded in the settlement intent. |
| Selected source meaning | A readiness decision use of a gap plan ended: released after the irreversible decision, or cleared after the decision was canceled. A settlement before any grant fences the key so a delayed acquisition cannot recreate the authority. The disposition is recorded in the settlement intent. |
| Meaning source | record summary in src/Compliance.Common/Features/Readiness/Events/ReadinessGapPlanReadinessDecisionUseSettled.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | ReadinessGapPlanReadinessDecisionUseSettled |
| Schema discriminator | bdgrz.readiness-gap-plan.readiness-gap-plan-readiness-decision-use-settled |
| Schema discriminatorVersion | 1 |
| Schema owner | ReadinessGapPlan |
| Schema status | selected target schema; implementation pending |
| Schema reason | typed reactor receiver accepted business fact |
| Schema changes | None listed |
| Schema reducerRule | Validate owner/generation and local predecessor; apply only this owner delta or immutable content reference. Never hydrate a foreign aggregate or mutate based on a projection. |
| Schema serializer | Register concrete target event and every proof/content variant in ComplianceCoreJsonContext and attributed persistence preflight coverage. |
| Schema nativeResponseRule | FirstResponse is non-null only on this logical command first native accepting fact; it references FirstResponseDescriptor. OperationResult is non-null on the declared terminal business fact and references MutationResultContent. Both use verified content and static concrete result serializers; neither is reconstructed from current state. |

#### Event fields

| Field | Type | Required |
| --- | --- | --- |
| Owner | EventStreamAddress | true |
| Revision | long | true |
| Generation | long | true |
| Intent | ImmutableContentRef | true |
| SourceProofs | ImmutableContentRef? | true |
| Operation | OperationProvenance | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

#### Nested content schemas

#### Content schema: source-use-authorization-intent.v1

| Property | Value |
| --- | --- |
| field | Intent |
| schema | source-use-authorization-intent.v1 |
| recordType | SourceUseAuthorizationIntent |
| rule | A use fact points to the immutable original workflow intent plus this exact source-specific requirement/token. No arbitrary command snapshot serves as policy proof. Terminal settlement before grant fences the same key; neither lease expiry nor missing read releases it. |

##### Fields

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| ParentIntent | ImmutableContentRef | true |
| Reservation | ReservationKey | true |
| Requirement | SourceUseRequirement | true |
| Grant | ReservationGrant? | true |
| Disposition | SourceUseSettlementDisposition? | true |

##### Variant: SourceUseAuthorizationIntent

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| ParentIntent | ImmutableContentRef | true |
| Reservation | ReservationKey | true |
| Requirement | SourceUseRequirement | true |
| Grant | ReservationGrant? | true |
| Disposition | SourceUseSettlementDisposition? | true |


#### Resolved intent schema

SourceUseAuthorizationIntent

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [T066](../reactors/record-program-readiness-and-type-i-decisions-reactor.md#t066) | emitted semantic fact | ProgramReadiness | CloseReadinessGapPlanReadinessDecisionAuthorization | ReadinessGapPlan |

### ReadinessGapPlanned

| Canonical event detail | Value |
| --- | --- |
| Target event name | ReadinessGapPlanned |
| Selected source event name | ReadinessGapPlanned |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:ReadinessGapPlanned |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ReadinessGapPlanned |
| Adopted wire | `{"discriminator":"bdgrz.readiness.gap.planned","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Readiness/Events/ReadinessGapPlanned.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | A gap plan was recorded as the root native fact of its operation. Revision is the gap plan revision after this plan and PlanContent the complete ReadinessGapPlanView. |
| Selected source meaning | A gap plan was recorded as the root native fact of its operation. Revision is the gap plan revision after this plan and PlanContent the complete ReadinessGapPlanView. |
| Meaning source | record summary in src/Compliance.Common/Features/Readiness/Events/ReadinessGapPlanned.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | ReadinessGapPlanned |
| Schema discriminator | bdgrz.readiness.gap.planned |
| Schema discriminatorVersion | 1 |
| Schema owner | ReadinessGapPlan |
| Schema existingContract | src/Compliance.Common/Features/Readiness/ReadinessGapPlanned.cs |
| Schema status | selected target schema; implementation pending |
| Schema reason | source command accepted result |
| Schema changes | Externalize structured ReadinessGapPlanView including nested collections; a former item cap cannot become an event-size rejection for valid larger content. |
| Schema reducerRule | Validate owner/generation and local predecessor; apply only this owner delta or immutable content reference. Never hydrate a foreign aggregate or mutate based on a projection. |
| Schema serializer | Register concrete target event and every proof/content variant in ComplianceCoreJsonContext and attributed persistence preflight coverage. |
| Schema nativeResponseRule | FirstResponse is non-null only on this logical command first native accepting fact; it references FirstResponseDescriptor. OperationResult is non-null on the declared terminal business fact and references MutationResultContent. Both use verified content and static concrete result serializers; neither is reconstructed from current state. |

#### Event fields

| Field | Type | Required |
| --- | --- | --- |
| TenantId | Uuid | true |
| ProgramId | Uuid | true |
| Revision | long | true |
| PlanContent | ImmutableContentRef | true |
| Operation | OperationProvenance | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

#### Nested content schemas

#### Content schema: ReadinessGapPlanned.PlanContent.v1

| Property | Value |
| --- | --- |
| field | PlanContent |
| schema | ReadinessGapPlanned.PlanContent.v1 |
| contentType | ReadinessGapPlanView |
| rule | Complete original typed structured value, including nested collections, written/verified before this event. No cumulative parent view or mutable latest pointer. |


[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]




[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]
