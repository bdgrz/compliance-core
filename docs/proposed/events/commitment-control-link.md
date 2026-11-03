# CommitmentControlLink events

Canonical events: **3**. Complete canonical event and available schema records: [owner event JSON](../data/events/commitment-control-link.json).

| Target event | Selected source name | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- | --- |
| CommitmentControlLinkProposed | CommitmentControlLinkProposed | business | [Placeholder: value not recorded] | [Placeholder: value not recorded] | [Placeholder: schema absent] | A link between an exact commitment version and an exact control version was proposed with rationale, awaiting independent review. |
| CommitmentControlLinkRetired | CommitmentControlLinkRetired | business | [Placeholder: value not recorded] | [Placeholder: value not recorded] | [Placeholder: schema absent] | A commitment-to-control link was retired; the pair no longer supports the commitment and no successor can revive this link identity. |
| CommitmentControlLinkReviewed | CommitmentControlLinkReviewed | business | [Placeholder: value not recorded] | [Placeholder: value not recorded] | [Placeholder: schema absent] | An independent reviewer accepted or returned the proposed commitment-to-control link version; acceptance makes it the current link. |

## Event details

### CommitmentControlLinkProposed

| Canonical event detail | Value |
| --- | --- |
| Target event name | CommitmentControlLinkProposed |
| Selected source event name | CommitmentControlLinkProposed |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:CommitmentControlLinkProposed |
| Owner selection | one exact canonical authority owner |
| Target concrete type | CommitmentControlLinkProposed |
| Adopted wire | [Placeholder: value not recorded] |
| Wire qualification | Planned; no declared record yet. |
| Schema status | planned_schema |
| Source status | planned_no_source_type |
| Adopted decision IDs | OWNER_CommitmentControlLink |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | A link between an exact commitment version and an exact control version was proposed with rationale, awaiting independent review. |
| Selected source meaning | A link between an exact commitment version and an exact control version was proposed with rationale, awaiting independent review. |
| Meaning source | authored 2026-10-29 |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### CommitmentControlLinkRetired

| Canonical event detail | Value |
| --- | --- |
| Target event name | CommitmentControlLinkRetired |
| Selected source event name | CommitmentControlLinkRetired |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:CommitmentControlLinkRetired |
| Owner selection | one exact canonical authority owner |
| Target concrete type | CommitmentControlLinkRetired |
| Adopted wire | [Placeholder: value not recorded] |
| Wire qualification | Planned; no declared record yet. |
| Schema status | planned_schema |
| Source status | planned_no_source_type |
| Adopted decision IDs | OWNER_CommitmentControlLink |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | A commitment-to-control link was retired; the pair no longer supports the commitment and no successor can revive this link identity. |
| Selected source meaning | A commitment-to-control link was retired; the pair no longer supports the commitment and no successor can revive this link identity. |
| Meaning source | authored 2026-10-29 |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### CommitmentControlLinkReviewed

| Canonical event detail | Value |
| --- | --- |
| Target event name | CommitmentControlLinkReviewed |
| Selected source event name | CommitmentControlLinkReviewed |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:CommitmentControlLinkReviewed |
| Owner selection | one exact canonical authority owner |
| Target concrete type | CommitmentControlLinkReviewed |
| Adopted wire | [Placeholder: value not recorded] |
| Wire qualification | Planned; no declared record yet. |
| Schema status | planned_schema |
| Source status | planned_no_source_type |
| Adopted decision IDs | OWNER_CommitmentControlLink |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | An independent reviewer accepted or returned the proposed commitment-to-control link version; acceptance makes it the current link. |
| Selected source meaning | An independent reviewer accepted or returned the proposed commitment-to-control link version; acceptance makes it the current link. |
| Meaning source | authored 2026-10-29 |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]




[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]
