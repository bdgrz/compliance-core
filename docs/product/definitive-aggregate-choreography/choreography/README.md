# Selected choreography and physical plan — source 8074fb02

All13 root/Jev decisions are adopted for target definition. Application/tooling implementation remains halted. The frozen predecessor evidence is unchanged; current source mappings and contract discrepancies are implementation evidence, not alternate target choices.

The graph contains **162 reaction/regular-stage rows +22 abort bindings +1 artifact personal admission +1 artifact scoped read =186 architecture rows**. The physical plan retains **65 existing registered reactors +2 planned process starters =67 target reactors** and **28 existing process definitions +2 planned process definitions =30 target definitions**. Reuse ProcessRootReaction, ProcessDriver, record store and worker ProcessSweep; no per-stage reactor expansion or new hosted worker type.

## Selected artifact path

`PUT /api/v1/tenants/{tenant_id}/evidence-artifacts/{artifact_id}/downloads/{command_id}` stabilizes existing IssueArtifactDownload with required CommandId/IIdentifiedRequest and all existing authored fields/guards. Current personal authorization precedes replay. EvidenceArtifact remains first owner; its native Requested fact atomically records the local available-artifact reservation and original fixed202. A future fact is never presented as a preappend source proof.

GrantId is UUIDv5 in ArtifactId namespace over UTF8 `artifact-download-grant:v1:{canonical TenantId}:{canonical CommandId}`. The actual Requested event starts one planned ArtifactDownloadRequestedReactor/ArtifactDownloadRequestedV1 workload and ArtifactDownloadProcess. Canonically acquire exact optional handoff + explicit/default handling-policy uses, independently verify the complete typed proof set at the frozen issue decision, write/verify encrypted original ticket, append receiving issuedv2, and settle all exact uses from issued/canceled native proof. Cancellation fences delayed issue before settlement. Missing content alone is not cancellation authority.

`GET /api/v1/tenants/{tenant_id}/evidence-artifacts/{artifact_id}/downloads/{command_id}` (GetArtifactDownloadGrant) derives the receiving identity locally. It requires the **same original personal actor AND current exact artifact/advisor rights**. Closed response union: Pending / issued / canceled. Issued returns original protected ticket bytes and original expiry, including an expired original; no renewal under the same root. There is no global operation lookup. Legacy artifact-issued v1 remains read-only historical compatibility and is never reinterpreted as a native receiving grant; new receiving issuance uses v2.

## Selected proof and identity rules

## Ordered tenant bootstrap

Supplemental edges S011–S016 are one serial `TenantRbacBootstrapReactor` sequence from the same `TenantRegistered` admission. Define the administrator role and team first. Register the owner only when `ownerBecomesAdministrator` is true. Then commit the administrator team-role link and the three core grants (`TenantAccess`, `TenantRbacManage`, `ProgramManage`). Only after those writes succeed may the reactor assign that owner to the administrators team; that `TeamMemberAssigned` fact can trigger tenant activation. A failed prerequisite prevents the activation-triggering assignment, and a failed assignment prevents the reactor checkpoint from completing. See the exact ordered plan in `physical-plan.json`.

## Selected proof and identity rules

Physical subscription trigger, original admission reference and logical prerequisite are separate. The continuation preserves admission/root identity but must independently materialize/verify every required actual typed intermediate/terminal proof and immutable binding before effect or early replay when receipt binding does not cover supplied terms. A pointer, process step, checkpoint or count is never authority. Existing complete manifest/source/tenant/program/actor/generation/fence guards remain mandatory.

Occupied v1 receiver UUID bytes and each process target/episode selector are frozen. Only the new unoccupied Artifact domain uses adopted effect-identity-v2: namespace actual committed Requested.Metadata.EventId; name bytes UTF8 `effect-identity:v2`, then unsigned32-bit big-endian UTF8 byte-length-prefixed fields in order: canonical receiver effect kind, canonical target EventStreamAddress, exact episode (`initial` when absent). UUIDv5 uses those exact bytes. Fixed vectors are required before implementation activation; no existing IDs are silently rewritten.

## Handoff files

- intended-choreography.json: selected121 logical edges, required proof sets, adopted policies, and counts for nested stages and target rows.
- supplemental-choreography.json: selected39 supplemental logical rows, including the planned rollover process root.
- abort-bindings.json: selected22 abort/cancel/rejection bindings.
- artifact-entry-read-rows.json: new selected P000 admission and Q000 scoped query.
- artifact-download-selected.json: complete producer/local-hold/foreign-use/issue/read/compatibility contract.
- physical-plan.json: exact existing65 registrations plus planned Artifact and ProgramRollover roots/processes, counts and worker plan.
- authority-policy.json / selected-decisions.json / adopted-decisions.json: adopted proof/identity policies and all13 decisions; no pending owner/protocol decision labels.

Names, owners, semantic facts and causal/proof rules are selected. Native field/wire migrations, missing runtime pieces, conformance tests and activation remain implementation work; this packet makes no runtime, merge or deployment claim.

The rollover execution sequence and proof gates are specified once in [the choreography catalog](../choreography.md).

The conditional tenant bootstrap dependency decision is specified once in [required choreography](../choreography.md).
