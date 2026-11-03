# AttestEngagementRecordPackage

- Classification: `business_aggregate`
- Source area: `AttestRecords` (derived from source path; not a bounded-context ruling).
- Business aggregate: `true`
- Selected status: `PROPOSED_TARGET_OWNER_FROM_ACCEPTED_PRODUCT_RULE`
- Selected source owner: `ClientExport`.

## Identity and invariant

**Selected identity:** Tenant + ServiceEngagementId + PackageId. PackageId identifies the requested record package for the exact firm attest engagement.

**Invariant:** The firm’s own attest engagement record is preserved by generating its exact record package and recording delivery into the firm’s audit software before engagement closure or client offboarding. ClientExport delivery cannot satisfy this obligation.

**Additional decision rule:** The package preserves the firm’s own in-product attest engagement record and is separate from any ClientExport. Only a recorded successful delivery to the attest team's audit software satisfies the M0-D27 obligation.

**Must preserve:** [Placeholder: preservation rule not recorded.]

**Replay:** [Placeholder: replay rule not recorded.]

**Lifecycle:** Request the engagement record package, record whether it was generated, and record successful delivery to the firm's audit software. A failed generation or an unconfirmed delivery does not satisfy the obligation. Engagement closure and client offboarding wait for recorded delivery; the engagement hold continues to block evidence disposition until then.

## Source-to-target ownership

The source aggregate catalog groups these three commands under ClientExport. The source event ClientExportAttestPackageDeliveryRecorded also records delivered or not_delivered decisions for an M0-D27 package on the ClientExport stream. This target gives the package its own business record based on M0-D27 and the ClientExport review, which distinguishes the package from ClientExport’s export to its named recipient. Reconcile the old command and event records with this separate target owner before implementation.
**Target events:** These are proposed business facts only. No selected source event type, discriminator, version, or payload schema exists yet; define the event contracts before implementation.
## Source event compatibility

| Selected source event | Source owner | Target owner | Disposition |
| --- | --- | --- | --- |
| `ClientExportAttestPackageDeliveryRecorded` | `ClientExport` | `AttestEngagementRecordPackage` | The source event records both delivered and not_delivered decisions for an M0-D27 engagement package on the ClientExport stream. The target separates the package from ClientExport and names successful delivery as AttestEngagementRecordPackageDelivered. Preserve the old discriminator and history as source evidence; define how both old outcomes map to the target before implementation. |

## Commands

| Command | Kind | Identity field |
| --- | --- | --- |
| [RecordAttestEngagementRecordPackageDelivery](../commands/attest-engagement-record-package.md#recordattestengagementrecordpackagedelivery) | command | CommandId |
| [RecordAttestEngagementPackageBuildResult](../commands/attest-engagement-record-package.md#recordattestengagementpackagebuildresult) | command | CommandId |
| [RequestAttestEngagementRecordPackage](../commands/attest-engagement-record-package.md#requestattestengagementrecordpackage) | creation | PackageId |

## Events

| Event | Discriminator | Version |
| --- | --- | --- |
| [AttestEngagementRecordPackageRequested](../events/attest-engagement-record-package.md#attestengagementrecordpackagerequested) | [Placeholder: discriminator not recorded] | [Placeholder: value not recorded] |
| [AttestEngagementRecordPackageGenerated](../events/attest-engagement-record-package.md#attestengagementrecordpackagegenerated) | [Placeholder: discriminator not recorded] | [Placeholder: value not recorded] |
| [AttestEngagementRecordPackageGenerationFailed](../events/attest-engagement-record-package.md#attestengagementrecordpackagegenerationfailed) | [Placeholder: discriminator not recorded] | [Placeholder: value not recorded] |
| [AttestEngagementRecordPackageDelivered](../events/attest-engagement-record-package.md#attestengagementrecordpackagedelivered) | [Placeholder: discriminator not recorded] | [Placeholder: value not recorded] |

## Cross-owner flows

[Placeholder: no choreography row names this owner.]

## Product rules

- **M0-D27** — For an in-product attest engagement that must be preserved, request and generate its engagement record package, then record its delivery into the attest team's audit software before the engagement closes or the client is offboarded. ClientExport is a separate delivery to its named recipient; neither delivery satisfies the other. Keep the engagement hold in place until the attest engagement record package is delivered. Decision: [m0-d27-attest-scope.md](../decisions/m0-d27-attest-scope.md).

## Adopted architecture decisions

[Placeholder: no adopted architecture-decision reference is recorded.]

## Evidence and complete record

- Baseline source: [src/Compliance.Common/Features/AttestRecords/Commands/RequestOwnAttestRecordPackage.cs](../../../src/Compliance.Common/Features/AttestRecords/Commands/RequestOwnAttestRecordPackage.cs)
- Aggregate review: [Placeholder: no owner-specific review file is present]
- Full selected record: [JSON](../data/aggregates/attest-engagement-record-package.json)

> Target definition is separate from runtime qualification. Implementation status recorded by the source: `PROPOSED_TARGET_OWNER_FROM_ACCEPTED_PRODUCT_RULE`.
