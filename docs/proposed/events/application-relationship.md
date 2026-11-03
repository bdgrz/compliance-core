# ApplicationRelationship events

Canonical events: **3**. Complete canonical event and available schema records: [owner event JSON](../data/events/application-relationship.json).

| Target event | Selected source name | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- | --- |
| ApplicationRelationshipAdded | ApplicationRelationshipAdded | business | bdgrz.application.relationship.added | 1 | 12 | A relationship of a stated type was added from a declared application to a target application, recorded as revision 1 of that relationship by a named actor. |
| ApplicationRelationshipApproved | ApplicationRelationshipApproved | business | bdgrz.application.relationship.approved | 1 | 14 | A proposed successor relationship between two applications was approved by an active member other than its proposer, against the stated impact assessment and the current application revisions. |
| ApplicationRelationshipRemoved | ApplicationRelationshipRemoved | business | bdgrz.application.relationship.removed | 1 | 12 | An active relationship from a declared application to a target application was removed by a named actor, ending it at the next relationship revision. |

## Event details

### ApplicationRelationshipAdded

| Canonical event detail | Value |
| --- | --- |
| Target event name | ApplicationRelationshipAdded |
| Selected source event name | ApplicationRelationshipAdded |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:ApplicationRelationshipAdded |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ApplicationRelationshipAdded |
| Adopted wire | `{"discriminator":"bdgrz.application.relationship.added","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Applications/Events/ApplicationRelationshipAdded.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | A relationship of a stated type was added from a declared application to a target application, recorded as revision 1 of that relationship by a named actor. |
| Selected source meaning | A relationship of a stated type was added from a declared application to a target application, recorded as revision 1 of that relationship by a named actor. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/Applications/Events/ApplicationRelationshipAdded.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | ApplicationRelationshipAdded |
| Schema discriminator | bdgrz.application.relationship.added |
| Schema discriminatorVersion | 1 |
| Schema owner | ApplicationRelationship |
| Schema existingContract | src/Compliance.Common/Features/Applications/ApplicationRelationshipAdded.cs |
| Schema status | selected target schema; implementation pending |
| Schema reason | source command accepted result |
| Schema changes | None listed |
| Schema reducerRule | Validate owner/generation and local predecessor; apply only this owner delta or immutable content reference. Never hydrate a foreign aggregate or mutate based on a projection. |
| Schema serializer | Register concrete target event and every proof/content variant in ComplianceCoreJsonContext and attributed persistence preflight coverage. |
| Schema nativeResponseRule | FirstResponse is non-null only on this logical command first native accepting fact; it references FirstResponseDescriptor. OperationResult is non-null on the declared terminal business fact and references MutationResultContent. Both use verified content and static concrete result serializers; neither is reconstructed from current state. |

#### Event fields

| Field | Type | Required |
| --- | --- | --- |
| TenantId | Uuid | true |
| ApplicationId | Uuid | true |
| RelationshipId | Uuid | true |
| RelationshipRevision | long | true |
| TargetApplicationId | Uuid | true |
| RelationshipType | string | true |
| ActorMemberId | Uuid | true |
| ActorDisplay | string | true |
| ChangedAt | DateTimeOffset | true |
| Operation | OperationProvenance | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### ApplicationRelationshipApproved

| Canonical event detail | Value |
| --- | --- |
| Target event name | ApplicationRelationshipApproved |
| Selected source event name | ApplicationRelationshipApproved |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:ApplicationRelationshipApproved |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ApplicationRelationshipApproved |
| Adopted wire | `{"discriminator":"bdgrz.application.relationship.approved","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Applications/Events/ApplicationRelationshipApproved.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | A proposed successor relationship between two applications was approved by an active member other than its proposer, against the stated impact assessment and the current application revisions. |
| Selected source meaning | A proposed successor relationship between two applications was approved by an active member other than its proposer, against the stated impact assessment and the current application revisions. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/Applications/Events/ApplicationRelationshipApproved.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | ApplicationRelationshipApproved |
| Schema discriminator | bdgrz.application.relationship.approved |
| Schema discriminatorVersion | 1 |
| Schema owner | ApplicationRelationship |
| Schema existingContract | src/Compliance.Common/Features/Applications/ApplicationRelationshipApproved.cs |
| Schema status | selected target schema; implementation pending |
| Schema reason | source command accepted result |
| Schema changes | None listed |
| Schema reducerRule | Validate owner/generation and local predecessor; apply only this owner delta or immutable content reference. Never hydrate a foreign aggregate or mutate based on a projection. |
| Schema serializer | Register concrete target event and every proof/content variant in ComplianceCoreJsonContext and attributed persistence preflight coverage. |
| Schema nativeResponseRule | FirstResponse is non-null only on this logical command first native accepting fact; it references FirstResponseDescriptor. OperationResult is non-null on the declared terminal business fact and references MutationResultContent. Both use verified content and static concrete result serializers; neither is reconstructed from current state. |

#### Event fields

| Field | Type | Required |
| --- | --- | --- |
| TenantId | Uuid | true |
| RelationshipId | Uuid | true |
| SourceApplicationId | Uuid | true |
| TargetApplicationId | Uuid | true |
| Revision | long | true |
| SourceApplicationRevision | long | true |
| TargetApplicationRevision | long | true |
| ImpactDigest | string | true |
| ApproverMemberId | Uuid | true |
| ApproverDisplay | string | true |
| ApprovedAt | DateTimeOffset | true |
| Operation | OperationProvenance | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### ApplicationRelationshipRemoved

| Canonical event detail | Value |
| --- | --- |
| Target event name | ApplicationRelationshipRemoved |
| Selected source event name | ApplicationRelationshipRemoved |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:ApplicationRelationshipRemoved |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ApplicationRelationshipRemoved |
| Adopted wire | `{"discriminator":"bdgrz.application.relationship.removed","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Applications/Events/ApplicationRelationshipRemoved.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | An active relationship from a declared application to a target application was removed by a named actor, ending it at the next relationship revision. |
| Selected source meaning | An active relationship from a declared application to a target application was removed by a named actor, ending it at the next relationship revision. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/Applications/Events/ApplicationRelationshipRemoved.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | ApplicationRelationshipRemoved |
| Schema discriminator | bdgrz.application.relationship.removed |
| Schema discriminatorVersion | 1 |
| Schema owner | ApplicationRelationship |
| Schema existingContract | src/Compliance.Common/Features/Applications/ApplicationRelationshipRemoved.cs |
| Schema status | selected target schema; implementation pending |
| Schema reason | source command accepted result |
| Schema changes | None listed |
| Schema reducerRule | Validate owner/generation and local predecessor; apply only this owner delta or immutable content reference. Never hydrate a foreign aggregate or mutate based on a projection. |
| Schema serializer | Register concrete target event and every proof/content variant in ComplianceCoreJsonContext and attributed persistence preflight coverage. |
| Schema nativeResponseRule | FirstResponse is non-null only on this logical command first native accepting fact; it references FirstResponseDescriptor. OperationResult is non-null on the declared terminal business fact and references MutationResultContent. Both use verified content and static concrete result serializers; neither is reconstructed from current state. |

#### Event fields

| Field | Type | Required |
| --- | --- | --- |
| TenantId | Uuid | true |
| ApplicationId | Uuid | true |
| RelationshipId | Uuid | true |
| RelationshipRevision | long | true |
| TargetApplicationId | Uuid | true |
| RelationshipType | string | true |
| ActorMemberId | Uuid | true |
| ActorDisplay | string | true |
| ChangedAt | DateTimeOffset | true |
| Operation | OperationProvenance | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [X005](../reactors/remove-relationships-after-application-retirement-reactor.md#x005) | emitted semantic fact | DeclaredApplication | RemoveApplicationRelationship | ApplicationRelationship |




[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]
