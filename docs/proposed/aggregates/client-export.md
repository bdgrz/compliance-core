# ClientExport

- Classification: `business_aggregate`
- Source area: `Exports` (derived from source path; not a bounded-context ruling).
- Business aggregate: `true`
- Selected status: `SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED`

## Identity and invariant

**Selected identity:** Tenant / client-exports / ExportId, with exact approved frozen inventory, attempt identity and attest-package proofs. ExportId creates the export; selected mutations use their declared CommandId/PackageId.

**Invariant:** Final export delivery requires archive counts matching frozen included/withheld inventory and a successful exact-manifest delivery for each required attest package.

**Additional decision rule:** Final delivery is accepted only when the archive counts match the included and withheld items listed at the start, and every required attestation package has been delivered successfully with the required contents.

**Must preserve:** [Placeholder: preservation rule not recorded.]

**Replay:** Current required authorization precedes replay; exact authorized retries resolve the original accepted result using the selected logical identity and immutable actor/intent/source binding. Fresh mutable feasibility, clock selection and body preparation cannot replace that original result. Expected revision is concurrency precondition, not a new identity.

**Lifecycle:** RequestClientExport starts the delivery obligation. A build result alone does not mean the client received it. RecordClientExportDelivery completes the obligation and provides the delivery fact needed before offboarding and evidence disposition.

## Commands

| Command | Kind | Identity field |
| --- | --- | --- |
| [RecordClientExportBuildResult](../commands/client-export.md#recordclientexportbuildresult) | command | CommandId |
| [RecordClientExportDelivery](../commands/client-export.md#recordclientexportdelivery) | command | CommandId |
| [RecordOwnAttestRecordDelivery](../commands/client-export.md#recordownattestrecorddelivery) | command | CommandId |
| [RecordOwnAttestRecordPackageResult](../commands/client-export.md#recordownattestrecordpackageresult) | command | CommandId |
| [RequestClientExport](../commands/client-export.md#requestclientexport) | creation | ExportId |
| [RequestOwnAttestRecordPackage](../commands/client-export.md#requestownattestrecordpackage) | creation | PackageId |

## Events

| Event | Discriminator | Version |
| --- | --- | --- |
| [ClientExportAttestPackageDeliveryRecorded](../events/client-export.md#clientexportattestpackagedeliveryrecorded) | bdgrz.exports.client-export-attest-package-delivery-recorded | 1 |
| [ClientExportBuildFailed](../events/client-export.md#clientexportbuildfailed) | bdgrz.exports.client-export-build-failed | 2 |
| [ClientExportBuilt](../events/client-export.md#clientexportbuilt) | bdgrz.exports.client-export-built | 2 |
| [ClientExportDelivered](../events/client-export.md#clientexportdelivered) | bdgrz.exports.client-export-delivered | 2 |
| [ClientExportRequested](../events/client-export.md#clientexportrequested) | bdgrz.exports.client-export-requested | 2 |

## Cross-owner flows

| Row | Kind | Source owner | Target owner | Command |
| --- | --- | --- | --- | --- |
| [X007](../reactors/client-export-delivery-reactor.md#x007) | planned_reaction | ClientExport | ArtifactEngagementHold | RecordEngagementPackageDelivery |

## Product rules

[Placeholder: product-rule traceability is not recorded for this owner.]

## Adopted architecture decisions

[Placeholder: no adopted architecture-decision reference is recorded.]

## Evidence and complete record

- Baseline source: [Placeholder: source file is not present in this checkout]
- Aggregate review: [client-export-review-2026-10-29.md](../../product/definitive-aggregate-choreography/client-export-review-2026-10-29.md)
- Full selected record: [JSON](../data/aggregates/client-export.json)

> Target definition is separate from runtime qualification. Implementation status recorded by the source: `SEPARATE_VERIFICATION_REQUIRED_IMPLEMENTATION_HALTED`.
