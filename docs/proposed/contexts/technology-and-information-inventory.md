# Technology and information inventory

Status: **locked**, 2026-11-03. Hand-authored source of truth for this context.

## Purpose

This context records the technology, information, data flows, locations and
operational processes the scoped system depends on, at the granularity a small
team manages: accounts and environments, not individual resources (M0-D08). The
inventory in the product is authoritative; cloud consoles, MDM and repository
hosts are only for discovering gaps.

**Owns:** technology components, information assets, data flows, locations and
operational processes.

**Does not own:**

| Concern | Owner |
| --- | --- |
| Cloud accounts | Application inventory: a cloud account is a `SystemInstance` of kind `cloud_account` (TI-01) |
| Owners | Workforce assurance (`Person`) |
| Approved exceptions to encryption rules | Assurance work (waivers) |
| Retention periods | Retention (M0-D16); assets hold a reference |
| Written procedures | Policies; a procedure is policy content, never an operational process |
| Connectors | Deferred: none is built now (M0-D08) |

## Language

| Term | Meaning |
| --- | --- |
| Technology component | One governed environment, network, data store, repository or endpoint class. |
| Endpoint class | A group of devices, such as "managed macOS laptops", recorded by count and management source. Individual devices are out of scope. |
| Information asset | A body of information with one classification (`public`, `internal`, `confidential`, `restricted`), a retention reference and an owner. |
| Data flow | Information moving from a service or system instance to a data store or an external party, with its purpose and encryption in transit and at rest. |
| Location | A place the system depends on, such as an office, data center or cloud region, with a kind. |
| Operational process | A governed business process, such as change management or onboarding. |

## Aggregates

All five aggregates follow the same lifecycle: active, then retired. Retirement
is final, keeps the identity for history, and needs a reason; something revived
later is a new record (TI-03). Changes are recorded as named facts, not one
generic revision (as WF-03).

### TechnologyComponent

**Identity:** `TenantId` + `ComponentId`.

**Invariant:** one component has one category for life (`environment`, `network`,
`data_store`, `repository`, `endpoint_class`), a governed name, an owner, an
environment reference, and a location reference.

- An endpoint class also has a count and a management source.
- A component that lives in a cloud account references that account's system
  instance (TI-01).

| Command | Who | Accepts when | Emits |
| --- | --- | --- | --- |
| RecordTechnologyComponent | Compliance Lead or Contributor | `ComponentId` is new; category fields complete | TechnologyComponentRecorded |
| ChangeTechnologyComponentDetails | Compliance Lead or Contributor | Active | TechnologyComponentDetailsChanged |
| ChangeTechnologyComponentOwner | Compliance Lead | Active | TechnologyComponentOwnerChanged |
| RetireTechnologyComponent | Compliance Lead | Active; reason given | TechnologyComponentRetired |

### InformationAsset

**Identity:** `TenantId` + `InformationAssetId`.

**Invariant:** one asset has exactly one classification, one retention reference
and one owner.

- Raising the classification is allowed even when flows that carry the asset no
  longer meet the encryption rule; those flows show as gaps (TI-02).

| Command | Who | Accepts when | Emits |
| --- | --- | --- | --- |
| RecordInformationAsset | Compliance Lead or Contributor | `InformationAssetId` is new; one classification | InformationAssetRecorded |
| ClassifyInformationAsset | Compliance Lead | Active; classification differs | InformationAssetClassified |
| ChangeInformationAssetDetails | Compliance Lead or Contributor | Active | InformationAssetDetailsChanged |
| ChangeInformationAssetOwner | Compliance Lead | Active | InformationAssetOwnerChanged |
| RetireInformationAsset | Compliance Lead | Active; reason given | InformationAssetRetired |

Retiring an asset does not change its data flows.

### DataFlow

**Identity:** `TenantId` + `DataFlowId`.

**Invariant:** one flow has one source (a client service or system instance), one
destination (a data store or an external party), one or more information assets,
a purpose, and explicit `encrypted_in_transit` and `encrypted_at_rest` flags. Its
versions are effective-dated, and the effective date never moves backward.

- When a carried asset is `confidential` or `restricted`, both encryption flags
  must be true unless an approved exception is referenced. This is checked when
  the flow is recorded or changed, using each asset's current classification.

| Command | Who | Accepts when | Emits |
| --- | --- | --- | --- |
| RecordDataFlow | Compliance Lead or Contributor | `DataFlowId` is new; rules hold | DataFlowRecorded |
| ReviseDataFlow | Compliance Lead or Contributor | Active; rules hold; effective date not earlier | DataFlowRevised |
| RetireDataFlow | Compliance Lead | Active; reason given | DataFlowRetired |

A data flow is a versioned statement, so its changes are one `DataFlowRevised`
fact carrying the full new version.

### Location

**Identity:** `TenantId` + `LocationId`.

**Invariant:** one location has one kind for life and a name. Among active
locations of the same kind, names are unique; this is checked against current
state when a name is set.

| Command | Who | Accepts when | Emits |
| --- | --- | --- | --- |
| RecordLocation | Compliance Lead or Contributor | `LocationId` is new; name unused within kind | LocationRecorded |
| RenameLocation | Compliance Lead or Contributor | Active; name unused within kind | LocationRenamed |
| ChangeLocationDetails | Compliance Lead or Contributor | Active | LocationDetailsChanged |
| RetireLocation | Compliance Lead | Active; reason given | LocationRetired |

A retired location's name becomes available again.

### OperationalProcess

**Identity:** `TenantId` + `OperationalProcessId`.

**Invariant:** one process has one type for life, a name, an owner and a
description. Names are unique among active processes; this is checked against
current state when a name is set.

| Command | Who | Accepts when | Emits |
| --- | --- | --- | --- |
| RecordOperationalProcess | Compliance Lead or Contributor | `OperationalProcessId` is new; name unused | OperationalProcessRecorded |
| RenameOperationalProcess | Compliance Lead or Contributor | Active; name unused | OperationalProcessRenamed |
| ChangeOperationalProcessDetails | Compliance Lead or Contributor | Active | OperationalProcessDetailsChanged |
| ChangeOperationalProcessOwner | Compliance Lead | Active | OperationalProcessOwnerChanged |
| RetireOperationalProcess | Compliance Lead | Active; reason given | OperationalProcessRetired |

Every event carries the tenant, the record ID, the changed values and the actor.
Retirement events also carry the effective date and reason.

## Flows

This context has no reactors. Each command checks the facts it needs against
current state. Consumers such as risk, the system description and controls react
to its facts in their own contexts.

## Decisions

Jev answers are from `jev-1.14.0`, 2026-11-03. A yes is ≥ 0.8 and a no is ≤ 0.2.
Anything in between is settled by common sense, with the reasoning stated.

| ID | Decision | Evidence |
| --- | --- | --- |
| TI-01 | Cloud accounts are recorded only as system instances; no cloud-account component. | 0.75. M0-D08: cloud-account components correspond one-to-one with system instances and are not duplicated. |
| TI-02 | Raising a classification is allowed, and flows that no longer meet the encryption rule show as gaps. | 0.74. Same pattern as an overdue service-identity review: show the gap, don't block the truth. |
| TI-03 | Retirement is final for all inventory records; something revived is a new record. | Location restorable: 0.49; process retirement final: 0.46, both even. Common sense: the same rule as applications and work relationships keeps lifecycles simple. This replaces the earlier allowance to reactivate assets and locations. |
| TI-04 | Name uniqueness for locations and processes is checked against current state; there are no name-claim aggregates or name-change processes. | Small independent aggregates (as AI-03). |

## Open questions

None at this time.

## Superseded owners

| Old owner | Becomes |
| --- | --- |
| TechnologyComponent | TechnologyComponent (no `cloud_account` category; TI-01) |
| InformationAsset | InformationAsset |
| DataFlow | DataFlow |
| Location | Location |
| OperationalProcess | OperationalProcess |
| LocationNameClaim, OperationalProcessNameClaim | Removed (TI-04) |
| LocationNameChangeProcess, OperationalProcessNameChangeProcess | Removed (TI-04) |
