# Person

- Classification: `business_aggregate`
- Source area: `Workforce` (derived from source path; not a bounded-context ruling).
- Business aggregate: `true`
- Selected status: `SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED`

## Identity and invariant

**Selected identity:** Tenant + caller PersonId; PersonId is exact immutable creation logical identity. Revise/correlation use caller CommandId and person revision.

**Invariant:** One recorded workforce person has immutable creation identity, revisioned contact content and at most one current platform membership correlation.

**Additional decision rule:** Person is owned solely by workforce assurance: access governance may correlate an observed account to a Person but never imports a competing person roster, and a platform member is a separate access relationship from the Person. A Person is the stable identity for someone in the workforce. It is separate from that person's employment record and their access to the platform.

**Must preserve:** [Placeholder: preservation rule not recorded.]

**Replay:** Current required authorization precedes replay; exact authorized retries resolve the original accepted result using the selected logical identity and immutable actor/intent/source binding. Fresh mutable feasibility, clock selection and body preparation cannot replace that original result. Expected revision is concurrency precondition, not a new identity.

**Lifecycle:** The proposed target has no separate event for ending or retiring a Person. WorkRelationship records employment status; Member records platform access. Changing contact details or platform access does not end the person's employment.

## Commands

| Command | Kind | Identity field |
| --- | --- | --- |
| [SetPersonTenantMemberLink](../commands/person.md#setpersontenantmemberlink) | command | CommandId |
| [RecordPerson](../commands/person.md#recordperson) | http | PersonId |
| [RevisePerson](../commands/person.md#reviseperson) | command | CommandId |

## Events

| Event | Discriminator | Version |
| --- | --- | --- |
| [PersonMemberLinkChanged](../events/person.md#personmemberlinkchanged) | bdgrz.workforce.person.membership-correlated | 2 |
| [PersonRecorded](../events/person.md#personrecorded) | bdgrz.workforce.person.recorded | 2 |
| [PersonRevised](../events/person.md#personrevised) | bdgrz.workforce.person.revised | 2 |

## Cross-owner flows

[Placeholder: no choreography row names this owner.]

## Product rules

- **D22-02** — Person and WorkRelationship are owned by workforce assurance; access governance correlates observed accounts to a person and must not import a competing person roster; a platform member is a separate access relationship. Source: [m0-d22-canonical-ownership.md](../../product/decisions/m0-d22-canonical-ownership.md).

## Adopted architecture decisions

[Placeholder: no adopted architecture-decision reference is recorded.]

## Evidence and complete record

- Baseline source: [Placeholder: source file is not present in this checkout]
- Aggregate review: [person-review-2026-10-29.md](../../product/definitive-aggregate-choreography/person-review-2026-10-29.md)
- Full selected record: [JSON](../data/aggregates/person.json)

> Target definition is separate from runtime qualification. Implementation status recorded by the source: `SEPARATE_VERIFICATION_REQUIRED_IMPLEMENTATION_HALTED`.
