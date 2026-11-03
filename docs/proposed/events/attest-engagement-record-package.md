# AttestEngagementRecordPackage events

Canonical events: **4**. Complete canonical event and available schema records: [owner event JSON](../data/events/attest-engagement-record-package.json).

| Target event | Selected source name | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- | --- |
| AttestEngagementRecordPackageRequested | New proposed target fact | proposed_target_business_fact | No source wire selected | Not selected | Contract required | The firm requested the engagement record package needed to preserve its own attest engagement. |
| AttestEngagementRecordPackageGenerated | New proposed target fact | proposed_target_business_fact | No source wire selected | Not selected | Contract required | The exact attest engagement record package was generated successfully and is ready for delivery. |
| AttestEngagementRecordPackageGenerationFailed | New proposed target fact | proposed_target_business_fact | No source wire selected | Not selected | Contract required | Generation of the exact attest engagement record package failed; it has not been delivered. |
| AttestEngagementRecordPackageDelivered | New proposed target fact | proposed_target_business_fact | No source wire selected | Not selected | Contract required | The exact engagement record package was delivered to the firm’s audit software and its delivery was recorded. |

## Event details

### AttestEngagementRecordPackageRequested

| Canonical event detail | Value |
| --- | --- |
| Target event name | AttestEngagementRecordPackageRequested |
| Selected source event name | No selected source event type |
| Name decision | Names the completed request for the exact package the firm must retain. |
| Canonical semantic key | [Placeholder: value not recorded] |
| Owner selection | [Placeholder: value not recorded] |
| Target concrete type | [Placeholder: value not recorded] |
| Adopted wire | No source discriminator or version selected |
| Wire qualification | [Placeholder: value not recorded] |
| Schema status | [Placeholder: value not recorded] |
| Source status | TARGET_ONLY_NO_SELECTED_SOURCE_EVENT_TYPE_OR_WIRE |
| Adopted decision IDs | [Placeholder: value not recorded] |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | The firm requested the engagement record package needed to preserve its own attest engagement. |
| Selected source meaning | No source event meaning; see selected product rule. |
| Meaning source | [Placeholder: value not recorded] |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Target event contract

[Placeholder: define the event fields, wire discriminator, version, and emitting command before implementation.]

### AttestEngagementRecordPackageGenerated

| Canonical event detail | Value |
| --- | --- |
| Target event name | AttestEngagementRecordPackageGenerated |
| Selected source event name | No selected source event type |
| Name decision | Names successful generation of the exact engagement record package. |
| Canonical semantic key | [Placeholder: value not recorded] |
| Owner selection | [Placeholder: value not recorded] |
| Target concrete type | [Placeholder: value not recorded] |
| Adopted wire | No source discriminator or version selected |
| Wire qualification | [Placeholder: value not recorded] |
| Schema status | [Placeholder: value not recorded] |
| Source status | TARGET_ONLY_NO_SELECTED_SOURCE_EVENT_TYPE_OR_WIRE |
| Adopted decision IDs | [Placeholder: value not recorded] |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | The exact attest engagement record package was generated successfully and is ready for delivery. |
| Selected source meaning | No source event meaning; see selected product rule. |
| Meaning source | [Placeholder: value not recorded] |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Target event contract

[Placeholder: define the event fields, wire discriminator, version, and emitting command before implementation.]

### AttestEngagementRecordPackageGenerationFailed

| Canonical event detail | Value |
| --- | --- |
| Target event name | AttestEngagementRecordPackageGenerationFailed |
| Selected source event name | No selected source event type |
| Name decision | Names the completed failure to generate the exact engagement record package. |
| Canonical semantic key | [Placeholder: value not recorded] |
| Owner selection | [Placeholder: value not recorded] |
| Target concrete type | [Placeholder: value not recorded] |
| Adopted wire | No source discriminator or version selected |
| Wire qualification | [Placeholder: value not recorded] |
| Schema status | [Placeholder: value not recorded] |
| Source status | TARGET_ONLY_NO_SELECTED_SOURCE_EVENT_TYPE_OR_WIRE |
| Adopted decision IDs | [Placeholder: value not recorded] |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | Generation of the exact attest engagement record package failed; it has not been delivered. |
| Selected source meaning | No source event meaning; see selected product rule. |
| Meaning source | [Placeholder: value not recorded] |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Target event contract

[Placeholder: define the event fields, wire discriminator, version, and emitting command before implementation.]

### AttestEngagementRecordPackageDelivered

| Canonical event detail | Value |
| --- | --- |
| Target event name | AttestEngagementRecordPackageDelivered |
| Selected source event name | No selected source event type |
| Name decision | Names successful delivery of the exact package to the firm’s audit software. |
| Canonical semantic key | [Placeholder: value not recorded] |
| Owner selection | [Placeholder: value not recorded] |
| Target concrete type | [Placeholder: value not recorded] |
| Adopted wire | No source discriminator or version selected |
| Wire qualification | [Placeholder: value not recorded] |
| Schema status | [Placeholder: value not recorded] |
| Source status | TARGET_ONLY_NO_SELECTED_SOURCE_EVENT_TYPE_OR_WIRE |
| Adopted decision IDs | [Placeholder: value not recorded] |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | The exact engagement record package was delivered to the firm’s audit software and its delivery was recorded. |
| Selected source meaning | No source event meaning; see selected product rule. |
| Meaning source | [Placeholder: value not recorded] |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Target event contract

[Placeholder: define the event fields, wire discriminator, version, and emitting command before implementation.]




[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]
