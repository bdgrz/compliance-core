# Implementation order

Reviewed 2026-11-05 against the 24 locked [contexts](README.md#bounded-contexts-source-of-truth),
their decisions and flow rules, the [requirement coverage](requirement-coverage.md),
the [preserved coverage handoffs](restart-artifacts/2026-11-04/coverage-index.json),
and the current integration and WIP branch checkpoints. This is a recommended
execution order for the remaining reviewed backend scope, not a new domain design
or a claim that any context is complete.

## Recommendation

Prioritize authoritative access and identity, the criteria/program foundation,
then the workflows that produce compliance evidence. Build readiness and
examination conclusions from those facts. Integrate useful preserved work in
bounded capabilities; do not restart it or merge entire WIP branches.

The first client's path is manual, with no existing material to import
([M0-D04](decisions/m0-d04-readiness-material.md)). Connectors are deferred
([M0-D20](decisions/m0-d20-integration-sources.md)). This favors usable authoring,
operating, review and evidence workflows over import expansion or convenience
automation.

The table is the default selection order when several capabilities are ready.
It is not a requirement to finish an entire context before starting the next.
Each row includes its public/internal wiring, authorization, read behavior and
receiving consumers. Work projections and relevant readiness rules accompany
each source capability rather than accumulating in a final wiring batch.

## Validation prerequisite, separate from business order

The integration checkpoint `5c9d9b83` has a strict Release build, whitespace
verification and 148 selected owner tests passing. It is not full-suite acceptance.
The single native schema probe fails during Portia initialization on the pinned
Cassie image; [#1188](https://github.com/bdgrz/compliance-core/issues/1188) records
the reproduction. Resolve this through a compatible published dependency, then
run the same probe, focused native cases and the full backend unit gate.

This is the first qualification task. It does not require stopping independent
business-source work. Avoid expanding SQL conversion while its native contract
cannot be exercised. Convert each surviving feature's read models with its
business cutover; the old migration inventory includes owners the locked proposal
removes. Fitz remains the event store; authoritative history readers remain
authoritative. Do not add application persistence/retry machinery to bypass an
upstream failure.

## Ordered capabilities

| Order | Capability and contexts | Concrete remaining work | Dependency / reason for this position |
| --- | --- | --- | --- |
| 1 | **Access authority and continuity** — [Platform access](contexts/platform-access.md) | Scoped team roles and custom-role permission admission; guest program caps on membership episodes; built-in protections and immediate deleted-role/team denial; primary designation with Member outcomes and creator bootstrap; rejected-first-slug recovery. Retire direct member-grant authority with its replacement. | PA-01/02/03/18/21/27 govern every later actor matrix. Primary protection must exist before removing legacy ordinary-admin continuity restrictions (PA-23). Finish coherent permissions, not just new scope fields. |
| 2 | **Stable people and platform identities** — [Workforce](contexts/workforce-assurance.md), Platform access | Authoritative member-to-one-Person admission with many members per Person; Org Admin link/unlink and current-episode consumers. Complete verified-email claim and same-user sign-in recovery as a separate access capability. Align the UserId-keyed firm-staff directory before new professional assignment consumers. | WF-05 supplies person-owned control, review and campaign authority. PA-10/11 must preserve the actor identity attached to historical approvals. Do not make recovery completion a dependency of unrelated manual business authoring. |
| 3 | **Published criteria and canonical program/scope** — [Criteria](contexts/criteria.md), [Program and scope](contexts/program-and-scope.md) | Runtime edition/entry publication, mandatory published edition at Program creation, named targets/stages, program-keyed boundary, all-author one-step boundary review and discard of never-approved drafts. Then applicability revisions/POFs and shared applicable-criteria composition. | CR-01/08 precede the target CreateProgram contract. The current static catalog is useful existing data, not runtime publication authority. Boundary scope may use the explicitly permitted typed unresolved references; do not wait for every inventory. Stage consumers attach when their exact readiness/outcome sources exist. |
| 4 | **Manual inventories and source identities** — Workforce, [Application inventory](contexts/application-inventory.md), [Technology/information](contexts/technology-and-information-inventory.md), provider foundation in [Risk/provider](contexts/risk-and-provider-oversight.md) | Finish workforce lifecycle/owner reads; applications, aliases, instance owner override, access-review scope, merge-to-live-survivor and retirement/instance admission; named technology/information/location/process lifecycles and final retirement; provider identities and ownership. Resolve boundary references as records become available. | These are the subjects of controls, access populations, risk and description citations. Preserve the source-observation revision fence and canonical application outcomes in the later workforce reconciliation slice; no HRIS connector or roster-file importer is needed to use the manual path. |
| 5 | **Usable evidence and protection** — [Evidence](contexts/evidence.md), [Retention](contexts/retention.md) | Complete capture/inspection and derivative provenance; handling-class/restricted access; terminal independent redaction review; support withdrawal with historical-versus-new-use consumers; person/team requests and fulfilment links. Add source-derived disclosures, artifact retention set/extend, and tenant HoldRegister legal/engagement coverage. | Exact available artifact versions underpin attestations, action completion and packages. EV-03/06 disclosures feed RT-06/08 coverage. The register's authoritative disposal decision must exist before target DisposeArtifact is enabled. Final package-dependent hold release comes later. |
| 6 | **Governed controls and commitments** — [Control environment](contexts/control-environment.md), [Commitments](contexts/commitments.md) | Complete control version terms (kind, nature, cadence, subject, evidence), all-author proposals, effective person/team ControlResponsibility, reviewed mapping/unmapping and retirement. Deliver one-step commitment governance and version-bound commitment/control pair links. | CE-01/02 are prerequisites for correct occurrence identities and actor policy. Move boundary responsibility ownership with the control consumer cutover. CM-05 and CE-09 retain identifier authority in their own streams. |
| 7 | **Operating, reviewing and correcting** — Control environment, [Assurance work](contexts/assurance-work.md), [Work queue](contexts/work-queue.md) | Calendar/subject occurrences independent of examinations; exact support, return/correction and retirement cancellation; reviewer raise/none decisions; freeze-on-use evaluation plans and selected-deviation retests. Complete Finding proposal/withdraw/return/review/reopen and canonical action identities, source outcomes and exact waiver consumers. | CE-03/06 and AW-02/09 turn authored controls into an auditable operating loop. Closure uses recorded action outcomes. Coordinate Finding source vocabulary with producers under #1180. Work assignment/claim/source-round identity and recorded escalation ship with these sources. |
| 8 | **Recurring assurance workflows** — [Policies](contexts/policies.md), Risk/provider, [External access governance](contexts/external-access-governance.md) | Policy approval/campaign participant outcomes and attributed workforce amendments; training and annual gaps. Risk appetite/method/current assessments, actions/acceptances and provider report/review/gap outcomes. Access owner/delegation, accepted hashed populations, persistent classification, independent exceptions, individual/bulk decisions, later-population remediation verification and recorded item-close answers. | Workforce, instances, exact artifacts and responsibilities now have usable authority. Access campaign completion depends on item answers, not a successful roster read. Provider reviews are evidence for ordinary review-control occurrences. These are independently selectable slices, not one large batch. |
| 9 | **Significant changes and accurate system description** — [Change/incident](contexts/change-and-incident-oversight.md), [System description](contexts/system-description.md), risk triggers | All-author independent significance assessment and current-assessment clearing; separate incident resolution. Program version line/per-draft description, section citations, SectionReview open/resolve/withdraw/no-change and stale-citation gaps; committed significance/provider triggers and risk triage. | CI facts drive SD section reviews and RP reassessment. Remove the legacy description requirement for a frozen examination baseline: target description starts from an active program and is itself an examination input. This removes a false dependency cycle. |
| 10 | **Professional engagement authority** — [Firm services](contexts/firm-services.md) | Partner designations, immutable ratified rule versions, reasoned nonattest service records, exact-current-history independence evaluation/management acknowledgement, accepted engagement lifecycle, UserId/client compartment admission and independent assignment outcomes. Full-window impairment blocks new assignments and audit outcomes while still recording service truth. | This is an independent foundation track after access/staff identity, and may start beside orders 3–9. It must finish before assigned Advisor/Attest access and EX-13 consumers cut over. Client manual workflows do not need to wait for all professional-service functionality. Keep close/package delivery for order 14. |
| 11 | **Reuse and consultant collaboration** — [Firm methodology](contexts/firm-methodology.md), [Advisor collaboration](contexts/advisor-collaboration.md) | Finish template creation/retirement and ordered per-version destination create/propose/refuse outcomes with parent completion. Deliver open handoff selection changes, direct attributed feedback/corrections/replies, reasoned change-request resolution and unified close; keep feedback separate from approval. | Methodology can draft/publish independently earlier; application needs the destination owners from orders 6/8. Consultant access needs order 10 (or guest caps from order 1), plus exact selected-source reads/disclosures. Integrate once those contracts stabilize to avoid repeating destination adapters. |
| 12 | **Truthful readiness and management entry** — [Readiness](contexts/readiness.md), Program stage consumer | Immutable versioned assessments with explicit complete/incomplete verdict and source versions; daily/manual calculator; stable gap plans/notes with member/team owners; immutable Org Admin Type I decision copying every open gap and plan. Wire its exact citation into Program stage and management-review support. | Source rule families arrive incrementally from orders 3–9. Unknown/draft or unimplemented required inputs must stay explicit; a partial evaluator cannot claim complete readiness. RD-02 permits a complete historical assessment, not necessarily the latest. Open gaps may be accepted with plans (RD-01). |
| 13 | **Examination and auditor delivery** — [Examinations](contexts/examinations.md), [Auditor collaboration](contexts/auditor-collaboration.md) | Canonical examination/signers/attest bridge; exact Type I baseline; one Type II plan/period per examination with manual carry-forward; calendar occurrence reconciliation, material acknowledgements and late-item amendments. Requests/responses/comments, immutable population successors and samples, package contents/build/validation/delivery with current shareability; exact assertion → letter → outcome → sign-off chain and report reissues. | Setup contracts can be prepared earlier. Full acceptance needs source versions, operating records, section reviews and exact package lineage; professional access needs order 10. Split this row into setup/baseline, Type II close, package delivery, and signing/outcome capabilities. Packages and examination consumers must cut over together. |
| 14 | **Complete-copy delivery and lifecycle closeout** — [Exports](contexts/exports.md), Firm services, Retention, Platform access | Real client archive/withholding inventory, prepared-once proof and recipient-confirmed receipt; separate own-attest record package and its delivery. Then accepted engagement close/assignment ending, explicit package/report-gated hold release and offboarding completion from required facts. Finish authorized per-artifact disposal/refusal outcomes and late retention-extension reporting. | ClientExportReceived, AttestEngagementRecordPackageDelivered and no accepted/open engagements are distinct prerequisites. Download/build success is not client receipt; client export and auditor package are not the firm's own record package. Earlier preservation/hold protection remains in force. |

## Immediate queue from the preserved work

1. **Restore native qualification** using #1188's single probe. Revalidate the
   already-assembled Support/Finding/Access/Program wiring before accepting it.
   The latest build repair is not another business slice to rewrite.
2. **Finish scoped access as one capability.** Inspect
   `wip/proposed-access-cassie` through `686b415c`: scope/guest source exists,
   but the last commit is a behavioral test specification, not proof of GREEN.
   Complete authoritative permission consumers, guest caps, role protections and
   public/read paths. Then implement primary continuity separately.
3. **Finish Person/member linkage** from `wip/proposed-workforce` (`c7277f6b`),
   including the Member admission answer, source-bound receiving effect,
   many-member Person view, unlink/relink races and real actor/read consumers.
4. **Deliver Criteria publication plus Program creation**, then canonical
   boundary review. `wip/proposed-program-scope-cassie` (`44c8cb49`) contains
   prepared named-lifecycle work; the missing runtime Criteria publication owner
   is a real prerequisite, not satisfied by a static catalog lookup.
5. **Advance two ready business slices in selection order:** evidence protection
   from `wip/resume-evidence-code` (`2322936a`), then control terms/responsibility
   and operating outcomes from `wip/resume-controls-code` (`6ae26774`). They can
   be prepared independently where file ownership permits. Do not skip the
   missing responsibility foundation because occurrence-cancellation code exists.

Use the [preservation manifest](restart-artifacts/2026-11-04/worktree-cleanup-manifest.json)
to recover the remaining feature packets. Branch names and `feat` subjects do
not establish source completeness; several tips preserve stubs, failing authored
tests or incomplete wiring. Select exact owned changes and inspect their diff
against the integration head. This review did not execute those branch tips.

## Completion and scheduling rules

- **Implement one complete capability:** invariant, lifecycle, events, handlers,
  current authorization, read model and needed API/MCP/receiving effects. Retire
  a superseded writer in the same cutover so it cannot bypass the new invariant.
- **Keep evidence separate:** source prepared; compiled/focused behavior verified;
  full-suite/final-head CI accepted; merged. Update requirement rows at the
  relevant head. Historical coverage handoffs are candidate evidence, not fresh
  declarations that a behavior is still missing.
- **Work is incremental.** Source-owned open/completed/reopened work and access
  checks accompany every capability. Weekly digest sent/skipped/retry integration
  is lower priority than correct source work; it need not block orders 6–9.
- **Readiness is incremental too.** Establish its result/completeness contracts
  early; add each rule family with its source. Order 12 is the complete assessment
  and management-decision gate, not a reason to defer every readiness test.
- **Protect authority.** Apply CV-05/10/11/12: advisory checks expose repairable
  disagreement; irreversible decisions use their named owner and recorded
  participant outcomes. No new roster aggregate or custom execution framework.
- **Keep deferred scope deferred:** connectors, application/roster import,
  TenantSignInConnection (F1-06), commercial subscription/billing, and client UI
  implementation are outside this queue. Accepted workforce-observation business
  application remains in scope even though collecting it through a connector is not.

## Review observations

- The README, progress ledger and delivery reset contain historical paused,
  sole-candidate and not-mapped statements. Prefer the latest restart checkpoint
  and exact source/evidence; those historical tables must not drive selection.
- The old read-model inventory and query-gap counts predate later implementation
  and the locked simplifications. Reconcile by surviving business behavior before
  converting a store or adding a similarly named query.
- Readiness, period close, package validation and offboarding are consumers of
  evidence. Their attractive terminal APIs do not justify fabricating completion
  when upstream source contracts/outcomes are still missing.
- No new domain design dispute was resolved by this prioritization. Locked
  context contracts remain authoritative. This review made no paid model calls,
  changed no GitHub backlog priority fields and started no implementation agents.
