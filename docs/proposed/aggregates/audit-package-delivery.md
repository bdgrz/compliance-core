# AuditPackageDelivery

- Classification: `business_aggregate`
- Source area: `AuditPackages` (derived from source path; not a bounded-context ruling).
- Business aggregate: `true`
- Selected status: `SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED`


## Identity and invariant

**Selected identity:** tenant / (packageId, manifestVersion, deliveryId)

**Invariant:** One delivery authorization and terminal delivery for exact recipient/version.

**Additional decision rule:** [Placeholder: no owner-specific decision rule is recorded.]

**Must preserve:** No current aggregate should retain all build attempts or delivery history as command state.

**Replay:** Current required authorization precedes replay; exact authorized retries resolve the original accepted result using the selected logical identity and immutable actor/intent/source binding. Fresh mutable feasibility, clock selection and body preparation cannot replace that original result. Expected revision is concurrency precondition, not a new identity.

**Lifecycle:** [Placeholder: a consolidated lifecycle sequence is not present in the selected owner record.]





## Commands

| Command | Kind | Identity field |
| --- | --- | --- |
| [RecordAuditPackageDelivery](../commands/audit-package-delivery.md#recordauditpackagedelivery) | receiver | CommandId |

## Events

| Event | Discriminator | Version |
| --- | --- | --- |
| [AuditPackageDelivered](../events/audit-package-delivery.md#auditpackagedelivered) | bdgrz.auditpackages.audit-package-delivered | 1 |

## Cross-owner flows

| Row | Kind | Source owner | Target owner | Command |
| --- | --- | --- | --- | --- |
| [T081](../reactors/record-audit-package-delivery-reactor.md#t081) | reaction_or_regular_process_stage | AuditPackageManifest | AuditPackageDelivery | RecordAuditPackageDelivery |

## Product rules

[Placeholder: product-rule traceability is not recorded for this owner.]

## Adopted architecture decisions

[Placeholder: no adopted architecture-decision reference is recorded.]

## Evidence and complete record

- Baseline source: [Placeholder: source file is not present in this checkout]
- Aggregate review: [Placeholder: no owner-specific review file is present]
- Full selected record: [JSON](../data/aggregates/audit-package-delivery.json)

> Target definition is separate from runtime qualification. Implementation status recorded by the source: `SEPARATE_VERIFICATION_REQUIRED_IMPLEMENTATION_HALTED`.
