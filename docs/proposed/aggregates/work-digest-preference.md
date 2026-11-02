# WorkDigestPreference

- Classification: `technical_settings_state`
- Source area: `Work` (derived from source path; not a bounded-context ruling).
- Business aggregate: `false`
- Selected status: `SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED`

## Identity and invariant

**Selected identity:** Tenant / work-digest-preferences / MemberId. Selected CommandId binds the exact preference update; unchanged values still have an original recorded result under the adopted preference decision.

**Invariant:** One member stores current notification consent and valid scheduling time zone; identical current setting is no-op. This is chiefly a settings state holder, with no multi-part business invariant identified.

**Additional decision rule:** [Placeholder: no owner-specific decision rule is recorded.]

**Must preserve:** [Placeholder: preservation rule not recorded.]

**Replay:** Current required authorization precedes replay; exact authorized retries resolve the original accepted result using the selected logical identity and immutable actor/intent/source binding. Fresh mutable feasibility, clock selection and body preparation cannot replace that original result. Expected revision is concurrency precondition, not a new identity.

**Lifecycle:** [Placeholder: a consolidated lifecycle sequence is not present in the selected owner record.]

## Commands

| Command | Kind | Identity field |
| --- | --- | --- |
| [SetWorkDigestPreference](../commands/work-digest-preference.md#setworkdigestpreference) | [Placeholder: command kind not recorded] | [Placeholder: identity field not recorded] |

## Events

| Event | Discriminator | Version |
| --- | --- | --- |
| [WorkDigestPreferenceRecorded](../events/work-digest-preference.md#workdigestpreferencerecorded) | bdgrz.work.digest_preference.recorded | 1 |

## Cross-owner flows

[Placeholder: no choreography row names this owner.]

## Product rules

[Placeholder: product-rule traceability is not recorded for this owner.]

## Adopted architecture decisions

[Placeholder: no adopted architecture-decision reference is recorded.]

## Evidence and complete record

- Baseline source: [Placeholder: source file is not present in this checkout]
- Aggregate review: [Placeholder: no owner-specific review file is present]
- Full selected record: [JSON](../data/aggregates/work-digest-preference.json)

> Target definition is separate from runtime qualification. Implementation status recorded by the source: `SEPARATE_VERIFICATION_REQUIRED_IMPLEMENTATION_HALTED`.
