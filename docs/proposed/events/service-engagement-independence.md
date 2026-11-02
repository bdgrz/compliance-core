# ServiceEngagementIndependence events

Canonical events: **16**. Complete canonical event and available schema records: [owner event JSON](../data/events/service-engagement-independence.json).

| Event | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- |
| AssignmentIndependenceReevaluated | business | bdgrz.independence.assignment-reevaluated | 1 | 6 | A lifecycle change prompted a reassessment of an engagement staff assignment's independence, retained as a receipt that blocks production acceptance and never refreshes the earlier acceptance. |
| DirectoryIndependenceReevaluated | business | bdgrz.independence.directory-reevaluated | 1 | 6 | A denial-only receipt that one accepted staff member's directory record changed after the acceptance. The complete original DirectoryIndependenceReevaluationView is immutable content; the receipt never reactivates an old acceptance. Revision is the engagement revision after it. |
| EngagementPersonalDecisionAdmitted | business | bdgrz.service-engagement-independence.engagement-personal-decision-admitted | 1 | 8 | The root native acceptance of a personal engagement decision on the engagement owner. It freezes the complete decision intent at Revision, the engagement content revision at admission, and pins that content until the decision is materialized or canceled. It does not advance the engagement revision. |
| EngagementPersonalDecisionAuthorized | reservation | bdgrz.service-engagement-independence.engagement-personal-decision-authorized | 1 | 10 | The irreversible authorization of a personal engagement decision, appended only after every required source use was acquired. DecisionAt is the server-frozen decision instant that every temporal validity interval is judged at; the child decision materializes from this fact and nothing earlier. |
| EngagementPersonalDecisionCanceled | business | bdgrz.service-engagement-independence.engagement-personal-decision-canceled | 1 | 8 | The decision was canceled before authorization: it fences authorization and releases the engagement content pin. An authorized decision recovers forward and is never canceled. |
| EngagementPersonalDecisionMaterialized | business | bdgrz.service-engagement-independence.engagement-personal-decision-materialized | 1 | 8 | The materialized personal decision was recorded on the engagement: the content pin is released and the engagement's current personal decision pointer advances unless a later decision already holds it. It does not advance the engagement revision. |
| EngagementPracticeCommitmentsAuthorized | reservation | bdgrz.service-engagement-independence.engagement-practice-commitments-authorized | 1 | 10 | The irreversible authorization of an acceptance or positive staff assignment, appended only when every temporary reservation and every source use is held by its exact grant. DecisionAt is the server-frozen instant every temporal validity interval is judged at. Only after this fact are permanent practice walls committed; before it, every temporary grant can still be fenced and settled. |
| ServiceEngagementAcceptanceRecorded | business | bdgrz.service-engagement.acceptance-recorded | 1 | 8 | The engagement was accepted: every temporary grant was authorized irreversibly and every permanent practice wall was committed before this fact. AcceptanceContent is the complete original ServiceEngagementAcceptanceView, and Revision is the engagement revision after it. OperationResult is the terminal result of the acceptance operation. |
| ServiceEngagementAcceptanceRejected | business | bdgrz.service-engagement-independence.service-engagement-acceptance-rejected | 1 | 8 | A requested acceptance was rejected before the irreversible authorization. The rejection fences the pending acceptance against late delivery and releases the engagement content pin; no permanent practice commitment was ever made for it. |
| ServiceEngagementAcceptanceRequested | business | bdgrz.service-engagement-independence.service-engagement-acceptance-requested | 1 | 8 | The root native acceptance of an engagement acceptance on the engagement owner. It freezes the complete engagement independence intent at Revision, the engagement content revision at admission, and pins that content until the acceptance is recorded or rejected. It does not advance the engagement revision and grants no authority by itself. |
| ServiceEngagementAssignmentRevoked | business | bdgrz.service-engagement.assignment-revoked | 1 | 12 | A staff member's assignment on an accepted service engagement was revoked with a stated reason, or the whole engagement was closed when no staff member is named. |
| ServiceEngagementMutationRecorded | business | bdgrz.service-engagement.changed | 1 | 8 | One accepted lifecycle change of a service engagement draft: create, amend, propose, withdraw or close. Intent is the stable command kind and Change is the immutable ServiceEngagementChange; the complete resulting view is the operation result. No cumulative parent snapshot is carried. Revision is the engagement revision after the change. |
| ServiceEngagementStaffAssigned | business | bdgrz.access-control.service-engagement.staff-assigned | 1 | 15 | One actual staff assignment was recorded on an accepted engagement after the partner authority, the service epoch and the staff and canonical-user practice walls were authorized and committed. The directory snapshots the assignment relied on are retained as immutable content. Revision is the engagement revision after it, and OperationResult is the terminal result: the complete revised acceptance view. |
| ServiceEngagementStaffAssignmentRejected | business | bdgrz.service-engagement-independence.service-engagement-staff-assignment-rejected | 1 | 8 | A requested positive staff assignment was rejected before the irreversible authorization. The rejection fences the pending assignment against late delivery; the engagement and every earlier permanent wall are retained. |
| ServiceEngagementStaffAssignmentRequested | business | bdgrz.service-engagement-independence.service-engagement-staff-assignment-requested | 1 | 8 | The root native acceptance of one positive actual staff assignment on the accepted engagement owner. It freezes the complete engagement independence intent at Revision and pins that content until the assignment is recorded or rejected. It does not advance the engagement revision and grants no authority by itself. |
| ServiceIndependenceReevaluated | business | bdgrz.independence.service-reevaluated | 1 | 6 | A change to a client's services prompted a reassessment of an accepted engagement's independence, retained as a record that blocks production acceptance and never refreshes the earlier acceptance. |

## Event details

### AssignmentIndependenceReevaluated

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:AssignmentIndependenceReevaluated |
| Owner selection | one exact canonical authority owner |
| Target concrete type | AssignmentIndependenceReevaluated |
| Adopted wire | `{"discriminator":"bdgrz.independence.assignment-reevaluated","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/AccessControl/Events/AssignmentIndependenceReevaluated.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | A lifecycle change prompted a reassessment of an engagement staff assignment's independence, retained as a receipt that blocks production acceptance and never refreshes the earlier acceptance. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/AccessControl/Events/AssignmentIndependenceReevaluated.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | AssignmentIndependenceReevaluated |
| Schema discriminator | bdgrz.independence.assignment-reevaluated |
| Schema discriminatorVersion | 1 |
| Schema owner | ServiceEngagementIndependence |
| Schema existingContract | src/Compliance.Common/Features/AccessControl/AssignmentIndependenceReevaluated.cs |
| Schema status | selected target schema; implementation pending |
| Schema reason | existing native business fact, rehomed with its invariant |
| Schema changes | Externalize structured AssignmentIndependenceReevaluationView including nested collections; a former item cap cannot become an event-size rejection for valid larger content., Remove former shared-ledger ExpectedSequence. Operation provenance retains authored concurrency; Revision names this new owner logical result revision. |
| Schema reducerRule | Validate owner/generation and local predecessor; apply only this owner delta or immutable content reference. Never hydrate a foreign aggregate or mutate based on a projection. |
| Schema serializer | Register concrete target event and every proof/content variant in ComplianceCoreJsonContext and attributed persistence preflight coverage. |
| Schema nativeResponseRule | FirstResponse is non-null only on this logical command first native accepting fact; it references FirstResponseDescriptor. OperationResult is non-null on the declared terminal business fact and references MutationResultContent. Both use verified content and static concrete result serializers; neither is reconstructed from current state. |

#### Event fields

| Field | Type | Required |
| --- | --- | --- |
| TenantId | Uuid | true |
| ReevaluationContent | ImmutableContentRef | true |
| Operation | OperationProvenance | true |
| Revision | ulong | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

#### Nested content schemas

#### Content schema: AssignmentIndependenceReevaluated.ReevaluationContent.v1

| Property | Value |
| --- | --- |
| field | ReevaluationContent |
| schema | AssignmentIndependenceReevaluated.ReevaluationContent.v1 |
| contentType | AssignmentIndependenceReevaluationView |
| rule | Complete original typed structured value, including nested collections, written/verified before this event. No cumulative parent view or mutable latest pointer. |


[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### DirectoryIndependenceReevaluated

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:DirectoryIndependenceReevaluated |
| Owner selection | one exact canonical authority owner |
| Target concrete type | DirectoryIndependenceReevaluated |
| Adopted wire | `{"discriminator":"bdgrz.independence.directory-reevaluated","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/AccessControl/Events/DirectoryIndependenceReevaluated.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | A denial-only receipt that one accepted staff member's directory record changed after the acceptance. The complete original DirectoryIndependenceReevaluationView is immutable content; the receipt never reactivates an old acceptance. Revision is the engagement revision after it. |
| Meaning source | record summary in src/Compliance.Common/Features/AccessControl/Events/DirectoryIndependenceReevaluated.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | DirectoryIndependenceReevaluated |
| Schema discriminator | bdgrz.independence.directory-reevaluated |
| Schema discriminatorVersion | 1 |
| Schema owner | ServiceEngagementIndependence |
| Schema existingContract | src/Compliance.Common/Features/AccessControl/DirectoryIndependenceReevaluated.cs |
| Schema status | selected target schema; implementation pending |
| Schema reason | source command accepted result |
| Schema changes | Externalize structured DirectoryIndependenceReevaluationView including nested collections; a former item cap cannot become an event-size rejection for valid larger content., Remove former shared-ledger ExpectedSequence. Operation provenance retains authored concurrency; Revision names this new owner logical result revision. |
| Schema reducerRule | Validate owner/generation and local predecessor; apply only this owner delta or immutable content reference. Never hydrate a foreign aggregate or mutate based on a projection. |
| Schema serializer | Register concrete target event and every proof/content variant in ComplianceCoreJsonContext and attributed persistence preflight coverage. |
| Schema nativeResponseRule | FirstResponse is non-null only on this logical command first native accepting fact; it references FirstResponseDescriptor. OperationResult is non-null on the declared terminal business fact and references MutationResultContent. Both use verified content and static concrete result serializers; neither is reconstructed from current state. |

#### Event fields

| Field | Type | Required |
| --- | --- | --- |
| TenantId | Uuid | true |
| ReevaluationContent | ImmutableContentRef | true |
| Operation | OperationProvenance | true |
| Revision | ulong | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

#### Nested content schemas

#### Content schema: DirectoryIndependenceReevaluated.ReevaluationContent.v1

| Property | Value |
| --- | --- |
| field | ReevaluationContent |
| schema | DirectoryIndependenceReevaluated.ReevaluationContent.v1 |
| contentType | DirectoryIndependenceReevaluationView |
| rule | Complete original typed structured value, including nested collections, written/verified before this event. No cumulative parent view or mutable latest pointer. |


[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [S017](../reactors/firm-staff-status-reevaluation-reactor.md#s017) | emitted semantic fact | FirmStaffRecord | RecordDirectoryIndependenceReevaluation | ServiceEngagementIndependence |
| [S018](../reactors/accepted-staff-directory-reconciliation-reactor.md#s018) | emitted semantic fact | ServiceEngagementIndependence | RecordDirectoryIndependenceReevaluation | ServiceEngagementIndependence |

### EngagementPersonalDecisionAdmitted

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:EngagementPersonalDecisionAdmitted |
| Owner selection | one exact canonical authority owner |
| Target concrete type | EngagementPersonalDecisionAdmitted |
| Adopted wire | `{"discriminator":"bdgrz.service-engagement-independence.engagement-personal-decision-admitted","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/AccessControl/Events/EngagementPersonalDecisionAdmitted.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | The root native acceptance of a personal engagement decision on the engagement owner. It freezes the complete decision intent at Revision, the engagement content revision at admission, and pins that content until the decision is materialized or canceled. It does not advance the engagement revision. |
| Meaning source | record summary in src/Compliance.Common/Features/AccessControl/Events/EngagementPersonalDecisionAdmitted.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | EngagementPersonalDecisionAdmitted |
| Schema discriminator | bdgrz.service-engagement-independence.engagement-personal-decision-admitted |
| Schema discriminatorVersion | 1 |
| Schema owner | ServiceEngagementIndependence |
| Schema existingContract | null (recorded) |
| Schema status | selected target schema; implementation pending |
| Schema reason | source command accepted result |
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

#### Content schema: engagement-personal-decision-intent.v1

| Property | Value |
| --- | --- |
| field | Intent |
| schema | engagement-personal-decision-intent.v1 |
| recordType | EngagementPersonalDecisionIntent |
| rule | DecisionId is AcknowledgementId or EvaluationId. Admission pins exact engagement content and epoch. Management content contains complete service record IDs and statement; partner content contains exact rule/digest/history/rationale and authoritative personal duty requirements. Authorize only after all required uses; cancel only before authorization. Child materialization consumes authorization, records frozen original actor and decision instant, and cannot overwrite a later personal decision pointer. |

##### Fields

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| DecisionId | Uuid | true |
| Kind | EngagementPersonalDecisionKind | true |
| EngagementId | Uuid | true |
| ExpectedEngagementRevision | ulong | true |
| ServiceEpoch | ulong | true |
| CompleteServiceManifest | ImmutableSelectionManifest | true |
| AuthoredContent | EngagementPersonalDecisionContent | true |
| RequiredSourceUses | ImmutableSelectionManifest | true |

##### Variant: EngagementPersonalDecisionIntent

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| DecisionId | Uuid | true |
| Kind | EngagementPersonalDecisionKind | true |
| EngagementId | Uuid | true |
| ExpectedEngagementRevision | ulong | true |
| ServiceEpoch | ulong | true |
| CompleteServiceManifest | ImmutableSelectionManifest | true |
| AuthoredContent | EngagementPersonalDecisionContent | true |
| RequiredSourceUses | ImmutableSelectionManifest | true |


#### Resolved intent schema

EngagementPersonalDecisionIntent

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [T089](../reactors/engagement-acceptance-reactor.md#t089) | physical root, logical prerequisite | ServiceEngagementIndependence | AuthorizeFirmStaffRecordEngagementUse | FirmStaffRecord |
| [T090](../reactors/engagement-acceptance-reactor.md#t090) | physical root | ServiceEngagementIndependence | SettleFirmStaffRecordEngagementUse | FirmStaffRecord |
| [T091](../reactors/engagement-acceptance-reactor.md#t091) | physical root, logical prerequisite | ServiceEngagementIndependence | AuthorizeFirmProfessionalDutyDesignationEngagementUse | FirmProfessionalDutyDesignation |
| [T092](../reactors/engagement-acceptance-reactor.md#t092) | physical root | ServiceEngagementIndependence | SettleFirmProfessionalDutyDesignationEngagementUse | FirmProfessionalDutyDesignation |
| [T093](../reactors/engagement-acceptance-reactor.md#t093) | physical root, logical prerequisite | ServiceEngagementIndependence | AuthorizeIndependenceRuleCatalogEngagementUse | IndependenceRuleCatalog |
| [T094](../reactors/engagement-acceptance-reactor.md#t094) | physical root | ServiceEngagementIndependence | SettleIndependenceRuleCatalogEngagementUse | IndependenceRuleCatalog |
| [T095](../reactors/engagement-acceptance-reactor.md#t095) | physical root, logical prerequisite | ServiceEngagementIndependence | AuthorizeIndependenceRuleRatificationCatalogEngagementUse | IndependenceRuleRatificationCatalog |
| [T096](../reactors/engagement-acceptance-reactor.md#t096) | physical root | ServiceEngagementIndependence | SettleIndependenceRuleRatificationCatalogEngagementUse | IndependenceRuleRatificationCatalog |
| [T107](../reactors/engagement-personal-decision-reactor.md#t107) | physical root, logical prerequisite | ServiceEngagementIndependence | AuthorizeClientServicePersonalDecisionUse | ClientServiceIndependence |
| [T108](../reactors/engagement-personal-decision-reactor.md#t108) | physical root | ServiceEngagementIndependence | MaterializeEngagementManagementAcknowledgement | EngagementManagementAcknowledgement |
| [T109](../reactors/engagement-personal-decision-reactor.md#t109) | physical root | ServiceEngagementIndependence | MaterializePartnerIndependenceDecision | PartnerIndependenceDecision |
| [T110](../reactors/engagement-personal-decision-reactor.md#t110) | physical root | EngagementManagementAcknowledgement\|PartnerIndependenceDecision | SettleEngagementPersonalDecision | ServiceEngagementIndependence |
| [T111](../reactors/engagement-personal-decision-reactor.md#t111) | physical root | ServiceEngagementIndependence | SettleClientServicePersonalDecisionUse | ClientServiceIndependence |
| [T112](../reactors/engagement-personal-decision-reactor.md#t112) | physical root, logical prerequisite | ServiceEngagementIndependence | AuthorizeEngagementPersonalDecision | ServiceEngagementIndependence |
| [A004](../reactors/unassigned-architecture-rows.md#a004) | physical root, logical prerequisite | ServiceEngagementIndependence | CancelEngagementPersonalDecision | ServiceEngagementIndependence |

### EngagementPersonalDecisionAuthorized

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:EngagementPersonalDecisionAuthorized |
| Owner selection | one exact canonical authority owner |
| Target concrete type | EngagementPersonalDecisionAuthorized |
| Adopted wire | `{"discriminator":"bdgrz.service-engagement-independence.engagement-personal-decision-authorized","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/AccessControl/Events/EngagementPersonalDecisionAuthorized.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | The irreversible authorization of a personal engagement decision, appended only after every required source use was acquired. DecisionAt is the server-frozen decision instant that every temporal validity interval is judged at; the child decision materializes from this fact and nothing earlier. |
| Meaning source | record summary in src/Compliance.Common/Features/AccessControl/Events/EngagementPersonalDecisionAuthorized.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | EngagementPersonalDecisionAuthorized |
| Schema discriminator | bdgrz.service-engagement-independence.engagement-personal-decision-authorized |
| Schema discriminatorVersion | 1 |
| Schema owner | ServiceEngagementIndependence |
| Schema existingContract | null (recorded) |
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
| DecisionAt | DateTimeOffset | true |
| AuthorizationProofs | ImmutableContentRef | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

#### Nested content schemas

#### Content schema: engagement-personal-decision-intent.v1

| Property | Value |
| --- | --- |
| field | Intent |
| schema | engagement-personal-decision-intent.v1 |
| recordType | EngagementPersonalDecisionIntent |
| rule | DecisionId is AcknowledgementId or EvaluationId. Admission pins exact engagement content and epoch. Management content contains complete service record IDs and statement; partner content contains exact rule/digest/history/rationale and authoritative personal duty requirements. Authorize only after all required uses; cancel only before authorization. Child materialization consumes authorization, records frozen original actor and decision instant, and cannot overwrite a later personal decision pointer. |

##### Fields

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| DecisionId | Uuid | true |
| Kind | EngagementPersonalDecisionKind | true |
| EngagementId | Uuid | true |
| ExpectedEngagementRevision | ulong | true |
| ServiceEpoch | ulong | true |
| CompleteServiceManifest | ImmutableSelectionManifest | true |
| AuthoredContent | EngagementPersonalDecisionContent | true |
| RequiredSourceUses | ImmutableSelectionManifest | true |

##### Variant: EngagementPersonalDecisionIntent

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| DecisionId | Uuid | true |
| Kind | EngagementPersonalDecisionKind | true |
| EngagementId | Uuid | true |
| ExpectedEngagementRevision | ulong | true |
| ServiceEpoch | ulong | true |
| CompleteServiceManifest | ImmutableSelectionManifest | true |
| AuthoredContent | EngagementPersonalDecisionContent | true |
| RequiredSourceUses | ImmutableSelectionManifest | true |


#### Resolved intent schema

EngagementPersonalDecisionIntent

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [T090](../reactors/engagement-acceptance-reactor.md#t090) | logical prerequisite | ServiceEngagementIndependence | SettleFirmStaffRecordEngagementUse | FirmStaffRecord |
| [T092](../reactors/engagement-acceptance-reactor.md#t092) | logical prerequisite | ServiceEngagementIndependence | SettleFirmProfessionalDutyDesignationEngagementUse | FirmProfessionalDutyDesignation |
| [T094](../reactors/engagement-acceptance-reactor.md#t094) | logical prerequisite | ServiceEngagementIndependence | SettleIndependenceRuleCatalogEngagementUse | IndependenceRuleCatalog |
| [T096](../reactors/engagement-acceptance-reactor.md#t096) | logical prerequisite | ServiceEngagementIndependence | SettleIndependenceRuleRatificationCatalogEngagementUse | IndependenceRuleRatificationCatalog |
| [T108](../reactors/engagement-personal-decision-reactor.md#t108) | logical prerequisite | ServiceEngagementIndependence | MaterializeEngagementManagementAcknowledgement | EngagementManagementAcknowledgement |
| [T109](../reactors/engagement-personal-decision-reactor.md#t109) | logical prerequisite | ServiceEngagementIndependence | MaterializePartnerIndependenceDecision | PartnerIndependenceDecision |
| [T111](../reactors/engagement-personal-decision-reactor.md#t111) | logical prerequisite | ServiceEngagementIndependence | SettleClientServicePersonalDecisionUse | ClientServiceIndependence |
| [T112](../reactors/engagement-personal-decision-reactor.md#t112) | emitted semantic fact | ServiceEngagementIndependence | AuthorizeEngagementPersonalDecision | ServiceEngagementIndependence |

### EngagementPersonalDecisionCanceled

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:EngagementPersonalDecisionCanceled |
| Owner selection | one exact canonical authority owner |
| Target concrete type | EngagementPersonalDecisionCanceled |
| Adopted wire | `{"discriminator":"bdgrz.service-engagement-independence.engagement-personal-decision-canceled","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/AccessControl/Events/EngagementPersonalDecisionCanceled.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | The decision was canceled before authorization: it fences authorization and releases the engagement content pin. An authorized decision recovers forward and is never canceled. |
| Meaning source | record summary in src/Compliance.Common/Features/AccessControl/Events/EngagementPersonalDecisionCanceled.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | EngagementPersonalDecisionCanceled |
| Schema discriminator | bdgrz.service-engagement-independence.engagement-personal-decision-canceled |
| Schema discriminatorVersion | 1 |
| Schema owner | ServiceEngagementIndependence |
| Schema existingContract | null (recorded) |
| Schema status | selected target schema; implementation pending |
| Schema reason | source admission contract |
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

#### Content schema: engagement-personal-decision-intent.v1

| Property | Value |
| --- | --- |
| field | Intent |
| schema | engagement-personal-decision-intent.v1 |
| recordType | EngagementPersonalDecisionIntent |
| rule | DecisionId is AcknowledgementId or EvaluationId. Admission pins exact engagement content and epoch. Management content contains complete service record IDs and statement; partner content contains exact rule/digest/history/rationale and authoritative personal duty requirements. Authorize only after all required uses; cancel only before authorization. Child materialization consumes authorization, records frozen original actor and decision instant, and cannot overwrite a later personal decision pointer. |

##### Fields

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| DecisionId | Uuid | true |
| Kind | EngagementPersonalDecisionKind | true |
| EngagementId | Uuid | true |
| ExpectedEngagementRevision | ulong | true |
| ServiceEpoch | ulong | true |
| CompleteServiceManifest | ImmutableSelectionManifest | true |
| AuthoredContent | EngagementPersonalDecisionContent | true |
| RequiredSourceUses | ImmutableSelectionManifest | true |

##### Variant: EngagementPersonalDecisionIntent

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| DecisionId | Uuid | true |
| Kind | EngagementPersonalDecisionKind | true |
| EngagementId | Uuid | true |
| ExpectedEngagementRevision | ulong | true |
| ServiceEpoch | ulong | true |
| CompleteServiceManifest | ImmutableSelectionManifest | true |
| AuthoredContent | EngagementPersonalDecisionContent | true |
| RequiredSourceUses | ImmutableSelectionManifest | true |


#### Resolved intent schema

EngagementPersonalDecisionIntent

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [T090](../reactors/engagement-acceptance-reactor.md#t090) | logical prerequisite | ServiceEngagementIndependence | SettleFirmStaffRecordEngagementUse | FirmStaffRecord |
| [T092](../reactors/engagement-acceptance-reactor.md#t092) | logical prerequisite | ServiceEngagementIndependence | SettleFirmProfessionalDutyDesignationEngagementUse | FirmProfessionalDutyDesignation |
| [T094](../reactors/engagement-acceptance-reactor.md#t094) | logical prerequisite | ServiceEngagementIndependence | SettleIndependenceRuleCatalogEngagementUse | IndependenceRuleCatalog |
| [T096](../reactors/engagement-acceptance-reactor.md#t096) | logical prerequisite | ServiceEngagementIndependence | SettleIndependenceRuleRatificationCatalogEngagementUse | IndependenceRuleRatificationCatalog |
| [T111](../reactors/engagement-personal-decision-reactor.md#t111) | logical prerequisite | ServiceEngagementIndependence | SettleClientServicePersonalDecisionUse | ClientServiceIndependence |
| [A004](../reactors/unassigned-architecture-rows.md#a004) | emitted semantic fact | ServiceEngagementIndependence | CancelEngagementPersonalDecision | ServiceEngagementIndependence |

### EngagementPersonalDecisionMaterialized

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:EngagementPersonalDecisionMaterialized |
| Owner selection | one exact canonical authority owner |
| Target concrete type | EngagementPersonalDecisionMaterialized |
| Adopted wire | `{"discriminator":"bdgrz.service-engagement-independence.engagement-personal-decision-materialized","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/AccessControl/Events/EngagementPersonalDecisionMaterialized.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | The materialized personal decision was recorded on the engagement: the content pin is released and the engagement's current personal decision pointer advances unless a later decision already holds it. It does not advance the engagement revision. |
| Meaning source | record summary in src/Compliance.Common/Features/AccessControl/Events/EngagementPersonalDecisionMaterialized.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | EngagementPersonalDecisionMaterialized |
| Schema discriminator | bdgrz.service-engagement-independence.engagement-personal-decision-materialized |
| Schema discriminatorVersion | 1 |
| Schema owner | ServiceEngagementIndependence |
| Schema existingContract | null (recorded) |
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

#### Content schema: engagement-personal-decision-intent.v1

| Property | Value |
| --- | --- |
| field | Intent |
| schema | engagement-personal-decision-intent.v1 |
| recordType | EngagementPersonalDecisionIntent |
| rule | DecisionId is AcknowledgementId or EvaluationId. Admission pins exact engagement content and epoch. Management content contains complete service record IDs and statement; partner content contains exact rule/digest/history/rationale and authoritative personal duty requirements. Authorize only after all required uses; cancel only before authorization. Child materialization consumes authorization, records frozen original actor and decision instant, and cannot overwrite a later personal decision pointer. |

##### Fields

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| DecisionId | Uuid | true |
| Kind | EngagementPersonalDecisionKind | true |
| EngagementId | Uuid | true |
| ExpectedEngagementRevision | ulong | true |
| ServiceEpoch | ulong | true |
| CompleteServiceManifest | ImmutableSelectionManifest | true |
| AuthoredContent | EngagementPersonalDecisionContent | true |
| RequiredSourceUses | ImmutableSelectionManifest | true |

##### Variant: EngagementPersonalDecisionIntent

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| DecisionId | Uuid | true |
| Kind | EngagementPersonalDecisionKind | true |
| EngagementId | Uuid | true |
| ExpectedEngagementRevision | ulong | true |
| ServiceEpoch | ulong | true |
| CompleteServiceManifest | ImmutableSelectionManifest | true |
| AuthoredContent | EngagementPersonalDecisionContent | true |
| RequiredSourceUses | ImmutableSelectionManifest | true |


#### Resolved intent schema

EngagementPersonalDecisionIntent

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [T110](../reactors/engagement-personal-decision-reactor.md#t110) | emitted semantic fact | EngagementManagementAcknowledgement\|PartnerIndependenceDecision | SettleEngagementPersonalDecision | ServiceEngagementIndependence |

### EngagementPracticeCommitmentsAuthorized

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:EngagementPracticeCommitmentsAuthorized |
| Owner selection | one exact canonical authority owner |
| Target concrete type | EngagementPracticeCommitmentsAuthorized |
| Adopted wire | `{"discriminator":"bdgrz.service-engagement-independence.engagement-practice-commitments-authorized","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/AccessControl/Events/EngagementPracticeCommitmentsAuthorized.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | The irreversible authorization of an acceptance or positive staff assignment, appended only when every temporary reservation and every source use is held by its exact grant. DecisionAt is the server-frozen instant every temporal validity interval is judged at. Only after this fact are permanent practice walls committed; before it, every temporary grant can still be fenced and settled. |
| Meaning source | record summary in src/Compliance.Common/Features/AccessControl/Events/EngagementPracticeCommitmentsAuthorized.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | EngagementPracticeCommitmentsAuthorized |
| Schema discriminator | bdgrz.service-engagement-independence.engagement-practice-commitments-authorized |
| Schema discriminatorVersion | 1 |
| Schema owner | ServiceEngagementIndependence |
| Schema existingContract | null (recorded) |
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
| DecisionAt | DateTimeOffset | true |
| AuthorizationProofs | ImmutableContentRef | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

#### Nested content schemas

#### Content schema: engagement-independence-intent.v1

| Property | Value |
| --- | --- |
| field | Intent |
| schema | engagement-independence-intent.v1 |
| recordType | EngagementIndependenceIntent |
| rule | Capture exact accepted engagement epoch plus complete staff/canonical user set. Freeze directory, partner duty, rule and ratification requirements. All source uses and temporary subject reservations precede irreversible EngagementPracticeCommitmentsAuthorized. No new permanent wall before all reservations/proofs exist. All temporary grants derive from original immutable engagement admission and are acquired by the complete canonical key manifest, with no epoch-before-practice dependency. Only irreversible authorization validates the complete current exact grant set and permits permanent commitment. |

##### Fields

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| EngagementId | Uuid | true |
| EngagementRevision | ulong | true |
| OperationKind | EngagementIndependenceOperationKind | true |
| ServiceEpoch | ulong | true |
| CompleteServiceHistory | ImmutableSelectionManifest | true |
| RuleProof | SourceFactLocator | true |
| RatificationProof | SourceFactLocator | true |
| PartnerDecision | SourceFactLocator | true |
| ManagementAcknowledgement | SourceFactLocator | true |
| Subjects | ImmutableSelectionManifest | true |
| SourceUses | ImmutableSelectionManifest | true |
| EffectiveFrom | DateOnly? | true |

##### Variant: EngagementIndependenceIntent

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| EngagementId | Uuid | true |
| EngagementRevision | ulong | true |
| OperationKind | EngagementIndependenceOperationKind | true |
| ServiceEpoch | ulong | true |
| CompleteServiceHistory | ImmutableSelectionManifest | true |
| RuleProof | SourceFactLocator | true |
| RatificationProof | SourceFactLocator | true |
| PartnerDecision | SourceFactLocator | true |
| ManagementAcknowledgement | SourceFactLocator | true |
| Subjects | ImmutableSelectionManifest | true |
| SourceUses | ImmutableSelectionManifest | true |
| EffectiveFrom | DateOnly? | true |


#### Resolved intent schema

EngagementIndependenceIntent

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [T005](../reactors/engagement-acceptance-reactor.md#t005) | logical prerequisite | ServiceEngagementIndependence | CommitPracticeIndependence | PracticeIndependenceCommitment |
| [T006](../reactors/engagement-acceptance-reactor.md#t006) | logical prerequisite | ServiceEngagementIndependence | CompleteServiceEngagementAcceptance | ServiceEngagementIndependence |
| [T082](../reactors/engagement-acceptance-reactor.md#t082) | logical prerequisite | ServiceEngagementIndependence | CompleteServiceEngagementStaffAssignment | ServiceEngagementIndependence |
| [T084](../reactors/engagement-acceptance-reactor.md#t084) | emitted semantic fact | ServiceEngagementIndependence | AuthorizeEngagementPracticeCommitments | ServiceEngagementIndependence |
| [T090](../reactors/engagement-acceptance-reactor.md#t090) | logical prerequisite | ServiceEngagementIndependence | SettleFirmStaffRecordEngagementUse | FirmStaffRecord |
| [T092](../reactors/engagement-acceptance-reactor.md#t092) | logical prerequisite | ServiceEngagementIndependence | SettleFirmProfessionalDutyDesignationEngagementUse | FirmProfessionalDutyDesignation |
| [T094](../reactors/engagement-acceptance-reactor.md#t094) | logical prerequisite | ServiceEngagementIndependence | SettleIndependenceRuleCatalogEngagementUse | IndependenceRuleCatalog |
| [T096](../reactors/engagement-acceptance-reactor.md#t096) | logical prerequisite | ServiceEngagementIndependence | SettleIndependenceRuleRatificationCatalogEngagementUse | IndependenceRuleRatificationCatalog |

### ServiceEngagementAcceptanceRecorded

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:ServiceEngagementAcceptanceRecorded |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ServiceEngagementAcceptanceRecorded |
| Adopted wire | `{"discriminator":"bdgrz.service-engagement.acceptance-recorded","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/AccessControl/Events/ServiceEngagementAcceptanceRecorded.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | The engagement was accepted: every temporary grant was authorized irreversibly and every permanent practice wall was committed before this fact. AcceptanceContent is the complete original ServiceEngagementAcceptanceView, and Revision is the engagement revision after it. OperationResult is the terminal result of the acceptance operation. |
| Meaning source | record summary in src/Compliance.Common/Features/AccessControl/Events/ServiceEngagementAcceptanceRecorded.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | ServiceEngagementAcceptanceRecorded |
| Schema discriminator | bdgrz.service-engagement.acceptance-recorded |
| Schema discriminatorVersion | 1 |
| Schema owner | ServiceEngagementIndependence |
| Schema existingContract | src/Compliance.Common/Features/AccessControl/ServiceEngagementAcceptanceRecorded.cs |
| Schema status | selected target schema; implementation pending |
| Schema reason | typed reactor receiver accepted business fact |
| Schema changes | Externalize structured ServiceEngagementAcceptanceView including nested collections; a former item cap cannot become an event-size rejection for valid larger content., Remove former shared-ledger ExpectedSequence. Operation provenance retains authored concurrency; Revision names this new owner logical result revision. |
| Schema reducerRule | Validate owner/generation and local predecessor; apply only this owner delta or immutable content reference. Never hydrate a foreign aggregate or mutate based on a projection. |
| Schema serializer | Register concrete target event and every proof/content variant in ComplianceCoreJsonContext and attributed persistence preflight coverage. |
| Schema nativeResponseRule | FirstResponse is non-null only on this logical command first native accepting fact; it references FirstResponseDescriptor. OperationResult is non-null on the declared terminal business fact and references MutationResultContent. Both use verified content and static concrete result serializers; neither is reconstructed from current state. |

#### Event fields

| Field | Type | Required |
| --- | --- | --- |
| TenantId | Uuid | true |
| OperationId | Uuid | true |
| Intent | string | true |
| AcceptanceContent | ImmutableContentRef | true |
| Operation | OperationProvenance | true |
| Revision | ulong | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

#### Nested content schemas

#### Content schema: ServiceEngagementAcceptanceRecorded.AcceptanceContent.v1

| Property | Value |
| --- | --- |
| field | AcceptanceContent |
| schema | ServiceEngagementAcceptanceRecorded.AcceptanceContent.v1 |
| contentType | ServiceEngagementAcceptanceView |
| rule | Complete original typed structured value, including nested collections, written/verified before this event. No cumulative parent view or mutable latest pointer. |

#### Content schema: engagement-independence-intent.v1

| Property | Value |
| --- | --- |
| field | Intent |
| schema | engagement-independence-intent.v1 |
| recordType | EngagementIndependenceIntent |
| rule | Capture exact accepted engagement epoch plus complete staff/canonical user set. Freeze directory, partner duty, rule and ratification requirements. All source uses and temporary subject reservations precede irreversible EngagementPracticeCommitmentsAuthorized. No new permanent wall before all reservations/proofs exist. All temporary grants derive from original immutable engagement admission and are acquired by the complete canonical key manifest, with no epoch-before-practice dependency. Only irreversible authorization validates the complete current exact grant set and permits permanent commitment. |

##### Fields

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| EngagementId | Uuid | true |
| EngagementRevision | ulong | true |
| OperationKind | EngagementIndependenceOperationKind | true |
| ServiceEpoch | ulong | true |
| CompleteServiceHistory | ImmutableSelectionManifest | true |
| RuleProof | SourceFactLocator | true |
| RatificationProof | SourceFactLocator | true |
| PartnerDecision | SourceFactLocator | true |
| ManagementAcknowledgement | SourceFactLocator | true |
| Subjects | ImmutableSelectionManifest | true |
| SourceUses | ImmutableSelectionManifest | true |
| EffectiveFrom | DateOnly? | true |

##### Variant: EngagementIndependenceIntent

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| EngagementId | Uuid | true |
| EngagementRevision | ulong | true |
| OperationKind | EngagementIndependenceOperationKind | true |
| ServiceEpoch | ulong | true |
| CompleteServiceHistory | ImmutableSelectionManifest | true |
| RuleProof | SourceFactLocator | true |
| RatificationProof | SourceFactLocator | true |
| PartnerDecision | SourceFactLocator | true |
| ManagementAcknowledgement | SourceFactLocator | true |
| Subjects | ImmutableSelectionManifest | true |
| SourceUses | ImmutableSelectionManifest | true |
| EffectiveFrom | DateOnly? | true |


#### Resolved intent schema

EngagementIndependenceIntent

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [T006](../reactors/engagement-acceptance-reactor.md#t006) | emitted semantic fact | ServiceEngagementIndependence | CompleteServiceEngagementAcceptance | ServiceEngagementIndependence |
| [T007](../reactors/engagement-acceptance-reactor.md#t007) | logical prerequisite | ServiceEngagementIndependence | ReleaseEngagementServiceEpoch | ClientServiceIndependence |
| [T085](../reactors/engagement-acceptance-reactor.md#t085) | logical prerequisite | ServiceEngagementIndependence | SettlePracticeIndependenceReservation | PracticeIndependenceCommitment |
| [S018](../reactors/accepted-staff-directory-reconciliation-reactor.md#s018) | physical root, logical prerequisite | ServiceEngagementIndependence | RecordDirectoryIndependenceReevaluation | ServiceEngagementIndependence |

### ServiceEngagementAcceptanceRejected

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:ServiceEngagementAcceptanceRejected |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ServiceEngagementAcceptanceRejected |
| Adopted wire | `{"discriminator":"bdgrz.service-engagement-independence.service-engagement-acceptance-rejected","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/AccessControl/Events/ServiceEngagementAcceptanceRejected.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | A requested acceptance was rejected before the irreversible authorization. The rejection fences the pending acceptance against late delivery and releases the engagement content pin; no permanent practice commitment was ever made for it. |
| Meaning source | record summary in src/Compliance.Common/Features/AccessControl/Events/ServiceEngagementAcceptanceRejected.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | ServiceEngagementAcceptanceRejected |
| Schema discriminator | bdgrz.service-engagement-independence.service-engagement-acceptance-rejected |
| Schema discriminatorVersion | 1 |
| Schema owner | ServiceEngagementIndependence |
| Schema existingContract | null (recorded) |
| Schema status | selected target schema; implementation pending |
| Schema reason | source admission contract |
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

#### Content schema: engagement-independence-intent.v1

| Property | Value |
| --- | --- |
| field | Intent |
| schema | engagement-independence-intent.v1 |
| recordType | EngagementIndependenceIntent |
| rule | Capture exact accepted engagement epoch plus complete staff/canonical user set. Freeze directory, partner duty, rule and ratification requirements. All source uses and temporary subject reservations precede irreversible EngagementPracticeCommitmentsAuthorized. No new permanent wall before all reservations/proofs exist. All temporary grants derive from original immutable engagement admission and are acquired by the complete canonical key manifest, with no epoch-before-practice dependency. Only irreversible authorization validates the complete current exact grant set and permits permanent commitment. |

##### Fields

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| EngagementId | Uuid | true |
| EngagementRevision | ulong | true |
| OperationKind | EngagementIndependenceOperationKind | true |
| ServiceEpoch | ulong | true |
| CompleteServiceHistory | ImmutableSelectionManifest | true |
| RuleProof | SourceFactLocator | true |
| RatificationProof | SourceFactLocator | true |
| PartnerDecision | SourceFactLocator | true |
| ManagementAcknowledgement | SourceFactLocator | true |
| Subjects | ImmutableSelectionManifest | true |
| SourceUses | ImmutableSelectionManifest | true |
| EffectiveFrom | DateOnly? | true |

##### Variant: EngagementIndependenceIntent

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| EngagementId | Uuid | true |
| EngagementRevision | ulong | true |
| OperationKind | EngagementIndependenceOperationKind | true |
| ServiceEpoch | ulong | true |
| CompleteServiceHistory | ImmutableSelectionManifest | true |
| RuleProof | SourceFactLocator | true |
| RatificationProof | SourceFactLocator | true |
| PartnerDecision | SourceFactLocator | true |
| ManagementAcknowledgement | SourceFactLocator | true |
| Subjects | ImmutableSelectionManifest | true |
| SourceUses | ImmutableSelectionManifest | true |
| EffectiveFrom | DateOnly? | true |


#### Resolved intent schema

EngagementIndependenceIntent

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [T007](../reactors/engagement-acceptance-reactor.md#t007) | logical prerequisite | ServiceEngagementIndependence | ReleaseEngagementServiceEpoch | ClientServiceIndependence |
| [T085](../reactors/engagement-acceptance-reactor.md#t085) | logical prerequisite | ServiceEngagementIndependence | SettlePracticeIndependenceReservation | PracticeIndependenceCommitment |
| [T090](../reactors/engagement-acceptance-reactor.md#t090) | logical prerequisite | ServiceEngagementIndependence | SettleFirmStaffRecordEngagementUse | FirmStaffRecord |
| [T092](../reactors/engagement-acceptance-reactor.md#t092) | logical prerequisite | ServiceEngagementIndependence | SettleFirmProfessionalDutyDesignationEngagementUse | FirmProfessionalDutyDesignation |
| [T094](../reactors/engagement-acceptance-reactor.md#t094) | logical prerequisite | ServiceEngagementIndependence | SettleIndependenceRuleCatalogEngagementUse | IndependenceRuleCatalog |
| [T096](../reactors/engagement-acceptance-reactor.md#t096) | logical prerequisite | ServiceEngagementIndependence | SettleIndependenceRuleRatificationCatalogEngagementUse | IndependenceRuleRatificationCatalog |
| [A002](../reactors/unassigned-architecture-rows.md#a002) | emitted semantic fact | ServiceEngagementIndependence | RejectServiceEngagementAcceptance | ServiceEngagementIndependence |

### ServiceEngagementAcceptanceRequested

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:ServiceEngagementAcceptanceRequested |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ServiceEngagementAcceptanceRequested |
| Adopted wire | `{"discriminator":"bdgrz.service-engagement-independence.service-engagement-acceptance-requested","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/AccessControl/Events/ServiceEngagementAcceptanceRequested.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | The root native acceptance of an engagement acceptance on the engagement owner. It freezes the complete engagement independence intent at Revision, the engagement content revision at admission, and pins that content until the acceptance is recorded or rejected. It does not advance the engagement revision and grants no authority by itself. |
| Meaning source | record summary in src/Compliance.Common/Features/AccessControl/Events/ServiceEngagementAcceptanceRequested.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | ServiceEngagementAcceptanceRequested |
| Schema discriminator | bdgrz.service-engagement-independence.service-engagement-acceptance-requested |
| Schema discriminatorVersion | 1 |
| Schema owner | ServiceEngagementIndependence |
| Schema existingContract | null (recorded) |
| Schema status | selected target schema; implementation pending |
| Schema reason | source command accepted result |
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

#### Content schema: engagement-independence-intent.v1

| Property | Value |
| --- | --- |
| field | Intent |
| schema | engagement-independence-intent.v1 |
| recordType | EngagementIndependenceIntent |
| rule | Capture exact accepted engagement epoch plus complete staff/canonical user set. Freeze directory, partner duty, rule and ratification requirements. All source uses and temporary subject reservations precede irreversible EngagementPracticeCommitmentsAuthorized. No new permanent wall before all reservations/proofs exist. All temporary grants derive from original immutable engagement admission and are acquired by the complete canonical key manifest, with no epoch-before-practice dependency. Only irreversible authorization validates the complete current exact grant set and permits permanent commitment. |

##### Fields

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| EngagementId | Uuid | true |
| EngagementRevision | ulong | true |
| OperationKind | EngagementIndependenceOperationKind | true |
| ServiceEpoch | ulong | true |
| CompleteServiceHistory | ImmutableSelectionManifest | true |
| RuleProof | SourceFactLocator | true |
| RatificationProof | SourceFactLocator | true |
| PartnerDecision | SourceFactLocator | true |
| ManagementAcknowledgement | SourceFactLocator | true |
| Subjects | ImmutableSelectionManifest | true |
| SourceUses | ImmutableSelectionManifest | true |
| EffectiveFrom | DateOnly? | true |

##### Variant: EngagementIndependenceIntent

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| EngagementId | Uuid | true |
| EngagementRevision | ulong | true |
| OperationKind | EngagementIndependenceOperationKind | true |
| ServiceEpoch | ulong | true |
| CompleteServiceHistory | ImmutableSelectionManifest | true |
| RuleProof | SourceFactLocator | true |
| RatificationProof | SourceFactLocator | true |
| PartnerDecision | SourceFactLocator | true |
| ManagementAcknowledgement | SourceFactLocator | true |
| Subjects | ImmutableSelectionManifest | true |
| SourceUses | ImmutableSelectionManifest | true |
| EffectiveFrom | DateOnly? | true |


#### Resolved intent schema

EngagementIndependenceIntent

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [T004](../reactors/engagement-acceptance-reactor.md#t004) | physical root, logical prerequisite | ServiceEngagementIndependence | AcquireEngagementServiceEpoch | ClientServiceIndependence |
| [T005](../reactors/engagement-acceptance-reactor.md#t005) | physical root | ServiceEngagementIndependence | CommitPracticeIndependence | PracticeIndependenceCommitment |
| [T006](../reactors/engagement-acceptance-reactor.md#t006) | physical root | ServiceEngagementIndependence | CompleteServiceEngagementAcceptance | ServiceEngagementIndependence |
| [T007](../reactors/engagement-acceptance-reactor.md#t007) | physical root | ServiceEngagementIndependence | ReleaseEngagementServiceEpoch | ClientServiceIndependence |
| [T082](../reactors/engagement-acceptance-reactor.md#t082) | physical root | ServiceEngagementIndependence | CompleteServiceEngagementStaffAssignment | ServiceEngagementIndependence |
| [T083](../reactors/engagement-acceptance-reactor.md#t083) | physical root, logical prerequisite | ServiceEngagementIndependence | ReservePracticeIndependence | PracticeIndependenceCommitment |
| [T084](../reactors/engagement-acceptance-reactor.md#t084) | physical root, logical prerequisite | ServiceEngagementIndependence | AuthorizeEngagementPracticeCommitments | ServiceEngagementIndependence |
| [T085](../reactors/engagement-acceptance-reactor.md#t085) | physical root | ServiceEngagementIndependence | SettlePracticeIndependenceReservation | PracticeIndependenceCommitment |
| [T089](../reactors/engagement-acceptance-reactor.md#t089) | physical root, logical prerequisite | ServiceEngagementIndependence | AuthorizeFirmStaffRecordEngagementUse | FirmStaffRecord |
| [T090](../reactors/engagement-acceptance-reactor.md#t090) | physical root | ServiceEngagementIndependence | SettleFirmStaffRecordEngagementUse | FirmStaffRecord |
| [T091](../reactors/engagement-acceptance-reactor.md#t091) | physical root, logical prerequisite | ServiceEngagementIndependence | AuthorizeFirmProfessionalDutyDesignationEngagementUse | FirmProfessionalDutyDesignation |
| [T092](../reactors/engagement-acceptance-reactor.md#t092) | physical root | ServiceEngagementIndependence | SettleFirmProfessionalDutyDesignationEngagementUse | FirmProfessionalDutyDesignation |
| [T093](../reactors/engagement-acceptance-reactor.md#t093) | physical root, logical prerequisite | ServiceEngagementIndependence | AuthorizeIndependenceRuleCatalogEngagementUse | IndependenceRuleCatalog |
| [T094](../reactors/engagement-acceptance-reactor.md#t094) | physical root | ServiceEngagementIndependence | SettleIndependenceRuleCatalogEngagementUse | IndependenceRuleCatalog |
| [T095](../reactors/engagement-acceptance-reactor.md#t095) | physical root, logical prerequisite | ServiceEngagementIndependence | AuthorizeIndependenceRuleRatificationCatalogEngagementUse | IndependenceRuleRatificationCatalog |
| [T096](../reactors/engagement-acceptance-reactor.md#t096) | physical root | ServiceEngagementIndependence | SettleIndependenceRuleRatificationCatalogEngagementUse | IndependenceRuleRatificationCatalog |
| [A002](../reactors/unassigned-architecture-rows.md#a002) | physical root, logical prerequisite | ServiceEngagementIndependence | RejectServiceEngagementAcceptance | ServiceEngagementIndependence |
| [A003](../reactors/unassigned-architecture-rows.md#a003) | physical root, logical prerequisite | ServiceEngagementIndependence | RejectServiceEngagementStaffAssignment | ServiceEngagementIndependence |

### ServiceEngagementAssignmentRevoked

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:ServiceEngagementAssignmentRevoked |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ServiceEngagementAssignmentRevoked |
| Adopted wire | `{"discriminator":"bdgrz.service-engagement.assignment-revoked","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/AccessControl/Events/ServiceEngagementAssignmentRevoked.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | A staff member's assignment on an accepted service engagement was revoked with a stated reason, or the whole engagement was closed when no staff member is named. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/AccessControl/Events/ServiceEngagementAssignmentRevoked.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | ServiceEngagementAssignmentRevoked |
| Schema discriminator | bdgrz.service-engagement.assignment-revoked |
| Schema discriminatorVersion | 1 |
| Schema owner | ServiceEngagementIndependence |
| Schema existingContract | src/Compliance.Common/Features/AccessControl/ServiceEngagementAssignmentRevoked.cs |
| Schema status | selected target schema; implementation pending |
| Schema reason | source command accepted result |
| Schema changes | Remove former shared-ledger ExpectedSequence. Operation provenance retains authored concurrency; Revision names this new owner logical result revision. |
| Schema reducerRule | Validate owner/generation and local predecessor; apply only this owner delta or immutable content reference. Never hydrate a foreign aggregate or mutate based on a projection. |
| Schema serializer | Register concrete target event and every proof/content variant in ComplianceCoreJsonContext and attributed persistence preflight coverage. |
| Schema nativeResponseRule | FirstResponse is non-null only on this logical command first native accepting fact; it references FirstResponseDescriptor. OperationResult is non-null on the declared terminal business fact and references MutationResultContent. Both use verified content and static concrete result serializers; neither is reconstructed from current state. |

#### Event fields

| Field | Type | Required |
| --- | --- | --- |
| TenantId | Uuid | true |
| OperationId | Uuid | true |
| EngagementId | Uuid | true |
| StaffMemberId | Uuid? | true |
| Reason | string | true |
| Intent | string | true |
| Actor | ActorReference | true |
| RecordedAt | DateTimeOffset | true |
| Operation | OperationProvenance | true |
| Revision | ulong | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### ServiceEngagementMutationRecorded

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:ServiceEngagementMutationRecorded |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ServiceEngagementMutationRecorded |
| Adopted wire | `{"discriminator":"bdgrz.service-engagement.changed","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/AccessControl/Events/ServiceEngagementMutationRecorded.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | One accepted lifecycle change of a service engagement draft: create, amend, propose, withdraw or close. Intent is the stable command kind and Change is the immutable ServiceEngagementChange; the complete resulting view is the operation result. No cumulative parent snapshot is carried. Revision is the engagement revision after the change. |
| Meaning source | record summary in src/Compliance.Common/Features/AccessControl/Events/ServiceEngagementMutationRecorded.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | ServiceEngagementMutationRecorded |
| Schema discriminator | bdgrz.service-engagement.changed |
| Schema discriminatorVersion | 1 |
| Schema owner | ServiceEngagementIndependence |
| Schema existingContract | src/Compliance.Common/Features/AccessControl/ServiceEngagementMutationRecorded.cs |
| Schema status | selected target schema; implementation pending |
| Schema reason | source command accepted result |
| Schema changes | Replace full parent snapshot with a verified immutable event-specific change. Reducer applies the accepted delta; it never re-executes a command or consults foreign state., Remove former shared-ledger ExpectedSequence. Operation provenance retains authored concurrency; Revision names this new owner logical result revision. |
| Schema reducerRule | Validate owner/generation and local predecessor; apply only this owner delta or immutable content reference. Never hydrate a foreign aggregate or mutate based on a projection. |
| Schema serializer | Register concrete target event and every proof/content variant in ComplianceCoreJsonContext and attributed persistence preflight coverage. |
| Schema nativeResponseRule | FirstResponse is non-null only on this logical command first native accepting fact; it references FirstResponseDescriptor. OperationResult is non-null on the declared terminal business fact and references MutationResultContent. Both use verified content and static concrete result serializers; neither is reconstructed from current state. |

#### Event fields

| Field | Type | Required |
| --- | --- | --- |
| TenantId | Uuid | true |
| OperationId | Uuid | true |
| Intent | string | true |
| Change | ImmutableContentRef | true |
| Operation | OperationProvenance | true |
| Revision | ulong | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

#### Nested content schemas

#### Content schema: ServiceEngagementMutationRecorded.change.v1

| Property | Value |
| --- | --- |
| field | Change |
| schema | ServiceEngagementMutationRecorded.change.v1 |
| sourceCommandVariants | AmendServiceEngagement, CloseServiceEngagement, CreateServiceEngagement, ProposeServiceEngagementStaff, WithdrawServiceEngagementStaffProposal |
| rule | Tagged by stable source command kind. Contains normalized accepted business field values and exact source proof references from that command, plus server-accepted result identities; excludes unrelated children, history, bearer material and process state. The result data required for replay is immutable and complete. |


[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### ServiceEngagementStaffAssigned

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:ServiceEngagementStaffAssigned |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ServiceEngagementStaffAssigned |
| Adopted wire | `{"discriminator":"bdgrz.access-control.service-engagement.staff-assigned","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/AccessControl/Events/ServiceEngagementStaffAssigned.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | One actual staff assignment was recorded on an accepted engagement after the partner authority, the service epoch and the staff and canonical-user practice walls were authorized and committed. The directory snapshots the assignment relied on are retained as immutable content. Revision is the engagement revision after it, and OperationResult is the terminal result: the complete revised acceptance view. |
| Meaning source | record summary in src/Compliance.Common/Features/AccessControl/Events/ServiceEngagementStaffAssigned.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | ServiceEngagementStaffAssigned |
| Schema discriminator | bdgrz.access-control.service-engagement.staff-assigned |
| Schema discriminatorVersion | 1 |
| Schema owner | ServiceEngagementIndependence |
| Schema existingContract | src/Compliance.Common/Features/AccessControl/ServiceEngagementStaffAssigned.cs |
| Schema status | selected target schema; implementation pending |
| Schema reason | typed reactor receiver accepted business fact |
| Schema changes | Externalize structured FirmStaffMemberView including nested collections; a former item cap cannot become an event-size rejection for valid larger content., Externalize structured FirmStaffMemberView including nested collections; a former item cap cannot become an event-size rejection for valid larger content., Externalize structured FirmProfessionalDutyDesignationView including nested collections; a former item cap cannot become an event-size rejection for valid larger content., Remove former shared-ledger ExpectedSequence. Operation provenance retains authored concurrency; Revision names this new owner logical result revision. |
| Schema reducerRule | Validate owner/generation and local predecessor; apply only this owner delta or immutable content reference. Never hydrate a foreign aggregate or mutate based on a projection. |
| Schema serializer | Register concrete target event and every proof/content variant in ComplianceCoreJsonContext and attributed persistence preflight coverage. |
| Schema nativeResponseRule | FirstResponse is non-null only on this logical command first native accepting fact; it references FirstResponseDescriptor. OperationResult is non-null on the declared terminal business fact and references MutationResultContent. Both use verified content and static concrete result serializers; neither is reconstructed from current state. |

#### Event fields

| Field | Type | Required |
| --- | --- | --- |
| TenantId | Uuid | true |
| OperationId | Uuid | true |
| EngagementId | Uuid | true |
| AssignedStaffContent | ImmutableContentRef | true |
| EffectiveFrom | DateOnly | true |
| PartnerStaffContent | ImmutableContentRef | true |
| PartnerDesignationContent | ImmutableContentRef | true |
| PartnerDutyContinuityStartRevision | long | true |
| Intent | string | true |
| Actor | ActorReference | true |
| RecordedAt | DateTimeOffset | true |
| Operation | OperationProvenance | true |
| Revision | ulong | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

#### Nested content schemas

#### Content schema: ServiceEngagementStaffAssigned.AssignedStaffContent.v1

| Property | Value |
| --- | --- |
| field | AssignedStaffContent |
| schema | ServiceEngagementStaffAssigned.AssignedStaffContent.v1 |
| contentType | FirmStaffMemberView |
| rule | Complete original typed structured value, including nested collections, written/verified before this event. No cumulative parent view or mutable latest pointer. |

#### Content schema: ServiceEngagementStaffAssigned.PartnerStaffContent.v1

| Property | Value |
| --- | --- |
| field | PartnerStaffContent |
| schema | ServiceEngagementStaffAssigned.PartnerStaffContent.v1 |
| contentType | FirmStaffMemberView |
| rule | Complete original typed structured value, including nested collections, written/verified before this event. No cumulative parent view or mutable latest pointer. |

#### Content schema: ServiceEngagementStaffAssigned.PartnerDesignationContent.v1

| Property | Value |
| --- | --- |
| field | PartnerDesignationContent |
| schema | ServiceEngagementStaffAssigned.PartnerDesignationContent.v1 |
| contentType | FirmProfessionalDutyDesignationView |
| rule | Complete original typed structured value, including nested collections, written/verified before this event. No cumulative parent view or mutable latest pointer. |

#### Content schema: engagement-independence-intent.v1

| Property | Value |
| --- | --- |
| field | Intent |
| schema | engagement-independence-intent.v1 |
| recordType | EngagementIndependenceIntent |
| rule | Capture exact accepted engagement epoch plus complete staff/canonical user set. Freeze directory, partner duty, rule and ratification requirements. All source uses and temporary subject reservations precede irreversible EngagementPracticeCommitmentsAuthorized. No new permanent wall before all reservations/proofs exist. All temporary grants derive from original immutable engagement admission and are acquired by the complete canonical key manifest, with no epoch-before-practice dependency. Only irreversible authorization validates the complete current exact grant set and permits permanent commitment. |

##### Fields

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| EngagementId | Uuid | true |
| EngagementRevision | ulong | true |
| OperationKind | EngagementIndependenceOperationKind | true |
| ServiceEpoch | ulong | true |
| CompleteServiceHistory | ImmutableSelectionManifest | true |
| RuleProof | SourceFactLocator | true |
| RatificationProof | SourceFactLocator | true |
| PartnerDecision | SourceFactLocator | true |
| ManagementAcknowledgement | SourceFactLocator | true |
| Subjects | ImmutableSelectionManifest | true |
| SourceUses | ImmutableSelectionManifest | true |
| EffectiveFrom | DateOnly? | true |

##### Variant: EngagementIndependenceIntent

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| EngagementId | Uuid | true |
| EngagementRevision | ulong | true |
| OperationKind | EngagementIndependenceOperationKind | true |
| ServiceEpoch | ulong | true |
| CompleteServiceHistory | ImmutableSelectionManifest | true |
| RuleProof | SourceFactLocator | true |
| RatificationProof | SourceFactLocator | true |
| PartnerDecision | SourceFactLocator | true |
| ManagementAcknowledgement | SourceFactLocator | true |
| Subjects | ImmutableSelectionManifest | true |
| SourceUses | ImmutableSelectionManifest | true |
| EffectiveFrom | DateOnly? | true |


#### Resolved intent schema

EngagementIndependenceIntent

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [T007](../reactors/engagement-acceptance-reactor.md#t007) | logical prerequisite | ServiceEngagementIndependence | ReleaseEngagementServiceEpoch | ClientServiceIndependence |
| [T082](../reactors/engagement-acceptance-reactor.md#t082) | emitted semantic fact | ServiceEngagementIndependence | CompleteServiceEngagementStaffAssignment | ServiceEngagementIndependence |
| [T085](../reactors/engagement-acceptance-reactor.md#t085) | logical prerequisite | ServiceEngagementIndependence | SettlePracticeIndependenceReservation | PracticeIndependenceCommitment |
| [S018](../reactors/accepted-staff-directory-reconciliation-reactor.md#s018) | physical root, logical prerequisite | ServiceEngagementIndependence | RecordDirectoryIndependenceReevaluation | ServiceEngagementIndependence |

### ServiceEngagementStaffAssignmentRejected

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:ServiceEngagementStaffAssignmentRejected |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ServiceEngagementStaffAssignmentRejected |
| Adopted wire | `{"discriminator":"bdgrz.service-engagement-independence.service-engagement-staff-assignment-rejected","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/AccessControl/Events/ServiceEngagementStaffAssignmentRejected.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | A requested positive staff assignment was rejected before the irreversible authorization. The rejection fences the pending assignment against late delivery; the engagement and every earlier permanent wall are retained. |
| Meaning source | record summary in src/Compliance.Common/Features/AccessControl/Events/ServiceEngagementStaffAssignmentRejected.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | ServiceEngagementStaffAssignmentRejected |
| Schema discriminator | bdgrz.service-engagement-independence.service-engagement-staff-assignment-rejected |
| Schema discriminatorVersion | 1 |
| Schema owner | ServiceEngagementIndependence |
| Schema existingContract | null (recorded) |
| Schema status | selected target schema; implementation pending |
| Schema reason | source admission contract |
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

#### Content schema: engagement-independence-intent.v1

| Property | Value |
| --- | --- |
| field | Intent |
| schema | engagement-independence-intent.v1 |
| recordType | EngagementIndependenceIntent |
| rule | Capture exact accepted engagement epoch plus complete staff/canonical user set. Freeze directory, partner duty, rule and ratification requirements. All source uses and temporary subject reservations precede irreversible EngagementPracticeCommitmentsAuthorized. No new permanent wall before all reservations/proofs exist. All temporary grants derive from original immutable engagement admission and are acquired by the complete canonical key manifest, with no epoch-before-practice dependency. Only irreversible authorization validates the complete current exact grant set and permits permanent commitment. |

##### Fields

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| EngagementId | Uuid | true |
| EngagementRevision | ulong | true |
| OperationKind | EngagementIndependenceOperationKind | true |
| ServiceEpoch | ulong | true |
| CompleteServiceHistory | ImmutableSelectionManifest | true |
| RuleProof | SourceFactLocator | true |
| RatificationProof | SourceFactLocator | true |
| PartnerDecision | SourceFactLocator | true |
| ManagementAcknowledgement | SourceFactLocator | true |
| Subjects | ImmutableSelectionManifest | true |
| SourceUses | ImmutableSelectionManifest | true |
| EffectiveFrom | DateOnly? | true |

##### Variant: EngagementIndependenceIntent

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| EngagementId | Uuid | true |
| EngagementRevision | ulong | true |
| OperationKind | EngagementIndependenceOperationKind | true |
| ServiceEpoch | ulong | true |
| CompleteServiceHistory | ImmutableSelectionManifest | true |
| RuleProof | SourceFactLocator | true |
| RatificationProof | SourceFactLocator | true |
| PartnerDecision | SourceFactLocator | true |
| ManagementAcknowledgement | SourceFactLocator | true |
| Subjects | ImmutableSelectionManifest | true |
| SourceUses | ImmutableSelectionManifest | true |
| EffectiveFrom | DateOnly? | true |


#### Resolved intent schema

EngagementIndependenceIntent

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [T007](../reactors/engagement-acceptance-reactor.md#t007) | logical prerequisite | ServiceEngagementIndependence | ReleaseEngagementServiceEpoch | ClientServiceIndependence |
| [T085](../reactors/engagement-acceptance-reactor.md#t085) | logical prerequisite | ServiceEngagementIndependence | SettlePracticeIndependenceReservation | PracticeIndependenceCommitment |
| [T090](../reactors/engagement-acceptance-reactor.md#t090) | logical prerequisite | ServiceEngagementIndependence | SettleFirmStaffRecordEngagementUse | FirmStaffRecord |
| [T092](../reactors/engagement-acceptance-reactor.md#t092) | logical prerequisite | ServiceEngagementIndependence | SettleFirmProfessionalDutyDesignationEngagementUse | FirmProfessionalDutyDesignation |
| [T094](../reactors/engagement-acceptance-reactor.md#t094) | logical prerequisite | ServiceEngagementIndependence | SettleIndependenceRuleCatalogEngagementUse | IndependenceRuleCatalog |
| [T096](../reactors/engagement-acceptance-reactor.md#t096) | logical prerequisite | ServiceEngagementIndependence | SettleIndependenceRuleRatificationCatalogEngagementUse | IndependenceRuleRatificationCatalog |
| [A003](../reactors/unassigned-architecture-rows.md#a003) | emitted semantic fact | ServiceEngagementIndependence | RejectServiceEngagementStaffAssignment | ServiceEngagementIndependence |

### ServiceEngagementStaffAssignmentRequested

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:ServiceEngagementStaffAssignmentRequested |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ServiceEngagementStaffAssignmentRequested |
| Adopted wire | `{"discriminator":"bdgrz.service-engagement-independence.service-engagement-staff-assignment-requested","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/AccessControl/Events/ServiceEngagementStaffAssignmentRequested.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | The root native acceptance of one positive actual staff assignment on the accepted engagement owner. It freezes the complete engagement independence intent at Revision and pins that content until the assignment is recorded or rejected. It does not advance the engagement revision and grants no authority by itself. |
| Meaning source | record summary in src/Compliance.Common/Features/AccessControl/Events/ServiceEngagementStaffAssignmentRequested.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | ServiceEngagementStaffAssignmentRequested |
| Schema discriminator | bdgrz.service-engagement-independence.service-engagement-staff-assignment-requested |
| Schema discriminatorVersion | 1 |
| Schema owner | ServiceEngagementIndependence |
| Schema existingContract | null (recorded) |
| Schema status | selected target schema; implementation pending |
| Schema reason | source command accepted result |
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

#### Content schema: engagement-independence-intent.v1

| Property | Value |
| --- | --- |
| field | Intent |
| schema | engagement-independence-intent.v1 |
| recordType | EngagementIndependenceIntent |
| rule | Capture exact accepted engagement epoch plus complete staff/canonical user set. Freeze directory, partner duty, rule and ratification requirements. All source uses and temporary subject reservations precede irreversible EngagementPracticeCommitmentsAuthorized. No new permanent wall before all reservations/proofs exist. All temporary grants derive from original immutable engagement admission and are acquired by the complete canonical key manifest, with no epoch-before-practice dependency. Only irreversible authorization validates the complete current exact grant set and permits permanent commitment. |

##### Fields

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| EngagementId | Uuid | true |
| EngagementRevision | ulong | true |
| OperationKind | EngagementIndependenceOperationKind | true |
| ServiceEpoch | ulong | true |
| CompleteServiceHistory | ImmutableSelectionManifest | true |
| RuleProof | SourceFactLocator | true |
| RatificationProof | SourceFactLocator | true |
| PartnerDecision | SourceFactLocator | true |
| ManagementAcknowledgement | SourceFactLocator | true |
| Subjects | ImmutableSelectionManifest | true |
| SourceUses | ImmutableSelectionManifest | true |
| EffectiveFrom | DateOnly? | true |

##### Variant: EngagementIndependenceIntent

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| EngagementId | Uuid | true |
| EngagementRevision | ulong | true |
| OperationKind | EngagementIndependenceOperationKind | true |
| ServiceEpoch | ulong | true |
| CompleteServiceHistory | ImmutableSelectionManifest | true |
| RuleProof | SourceFactLocator | true |
| RatificationProof | SourceFactLocator | true |
| PartnerDecision | SourceFactLocator | true |
| ManagementAcknowledgement | SourceFactLocator | true |
| Subjects | ImmutableSelectionManifest | true |
| SourceUses | ImmutableSelectionManifest | true |
| EffectiveFrom | DateOnly? | true |


#### Resolved intent schema

EngagementIndependenceIntent

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [T004](../reactors/engagement-acceptance-reactor.md#t004) | physical root, logical prerequisite | ServiceEngagementIndependence | AcquireEngagementServiceEpoch | ClientServiceIndependence |
| [T005](../reactors/engagement-acceptance-reactor.md#t005) | physical root | ServiceEngagementIndependence | CommitPracticeIndependence | PracticeIndependenceCommitment |
| [T006](../reactors/engagement-acceptance-reactor.md#t006) | physical root | ServiceEngagementIndependence | CompleteServiceEngagementAcceptance | ServiceEngagementIndependence |
| [T007](../reactors/engagement-acceptance-reactor.md#t007) | physical root | ServiceEngagementIndependence | ReleaseEngagementServiceEpoch | ClientServiceIndependence |
| [T082](../reactors/engagement-acceptance-reactor.md#t082) | physical root | ServiceEngagementIndependence | CompleteServiceEngagementStaffAssignment | ServiceEngagementIndependence |
| [T083](../reactors/engagement-acceptance-reactor.md#t083) | physical root, logical prerequisite | ServiceEngagementIndependence | ReservePracticeIndependence | PracticeIndependenceCommitment |
| [T084](../reactors/engagement-acceptance-reactor.md#t084) | physical root, logical prerequisite | ServiceEngagementIndependence | AuthorizeEngagementPracticeCommitments | ServiceEngagementIndependence |
| [T085](../reactors/engagement-acceptance-reactor.md#t085) | physical root | ServiceEngagementIndependence | SettlePracticeIndependenceReservation | PracticeIndependenceCommitment |
| [T089](../reactors/engagement-acceptance-reactor.md#t089) | physical root, logical prerequisite | ServiceEngagementIndependence | AuthorizeFirmStaffRecordEngagementUse | FirmStaffRecord |
| [T090](../reactors/engagement-acceptance-reactor.md#t090) | physical root | ServiceEngagementIndependence | SettleFirmStaffRecordEngagementUse | FirmStaffRecord |
| [T091](../reactors/engagement-acceptance-reactor.md#t091) | physical root, logical prerequisite | ServiceEngagementIndependence | AuthorizeFirmProfessionalDutyDesignationEngagementUse | FirmProfessionalDutyDesignation |
| [T092](../reactors/engagement-acceptance-reactor.md#t092) | physical root | ServiceEngagementIndependence | SettleFirmProfessionalDutyDesignationEngagementUse | FirmProfessionalDutyDesignation |
| [T093](../reactors/engagement-acceptance-reactor.md#t093) | physical root, logical prerequisite | ServiceEngagementIndependence | AuthorizeIndependenceRuleCatalogEngagementUse | IndependenceRuleCatalog |
| [T094](../reactors/engagement-acceptance-reactor.md#t094) | physical root | ServiceEngagementIndependence | SettleIndependenceRuleCatalogEngagementUse | IndependenceRuleCatalog |
| [T095](../reactors/engagement-acceptance-reactor.md#t095) | physical root, logical prerequisite | ServiceEngagementIndependence | AuthorizeIndependenceRuleRatificationCatalogEngagementUse | IndependenceRuleRatificationCatalog |
| [T096](../reactors/engagement-acceptance-reactor.md#t096) | physical root | ServiceEngagementIndependence | SettleIndependenceRuleRatificationCatalogEngagementUse | IndependenceRuleRatificationCatalog |
| [A002](../reactors/unassigned-architecture-rows.md#a002) | physical root, logical prerequisite | ServiceEngagementIndependence | RejectServiceEngagementAcceptance | ServiceEngagementIndependence |
| [A003](../reactors/unassigned-architecture-rows.md#a003) | physical root, logical prerequisite | ServiceEngagementIndependence | RejectServiceEngagementStaffAssignment | ServiceEngagementIndependence |

### ServiceIndependenceReevaluated

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:ServiceIndependenceReevaluated |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ServiceIndependenceReevaluated |
| Adopted wire | `{"discriminator":"bdgrz.independence.service-reevaluated","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/AccessControl/Events/ServiceIndependenceReevaluated.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | A change to a client's services prompted a reassessment of an accepted engagement's independence, retained as a record that blocks production acceptance and never refreshes the earlier acceptance. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/AccessControl/Events/ServiceIndependenceReevaluated.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | ServiceIndependenceReevaluated |
| Schema discriminator | bdgrz.independence.service-reevaluated |
| Schema discriminatorVersion | 1 |
| Schema owner | ServiceEngagementIndependence |
| Schema existingContract | src/Compliance.Common/Features/AccessControl/ServiceIndependenceReevaluated.cs |
| Schema status | selected target schema; implementation pending |
| Schema reason | existing native business fact, rehomed with its invariant |
| Schema changes | Externalize structured ServiceIndependenceReevaluationView including nested collections; a former item cap cannot become an event-size rejection for valid larger content., Remove former shared-ledger ExpectedSequence. Operation provenance retains authored concurrency; Revision names this new owner logical result revision. |
| Schema reducerRule | Validate owner/generation and local predecessor; apply only this owner delta or immutable content reference. Never hydrate a foreign aggregate or mutate based on a projection. |
| Schema serializer | Register concrete target event and every proof/content variant in ComplianceCoreJsonContext and attributed persistence preflight coverage. |
| Schema nativeResponseRule | FirstResponse is non-null only on this logical command first native accepting fact; it references FirstResponseDescriptor. OperationResult is non-null on the declared terminal business fact and references MutationResultContent. Both use verified content and static concrete result serializers; neither is reconstructed from current state. |

#### Event fields

| Field | Type | Required |
| --- | --- | --- |
| TenantId | Uuid | true |
| ReevaluationContent | ImmutableContentRef | true |
| Operation | OperationProvenance | true |
| Revision | ulong | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

#### Nested content schemas

#### Content schema: ServiceIndependenceReevaluated.ReevaluationContent.v1

| Property | Value |
| --- | --- |
| field | ReevaluationContent |
| schema | ServiceIndependenceReevaluated.ReevaluationContent.v1 |
| contentType | ServiceIndependenceReevaluationView |
| rule | Complete original typed structured value, including nested collections, written/verified before this event. No cumulative parent view or mutable latest pointer. |


[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]




[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]
