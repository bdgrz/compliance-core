# Source events kept for compatibility

These records describe facts from the selected source catalog that do not have a direct event name in the proposed target. They preserve the old source meaning and wire for later reconciliation; they are not additional target business events.

| Selected source event | Source owner | Proposed owner | Related target event | Wire | Version | Disposition |
| --- | --- | --- | --- | --- | --- | --- |
| `ClientExportAttestPackageDeliveryRecorded` | `ClientExport` | `AttestEngagementRecordPackage` | `AttestEngagementRecordPackageDelivered` | bdgrz.exports.client-export-attest-package-delivery-recorded | 1 | The source event records both delivered and not_delivered decisions for an M0-D27 engagement package on the ClientExport stream. The target separates the package from ClientExport and names successful delivery as AttestEngagementRecordPackageDelivered. Preserve the old discriminator and history as source evidence; define how both old outcomes map to the target before implementation. |

## ClientExportAttestPackageDeliveryRecorded

**Selected source meaning:** A delivery decision for an attest package was recorded on a successfully built client export, with its rationale, recipient and delivery reference, for the exact package manifest.

**Source status:** declared

**Target disposition:** The source event records both delivered and not_delivered decisions for an M0-D27 engagement package on the ClientExport stream. The target separates the package from ClientExport and names successful delivery as AttestEngagementRecordPackageDelivered. Preserve the old discriminator and history as source evidence; define how both old outcomes map to the target before implementation.
