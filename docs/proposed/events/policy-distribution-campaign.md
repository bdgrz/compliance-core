# PolicyDistributionCampaign events

Canonical events: **7**. Complete canonical event and available schema records: [owner event JSON](../data/events/policy-distribution-campaign.json).

| Event | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- |
| PolicyCampaignAudienceAmended | business | bdgrz.policy_campaign.audience.amended | 2 | 13 | A policy distribution campaign's audience was reconciled against a later roster snapshot, adding or removing recipients without editing the frozen launch audience. |
| PolicyCampaignAudienceFrozen | business | bdgrz.policy_campaign.audience.frozen | 2 | 7 | The complete frozen launch audience. EntriesManifest holds every CampaignAudienceEntry in canonical person order, with no count cap. |
| PolicyCampaignClosed | business | bdgrz.policy_campaign.closed | 2 | 13 | The terminal closure of a campaign, appended by the closure process after every admitted participant result was applied and every participant use was held. TotalsContent is the complete CampaignTotalsView; ParticipantProofs the complete participant proof list. |
| PolicyCampaignClosureAccepted | business | bdgrz.policy-distribution-campaign.policy-campaign-closure-accepted | 1 | 8 | The root native acceptance of a campaign closure. It seals result admission on the open generation and freezes the complete admitted participant results and the exact participant uses to hold. |
| PolicyCampaignClosureCanceled | business | bdgrz.policy-distribution-campaign.policy-campaign-closure-canceled | 1 | 8 | The pending closure can definitely never complete. The cancellation fences every delayed local finalization of the generation and reopens result admission on the next generation. |
| PolicyCampaignLaunched | business | bdgrz.policy_campaign.launched | 2 | 19 | Launches a campaign bound to exact material and a frozen roster snapshot. The audience itself follows as one PolicyCampaignAudienceFrozen fact in the same batch. AudienceTeamsManifest is the complete original list of team names. |
| PolicyParticipantResultAdmitted | business | bdgrz.policy-distribution-campaign.policy-participant-result-admitted | 1 | 8 | The campaign admitted one complete participant result on its open generation. The campaign's logical revision advances to Revision; the participant applies the result afterwards from the frozen ChildAdmissionIntent. The root fact carries the original 202 acknowledgment. |

## Event details

### PolicyCampaignAudienceAmended

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:PolicyCampaignAudienceAmended |
| Owner selection | one exact canonical authority owner |
| Target concrete type | PolicyCampaignAudienceAmended |
| Adopted wire | `{"discriminator":"bdgrz.policy_campaign.audience.amended","version":2}` |
| Wire qualification | Adopted wire is the occupied discriminator declared in src/Compliance.Common/Features/PolicyDistribution/Events/PolicyCampaignAudienceAmended.cs at 60029b8f (2026-10-29 reconciliation). The earlier selected wire is retained as supersededSelectedWire for provenance only. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | `{"discriminator":"bdgrz.policy_campaign.audience.amended","version":1}` |
| Meaning | A policy distribution campaign's audience was reconciled against a later roster snapshot, adding or removing recipients without editing the frozen launch audience. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/PolicyDistribution/Events/PolicyCampaignAudienceAmended.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | PolicyCampaignAudienceAmended |
| Schema discriminator | bdgrz.policy_campaign.audience.amended |
| Schema discriminatorVersion | 1 |
| Schema owner | PolicyDistributionCampaign |
| Schema existingContract | src/Compliance.Common/Features/PolicyDistribution/PolicyCampaignAudienceAmended.cs |
| Schema status | selected target schema; implementation pending |
| Schema reason | source command accepted result |
| Schema changes | Externalize complete IReadOnlyList<CampaignAudienceAmendment> to verified immutable content; preserve all items/order/identity, no truncation. |
| Schema reducerRule | Validate owner/generation and local predecessor; apply only this owner delta or immutable content reference. Never hydrate a foreign aggregate or mutate based on a projection. |
| Schema serializer | Register concrete target event and every proof/content variant in ComplianceCoreJsonContext and attributed persistence preflight coverage. |
| Schema nativeResponseRule | FirstResponse is non-null only on this logical command first native accepting fact; it references FirstResponseDescriptor. OperationResult is non-null on the declared terminal business fact and references MutationResultContent. Both use verified content and static concrete result serializers; neither is reconstructed from current state. |

#### Event fields

| Field | Type | Required |
| --- | --- | --- |
| TenantId | Uuid | true |
| CampaignId | Uuid | true |
| Reconciliation | long | true |
| Batch | int | true |
| RosterSnapshotId | Uuid | true |
| RosterContentSha256 | string | true |
| ObservedOn | DateOnly | true |
| AmendmentsManifest | ImmutableContentRef | true |
| Actor | ActorReference | true |
| AmendedAt | DateTimeOffset | true |
| Operation | OperationProvenance | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

#### Nested content schemas

#### Content schema: PolicyCampaignAudienceAmended.AmendmentsManifest.v1

| Property | Value |
| --- | --- |
| field | AmendmentsManifest |
| schema | PolicyCampaignAudienceAmended.AmendmentsManifest.v1 |
| contentType | IReadOnlyList<CampaignAudienceAmendment> |
| rule | Full exact original collection with complete count and digest; readers verify content before use. |


[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### PolicyCampaignAudienceFrozen

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:PolicyCampaignAudienceFrozen |
| Owner selection | one exact canonical authority owner |
| Target concrete type | PolicyCampaignAudienceFrozen |
| Adopted wire | `{"discriminator":"bdgrz.policy_campaign.audience.frozen","version":2}` |
| Wire qualification | Adopted wire is the occupied discriminator declared in src/Compliance.Common/Features/PolicyDistribution/Events/PolicyCampaignAudienceFrozen.cs at 60029b8f (2026-10-29 reconciliation). The earlier selected wire is retained as supersededSelectedWire for provenance only. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | `{"discriminator":"bdgrz.policy_campaign.audience.frozen","version":1}` |
| Meaning | The complete frozen launch audience. EntriesManifest holds every CampaignAudienceEntry in canonical person order, with no count cap. |
| Meaning source | record summary in src/Compliance.Common/Features/PolicyDistribution/Events/PolicyCampaignAudienceFrozen.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | PolicyCampaignAudienceFrozen |
| Schema discriminator | bdgrz.policy_campaign.audience.frozen |
| Schema discriminatorVersion | 1 |
| Schema owner | PolicyDistributionCampaign |
| Schema existingContract | src/Compliance.Common/Features/PolicyDistribution/PolicyCampaignAudienceFrozen.cs |
| Schema status | selected target schema; implementation pending |
| Schema reason | existing native business fact, rehomed with its invariant |
| Schema changes | Externalize complete IReadOnlyList<CampaignAudienceEntry> to verified immutable content; preserve all items/order/identity, no truncation. |
| Schema reducerRule | Validate owner/generation and local predecessor; apply only this owner delta or immutable content reference. Never hydrate a foreign aggregate or mutate based on a projection. |
| Schema serializer | Register concrete target event and every proof/content variant in ComplianceCoreJsonContext and attributed persistence preflight coverage. |
| Schema nativeResponseRule | FirstResponse is non-null only on this logical command first native accepting fact; it references FirstResponseDescriptor. OperationResult is non-null on the declared terminal business fact and references MutationResultContent. Both use verified content and static concrete result serializers; neither is reconstructed from current state. |

#### Event fields

| Field | Type | Required |
| --- | --- | --- |
| TenantId | Uuid | true |
| CampaignId | Uuid | true |
| Batch | int | true |
| EntriesManifest | ImmutableContentRef | true |
| Operation | OperationProvenance | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

#### Nested content schemas

#### Content schema: PolicyCampaignAudienceFrozen.EntriesManifest.v1

| Property | Value |
| --- | --- |
| field | EntriesManifest |
| schema | PolicyCampaignAudienceFrozen.EntriesManifest.v1 |
| contentType | IReadOnlyList<CampaignAudienceEntry> |
| rule | Full exact original collection with complete count and digest; readers verify content before use. |


[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### PolicyCampaignClosed

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:PolicyCampaignClosed |
| Owner selection | one exact canonical authority owner |
| Target concrete type | PolicyCampaignClosed |
| Adopted wire | `{"discriminator":"bdgrz.policy_campaign.closed","version":2}` |
| Wire qualification | Adopted wire is the occupied discriminator declared in src/Compliance.Common/Features/PolicyDistribution/Events/PolicyCampaignClosed.cs at 60029b8f (2026-10-29 reconciliation). The earlier selected wire is retained as supersededSelectedWire for provenance only. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | `{"discriminator":"bdgrz.policy_campaign.closed","version":1}` |
| Meaning | The terminal closure of a campaign, appended by the closure process after every admitted participant result was applied and every participant use was held. TotalsContent is the complete CampaignTotalsView; ParticipantProofs the complete participant proof list. |
| Meaning source | record summary in src/Compliance.Common/Features/PolicyDistribution/Events/PolicyCampaignClosed.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | PolicyCampaignClosed |
| Schema discriminator | bdgrz.policy_campaign.closed |
| Schema discriminatorVersion | 1 |
| Schema owner | PolicyDistributionCampaign |
| Schema existingContract | src/Compliance.Common/Features/PolicyDistribution/PolicyCampaignClosed.cs |
| Schema status | selected target schema; implementation pending |
| Schema reason | typed reactor receiver accepted business fact |
| Schema changes | Externalize structured CampaignTotalsView including nested collections; a former item cap cannot become an event-size rejection for valid larger content. |
| Schema reducerRule | Validate owner/generation and local predecessor; apply only this owner delta or immutable content reference. Never hydrate a foreign aggregate or mutate based on a projection. |
| Schema serializer | Register concrete target event and every proof/content variant in ComplianceCoreJsonContext and attributed persistence preflight coverage. |
| Schema nativeResponseRule | FirstResponse is non-null only on this logical command first native accepting fact; it references FirstResponseDescriptor. OperationResult is non-null on the declared terminal business fact and references MutationResultContent. Both use verified content and static concrete result serializers; neither is reconstructed from current state. |

#### Event fields

| Field | Type | Required |
| --- | --- | --- |
| TenantId | Uuid | true |
| CampaignId | Uuid | true |
| ClosedOn | DateOnly | true |
| Rationale | string | true |
| TotalsContent | ImmutableContentRef | true |
| Actor | ActorReference | true |
| ClosedAt | DateTimeOffset | true |
| Operation | OperationProvenance | true |
| AudienceGeneration | long | true |
| ParticipantProofs | ImmutableContentRef | true |
| Intent | ImmutableContentRef | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

#### Nested content schemas

#### Content schema: PolicyCampaignClosed.TotalsContent.v1

| Property | Value |
| --- | --- |
| field | TotalsContent |
| schema | PolicyCampaignClosed.TotalsContent.v1 |
| contentType | CampaignTotalsView |
| rule | Complete original typed structured value, including nested collections, written/verified before this event. No cumulative parent view or mutable latest pointer. |

#### Content schema: closure-intent.v1

| Property | Value |
| --- | --- |
| field | Intent |
| schema | closure-intent.v1 |
| recordType | ClosureIntent |
| rule | Seal parent admission, drain all previously admitted materializations, then grant complete exact child/source uses. Nonempty action/response requirements and accepted dispositions remain policy-specific. An abort atomically fences parent finalization before any source settlement. |

##### Fields

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| ParentKind | ClosureParentKind | true |
| ParentId | Uuid | true |
| ParentGeneration | ulong | true |
| AdmittedChildren | ImmutableSelectionManifest | true |
| AuthoredDecision | ImmutableContentRef | true |
| SourceUses | ImmutableSelectionManifest | true |
| WaiverProof | ImmutableContentRef? | true |

##### Variant: ClosureIntent

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| ParentKind | ClosureParentKind | true |
| ParentId | Uuid | true |
| ParentGeneration | ulong | true |
| AdmittedChildren | ImmutableSelectionManifest | true |
| AuthoredDecision | ImmutableContentRef | true |
| SourceUses | ImmutableSelectionManifest | true |
| WaiverProof | ImmutableContentRef? | true |


#### Resolved intent schema

ClosureIntent

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [T038](../reactors/policy-campaign-closure-reactor.md#t038) | emitted semantic fact | PolicyDistributionCampaign | CompletePolicyCampaignClosure | PolicyDistributionCampaign |
| [T068](../reactors/policy-campaign-closure-reactor.md#t068) | logical prerequisite | PolicyDistributionCampaign | SettlePolicyCampaignParticipantCampaignClosureUse | PolicyCampaignParticipant |

### PolicyCampaignClosureAccepted

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:PolicyCampaignClosureAccepted |
| Owner selection | one exact canonical authority owner |
| Target concrete type | PolicyCampaignClosureAccepted |
| Adopted wire | `{"discriminator":"bdgrz.policy-distribution-campaign.policy-campaign-closure-accepted","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/PolicyDistribution/Events/PolicyCampaignClosureAccepted.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | The root native acceptance of a campaign closure. It seals result admission on the open generation and freezes the complete admitted participant results and the exact participant uses to hold. |
| Meaning source | record summary in src/Compliance.Common/Features/PolicyDistribution/Events/PolicyCampaignClosureAccepted.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | PolicyCampaignClosureAccepted |
| Schema discriminator | bdgrz.policy-distribution-campaign.policy-campaign-closure-accepted |
| Schema discriminatorVersion | 1 |
| Schema owner | PolicyDistributionCampaign |
| Schema existingContract | null (recorded) |
| Schema status | selected target schema; implementation pending |
| Schema reason | source command accepted result |
| Schema changes | None listed |
| Schema reducerRule | Validate owner/generation and local predecessor; apply only this owner delta or immutable content reference. Never hydrate a foreign aggregate or mutate based on a projection. |
| Schema serializer | Register concrete target event and every proof/content variant in ComplianceCoreJsonContext and attributed persistence preflight coverage. |
| Schema nativeResponseRule | FirstResponse is non-null only on this logical command first native accepting fact; it references FirstResponseDescriptor. OperationResult is non-null on the declared terminal business fact and references MutationResultContent. Both use verified content and static concrete result serializers; neither is reconstructed from current state. |

#### Event fields

| Field | Type | Required |
| --- | --- | --- |
| Owner | EventStreamAddress | true |
| Revision | long | true |
| Generation | long | true |
| Intent | ImmutableContentRef | true |
| SourceProofs | ImmutableContentRef? | true |
| Operation | OperationProvenance | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

#### Nested content schemas

#### Content schema: closure-intent.v1

| Property | Value |
| --- | --- |
| field | Intent |
| schema | closure-intent.v1 |
| recordType | ClosureIntent |
| rule | Seal parent admission, drain all previously admitted materializations, then grant complete exact child/source uses. Nonempty action/response requirements and accepted dispositions remain policy-specific. An abort atomically fences parent finalization before any source settlement. |

##### Fields

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| ParentKind | ClosureParentKind | true |
| ParentId | Uuid | true |
| ParentGeneration | ulong | true |
| AdmittedChildren | ImmutableSelectionManifest | true |
| AuthoredDecision | ImmutableContentRef | true |
| SourceUses | ImmutableSelectionManifest | true |
| WaiverProof | ImmutableContentRef? | true |

##### Variant: ClosureIntent

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| ParentKind | ClosureParentKind | true |
| ParentId | Uuid | true |
| ParentGeneration | ulong | true |
| AdmittedChildren | ImmutableSelectionManifest | true |
| AuthoredDecision | ImmutableContentRef | true |
| SourceUses | ImmutableSelectionManifest | true |
| WaiverProof | ImmutableContentRef? | true |


#### Resolved intent schema

ClosureIntent

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [T038](../reactors/policy-campaign-closure-reactor.md#t038) | physical root, logical prerequisite | PolicyDistributionCampaign | CompletePolicyCampaignClosure | PolicyDistributionCampaign |
| [T067](../reactors/policy-campaign-closure-reactor.md#t067) | physical root, logical prerequisite | PolicyDistributionCampaign | AuthorizePolicyCampaignParticipantCampaignClosureUse | PolicyCampaignParticipant |
| [T068](../reactors/policy-campaign-closure-reactor.md#t068) | physical root | PolicyDistributionCampaign | SettlePolicyCampaignParticipantCampaignClosureUse | PolicyCampaignParticipant |
| [A010](../reactors/unassigned-architecture-rows.md#a010) | physical root, logical prerequisite | PolicyDistributionCampaign | AbortPolicyCampaignClosure | PolicyDistributionCampaign |

### PolicyCampaignClosureCanceled

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:PolicyCampaignClosureCanceled |
| Owner selection | one exact canonical authority owner |
| Target concrete type | PolicyCampaignClosureCanceled |
| Adopted wire | `{"discriminator":"bdgrz.policy-distribution-campaign.policy-campaign-closure-canceled","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/PolicyDistribution/Events/PolicyCampaignClosureCanceled.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | The pending closure can definitely never complete. The cancellation fences every delayed local finalization of the generation and reopens result admission on the next generation. |
| Meaning source | record summary in src/Compliance.Common/Features/PolicyDistribution/Events/PolicyCampaignClosureCanceled.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | PolicyCampaignClosureCanceled |
| Schema discriminator | bdgrz.policy-distribution-campaign.policy-campaign-closure-canceled |
| Schema discriminatorVersion | 1 |
| Schema owner | PolicyDistributionCampaign |
| Schema existingContract | null (recorded) |
| Schema status | selected target schema; implementation pending |
| Schema reason | source admission contract |
| Schema changes | None listed |
| Schema reducerRule | Validate owner/generation and local predecessor; apply only this owner delta or immutable content reference. Never hydrate a foreign aggregate or mutate based on a projection. |
| Schema serializer | Register concrete target event and every proof/content variant in ComplianceCoreJsonContext and attributed persistence preflight coverage. |
| Schema nativeResponseRule | FirstResponse is non-null only on this logical command first native accepting fact; it references FirstResponseDescriptor. OperationResult is non-null on the declared terminal business fact and references MutationResultContent. Both use verified content and static concrete result serializers; neither is reconstructed from current state. |

#### Event fields

| Field | Type | Required |
| --- | --- | --- |
| Owner | EventStreamAddress | true |
| Revision | long | true |
| Generation | long | true |
| Intent | ImmutableContentRef | true |
| SourceProofs | ImmutableContentRef? | true |
| Operation | OperationProvenance | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

#### Nested content schemas

#### Content schema: closure-intent.v1

| Property | Value |
| --- | --- |
| field | Intent |
| schema | closure-intent.v1 |
| recordType | ClosureIntent |
| rule | Seal parent admission, drain all previously admitted materializations, then grant complete exact child/source uses. Nonempty action/response requirements and accepted dispositions remain policy-specific. An abort atomically fences parent finalization before any source settlement. |

##### Fields

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| ParentKind | ClosureParentKind | true |
| ParentId | Uuid | true |
| ParentGeneration | ulong | true |
| AdmittedChildren | ImmutableSelectionManifest | true |
| AuthoredDecision | ImmutableContentRef | true |
| SourceUses | ImmutableSelectionManifest | true |
| WaiverProof | ImmutableContentRef? | true |

##### Variant: ClosureIntent

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| ParentKind | ClosureParentKind | true |
| ParentId | Uuid | true |
| ParentGeneration | ulong | true |
| AdmittedChildren | ImmutableSelectionManifest | true |
| AuthoredDecision | ImmutableContentRef | true |
| SourceUses | ImmutableSelectionManifest | true |
| WaiverProof | ImmutableContentRef? | true |


#### Resolved intent schema

ClosureIntent

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [T068](../reactors/policy-campaign-closure-reactor.md#t068) | logical prerequisite | PolicyDistributionCampaign | SettlePolicyCampaignParticipantCampaignClosureUse | PolicyCampaignParticipant |
| [A010](../reactors/unassigned-architecture-rows.md#a010) | emitted semantic fact | PolicyDistributionCampaign | AbortPolicyCampaignClosure | PolicyDistributionCampaign |

### PolicyCampaignLaunched

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:PolicyCampaignLaunched |
| Owner selection | one exact canonical authority owner |
| Target concrete type | PolicyCampaignLaunched |
| Adopted wire | `{"discriminator":"bdgrz.policy_campaign.launched","version":2}` |
| Wire qualification | Adopted wire is the occupied discriminator declared in src/Compliance.Common/Features/PolicyDistribution/Events/PolicyCampaignLaunched.cs at 60029b8f (2026-10-29 reconciliation). The earlier selected wire is retained as supersededSelectedWire for provenance only. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | `{"discriminator":"bdgrz.policy_campaign.launched","version":1}` |
| Meaning | Launches a campaign bound to exact material and a frozen roster snapshot. The audience itself follows as one PolicyCampaignAudienceFrozen fact in the same batch. AudienceTeamsManifest is the complete original list of team names. |
| Meaning source | record summary in src/Compliance.Common/Features/PolicyDistribution/Events/PolicyCampaignLaunched.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | PolicyCampaignLaunched |
| Schema discriminator | bdgrz.policy_campaign.launched |
| Schema discriminatorVersion | 1 |
| Schema owner | PolicyDistributionCampaign |
| Schema existingContract | src/Compliance.Common/Features/PolicyDistribution/PolicyCampaignLaunched.cs |
| Schema status | selected target schema; implementation pending |
| Schema reason | source command accepted result |
| Schema changes | Externalize complete IReadOnlyList<string> to verified immutable content; preserve all items/order/identity, no truncation. |
| Schema reducerRule | Validate owner/generation and local predecessor; apply only this owner delta or immutable content reference. Never hydrate a foreign aggregate or mutate based on a projection. |
| Schema serializer | Register concrete target event and every proof/content variant in ComplianceCoreJsonContext and attributed persistence preflight coverage. |
| Schema nativeResponseRule | FirstResponse is non-null only on this logical command first native accepting fact; it references FirstResponseDescriptor. OperationResult is non-null on the declared terminal business fact and references MutationResultContent. Both use verified content and static concrete result serializers; neither is reconstructed from current state. |

#### Event fields

| Field | Type | Required |
| --- | --- | --- |
| TenantId | Uuid | true |
| ProgramId | Uuid | true |
| CampaignId | Uuid | true |
| Subject | CampaignSubject | true |
| AudienceKind | string | true |
| AudienceTeamsManifest | ImmutableContentRef | true |
| RosterSnapshotId | Uuid | true |
| RosterContentSha256 | string | true |
| LaunchedOn | DateOnly | true |
| DueOn | DateOnly | true |
| Instructions | string? | true |
| AcknowledgementText | string? | true |
| AudienceCount | int | true |
| Actor | ActorReference | true |
| ActorMemberId | Uuid | true |
| LaunchedAt | DateTimeOffset | true |
| Operation | OperationProvenance | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

#### Nested content schemas

#### Content schema: PolicyCampaignLaunched.AudienceTeamsManifest.v1

| Property | Value |
| --- | --- |
| field | AudienceTeamsManifest |
| schema | PolicyCampaignLaunched.AudienceTeamsManifest.v1 |
| contentType | IReadOnlyList<string> |
| rule | Full exact original collection with complete count and digest; readers verify content before use. |


[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### PolicyParticipantResultAdmitted

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:PolicyParticipantResultAdmitted |
| Owner selection | one exact canonical authority owner |
| Target concrete type | PolicyParticipantResultAdmitted |
| Adopted wire | `{"discriminator":"bdgrz.policy-distribution-campaign.policy-participant-result-admitted","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/PolicyDistribution/Events/PolicyParticipantResultAdmitted.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | The campaign admitted one complete participant result on its open generation. The campaign's logical revision advances to Revision; the participant applies the result afterwards from the frozen ChildAdmissionIntent. The root fact carries the original 202 acknowledgment. |
| Meaning source | record summary in src/Compliance.Common/Features/PolicyDistribution/Events/PolicyParticipantResultAdmitted.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | PolicyParticipantResultAdmitted |
| Schema discriminator | bdgrz.policy-distribution-campaign.policy-participant-result-admitted |
| Schema discriminatorVersion | 1 |
| Schema owner | PolicyDistributionCampaign |
| Schema existingContract | null (recorded) |
| Schema status | selected target schema; implementation pending |
| Schema reason | source command accepted result |
| Schema changes | None listed |
| Schema reducerRule | Validate owner/generation and local predecessor; apply only this owner delta or immutable content reference. Never hydrate a foreign aggregate or mutate based on a projection. |
| Schema serializer | Register concrete target event and every proof/content variant in ComplianceCoreJsonContext and attributed persistence preflight coverage. |
| Schema nativeResponseRule | FirstResponse is non-null only on this logical command first native accepting fact; it references FirstResponseDescriptor. OperationResult is non-null on the declared terminal business fact and references MutationResultContent. Both use verified content and static concrete result serializers; neither is reconstructed from current state. |

#### Event fields

| Field | Type | Required |
| --- | --- | --- |
| Owner | EventStreamAddress | true |
| Revision | long | true |
| Generation | long | true |
| Intent | ImmutableContentRef | true |
| SourceProofs | ImmutableContentRef? | true |
| Operation | OperationProvenance | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

#### Nested content schemas

#### Content schema: child-admission-intent.v1

| Property | Value |
| --- | --- |
| field | Intent |
| schema | child-admission-intent.v1 |
| recordType | ChildAdmissionIntent |
| rule | Concrete child payload schemas are the corresponding normalized Common requests: advisor feedback, auditor response/comment, corrective action, policy acknowledgement/training/waiver. Preserve all authored fields and original actor; child reducer never loads live parent. No later close/revoke discards admitted work. |

##### Fields

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| ParentId | Uuid | true |
| ParentGeneration | ulong | true |
| ChildKind | AdmittedChildKind | true |
| ChildId | Uuid | true |
| CanonicalAuthoredContent | ImmutableContentRef | true |
| SelectedSourceProofs | ImmutableSelectionManifest | true |
| ParentAdmissionRevision | ulong | true |

##### Variant: ChildAdmissionIntent

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| ParentId | Uuid | true |
| ParentGeneration | ulong | true |
| ChildKind | AdmittedChildKind | true |
| ChildId | Uuid | true |
| CanonicalAuthoredContent | ImmutableContentRef | true |
| SelectedSourceProofs | ImmutableSelectionManifest | true |
| ParentAdmissionRevision | ulong | true |


#### Resolved intent schema

ChildAdmissionIntent

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [T076](../reactors/policy-participant-result-reactor.md#t076) | physical root, logical prerequisite | PolicyDistributionCampaign | ApplyPolicyParticipantResult | PolicyCampaignParticipant |


## Wire/schema version discrepancies

- **PolicyCampaignAudienceAmended** — [Placeholder: reconcile schema version 1 with adopted version 2].
- **PolicyCampaignAudienceFrozen** — [Placeholder: reconcile schema version 1 with adopted version 2].
- **PolicyCampaignClosed** — [Placeholder: reconcile schema version 1 with adopted version 2].
- **PolicyCampaignLaunched** — [Placeholder: reconcile schema version 1 with adopted version 2].

[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]
