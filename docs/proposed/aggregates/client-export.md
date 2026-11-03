# ClientExport

- Classification: `business_aggregate`
- Source area: `Exports` (derived from source path; not a bounded-context ruling).
- Business aggregate: `true`
- Selected status: `SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED`


## Identity and invariant

**Selected identity:** Tenant / client-exports / ExportId, with exact approved frozen inventory, attempt identity and attest-package proofs. ExportId creates the export; selected mutations use their declared CommandId/PackageId.

**Invariant:** Final export delivery requires archive counts matching frozen included/withheld inventory and a successful exact-manifest delivery for each required attest package.

**Additional decision rule:** Final delivery records that the named recipient received the agreed export after its included and withheld records were fixed and the export was successfully prepared. This satisfies the ClientExport obligation only; it does not satisfy the separate M0-D27 engagement record package delivery to the firm's audit software.

**Must preserve:** [Placeholder: preservation rule not recorded.]

**Replay:** Current required authorization precedes replay; exact authorized retries resolve the original accepted result using the selected logical identity and immutable actor/intent/source binding. Fresh mutable feasibility, clock selection and body preparation cannot replace that original result. Expected revision is concurrency precondition, not a new identity.

**Lifecycle:** RequestClientExport fixes the delivery obligation and its record list. A successful export result does not mean the recipient received it. RecordClientExportDelivery records the recipient's receipt and provides the delivery fact needed before offboarding and evidence disposition. The target keeps the M0-D27 engagement record package on its own owner.



## Source event compatibility

| Selected source event | Source owner | Target owner | Disposition |
| --- | --- | --- | --- |
| `ClientExportAttestPackageDeliveryRecorded` | `ClientExport` | `AttestEngagementRecordPackage` | The source event records both delivered and not_delivered decisions for an M0-D27 engagement package on the ClientExport stream. The target separates the package from ClientExport and names successful delivery as AttestEngagementRecordPackageDelivered. Preserve the old discriminator and history as source evidence; define how both old outcomes map to the target before implementation. |

## Commands

| Command | Kind | Identity field |
| --- | --- | --- |
| [RecordClientExportBuildResult](../commands/client-export.md#recordclientexportbuildresult) | command | CommandId |
| [RecordClientExportDelivery](../commands/client-export.md#recordclientexportdelivery) | command | CommandId |
| [RequestClientExport](../commands/client-export.md#requestclientexport) | creation | ExportId |

## Events

| Event | Discriminator | Version |
| --- | --- | --- |
| [ClientExportBuildFailed](../events/client-export.md#clientexportbuildfailed) | bdgrz.exports.client-export-build-failed | 2 |
| [ClientExportBuilt](../events/client-export.md#clientexportbuilt) | bdgrz.exports.client-export-built | 2 |
| [ClientExportDelivered](../events/client-export.md#clientexportdelivered) | bdgrz.exports.client-export-delivered | 2 |
| [ClientExportRequested](../events/client-export.md#clientexportrequested) | bdgrz.exports.client-export-requested | 2 |

## Cross-owner flows

| Row | Kind | Source owner | Target owner | Command |
| --- | --- | --- | --- | --- |
| [X007](../reactors/keep-engagement-evidence-protected-after-delivery-reactor.md#x007) | planned_reaction | ClientExport | ArtifactEngagementHold | RecordEngagementPackageDelivery |

## Product rules

[Placeholder: product-rule traceability is not recorded for this owner.]

## Adopted architecture decisions

[Placeholder: no adopted architecture-decision reference is recorded.]

## Evidence and complete record

- Baseline source: [Placeholder: source file is not present in this checkout]
- Aggregate review: [Placeholder: no owner-specific review file is present]
- Full selected record: [JSON](../data/aggregates/client-export.json)

> Target definition is separate from runtime qualification. Implementation status recorded by the source: `SEPARATE_VERIFICATION_REQUIRED_IMPLEMENTATION_HALTED`.
