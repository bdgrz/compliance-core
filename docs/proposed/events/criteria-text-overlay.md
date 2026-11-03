# CriteriaTextOverlay events

Canonical events: **1**. Complete canonical event and available schema records: [owner event JSON](../data/events/criteria-text-overlay.json).

| Target event | Selected source name | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- | --- |
| CriteriaTextOverlayEntryRevised | CriteriaTextOverlayEntryRevised | business | bdgrz.criteria-overlay.entry-revised | 2 | 11 | One revision of the licensed text overlay of one criterion. The logical id of Operation is the command id, and its actor and acceptance time are the attribution of the revision; the complete supplied content is verified immutable content, so the license and text stay exact however large the text is. |

## Event details

### CriteriaTextOverlayEntryRevised

| Canonical event detail | Value |
| --- | --- |
| Target event name | CriteriaTextOverlayEntryRevised |
| Selected source event name | CriteriaTextOverlayEntryRevised |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:CriteriaTextOverlayEntryRevised |
| Owner selection | one exact canonical authority owner |
| Target concrete type | CriteriaTextOverlayEntryRevised |
| Adopted wire | `{"discriminator":"bdgrz.criteria-overlay.entry-revised","version":2}` |
| Wire qualification | Adopted wire is the occupied discriminator declared in src/Compliance.Common/Features/Criteria/Events/CriteriaTextOverlayEntryRevised.cs at 60029b8f (2026-10-29 reconciliation). The earlier selected wire is retained as supersededSelectedWire for provenance only. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | `{"discriminator":"bdgrz.criteria-overlay.entry-revised","version":1}` |
| Target meaning | One revision of the licensed text overlay of one criterion. The logical id of Operation is the command id, and its actor and acceptance time are the attribution of the revision; the complete supplied content is verified immutable content, so the license and text stay exact however large the text is. |
| Selected source meaning | One revision of the licensed text overlay of one criterion. The logical id of Operation is the command id, and its actor and acceptance time are the attribution of the revision; the complete supplied content is verified immutable content, so the license and text stay exact however large the text is. |
| Meaning source | record summary in src/Compliance.Common/Features/Criteria/Events/CriteriaTextOverlayEntryRevised.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | CriteriaTextOverlayEntryRevised |
| Schema discriminator | bdgrz.criteria-overlay.entry-revised |
| Schema discriminatorVersion | 1 |
| Schema owner | CriteriaTextOverlay |
| Schema existingContract | src/Compliance.Common/Features/Criteria/CriteriaTextOverlayEntryRevised.cs |
| Schema status | selected target schema; implementation pending |
| Schema reason | source command accepted result |
| Schema changes | Externalize structured CriteriaTextOverlayContent including nested collections; a former item cap cannot become an event-size rejection for valid larger content. |
| Schema reducerRule | Validate owner/generation and local predecessor; apply only this owner delta or immutable content reference. Never hydrate a foreign aggregate or mutate based on a projection. |
| Schema serializer | Register concrete target event and every proof/content variant in ComplianceCoreJsonContext and attributed persistence preflight coverage. |
| Schema nativeResponseRule | FirstResponse is non-null only on this logical command first native accepting fact; it references FirstResponseDescriptor. OperationResult is non-null on the declared terminal business fact and references MutationResultContent. Both use verified content and static concrete result serializers; neither is reconstructed from current state. |

#### Event fields

| Field | Type | Required |
| --- | --- | --- |
| TenantId | Uuid | true |
| EditionId | Uuid | true |
| Identifier | string | true |
| RequestId | Uuid | true |
| Revision | long | true |
| ContentContent | ImmutableContentRef | true |
| Actor | ActorReference | true |
| RecordedAt | DateTimeOffset | true |
| Operation | OperationProvenance | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

#### Nested content schemas

#### Content schema: CriteriaTextOverlayEntryRevised.ContentContent.v1

| Property | Value |
| --- | --- |
| field | ContentContent |
| schema | CriteriaTextOverlayEntryRevised.ContentContent.v1 |
| contentType | CriteriaTextOverlayContent |
| rule | Complete original typed structured value, including nested collections, written/verified before this event. No cumulative parent view or mutable latest pointer. |


[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]


## Wire/schema version discrepancies

- **CriteriaTextOverlayEntryRevised** — [Placeholder: reconcile schema version 1 with adopted version 2].

[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]
