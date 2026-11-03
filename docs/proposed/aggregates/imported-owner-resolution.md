# ImportedOwnerResolution

- Classification: `business_aggregate`
- Source area: `[Placeholder: source capability area is not recorded]` (derived from source path; not a bounded-context ruling).
- Business aggregate: `true`
- Selected status: `SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED`


## Identity and invariant

**Selected identity:** Tenant + import source reference (the imported owner value and its source record); `ResolutionId` must equal that deterministic tuple; manual resolution uses CommandId.

**Invariant:** An imported named owner is bound to a workforce Person only by exact normalized email match at import, otherwise it stays an unresolved_owner reference until one attributed manual resolution names the Person; a name-only or ambiguous match never binds, and the resolution is immutable once recorded (M0-D04 D04-02).

**Additional decision rule:** [Placeholder: no owner-specific decision rule is recorded.]

**Must preserve:** [Placeholder: preservation rule not recorded.]

**Replay:** [Placeholder: replay rule not recorded.]

**Lifecycle:** [Placeholder: a consolidated lifecycle sequence is not present in the selected owner record.]





## Commands

| Command | Kind | Identity field |
| --- | --- | --- |
| [RecordImportedOwnerResolution](../commands/imported-owner-resolution.md#recordimportedownerresolution) | creation | ResolutionId |

## Events

| Event | Discriminator | Version |
| --- | --- | --- |
| [ImportedOwnerResolutionRecorded](../events/imported-owner-resolution.md#importedownerresolutionrecorded) | [Placeholder: discriminator not recorded] | [Placeholder: value not recorded] |

## Cross-owner flows

[Placeholder: no choreography row names this owner.]

## Product rules

- **D04-02** — An imported named owner matches a workforce person only by exact normalized email; a name-only or ambiguous match remains an unresolved_owner reference that a person must resolve. Decision: [m0-d04-readiness-material.md](../decisions/m0-d04-readiness-material.md).

## Adopted architecture decisions

[Placeholder: no adopted architecture-decision reference is recorded.]

## Evidence and complete record

- Baseline source: [Placeholder: baseline source not recorded]
- Aggregate review: [Placeholder: no owner-specific review file is present]
- Full selected record: [JSON](../data/aggregates/imported-owner-resolution.json)

> Target definition is separate from runtime qualification. Implementation status recorded by the source: `PLANNED_NO_SOURCE_TYPE`.
