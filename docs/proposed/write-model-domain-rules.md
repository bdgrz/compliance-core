# Write model domain rules

Business actions people request, facts recorded when those actions succeed, and the business purpose of automatic reactions. Screen and report views and internal delivery details are outside this pass.

These are the selected rules for the proposed target. The decisions and owner records below define the selected domain rules.

## Selected decisions

| Decision | Selected rule |
| --- | --- |
| Each campaign and hold has its own identity | CampaignId identifies one policy or access-review campaign. Creating a second campaign with a different CampaignId creates a separate campaign, even if the policy version and roster or access population match. HoldId identifies one legal direction. Separate holds on the same artifact stay active independently; releasing one does not release another. |
| Published criteria editions cannot be changed | The platform publishes criteria editions. Once published, an edition does not change; changes appear in a new edition. A program records which edition it uses through SelectProgramCriteriaEdition, which records ProgramCriteriaEditionSelected. Customers do not create or edit the platform editions. |
| Status changes stay with the existing record | Use the record's existing change command to update its status along with its other details. The command emits that record's existing revision event. The proposed target adds no separate command or event just to end, disable, retire, reactivate, or delete one of these records. A retired or ended record can change again unless its rule below says otherwise. Changing one record does not automatically change another. Location name changes follow the existing process: it reserves the new name when a change is accepted and releases the old name after the record changes. |
| ClientExport owns its separate recipient delivery | ClientExport is the business record for one agreed export to its named recipient. It fixes the included and withheld records, records the export result, and records final delivery. That delivery is required before client offboarding and later evidence disposition. It does not satisfy the separate M0-D27 requirement to deliver the firm's engagement record package into the attest team's audit software. |
| The firm’s attest engagement record package has its own business record | For an in-product attest engagement that must be preserved, request and generate its engagement record package, then record its delivery into the attest team's audit software before the engagement closes or the client is offboarded. ClientExport is a separate delivery to its named recipient; neither delivery satisfies the other. Keep the engagement hold in place until the attest engagement record package is delivered. |

### CAMPAIGN_AND_HOLD_IDENTITY

**Selected rule:** CampaignId identifies one policy or access-review campaign. Creating a second campaign with a different CampaignId creates a separate campaign, even if the policy version and roster or access population match. HoldId identifies one legal direction. Separate holds on the same artifact stay active independently; releasing one does not release another.

**Why:** No accepted rule says that only one open campaign may exist for a policy version and snapshot. A policy may be launched again with a different due date or instructions. Each legal direction stays in force until that same hold is released.

**Owners:** `PolicyDistributionCampaign`, `AccessReviewCampaign`, `ArtifactLegalHold`.

**Commands:** `LaunchPolicyCampaign`, `LaunchTrainingCampaign`, `LaunchAccessReviewCampaign`, `PlaceArtifactLegalHold`, `ReleaseArtifactLegalHold`.

**Events:** `PolicyCampaignLaunched`, `AccessReviewCampaignLaunched`, `ArtifactLegalHoldPlaced`, `ArtifactLegalHoldReleased`.

**Reactors:** None listed.

**Source evidence:** .

### CRITERIA_EDITION_OWNER

**Selected rule:** The platform publishes criteria editions. Once published, an edition does not change; changes appear in a new edition. A program records which edition it uses through SelectProgramCriteriaEdition, which records ProgramCriteriaEditionSelected. Customers do not create or edit the platform editions.

**Why:** M0-D02 places criteria editions under platform control and says a published edition stays unchanged. A customer program decides which edition it follows.

**Owners:** `ComplianceProgram`.

**Commands:** `SelectProgramCriteriaEdition`.

**Events:** `ProgramCriteriaEditionSelected`.

**Reactors:** None listed.

**Source evidence:** .

### INVENTORY_LIFECYCLE_FACTS

**Selected rule:** Use the record's existing change command to update its status along with its other details. The command emits that record's existing revision event. The proposed target adds no separate command or event just to end, disable, retire, reactivate, or delete one of these records. A retired or ended record can change again unless its rule below says otherwise. Changing one record does not automatically change another. Location name changes follow the existing process: it reserves the new name when a change is accepted and releases the old name after the record changes.

**Why:** Each record's status follows that record's own rules. Current product decisions do not say that changing one of these records automatically changes access or starts a separate process. Changes to people's work status follow comparison of accepted workforce roster versions. Location names follow the accepted location change: the new name is reserved when the change is accepted, and the old name is released after the record changes.

**Owners:** `ServiceIdentity`, `Person`, `WorkRelationship`, `Location`, `DataFlow`, `TechnologyComponent`, `InformationAsset`.

**Commands:** `ReviseServiceIdentity`, `RevisePerson`, `ReviseWorkRelationship`, `ReviseLocation`, `ReviseDataFlow`, `ReviseTechnologyComponent`, `ReviseInformationAsset`.

**Events:** `ServiceIdentityRevised`, `PersonRevised`, `WorkRelationshipRevised`, `LocationRevisionRecorded`, `DataFlowRevisionRecorded`, `TechnologyComponentRevisionRecorded`, `InformationAssetRevisionRecorded`.

**Reactors:** `KeepLocationNamesUniqueReactor`.

**Source evidence:** .

### CLIENT_EXPORT_BUSINESS_OWNER

**Selected rule:** ClientExport is the business record for one agreed export to its named recipient. It fixes the included and withheld records, records the export result, and records final delivery. That delivery is required before client offboarding and later evidence disposition. It does not satisfy the separate M0-D27 requirement to deliver the firm's engagement record package into the attest team's audit software.

**Why:** M0-D16 requires ClientExport delivery before client offboarding and evidence disposition. M0-D27 separately requires the firm's own engagement record package to be delivered into the attest team's audit software. The recipient and purpose are different, so one delivery cannot stand in for the other.

**Owners:** `ClientExport`, `ArtifactDisposition`.

**Commands:** `RequestClientExport`, `RecordClientExportBuildResult`, `RecordClientExportDelivery`.

**Events:** `ClientExportRequested`, `ClientExportBuilt`, `ClientExportBuildFailed`, `ClientExportDelivered`.

**Reactors:** None listed.

**Source evidence:** .

### ATTEST_ENGAGEMENT_RECORD_PACKAGE

**Selected rule:** For an in-product attest engagement that must be preserved, request and generate its engagement record package, then record its delivery into the attest team's audit software before the engagement closes or the client is offboarded. ClientExport is a separate delivery to its named recipient; neither delivery satisfies the other. Keep the engagement hold in place until the attest engagement record package is delivered.

**Why:** M0-D27 requires the firm’s own attest team to retain a package of its engagement record. The ClientExport review defines a different export to its recipient and explicitly places the own-attest package commands on the attest-record side rather than in ClientExport. The source event catalog still assigns a related delivery-decision event to ClientExport, so its historical outcome and wire mapping need reconciliation before implementation.

**Owners:** `AttestEngagementRecordPackage`, `ClientExport`, `ServiceEngagementIndependence`, `Tenant`, `ArtifactEngagementHold`.

**Commands:** `RequestAttestEngagementRecordPackage`, `RecordAttestEngagementPackageBuildResult`, `RecordAttestEngagementRecordPackageDelivery`, `CloseServiceEngagement`, `OffboardTenant`, `CompleteTenantOffboarding`.

**Events:** `AttestEngagementRecordPackageRequested`, `AttestEngagementRecordPackageGenerated`, `AttestEngagementRecordPackageGenerationFailed`, `AttestEngagementRecordPackageDelivered`.

**Reactors:** None listed.

**Source evidence:** .

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
| [ClientExport](aggregates/client-export.md) | Final delivery records that the named recipient received the agreed export after its included and withheld records were fixed and the export was successfully prepared. This satisfies the ClientExport obligation only; it does not satisfy the separate M0-D27 engagement record package delivery to the firm's audit software. | RequestClientExport fixes the delivery obligation and its record list. A successful export result does not mean the recipient received it. RecordClientExportDelivery records the recipient's receipt and provides the delivery fact needed before offboarding and evidence disposition. The target keeps the M0-D27 engagement record package on its own owner. | M0-D16; M0-D27; client-export-review-2026-10-29.md; Jev client_export_is_business_owner 0.96 and separate_owner 0.97. |
| [AttestEngagementRecordPackage](aggregates/attest-engagement-record-package.md) | The package preserves the firm’s own in-product attest engagement record and is separate from any ClientExport. Only a recorded successful delivery to the attest team's audit software satisfies the M0-D27 obligation. | Request the engagement record package, record whether it was generated, and record successful delivery to the firm's audit software. A failed generation or an unconfirmed delivery does not satisfy the obligation. Engagement closure and client offboarding wait for recorded delivery; the engagement hold continues to block evidence disposition until then. | M0-D27; client-export-review-2026-10-29.md; the three package commands and related delivery-decision event are currently grouped under ClientExport in the source catalog and are separated in this proposed target. |

## Source event compatibility

| Selected source event | Source owner | Target owner | Related target event | Reason |
| --- | --- | --- | --- | --- |
| `ClientExportAttestPackageDeliveryRecorded` | `ClientExport` | `AttestEngagementRecordPackage` | `AttestEngagementRecordPackageDelivered` | The source event records both delivered and not_delivered decisions for an M0-D27 engagement package on the ClientExport stream. The target separates the package from ClientExport and names successful delivery as AttestEngagementRecordPackageDelivered. Preserve the old discriminator and history as source evidence; define how both old outcomes map to the target before implementation. |

## Deferred to event-contract details

| Source label | Disposition |
| --- | --- |
| OPEN_ResponseReceiptsInPayload | Whether a command's original response belongs in its business event or in a separate operation receipt does not change the business command, event meaning, or reactor reason. Keep the existing source contract unchanged in this pass. |
