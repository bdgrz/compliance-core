# AuditPackageManifest

- Classification: `business_aggregate`
- Source area: `AuditPackages` (derived from source path; not a bounded-context ruling).
- Business aggregate: `true`
- Selected status: `SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED`

## Identity and invariant

**Selected identity:** Tenant-scoped packageId; `PackageId` on `CreateAuditPackage` is the caller-supplied immutable creation identity (exact immutable creation retry) so an exact retry resolves the original result and a different id is a different record; later mutations use CommandId bound to this identity and the expected revision.

**Invariant:** Manifest identity, selected immutable content version and delivery gate.

**Additional decision rule:** The selected manifest version lists every included file's path, hash, size and source record version, using deterministic names {engagement}/{section}/{stable_id}_{version}.{ext}; a delivery gate is refused if any entry lacks these values.

**Must preserve:** No current aggregate should retain all build attempts or delivery history as command state.

**Replay:** Current required authorization precedes replay; exact authorized retries resolve the original accepted result using the selected logical identity and immutable actor/intent/source binding. Fresh mutable feasibility, clock selection and body preparation cannot replace that original result. Expected revision is concurrency precondition, not a new identity.

**Lifecycle:** [Placeholder: a consolidated lifecycle sequence is not present in the selected owner record.]

## Commands

| Command | Kind | Identity field |
| --- | --- | --- |
| [AmendAuditPackage](../commands/audit-package-manifest.md#amendauditpackage) | http | CommandId |
| [ApproveAuditPackageDisclosure](../commands/audit-package-manifest.md#approveauditpackagedisclosure) | http | CommandId |
| [ApproveAuditPackageValidationOverride](../commands/audit-package-manifest.md#approveauditpackagevalidationoverride) | http | CommandId |
| [CreateAuditPackage](../commands/audit-package-manifest.md#createauditpackage) | http | PackageId |
| [RecordAuditPackageBuildResult](../commands/audit-package-manifest.md#recordauditpackagebuildresult) | receiver | CommandId |
| [ReportAuditPackageDelivery](../commands/audit-package-manifest.md#reportauditpackagedelivery) | http | DeliveryId |
| [RequestAuditPackageBuild](../commands/audit-package-manifest.md#requestauditpackagebuild) | http | BuildKey |
| [ValidateAuditPackage](../commands/audit-package-manifest.md#validateauditpackage) | http | CommandId |

## Events

| Event | Discriminator | Version |
| --- | --- | --- |
| [AuditPackageAmended](../events/audit-package-manifest.md#auditpackageamended) | bdgrz.auditpackages.audit-package-amended | 1 |
| [AuditPackageBuildRequested](../events/audit-package-manifest.md#auditpackagebuildrequested) | bdgrz.auditpackages.audit-package-build-requested | 1 |
| [AuditPackageBuilt](../events/audit-package-manifest.md#auditpackagebuilt) | bdgrz.auditpackages.audit-package-built | 1 |
| [AuditPackageCreated](../events/audit-package-manifest.md#auditpackagecreated) | bdgrz.auditpackages.audit-package-created | 1 |
| [AuditPackageDeliveryAdmitted](../events/audit-package-manifest.md#auditpackagedeliveryadmitted) | bdgrz.audit-package-manifest.audit-package-delivery-admitted | 1 |
| [AuditPackageDisclosureApproved](../events/audit-package-manifest.md#auditpackagedisclosureapproved) | bdgrz.auditpackages.audit-package-disclosure-approved | 1 |
| [AuditPackageValidated](../events/audit-package-manifest.md#auditpackagevalidated) | bdgrz.auditpackages.audit-package-validated | 1 |
| [AuditPackageValidationOverrideApproved](../events/audit-package-manifest.md#auditpackagevalidationoverrideapproved) | bdgrz.auditpackages.audit-package-validation-override-approved | 1 |

## Cross-owner flows

| Row | Kind | Source owner | Target owner | Command |
| --- | --- | --- | --- | --- |
| [T031](../reactors/audit-package-build-process-reactor.md#t031) | reaction_or_regular_process_stage | AuditPackageManifest | AuditPackageBuildProcess | StartAuditPackageBuild |
| [T032](../reactors/audit-package-build-process-reactor.md#t032) | reaction_or_regular_process_stage | AuditPackageBuildProcess | AuditPackageManifest | RecordAuditPackageBuildResult |
| [T081](../reactors/audit-package-delivery-admission-reactor.md#t081) | reaction_or_regular_process_stage | AuditPackageManifest | AuditPackageDelivery | RecordAuditPackageDelivery |

## Product rules

[Placeholder: product-rule traceability is not recorded for this owner.]

## Adopted architecture decisions

[Placeholder: no adopted architecture-decision reference is recorded.]

## Evidence and complete record

- Baseline source: [Placeholder: source file is not present in this checkout]
- Aggregate review: [audit-package-manifest-review-2026-10-29.md](../../product/definitive-aggregate-choreography/audit-package-manifest-review-2026-10-29.md)
- Full selected record: [JSON](../data/aggregates/audit-package-manifest.json)

> Target definition is separate from runtime qualification. Implementation status recorded by the source: `SEPARATE_VERIFICATION_REQUIRED_IMPLEMENTATION_HALTED`.
