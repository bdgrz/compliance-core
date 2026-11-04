# Reviewed requirement coverage

Tracking: [#1176](https://github.com/bdgrz/compliance-core/issues/1176).

This is a source-backed delivery ledger against the reviewed proposal, not a
replacement specification. The first mapping covers Platform access, Program and
scope, Firm services, Workforce assurance and Work queue at `1186ff1f`, plus the
bounded Person projection identity repair. Other contexts still need this mapping.
The reviews below did not execute tests. Named tests are evidence to qualify,
not proof of current GREEN, complete workflows or merged acceptance. No context
is declared complete by this ledger, and SQL migration does not close business gaps.

## Platform access

Paths below are under `Features/AccessControl` unless another feature is named.

| Requirement | Source evidence / implemented behavior | Remaining acceptance |
| --- | --- | --- |
| PA-01 | Separate role, permission, team-role and team-member owners; team paths in `PermissionProjectionState`; `PermissionProjectionStateTests` | Admit and authorize usable custom organization roles; cut over direct client member grants. |
| PA-02 | `TeamRole` owns independent team-role assignment episodes; `TeamRoleTests` | Scope is absent from owner identity, contracts and permission edges. Deliver scope and authorization together. |
| PA-03 | `Member` enforces finite guest expiry; `MemberLifecycleTests` | Membership episode must own allowed programs and cap every team-derived permission, replacing reliance on legacy Viewer grants. |
| PA-04 | Small membership/role/team owners and built-in team protections exist | Primary continuity absent; move fixed-ID protections into owners as required by PA-18/27. |
| PA-05 | Tenant creator/member/Administrators bootstrap; `TenantRbacBootstrapReactorTests` | First-primary outcome absent; activation still waits on bootstrap rather than only slug outcome. Depends on PA-27. |
| PA-06 | `FirmServices/FirmStaffRecord` plus identity-claim process; race tests | Replace caller StaffMemberId/claim authority with the specified UserId-owned staff entry and wire registration/status/reads. |
| PA-07 | Separate staff directory, professional duty and engagement independence owners | Align older signed-duty/engagement behavior with Firm services requirements; directory migration alone is insufficient. |
| PA-08 | `UserIdentities/UserIdentity` records observations as `AuditEvent`; `UserIdentityObservationTests` asserts separate audit stream and unchanged replay/authority | No concrete source gap found; execute existing regressions at the acceptance head. DomainEvent inheritance is not evidence of missing audit behavior. |
| PA-09 | Projection and final authoritative Member eligibility checks; `PermissionProjectionStateTests`, `FitzPermissionAuthorizerTests`, Work oversight cases | Retain episode checks through scoped/custom-role changes; qualify specified firm-staff affiliation behavior. |
| PA-10 | Existing EmailAddress challenge and older multi-stage IdentityRecovery | Implement specified PlatformUser/SignInIdentity attachment, same-user email recovery, all-old-identities revocation and notice. |
| PA-11 | Globally unique EmailAddress owner | Reservation currently precedes verification. Deliver verified claim and committed claim-denial choreography. |
| PA-12 | Legacy access-grant scope validation; protected Administrators link in handler | TeamRole lacks specified scope and role/scope exclusions. Deliver with PA-02 and owner/replay refusals. |
| PA-13 | Team validates its own name without a name-claim owner | Qualify explicit duplicate-name acceptance for two teams; team rename remains a separate body requirement. |
| PA-14 | `FirmStaffAdministrationAuthorizer` checks current operator; staff status/transport tests | Preserve gate across PA-06; identify/execute production nonoperator denial acceptance. |
| PA-15 | Member suspension/reinstatement retains teams; deprovisioning ends an episode; lifecycle and permission tests | Qualify existing behavior and replace continuity restrictions only after PA-27. |
| PA-16 | TenantSlug emits success/rejection and reactor maps both; `TenantSlugRecoveryTests` | Qualify denied-first-slug recovery, taken/retired reasons and exact reviewed claim vocabulary with PA-21. |
| PA-17 | Role lifecycle, permission edge and team-role edge are separate owners | Add scope, known-permission admission, custom roles and complete fixed built-in protections. |
| PA-18 | Role deletion protects built-ins; `RoleTeamContentOperationTests` | Rename and permission edits still admitted. Enforce all specified built-in immutability locally and admit only known permissions. |
| PA-19 | Deleted-team/role final checks deny immediately; RoleCleanup removes permission and team edges | TeamCleanup currently removes members only. Add episode-bound team-role cleanup and retain immediate-denial/race cases. |
| PA-20 | PlatformOperatorRoster owns last-operator continuity; tests cover last removal, restart and competing revocations | No missing invariant found. Execute at acceptance head; a naming difference does not justify splitting authority. |
| PA-21 | Slug rejection facts and active-tenant retry cases exist | Registered creator retry blocked by active-tenant owner/authorization gates. Deliver rejected first claim, creator retry, second outcome and foreign-user refusal. |
| PA-22 | Member reports live-episode conflict | Invitation reactor deliberately continues on that conflict; existing tests assert contrary behavior. Refuse active/suspended membership invitations authoritatively. |
| PA-23 | TenantManagerGuard currently protects last ordinary administrator | Superseded rule. After PA-27, protect primary seats only and permit ordinary-admin departure/suspension/removal. |
| PA-24 | Withdrawn requirement | No separate advisory repair feature; preserve PA-20/27 authority. |
| PA-25 | TenantSlug terminology and owner are present | Coordinate exact claim/retire vocabulary with PA-16; do not add another owner. |
| PA-26 | Tenant offboarding start/suspend/reactivate exist | Completion contract has no Core behavior/handler. Requires authoritative open/accepted engagement outcomes, export receipt and attest package delivery; no projection-only admission. |
| PA-27 | No primary-seat owner or member primary mark found | Implement one/two seat invariant, pending/accepted participant outcomes, bootstrap, designation/release/unmark, derived authority and public/query/JSON wiring. |
| Body: team rename | No RenameTeam/TeamRenamed implementation found | Active-team rename, deleted-team refusal, exact retry/replay and list/read/API wiring. |
| Body: SoD waiver revocation | Request and independent HTTP approval exist; waiver/personal approval tests | Revocation owner/fact/command, query/proof invalidation, expiry/state/retry/replay cases. |

Dependency-ready candidates are rejected-first-slug recovery, team rename,
team-role housekeeping and waiver revocation. Scoped/custom roles and primary
continuity each need a bounded complete workflow. Offboarding completion remains
dependent on named facts from other contexts.

## Program and scope

| Requirement | Source evidence / implemented behavior | Remaining acceptance |
| --- | --- | --- |
| PS-01 | `Boundaries/SystemBoundary` has owner-scoped history, keyed by independent BoundaryId; CreateBoundary checks Program existence | Enforce one authoritative boundary owner per tenant/program, including competing creation and exact retry/changed intent. |
| PS-02 | Legacy ProgramRollover owner/routes and tests remain | Deliver specified manual next-examination/Type II plan flow with carried-forward items, then remove obsolete rollover exposure. No rollover reactor is required. |
| PS-03 | Program directory derives Type I display stage from approved readiness; ProgramStageProjectionTests | Program must authoritatively record stage/attribution/rationale and exact readiness/outcome source; Type II transition absent. |
| PS-04 | Separate personal HTTP ReviewBoundary and ApproveBoundary; stale revision and waiver tests | Preserve every draft author for independence; one exact-revision decision must approve or request changes with required effective date. Current check uses latest author only. |
| PS-05 | Discard owner/handler exists and approved successor history is retained | Current owner rejects ever-reviewed drafts, including request_changes. Allow any never-approved draft to be discarded; preserve approved history. |
| PS-06 | ClientService owner binds permanent Program and terminal retirement; ProductClientServiceNativeHistoryTests covers rebind/mutation/retry refusals | Qualify real bus/actor behavior and competing appends. Native SQL migration is a separate gate. |
| PS-07 | Generic assignments dispatch control scopes to ControlOperations; boundary still owns assignment/revocation facts | Deliver specified ControlResponsibility effective tenure and work consumers; remove obsolete boundary responsibility ownership during coordinated cutover. |
| Body: criteria at creation | Program criteria selection exists after creation | Require exactly one published criteria edition at creation and qualify actor/source authority. |
| Body: target dates | ProgramRules validates adjacent target-date pairs | Compare all present dates across null intermediates; named changes must preserve ordering and retries. |
| Body: resolved scope | Boundary draft/version/source checks exist | Exact-version references and provider resolution remain incomplete; qualify source authority and actor matrix. |

Small independent repairs include target-date ordering, all-author review
independence and discard after unapproved review. The held five-store SQL slice
preserves existing behavior; it does not implement these requirements. Its
additional row/body identity acceptance item is recorded in the migration handoff.

## Firm services

Current implementations are primarily under `Features/AccessControl`.

| Requirement | Source evidence / implemented behavior | Remaining acceptance |
| --- | --- | --- |
| FS-01 | IndependenceCompartments computes from retained services/rules; policy/evaluation tests | Target evaluation owner/one approval/exact service revisions absent. Current calculation excludes services starting during the examination period. |
| FS-02 | RecordNonattestService handler and complete retained history; ClientServiceIndependenceTests | Independent per-record owner, reasoned revision and void, current-history queries and replay. |
| FS-03 | Acceptance admission checks current rules/client epoch and retained source cut; stale-input tests | Replace superseded epoch/reservation coupling with approved exact service-revision freshness after new record/evaluation owners exist. |
| FS-04 | EngagementAcceptancePolicy immediately allows advisory; a test asserts unconditional allowance | Engagement terms lack service kind. Add target terms and authoritative refusal of impairing advisory acceptance while an attest engagement is accepted/open. |
| FS-05 | Existing assignment admission refuses draft engagements; ServiceEngagementStaffAssignmentTests | Independent assignment owner with request/answer/completion/end/re-request lifecycle; implement with FS-13. |
| FS-06 | Close command admits draft only; accepted engagement can close through Revoke | Deliver own-attest package first, then accepted-to-closed gate, actual end date and assignment-ending reactions; remove revocation bypass. |
| FS-07 | Professional duty designations/signed assertions and current partner checks exist | Target operator-controlled PartnerDesignation(UserId,duty) absent. Carry active-staff check and wire practice partner actions/quality-partner rule drafting. |
| FS-08 | Rule catalog/ratification/current reader and governance tests | Historical ratification currently disappears with ratifier status/duty lineage. Implement immutable version owner, mutable draft and highest-ratified selection under target designations. |
| FS-09 | Personal HTTP management acknowledgement checks current client-personnel authority; transport tests | Move acknowledgement into target evaluation lifecycle and preserve current Org Admin authority through access cutover. |
| FS-10 | Current source-use reservations, epochs, catalog holds and multi-stage processes | Replace superseded coupling per target capability; existing workflow names do not establish approved small-owner completion. |
| FS-11 | Impairing service truth can be recorded; current professional access broadly denies on source changes | Correct examination-window overlap, void-aware history, computed engagement impairment, assignment refusal and Examinations outcome gate. Recording truth must remain allowed. |
| FS-12 | ProfessionalDutyAuthorityReader requires active staff/current designation; ProfessionalDutyChangeTests | Carry active-status behavior to target designation while preserving historical ratification after duty/status changes. |
| FS-13 | Permanent practice commitment and competing-practice tests exist | Replace dual staff/user reservations with UserId-owned ClientPracticeCompartment and explicit admitted/refused answers; coordinate independent assignment and close/completion races. |
| Body: engagement lifecycle | Current draft content has practice/scope/period/lead/boundary and native history | Add service kind; proposal authority must be practice-matched firm staff, not client RBAC management. Target decline/accept/close lifecycle and public contracts remain. |
| Body: advisory service reactions | Nonattest record append exists | Accepted advisory engagement records a service with engagement ID; closure revises actual end; retries remain exact. |
| Body: rules and initial content | Current rule set is a value object/catalog revision | Target version aggregate and initial service classifications/12-month rule behavior need implementation evidence. |
| Body: attest record package | Common OwnAttestRecordPackage contracts/events/JSON exist | No Core feature. Implement request/build success/failure/delivery and exact version manifest; generic AuditPackageDelivery does not satisfy own-attest delivery. |
| Body: cross-context gates | Retention holds and audit-package outcome checks exist | Own-attest delivery must gate engagement close, offboarding and hold release; Examinations needs engagement identity and impairment gate. |
| Body: inactivity and reads | Current status reconciliation and compartment read guards exist | End independent assignments on inactivity/close, including delayed completion; preserve permanent compartment and prove team permissions cannot bypass read boundaries. |

Independent nonattest records, partner designations and own-attest package
lifecycle can be prepared separately. Evaluation/rule acceptance depends on their
source owners. Assignment/compartment admission is one coordinated capability.

## Workforce assurance

Named command paths below have source/test evidence, not whole-context acceptance.

| Requirement | Source evidence / implemented behavior | Remaining acceptance |
| --- | --- | --- |
| WF-01 | WorkRelationship terminal latch and worker-identifier identity; provenance/lifecycle tests | Qualify explicit end-to-new-worker-identifier rehire journey and producer paths. |
| WF-02 | Person has permanent identity and no ending lifecycle; PersonNativeIdentityTests/PersonTests | Preserve permanent identity through full member-link/source-application workflows. |
| WF-03 | Named start/leave/return/end/manager/department/date correction owners, handlers and public wiring; lifecycle/named-change tests | Full actor/source conformance and receiving workflows remain; date correction must not reactivate ended relationships. |
| WF-04 | ServiceIdentity named review records actor/next date; owner/Lead HTTP checks and changed-owner race test | Qualify full current-owner/review workflow and receiving read/work surfaces at acceptance head. |
| WF-05 | Singular CorrelatedUserId replacement and duplicate reconciliation reporting exist | Several member links per Person plus authoritative member-to-one-Person admission; Org Admin-only named link/unlink facts/commands and coordination. Duplicate discovery is not admission. |
| WF-06 | Sponsor naming checks active internal relationship; later departure produces unsponsored read/work gap | Naming eligibility currently relies on a caught-up projection. Sponsor-ending versus naming authoritative admission remains unproved; retain later-departure gap behavior. |
| WF-07 | Immutable observation identity/revision and one attributed accepted/dismissed decision; source-observation tests | Consolidate separately live derived observation-resolution workflow; preserve bounded notes, retry and restricted-field handling. |
| WF-08 | Source classification/preview and manual canonical correction exist | Accepted HRIS application reactor, authoritative newer-accepted revision fence, stable new Person/relationship IDs, unmatched observation/matching and authorized system producer. Current tests require manual correction first and do not prove target application. |
| Body: Person writer | Named ChangePersonDetails emits only actual changes; native immutable content tests | Generic RevisePerson still writes unconditional revisions. Retire generic writer while retaining required historical readability/receipts. |
| Body: provenance | Worker identifier owns relationship; external IDs are immutable provenance | Qualify source application without replacing worker identity with an external source ID. |
| Body: ServiceIdentity lifecycle | Supported kinds, accountable owner, purpose, annual review, shared-account Person/justification, disable/enable/terminal retire; named handler/owner tests | Keep full invariant/actor/replay qualification explicit; lifecycle names alone do not complete context. |
| Body: owner/review gaps | ServiceIdentityOwnership/Accountability and native directory derive unowned work | Qualify receiving surfaces and keep disabled/retired account behavior distinct from owner/review gaps. |
| Body: privacy | Lists/frozen rosters redact restricted fields; detail grants and manager-chain checks tested | Exhaustive export/search/count coverage belongs to receiving surfaces and remains open. |
| Body: receiving reactions | Named joiner/mover/leaver facts are produced | Policies and External access named-event consumers were not found; implement receiving effects in those contexts. |
| Body: import | Legacy roster snapshots exist | Import/snapshot replacement remains explicitly deferred under #347; snapshots are not import completion. |

Person generic-writer retirement is small and independent. Member links require
one coherent uniqueness capability; source application requires revision ordering,
matching and authoritative application outcomes together.

## Work queue

| Requirement | Source evidence / implemented behavior | Remaining acceptance |
| --- | --- | --- |
| WQ-01 | Compose infers system escalation from seven days overdue; manual escalation persists | Scheduler must record one deterministic escalation fact, preserve assignee and no-op on repeated delivery. Computed display state is not a recorded once-only outcome. |
| WQ-02 | Live dispatch freezes stable message identity; Unknown currently blocks retry and needs operator authorization | Automatically retry unknown delivery with same message identity, current eligibility and local-week fence; remove operator approval from normal recovery. |
| Body: source-owned work | WorkSource and projectors compose open candidates; stale-source/cancellation/source-action tests | Qualify each proposed source opening/completion/reopen/disagreement; held Finding closure is not integrated. |
| Body: source round identity | Per-item assignment owner; several review keys include revision | Qualify canonical source-round mapping and new-round isolation. Some legacy decision keys include assigned member, so UUID use alone proves no canonical target mapping. |
| Body: accountability | Assignment/claim/delegation check current eligibility; assignment itself grants no source authority | Complete target actor matrix/source-owner cutovers and competing-claim cases; retain loss-of-eligibility checks. |
| Body: attribution/replay | Native assignment receipts/facts preserve original results and renewed authorization | Legacy-history import remains emitted/accepted although target excludes it; complete target writer cutover. |
| Body: preferences | Self-owned zone/default UTC, invalid-zone refusal, current membership and email opt-out preserve reminders | Execute target actor/receipt acceptance; no replacement preference owner is currently justified. |
| Body: local weekly schedule | Live sender has Monday 09:00 local, frozen zone/week and no-backfill tests | Separately registered weekly command path uses UTC scheduling and is not connected to the live SMTP path. Deliver one coherent target path. |
| Body: final send checks | Actual WorkDigestContentReader rechecks membership, verified email, preference and visible work | Connect selected weekly owner to that content/sender path; preserve failure and empty-content handling. |
| Body: weekly result | Stable member/week identity and multiple legacy attempt/outcome states exist | Target owner must record sent/skipped only with item keys, destination, frozen zone and reason; attempts stay technical. |

Storage migration remains separate. The current per-item assignment owner is real;
its legacy imports and generic responsibility dependencies still need cutover.
