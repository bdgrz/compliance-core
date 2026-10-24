# Retained independence draft rules and previews

M0-D25 keeps each client organization as the tenant boundary. The platform rule
catalog lives in the `bdgrz` platform partition; it contains classifications and
source attribution, never client service facts. Rule `source_reference` values
must cite general policy requirements, not client identities, services or other
business records. Client-specific sources belong in the client service stream. Service and evaluation records
live in each client's `client-independence` stream. There is no firm tenant.

A current platform operator can author and read draft rule versions. Every
version remains explicitly unratified. A client's active member with the normal
organization administration permission can record and read that client's
service facts and evaluation previews. Operator status alone, standing
`firm_staff` membership and system actors grant no client history access.
Organization administration does not grant professional engagement access.

The contracts use the normal API-user authentication policy and snake_case:

| Operation | HTTP route | MCP tool |
| --- | --- | --- |
| Author draft rule version | `PUT /api/v1/platform/independence/rules` | `bdgrz.independence.rules.revise` |
| Read draft rule history | `GET /api/v1/platform/independence/rules` | `bdgrz.independence.rules.list` |
| Read draft rules for a client preview | `GET /api/v1/tenants/{tenant_id}/independence/rules` | `bdgrz.independence.client-rules.get` |
| Record immutable service facts | `PUT /api/v1/tenants/{tenant_id}/nonattest-services/record-nonattest-service/{service_record_id}` | `bdgrz.independence.service.record` |
| Admit evaluation preview (202) | `PUT /api/v1/tenants/{tenant_id}/client-independence-evaluations/evaluate-client-independence/{evaluation_id}` | `bdgrz.independence.evaluate` |
| Read client quality-review history | `GET /api/v1/tenants/{tenant_id}/independence/history` | `bdgrz.independence.history.get` |

Recording a service requires the exact `expected_service_epoch`, the number of
services already recorded for that client; evaluations never advance it. Service
records live on `ClientServiceIndependence`, one owner per client with the
complete ordered history and no count cap. An evaluation is admitted on the same
owner at the current epoch (`expected_service_epoch` and the exact draft
`rule_set_version`) and acknowledged with a fixed 202. Admission freezes the
complete service history of that epoch, the draft rule version content and its
digest, and the committed frontier of each source read; the preview is then
materialized on its own `ClientIndependenceDecision` owner by
`ClientIndependencePreviewProcess` from that snapshot only. Request identities
retain their original intent and actor. Retrying an identical committed request
does not append events; reusing an identity for another intent or actor returns
a conflict. Historical previews retain their complete rule snapshot and all
client service facts considered, including services outside the look-back,
alongside the IDs actually classified inside the window. Later draft rule or
service additions do not rewrite them. The history read lists a preview once the
process has materialized it.

An evaluation computes the Compliance-owned policy and retains compatible,
conditionally compatible, impaired and unclassified outcomes. Impaired service
has no override. A readiness-only result still requires partner evaluation;
these preview operations cannot supply it. A draft rule version that is not the
exact requested one returns a transient conflict before anything is accepted.
The preview remains available in client history with its original version; it
never grants acceptance. This provides retained evidence, not cross-stream
atomic engagement acceptance.

The look-back is at least twelve months. Management-function classifications
must be impairing. Complete histories and previews are immutable content
referenced by the facts, so no service or preview count is capped and nothing is
truncated; the platform draft rule catalog still supports up to one hundred
versions. Reads use authoritative event-stream hydration, so no asynchronous
directory or projection can omit service facts from an evaluation.

Firm ratification evidence remains owned by the quality-management partner in
[#343](https://github.com/bdgrz/compliance/issues/343). Every preview returns
`production_acceptance_blocked: true`; no API or MCP input can turn a draft
version into ratified rules. Engagement creation, partner sign-off, management
acknowledgement, acceptance, assignment, revocation and record compartment
filters remain the separate consuming work in
[#269](https://github.com/bdgrz/compliance/issues/269) and
[#277](https://github.com/bdgrz/compliance/issues/277).
