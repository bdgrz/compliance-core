# InformationAsset

- Classification: `business_aggregate`
- Source area: `TechnologyInventory` (derived from source path; not a bounded-context ruling).
- Business aggregate: `true`
- Selected status: `SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED`

## Identity and invariant

**Selected identity:** Tenant + caller InformationAssetId under the preserved shared technology-inventory address/sentinel scheme.

**Invariant:** One tenant inventory identity has one record kind and one revisioned information classification/retention/owner/lifecycle record.

**Additional decision rule:** Each asset carries exactly one classification level from public, internal, confidential, or restricted plus a retention reference; a missing or second classification is refused. Each information asset has one classification, a retention rule, a responsible owner, and a status. The DataFlow record separately says which protections apply to information it carries.

**Must preserve:** [Placeholder: preservation rule not recorded.]

**Replay:** Current required authorization precedes replay; exact authorized retries resolve the original accepted result using the selected logical identity and immutable actor/intent/source binding. Fresh mutable feasibility, clock selection and body preparation cannot replace that original result. Expected revision is concurrency precondition, not a new identity.

**Lifecycle:** A new asset is active. ReviseInformationAsset may set it to active or retired. Retirement keeps the same identity and retention history. Reactivation is allowed. Changing asset status does not delete or change its data flows.

## Commands

| Command | Kind | Identity field |
| --- | --- | --- |
| [RecordInformationAsset](../commands/information-asset.md#recordinformationasset) | http | InformationAssetId |
| [ReviseInformationAsset](../commands/information-asset.md#reviseinformationasset) | command | CommandId |

## Events

| Event | Discriminator | Version |
| --- | --- | --- |
| [InformationAssetRevisionRecorded](../events/information-asset.md#informationassetrevisionrecorded) | bdgrz.inventory.information_asset.revision_recorded | 1 |

## Cross-owner flows

[Placeholder: no choreography row names this owner.]

## Product rules

- **D08-04** — Each InformationAsset carries exactly one classification level from public, internal, confidential, or restricted, plus a retention reference. Source: [m0-d08-technology-inventory.md](../../product/decisions/m0-d08-technology-inventory.md).

## Adopted architecture decisions

[Placeholder: no adopted architecture-decision reference is recorded.]

## Evidence and complete record

- Baseline source: [Placeholder: source file is not present in this checkout]
- Aggregate review: [information-asset-review-2026-10-29.md](../../product/definitive-aggregate-choreography/information-asset-review-2026-10-29.md)
- Full selected record: [JSON](../data/aggregates/information-asset.json)

> Target definition is separate from runtime qualification. Implementation status recorded by the source: `SEPARATE_VERIFICATION_REQUIRED_IMPLEMENTATION_HALTED`.
