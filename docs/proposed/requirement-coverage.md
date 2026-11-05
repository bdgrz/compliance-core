# Reviewed requirement coverage

Tracking: [#1176](https://github.com/bdgrz/compliance-core/issues/1176).

This is a source-backed delivery ledger against the reviewed proposal, not a
replacement specification. The first mapping covers Platform access, Program and
scope, Firm services, Workforce assurance, Work queue, Control environment, Firm
methodology and Assurance work. The source baseline is `cbaaba5f` plus the bounded
current-authorization repairs. Other contexts still need this mapping.
The reviews below did not execute tests. Named tests are evidence to qualify,
not proof of current GREEN, complete workflows or merged acceptance. No context
is declared complete by this ledger, and SQL migration does not close business gaps.

## 2026-11-05 criteria authority decision

[CR-09/10](contexts/criteria.md#decisions) lock independent criterion owners,
bounded draft admission windows, exact participant outcomes/fencing and a verified
published source cut. No complete edition is stored in one manifest or aggregate. This is design, not implemented behavior. The
static catalog remains data only; preserve its fixed ID and partial-coverage
warnings until a source-backed publication migration is qualified. Program
creation must select a published edition. Historical handoffs remain evidence of
their revision, not evidence that the updated design is already implemented.

## 2026-11-05 organization bootstrap supplement

[Organization creation and primary administration](reviews/organization-bootstrap.md)
supersedes older PA-04/05/21/23/27 rows that say primary seats, creator designation,
registered-tenant slug recovery or the ordinary-admin guard cutover are absent.
Creation now requires a client-created tenant ID on HTTP and MCP; exact retry
returns the original registration and changed intent conflicts. Both source
activation orderings and membership/primary/access reads are exercised. Membership
views now retain guest caps, resolving that source-view gap from the scoped-access
checkpoint. The controlled feature tests pass; native/full acceptance remains
blocked as documented in the review. Other requirements are not promoted to done.

## 2026-11-05 scoped-access supplement

The [scoped-team checkpoint](reviews/scoped-team-access.md) supersedes the older
PA rows below where they say custom/scoped roles, guest episode caps or direct-grant
cutover are absent. Those source paths are now integrated and the 296-case focused
selection passes. Platform access remains partial: command-side reference admission,
raw membership cap views, native/public transport/full-suite acceptance, primary
continuity, housekeeping and the other named requirements remain. Broader failures
are recorded in #1189; the native blocker remains #1188. Other historical rows are
not silently promoted to completed acceptance.

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
| Body: target dates | ProgramRules now validates all six present date pairs; 6 intended RED failures, then 65 focused/native/history/HTTP cases passed | Full gate pending. Named target-change command/fact remains separate; old invalid historical plans are unsupported under the no-backcompat scope. |
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
| Body: Person writer | Named ChangePersonDetails emits only actual changes; generic writer/public HTTP/MCP removal is assembled, with uniform typed refusal and historical V1/V2 decoding retained | Retirement observed RED (7 unit cases plus removed-route check); assembled full gate passed 8,707 units. Exact-head CI/merge are separate delivery gates. Member links/source application are separate gaps. |
| Body: provenance | Worker identifier owns relationship; external IDs are immutable provenance | Qualify source application without replacing worker identity with an external source ID. |
| Body: ServiceIdentity lifecycle | Supported kinds, accountable owner, purpose, annual review, shared-account Person/justification, disable/enable/terminal retire; named handler/owner tests | Keep full invariant/actor/replay qualification explicit; lifecycle names alone do not complete context. |
| Body: owner/review gaps | ServiceIdentityOwnership/Accountability and native directory derive unowned work | Qualify receiving surfaces and keep disabled/retired account behavior distinct from owner/review gaps. |
| Body: privacy | Lists/frozen rosters redact restricted fields; detail grants and manager-chain checks tested | Exhaustive export/search/count coverage belongs to receiving surfaces and remains open. |
| Body: receiving reactions | Named joiner/mover/leaver facts are produced | Policies and External access named-event consumers were not found; implement receiving effects in those contexts. |
| Body: import | Legacy roster snapshots exist | Import/snapshot replacement remains explicitly deferred under #347; snapshots are not import completion. |

Person generic-writer retirement is the current bounded integration. Member links require
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


## Control environment

| Requirement | Source evidence / implemented behavior | Remaining acceptance |
| --- | --- | --- |
| CE-01 | ControlDraft/ControlRules retain approved content, reviewed successors and effective dates; ControlLifecycleTests | Version terms lack kind/nature/cadence/occurrence subject; operating terms remain in separate ControlOperatingPlan. Deliver complete authoritative version terms. |
| CE-02 | Pending-version member responsibility sets and operating-plan holder changes exist | Standalone effective-dated single-holder ControlResponsibility, person/team support and ending history absent. Current plan approval moves open work; target must expose unassigned work. |
| CE-03 | Stable calendar-window identities and expected work independent of an examination | Persisted generation remains period/TypeIIPeriod based; subject dimension and daily/continuous cadence absent. Independent scheduler follows complete control terms. |
| CE-04 | Independent native mapping line, selected criteria checks, exact waiver and history | Reviewed unmapping absent; mapping currently requires approved version although target allows draft; effective-date coverage selection remains. |
| CE-05 | Reviewed retirement stops expected future work and retains history | Explicit cancellation transition/fact and reaction for already materialized later occurrences absent. Filtering expected work is not cancellation. |
| CE-06 | Verified committed review actions generate deterministic findings | Failed/skipped attestations also raise automatically. Add reviewer raise/none decision plus required none reason; react only to committed review. |
| CE-07 | Exact-attestation independence and exact waiver checks exist | Reviewer authority still comes from plan/member/program manager; consume target person/team ControlResponsibility. |
| CE-08 | Missed/overdue presentation is derived, not an event | Qualify window-end boundary instead of only DueOn < today; include late attestation. |
| CE-09 | Deterministic program-unique immutable identifier and retained retired stream | Behavior substantially present. Add explicit retired-identifier reuse refusal acceptance during owner cutover; no directory admission check. |
| CE-10 | Immutable exact procedures used by started evaluations; source/version tests | Independent plan identity and mutable-before-use draft/revise/frozen/supersedes lifecycle absent. Preserve immutable used revisions. |
| CE-11 | Generic recurring/event-driven work exists | Ordinary policy/provider/management review controls need subject terms/populations and exact readiness/monitoring/minutes support. |
| Body: proposal authors | Current author self-review refusals exist | Revisions replace the author marker. Retain every proposal author and refuse earlier author A after revision by B without an exact waiver. |
| Body: one open proposal | Open-draft guards exist | Successor proposal omits pending-retirement exclusion and can clear it. Refuse successor without append while retirement is pending. |
| Body: exact attestation support | Immutable control/plan/proof source checks exist | EvidenceReference strings remain unresolved; typed exact artifact/record-version and management-review support is incomplete. |
| Body: returned/terminal occurrence | Linked immutable correction history exists | Correction lacks returned/open-state gate; enforce the specified terminal/correction table and vocabulary. |
| Body: evaluation | Exact procedure/assertions/reference versions, complete submission, deviations, independent review and immutable rounds are implemented | Full Lead/Contributor/person/team actor policy remains; optional tester-independence rule can exclude an owner whom the proposal permits. |
| Body: retest lineage | Exact accepting predecessor event/version verified; original retained | Retest currently selects all predecessor material deviations, rather than explicitly selected still-open deviations. |
| Body: availability | Old handlers/routes/serializers and native replay/source integrity tests exist | New responsibility/cancellation/unmapping/subject contracts need full wiring. Activation/discard/lifecycle feature gates still control old workflow availability. |

## Firm methodology

| Requirement | Source evidence / implemented behavior | Remaining acceptance |
| --- | --- | --- |
| FM-01 | Separate draft tracks every revision author; independent human operator review and third-person approval; source-bound workflow tests | Current separate-draft decision path is implemented. Complete public creation remains prerequisite for the whole authoring journey. |
| FM-02 | V4 rejects contribution approval references before content writes | Legacy contribution owners/commands/handlers remain registered without current HTTP/MCP exposure. Retire live legacy admission while preserving required historical decoding. |
| FM-03 | Publication computes actual added/changed/removed entry keys; legacy rules plan draft dispositions | V4 ordered application intent and destination create/propose/refuse execution absent. Approved destination content must remain untouched. |
| FM-04 | Apply currently uses generic program.manage and personal actor | Require current tenant Compliance Lead, with non-Lead manager, ended membership, firm actor, scope and retry refusals. Public transport is not connected. |
| FM-05 | Separate line/draft streams and per-version one-terminal-entry outcome owner exist | Canonical parent identity still includes ApplicationKey; ordered versions/completion command/fact/reactor absent. Exact source-backed child outcomes and parent completion remain. |
| Body: global name creation | Line owner can create idle identity | Held name-admission source/36 unexecuted cases remain excluded. Wire claim race, committed source-bound creation and exact losing-claim release. |
| Body: line draft/publication | One open draft, latest base, immutable publication, discard closure and failed-send replay tests exist | Preserve these current behaviors while connecting creation. They do not implement destination application. |
| Body: idle retirement | Owner implements idle-only retirement and retired-start refusal | Current handler uses legacy PrepareRetire and refuses V4. Wire personal operator V4 retirement and retries. |
| Body: application admission | Legacy handler checks Program existence and version status | Active Program, nonretired line, V4 publication, canonical increasing version order and previous completion guards remain. |
| Body: destination answers | Version owner accepts three outcome shapes; opening verifies committed application | No destination execution/authoritative fact reference in entry outcome. Add one owning destination's deterministic create/propose/refuse proof before recording its answer. |
| Body: read/replay | Truthful V4 publication metadata, immutable selected entries, source cuts and native paging/isolation are implemented | Final-head delivery qualification remains separate. Opening/conclusion replay does not prove missing destination/completion behavior. |

## Assurance work

Held Finding closure source and its 78 unexecuted cases are excluded.

| Requirement | Source evidence / implemented behavior | Remaining acceptance |
| --- | --- | --- |
| AW-01 | Finding source remains immutable while generic analysis revisions preserve wording; FindingTests | Named ReviseFindingAnalysis and Lead/Contributor/owner authority absent. Existing owner-revision refusal is contrary to the approved table. |
| AW-02 | Finding owns unresolved admitted-action fence and exact terminal answers | Proposed zero-action closure and proposal/withdraw/return/independent-current-proposal review absent. Current Close excludes different actors and is not target closure acceptance. |
| AW-03 | CorrectiveAction is a separate owner with open/completed/cancelled lifecycle | Tenant-wide action identity and permanent one-Finding binding remain coordinated identity work. |
| AW-04 | Reopen preserves closure history and advances generation | Dedicated Lead-only admission and another reviewed closure path remain. Generic program management also admits Admin today. |
| AW-05 | Product Waiver request, independent Lead/Admin HTTP approval/rejection and reasoned revocation are wired | Competing-append acceptance proof and authoritative downstream consumers remain. |
| AW-06 | Waiver expiry is computed without an expiry event | Consumers must verify current exact-scope unexpired coverage; old approval alone is insufficient. |
| AW-07 | Low/moderate/high command and replay vocabulary, with Work medium mapping, is implemented | Named analysis actor cutover remains separate; no new severity rubric is needed. |
| AW-08 | Linked risk acceptance does not close a Finding; negative invariant tests exist | Target LinkFindingToRiskAcceptance/Lead admission absent; old linking command also accepts SOD-waiver links. |
| AW-09 | Finding owns action admission and exact source-verified terminal outcome reporting; committed-source reactors and reporting-lag tests | Target command/role vocabulary and reviewed closure are separate; retain planned-but-unmaterialized/refused closure behavior. |
| Body: identities | Current Finding includes ProgramId and Action includes program/finding in stream keys | Target tenant+FindingId and tenant+ActionId are not established by current tuple/wrong-stream tests. Coordinate canonical identity migration. |
| Body: source vocabulary | Readiness/occurrence source checks and stable deterministic reaction raises exist | Target control_evaluation_deviation/audit_test_exception/provider_review/management_review vocabulary and producer migration remain; preserve coordinated #1180. |
| Body: analysis/owner | Generic revision combines analysis and owner fields; active-member checks exist | Separate named analysis and Lead-only owner assignment, including approved person/team owner representation. |
| Body: plan/materialize | Finding admission then source-verified independent action materialization exists | Public Add currently means target Plan, internal Materialize means target Add. Named/role cutover remains. |
| Body: change action | Current repair source-checks acting membership and current Lead/finding-owner authority before receipt replay; checks repeat during new preparation | 58 relevant focused cases passed after behavioral RED; latest assembled gate/CI remain separate. This is an implemented repair, not a new missing feature. |
| Body: completion | Owner/program-manager completion, terminal/source-use and immutable evidence tests exist | Exact Lead policy and required artifact-version evidence replace generic manager/unresolved support references. |
| Body: cancellation/overdue | Reasoned Lead cancellation, attribution and Work/baseline consumption exist; overdue is derived | Qualify exact action due-date boundary and migrate old closure-independence policy with the proposal workflow. |
| Body: closure cutover | Live generic Close route remains | Held proposal/review/withdrawal must include Lead reopen, lineage independence, exact action/waiver tuple, source-use settle/abort, consumers and public cutover. |
| Body: waiver consumers | Waiver.Covers implements local exact coverage; no consumer outside Remediation found | Control-environment/data-flow consumption and visible readiness/audit-export waived-action flags remain. A catalog row is not a consumer. |
| Body: audit exception/source status | Legacy AuditObservation links validate old source ownership | Target LinkAuditTestExceptionToFinding and explicit source-status-independence acceptance remain. |
