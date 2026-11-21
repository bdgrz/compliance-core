# Resource-oriented HTTP route audit (2026-11-21)

Status: advisory analysis against [ADR 0011](../../architecture/decisions/0011-resource-oriented-http-over-command-query.md).
No runtime changes. Supersedes the route counts in the 2026-11-19 gap analysis, which predates the
tenant-header and flat-identity commits (`9a3110a5`..`f39870e5`).

## Method

- Routes re-extracted from current source: **593** (256 GET, 272 PUT, 57 POST, 8 DELETE). 520 already select the tenant by `Tenant-Id` header.
- Six Haiku workers each audited about 99 routes against rubric codes R1–R13 and proposed a flat target. Per-route output: [resource-oriented-http-audit-2026-11-21.csv](resource-oriented-http-audit-2026-11-21.csv).
- Jev (`jev-1.14.0`, TypeSafe System One) judged the eight policy questions below. Request/response are not checked in. Treat probabilities as signals, not decisions.
- Haiku verdicts are triage, not proof. They did not inspect runtime status or bodies.

## Headline

| | Routes |
| --- | --- |
| No violation flagged | 193 (33%) |
| At least one violation | 400 (67%) |

| Code | Violation | Hits |
| --- | --- | --- |
| R4 | Action verb in path instead of `/status`, part PUT or relationship | 204 |
| R3 | `{command_id}`/`{operation_id}` retry token in the URL | 193 |
| R12 | Write declares a response body | 178 |
| R1 | Nested path for a child with its own identity | 123 |
| R5 | POST used | 57 |
| R13 | Tenant-scoped route without header (see caveats) | 25 |
| R2 | Owner/scope in path | 19 |
| R11 | Collection without pagination | 14 |
| R8 | Prefix/platform issue (partly noise) | 12 |
| R7 | Non-UUID version/revision/key in path | 11 |
| R10 | Verb/method mismatch | 4 |
| R6 | User-defined value in path | 3 |

## Flattening

232 of 593 paths carry two or more path parameters. Flattening the flagged routes yields roughly
**158 distinct collection roots** (vs 124 route families today), so flattening mostly adds roots
rather than shrinking the surface. Largest drivers: controls (32 of 41 routes flagged), applications
(23/26), auditor-requests (17/17), risks (14/20), service-engagements (14/16), system-descriptions
(11/11), boundaries (11/14), examinations (10/11), criteria-editions (10/11).

Proposed target method mix after remediation: 302 PUT, 198 GET, 20 DELETE, 0 POST.
Every POST (57) has a PUT/DELETE/GET replacement proposal.

## Jev judgments on the open design questions

Higher = Jev agrees with the proposition stated in the question.

| Question | Jev | Reading |
| --- | --- | --- |
| Flatten a nested child that has its own UUID (e.g. risk treatment action cancellation) | 0.89 | Yes, flatten. |
| Is `PUT .../status {approved}` enough for approval facts that carry approver and rationale | 0.11 | No. |
| Model such approvals as their own flat resource (`PUT /v1/control-draft-approvals/{approval_id}`) | 0.87 | Yes. Decide per transition: pure state change uses `/status`; an attributable, reviewable fact becomes a resource. |
| Read-only preview as a GET sub-resource | 0.68 | Probably fine; not decisive. |
| Tenant id in the path of `GET /v1/tenants/{tenant_id}` violates the rule | 0.35 | Probably not a violation; the tenant is the addressed resource. |
| Writes returning `OperationAcceptedResponse` body violate the rule | 0.92 | Yes. 41 PUTs declare it; other result types raise R12 to 178. |
| Binary upload justifies a no-POST exception vs `PUT /v1/evidence-artifacts/{artifact_id}` | 0.20 | No exception; use a caller-minted artifact id. |
| Attributable download should be a GET | 0.28 | Keep it a write (PUT). |

## Findings that need a human decision

1. **Approvals, reviews, validations, closures** are the crux. Haiku defaulted to `/status`; Jev says facts with a who/why need their own resource. Each of the ~200 R4 routes must be classified as status change or fact resource before flattening.
2. **R12 depends on Portia native-result config.** A declared `PortiaCreateOutcome<T>` or `*Registration` may already be mapped to 201+Location by `OnNativeResult`. Verify wire behavior before counting 178 as real.
3. **Version paths** (`audit-packages/{id}/versions/{package_version}`, `{rule_version}`, `{generation_key}`, `{build_key}`): need UUID `*_id` or an explicit fixed-vocabulary decision. `PUT /v1/controls/{identifier}` is a user-defined path value.
4. **`/v1/*-revision` and `*-version` GET roots** (client-service, commitment, control-draft) read like selector-by-query routes; confirm they fit.
5. **Auth and identity routes** (`/auth/*`, `/v1/oidc-user-sessions`, `/v1/my/*`, `/v1/tenants` POST, `/v1/suspensions`) need their own contract decision; the ADR does not cover them.

## Caveats and likely noise

- **R13 (25)** probably includes false positives: my extractor looked only at the first 600 characters of each registration for `Tenant-Id`. Re-check against source before acting.
- **R8 on `/v1/tenants/...`, `/v1/my/*` and `/auth/*`** is not a platform-prefix issue; the worker over-applied the rule. Haiku also proposed moving `GET /v1/tenants/{tenant_id}` under `/system`; Jev's 0.35 argues against.
- 15 routes are flagged yet proposed "OK" by the worker (mostly R12 on an otherwise conformant path).
- Haiku proposals are not validated for collisions between proposed roots.

## Suggested next step

Resolve finding 1 (status vs fact resource) with a short rule, then run a second pass per family
(controls, applications, auditor-requests first: about 70 flagged routes) to produce an
implementable rename map and verify R12/R13 against source.
