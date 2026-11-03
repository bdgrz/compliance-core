# Write model domain rules

Business actions people request, facts recorded when those actions succeed, and the business purpose of automatic reactions. Screen and report views and internal delivery details are outside this pass.

These are the selected rules for the proposed target. The decisions and owner records below define the selected domain rules.

## Selected decisions

| Decision | Selected rule |
| --- | --- |
| Each campaign and hold has its own identity | CampaignId identifies one policy or access-review campaign. Creating a second campaign with a different CampaignId creates a separate campaign, even if the policy version and roster or access population match. HoldId identifies one legal direction. Separate holds on the same artifact stay active independently; releasing one does not release another. |
| Published criteria editions cannot be changed | The platform publishes criteria editions. Once published, an edition does not change; changes appear in a new edition. A program records which edition it uses through SelectProgramCriteriaEdition, which records ProgramCriteriaEditionSelected. Customers do not create or edit the platform editions. |
| Status changes stay with the existing record | Use the record's existing change command to update its status along with its other details. The command emits that record's existing revision event. The proposed target adds no separate command or event just to end, disable, retire, reactivate, or delete one of these records. A retired or ended record can change again unless its rule below says otherwise. Changing one record does not automatically change another. Location name changes follow the existing process: it reserves the new name when a change is accepted and releases the old name after the record changes. |
| ClientExport records a delivery obligation | ClientExport remains a business record. Final delivery records that the client received the approved export and is required before client offboarding and later evidence disposition. A failed build does not count as delivery. |

### CAMPAIGN_AND_HOLD_IDENTITY

**Selected rule:** CampaignId identifies one policy or access-review campaign. Creating a second campaign with a different CampaignId creates a separate campaign, even if the policy version and roster or access population match. HoldId identifies one legal direction. Separate holds on the same artifact stay active independently; releasing one does not release another.

**Why:** No accepted rule says that only one open campaign may exist for a policy version and snapshot. A policy may be launched again with a different due date or instructions. Each legal direction stays in force until that same hold is released.

**Owners:** PolicyDistributionCampaign, AccessReviewCampaign, ArtifactLegalHold.

**Commands:** LaunchPolicyCampaign, LaunchTrainingCampaign, LaunchAccessReviewCampaign, PlaceArtifactLegalHold, ReleaseArtifactLegalHold.

**Events:** PolicyCampaignLaunched, AccessReviewCampaignLaunched, ArtifactLegalHoldPlaced, ArtifactLegalHoldReleased.

**Reactors:** None listed.

**Source evidence:** [docs/product/decisions/m0-d12-policy-acknowledgement-training.md](../product/decisions/m0-d12-policy-acknowledgement-training.md), [docs/product/decisions/m0-d07-access-review-population.md](../product/decisions/m0-d07-access-review-population.md), [docs/product/decisions/m0-d16-evidence-handling.md](../product/decisions/m0-d16-evidence-handling.md), [docs/product/definitive-aggregate-choreography/aggregates.json](../product/definitive-aggregate-choreography/aggregates.json).


### CRITERIA_EDITION_OWNER

**Selected rule:** The platform publishes criteria editions. Once published, an edition does not change; changes appear in a new edition. A program records which edition it uses through SelectProgramCriteriaEdition, which records ProgramCriteriaEditionSelected. Customers do not create or edit the platform editions.

**Why:** M0-D02 places criteria editions under platform control and says a published edition stays unchanged. A customer program decides which edition it follows.

**Owners:** ComplianceProgram.

**Commands:** SelectProgramCriteriaEdition.

**Events:** ProgramCriteriaEditionSelected.

**Reactors:** None listed.

**Source evidence:** [docs/product/decisions/m0-d02-criteria-content.md](../product/decisions/m0-d02-criteria-content.md), [docs/product/definitive-aggregate-choreography/product-rule-traceability.json](../product/definitive-aggregate-choreography/product-rule-traceability.json).


### INVENTORY_LIFECYCLE_FACTS

**Selected rule:** Use the record's existing change command to update its status along with its other details. The command emits that record's existing revision event. The proposed target adds no separate command or event just to end, disable, retire, reactivate, or delete one of these records. A retired or ended record can change again unless its rule below says otherwise. Changing one record does not automatically change another. Location name changes follow the existing process: it reserves the new name when a change is accepted and releases the old name after the record changes.

**Why:** Each record's status follows that record's own rules. Current product decisions do not say that changing one of these records automatically changes access or starts a separate process. Changes to people's work status follow comparison of accepted workforce roster versions. Location names follow the accepted location change: the new name is reserved when the change is accepted, and the old name is released after the record changes.

**Owners:** ServiceIdentity, Person, WorkRelationship, Location, DataFlow, TechnologyComponent, InformationAsset.

**Commands:** ReviseServiceIdentity, RevisePerson, ReviseWorkRelationship, ReviseLocation, ReviseDataFlow, ReviseTechnologyComponent, ReviseInformationAsset.

**Events:** ServiceIdentityRevised, PersonRevised, WorkRelationshipRevised, LocationRevisionRecorded, DataFlowRevisionRecorded, TechnologyComponentRevisionRecorded, InformationAssetRevisionRecorded.

**Reactors:** KeepLocationNamesUniqueReactor.

**Source evidence:** [docs/product/decisions/m0-d06-workforce-source.md](../product/decisions/m0-d06-workforce-source.md), [docs/product/decisions/m0-d08-technology-inventory.md](../product/decisions/m0-d08-technology-inventory.md), [docs/product/decisions/m0-d22-canonical-ownership.md](../product/decisions/m0-d22-canonical-ownership.md), [docs/product/definitive-aggregate-choreography/service-identity-review-2026-10-27.md](../product/definitive-aggregate-choreography/service-identity-review-2026-10-27.md), [docs/product/definitive-aggregate-choreography/person-review-2026-10-29.md](../product/definitive-aggregate-choreography/person-review-2026-10-29.md), [docs/product/definitive-aggregate-choreography/data-flow-review-2026-10-29.md](../product/definitive-aggregate-choreography/data-flow-review-2026-10-29.md), [docs/product/definitive-aggregate-choreography/information-asset-review-2026-10-29.md](../product/definitive-aggregate-choreography/information-asset-review-2026-10-29.md), [docs/product/definitive-aggregate-choreography/location-review-2026-10-27.md](../product/definitive-aggregate-choreography/location-review-2026-10-27.md).


### CLIENT_EXPORT_BUSINESS_OWNER

**Selected rule:** ClientExport remains a business record. Final delivery records that the client received the approved export and is required before client offboarding and later evidence disposition. A failed build does not count as delivery.

**Why:** The export has an included and withheld item list fixed at the start, required attestation packages, and a final delivery decision. These facts determine whether client offboarding and evidence disposal may proceed; the work is more than copying files.

**Owners:** ClientExport, ArtifactDisposition.

**Commands:** RequestClientExport, RecordClientExportBuildResult, RecordClientExportDelivery.

**Events:** ClientExportRequested, ClientExportBuilt, ClientExportBuildFailed, ClientExportDelivered.

**Reactors:** None listed.

**Source evidence:** [docs/product/decisions/m0-d16-evidence-handling.md](../product/decisions/m0-d16-evidence-handling.md), [docs/product/definitive-aggregate-choreography/client-export-review-2026-10-29.md](../product/definitive-aggregate-choreography/client-export-review-2026-10-29.md), [docs/product/definitive-aggregate-choreography/aggregates.json](../product/definitive-aggregate-choreography/aggregates.json).


## Lifecycle by owner

| Owner | Decision | Lifecycle rule | Evidence |
| --- | --- | --- | --- |
| [PolicyDistributionCampaign](aggregates/policy-distribution-campaign.md) | Each campaign has its own CampaignId. A second campaign may use the same policy version and roster snapshot; no rule prohibits it. | At launch, the campaign saves the roster it covers. Later roster changes are recorded as amendments. Results can be added only while the campaign is open. An accepted closure stops results until that closure is canceled or completed. | M0-D12; CampaignId creation identity; OPEN_CampaignNaturalKey disposition. |
| [AccessReviewCampaign](aggregates/access-review-campaign.md) | Each campaign has its own CampaignId. A second campaign may use the same population snapshot; no rule prohibits overlapping campaigns. | Each campaign keeps its own saved population, completion decision, and closure. Completing one campaign does not close or change another. | M0-D07; caller-supplied CampaignId creation identity; Jev access_review_campaign_duplicate 0.05. |
| [ArtifactLegalHold](aggregates/artifact-legal-hold.md) | HoldId identifies one legal direction for one covered artifact. Different holds remain active independently, even when they cover the same artifact. | Place each hold once and release only that same hold. Evidence cannot be disposed of while any hold on it remains active. Releasing one hold leaves any other holds in force. | M0-D16; PlaceArtifactLegalHold and ReleaseArtifactLegalHold; Jev separate_legal_holds 0.95. |
| [ServiceIdentity](aggregates/service-identity.md) | Each service identity has one accountable person or team, an approved purpose, and a review date no more than one year away. | A service identity can be active, disabled, or retired. RecordServiceIdentity creates it; ReviseServiceIdentity changes its details or status and records ServiceIdentityRevised. Retiring it does not automatically change access at an outside service. If its owner relationship ends or its review date passes, it appears as work that needs a new owner or review. | M0-D06; D22-03; service-identity-review-2026-10-27.md. |
| [Person](aggregates/person.md) | A Person is the stable identity for someone in the workforce. It is separate from that person's employment record and their access to the platform. | The proposed target has no separate event for ending or retiring a Person. WorkRelationship records employment status; Member records platform access. Changing contact details or platform access does not end the person's employment. | M0-D06; M0-D22; person-review-2026-10-29.md; Jev person_lifecycle 0.11. |
| [WorkRelationship](aggregates/work-relationship.md) | A WorkRelationship links one Person to their record in a workforce source, using that source's stable worker ID. Its status and end date must agree; an ended relationship must have an end date. | Allowed statuses are pending, active, on_leave, and ended. ReviseWorkRelationship records a status or terms change as WorkRelationshipRevised. Changes to people's work status follow comparison of accepted workforce roster versions. Changing this record alone does not grant or revoke platform or outside-service access. | M0-D06; M0-D22; WorkRelationship owner status rules; Jev work_relationship_lifecycle 0.89. |
| [Location](aggregates/location.md) | A location keeps the same identity and kind over time. Only an active location must have a unique name within its kind. | A location starts active. ReviseLocation changes its details or status through the accepted location-change process. When a location retires, its old name becomes available after the updated record is saved; historical records keep its ID. The target does not prohibit restoring a retired location. | M0-D22; LocationNames and LocationNameChangeProcess; location-review-2026-10-27.md. |
| [DataFlow](aggregates/data-flow.md) | A data flow records where information starts and goes, which information it carries, why it moves, and how it is protected. Its effective date cannot move backward. Confidential or restricted information must be encrypted both when stored and sent, unless an approved exception applies. | A new data flow is active. ReviseDataFlow may set it to active or retired while preserving its source, destination, information, protection, and effective-date rules. Changing its status does not automatically change related asset or technology records, and a retired flow is not permanently closed by this rule. | M0-D08; D08-05; D08-06; data-flow-review-2026-10-29.md. |
| [TechnologyComponent](aggregates/technology-component.md) | Keep one record for each governed technology component at the level it is managed. A cloud account belongs to exactly one system instance. Endpoint categories are recorded by count and management source. | A new component is active. ReviseTechnologyComponent may set it to active or retired. Retirement keeps the same component identity and historical references. This change alone does not change the system instance or access. | M0-D08; D08-01 through D08-03; TechnologyInventoryRules. |
| [InformationAsset](aggregates/information-asset.md) | Each information asset has one classification, a retention rule, a responsible owner, and a status. The DataFlow record separately says which protections apply to information it carries. | A new asset is active. ReviseInformationAsset may set it to active or retired. Retirement keeps the same identity and retention history. Reactivation is allowed. Changing asset status does not delete or change its data flows. | M0-D08; D08-04; information-asset-review-2026-10-29.md. |
| [ClientExport](aggregates/client-export.md) | Final delivery is accepted only when the archive counts match the included and withheld items listed at the start, and every required attestation package has been delivered successfully with the required contents. | RequestClientExport starts the delivery obligation. A build result alone does not mean the client received it. RecordClientExportDelivery completes the obligation and provides the delivery fact needed before offboarding and evidence disposition. | M0-D16; client-export-review-2026-10-29.md; Jev client_export_is_business_owner 0.96. |

## Deferred to event-contract details

| Source label | Disposition |
| --- | --- |
| OPEN_ResponseReceiptsInPayload | Whether a command's original response belongs in its business event or in a separate operation receipt does not change the business command, event meaning, or reactor reason. Keep the existing source contract unchanged in this pass. |
