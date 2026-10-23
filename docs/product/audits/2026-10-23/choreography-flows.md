# Cross-owner choreography definitions

These diagrams describe the target protocols required by the aggregate boundary audit. A reactor receives a committed fact, validates its source identity, and sends typed commands to business owners. [Additional admission/independence flows](admission-choreography.md) cover professional duty, canonical staff, personal decisions, permanent practice commitments, previews, provider links and campaign participants. It does not become a second authority. Each receiver validates its own invariant and exact source proof. Current source reactors are indexed in [`reactor-contract-catalog.md`](reactor-contract-catalog.md); selected future effects and exact receiver commands are in [`target-reactor-contracts.md`](target-reactor-contracts.md); aggregate command, event, guard and test contracts are in [`aggregate-boundary-specification.md`](aggregate-boundary-specification.md).

Every flow that spans owners needs tests for: replay after receiver append but before checkpoint save; unknown append outcome; transient dependency failure preserving the source checkpoint; stale/superseded source; exact retry with no duplicate fact; changed intent under the same key; and concurrent conflicting operations. The flow-specific tests below add the relevant invariant.

## 1. Identity recovery — ACCESS-12 / CHOREO-01

Both proofs authorize an immutable preparation. Protect the exact associations before accepting the swap; acceptance then requires automatic completion.

```mermaid
flowchart LR
  P[Both proofs verified] --> Q[Claim challenge and prepare exact immutable swap]
  Q --> H[Protect old and replacement slots canonically]
  H --> A[Accept exact swap with complete protection proofs]
  H -->|definite conflict| X[Reject preparation and fence delayed acceptance]
  X --> F[Settle every targeted protection key]
  A --> N[Register or verify replacement association]
  N --> O[Revoke old with replacement proof]
  O --> C[Prove completion and settle protections]
  C --> M[Independent completion notice]
```

Use the frozen challenge/account/identities/actor/provenance. Never persist bearer tokens. Before acceptance, competing associations may reject preparation; after acceptance complete forward without another personal invocation. Existing same-account association and same-target revocation proofs resume the process even when they emit no new event. Test competing registrations/challenges, delayed acquires after rejection, crashes after each owner append and independent notice failure.

## 2. Tenant administrator withdrawal and restoration — ACCESS-01

```mermaid
flowchart LR
  W[Withdraw operation with member episode and generation] --> G[TenantManagerGuard records exact hold]
  G --> S[Member or TeamMember applies source transition]
  S --> F[Source fact proves exact operation completed]
  F --> R[Guard releases only that hold]
  X[Restore request] --> Q[Wait behind unfinished withdrawal]
  Q --> U[Restore Member or TeamMember source first]
  U --> E[Read authoritative current administrator eligibility]
  E --> Z[Release matching restoration/withdrawal generation]
```

The guard must not clear every reason for a member. Restore cannot release a pending withdrawal before its source command is fenced. Suspend, deprovision and reinstate always enter through the guard; when the episode has no contribution and no pending transition the guard records `TenantManagerSourceMutationAdmitted` and the source applies unchanged, so a concurrent administrator registration for that episode observes the mutation. Team assignment and removal enter through the guard only for the administrators team. Test the restore-between-hold-and-source race, multiple independent reasons, old episode callbacks, and crash recovery at each arrow.

## 3. Membership retirement and relationship cleanup — CHOREO-03/06, ACCESS-08

The current retirement slice uses immutable membership proofs and exact episode-bound receiver commands. The selected public identities and administrator fencing are specified in source-admission-contracts.md; their backend implementation remains pending.

```mermaid
flowchart LR
  D[MemberDeprovisioned for episode E] --> MR[MemberRetirementReactor]
  MR --> C[CompleteMemberRetirement with termination event ID]
  C --> S[Scan authoritative TeamMember and AccessGrant facts]
  S --> T[Cleanup exact old TeamMember assignment]
  S --> G[Cleanup exact old AccessGrant]
  T --> P[Record receiver proof]
  G --> P
  P --> F[Complete retirement only after full source coverage]
  TA[Late TeamMemberAssigned] --> TR[RetiredTeamAssignmentReactor]
  GA[Late AccessGrantIssued] --> GR[RetiredMemberGrantReactor]
  TR --> X[Dispatch only with exact retirement proof]
  GR --> X
  X --> T
  X --> G
```

Guards: each effect key includes source event, kind, canonical target owner, episode and exact termination event; active current episode causes no effect; unknown/missing authority is transient; checkpoints never skip undiscovered source tails. Test reverse completion order, late assignments after the scan cut, 1000+ unprojected relationships, retry after E2 registration, and proof mismatch.

## 4. Type II date reservation and period close — CHOREO-04/10

```mermaid
flowchart LR
  A[Persist accepted opening or amendment intent] --> H[Calendar reserves exact current and pending ranges]
  H --> P[TypeIIPeriod accepts exact range and proof]
  P --> K[Calendar confirms applied range]
  P --> C[Close frozen or amendment canceled fact]
  C --> R[Reservation recovery reactor]
  R --> V[Verify exact period event and generation]
  V --> L[Release only the matching old reservation]
```

A durable operation ID connects calendar holds and period facts. Amendments hold both date ranges until applied; cancellation fences delayed application before release. Close releases only its exact reservation after authoritative frozen-close proof. Test overlaps under concurrency, unknown calendar/period writes, cancellation racing delayed application, and interrupted close.

## 5. Application import and complete publication — ROOT-01/02, CHOREO-05

```mermaid
flowchart LR
  I[Freeze complete import rows and target revisions] --> A[Accept batch and source claim]
  A --> H[Acquire target holds in canonical stream-key order]
  H --> T[Apply each frozen target group in one native owner batch]
  T --> P[Collect exact target event/revision proofs]
  P --> C[Commit one complete batch publication fact]
  C --> R[Composed reads expose every target together]
  F[Definite permanent failure] --> X[Fence every unpublished target before cancellation]
```

Target replay consumes the complete frozen row group, initial target revision and source identity; it does not query live batch settlement to mutate business state. The batch owner retains the only publication gate for that exact batch. Test two batches acquiring overlapping targets in opposite source order, crash after partial target application, unknown commit outcome, and no partial read visibility.

## 6. Tenant registration and slug lifecycle — CHOREO-08

```mermaid
flowchart LR
  T[TenantRegistered] --> R[TenantRegistrationReactor]
  R --> S[Claim normalized slug in TenantSlug owner]
  R --> O[Register tenant owner]
  R --> B[Bootstrap RBAC owners]
  S --> D[TenantSlugRegistered or Rejected]
  D --> C[Confirm or reject exact tenant operation]
  N[TenantSlugChangeRequested] --> NS[Claim new slug]
  NS --> OK[Confirm tenant change]
  OK --> OLD[Surrender old slug]
  OLD --> ACK[Retire old slug claim]
```

Slug uniqueness is enforced by TenantSlug, not a preflight directory read. Outcome receivers compare the exact source operation and treat a proven old result as no-op; changed intent remains conflict. Test checkpoint replay after later slug changes, rejection replay, simultaneous same-name claims, and old surrender arriving late.

## 7. Invitation acceptance and provisioned authority

```mermaid
flowchart LR
  A[TenantInvitationAccepted with frozen terms] --> R[TenantInvitationReactor]
  R --> M[Register exact membership episode]
  M --> G[Issue scoped guest grants for that episode]
  G --> T[Assign admin or invited team authority]
  T --> V[Verify current source membership and required permissions]
  V --> X[Activate tenant if this invitation is the creator admin]
```

Each target is its own authority: Member owns the episode, AccessGrant owns grant terms, TeamMember/TeamRole own assignments, Tenant owns activation. Test replay after partial provisioning, invitation retry after expiry, new invitation generation after prior acceptance, and episode change before grant/activation.

## 8. Challenge, invitation and recovery notice delivery — CHOREO-07/13

```mermaid
flowchart LR
  E[Challenge, invitation or notice fact] --> D[Delivery process records exact effect intent]
  D --> S[Send exact payload to exact recipient]
  S -->|accepted| OK[Record delivered outcome]
  S -->|definite rejection| F[Record definite failure]
  S -->|unknown result| U[Record/reconcile unknown outcome]
  OK --> C[Persist matching confirmed process outcome]
  F --> R[Apply accepted at-least-once retry policy]
```

Keep delivery attempts outside domain event streams. Preserve at-least-once challenge/invitation delivery; keep digest’s stricter rule against automatic retry after unknown send. Test crash after SMTP acceptance before outcome append, callback from an old attempt, exact payload on retry, and notice retry independent of recovery completion.

## 9. Source decisions that create findings

```mermaid
flowchart LR
  E[Committed attestation or evaluation decision] --> R[Source-specific finding reactor]
  R --> P[Validate exact event, owner, version and full intent]
  P --> C[RaiseFinding with source-derived FindingId]
  C --> F[FindingAggregate owns status and closure gate]
  F --> A[CorrectiveAction owners hold independent actions]
```

The target ID binds source identity and effect kind. Replayed source facts return the accepted result without a duplicate finding. Test every source fact variant, changed source revision, duplicate replay, and migration/unavailable receiver recovery.

## 10. Risk method and boundary reassessment

```mermaid
flowchart LR
  E[RiskMethodVersionPublished or BoundaryApproved] --> R[Per-tenant reassessment reactor]
  R --> D[Discover complete authoritative risk set]
  D --> P[Build exact risk and source-version proofs]
  P --> C[Raise risk trigger in per-risk RiskGovernance owner]
  C --> V[RiskEvaluation consumes current method and trigger]
```

Reassessment cannot erase a permanent independence commitment or accept a risk by itself. The receiver deduplicates on risk, trigger kind and source event. Test checkpoint retention when discovery is incomplete, source changes during discovery, duplicate trigger replay and risks added late.

## 11. Firm staff status and accepted service epochs

```mermaid
flowchart LR
  S[FirmStaffChangeRecorded] --> SR[FirmStaffStatusReevaluationReactor]
  A[ServiceEngagementAcceptanceRecorded] --> AR[AcceptedStaffDirectoryReconciliationReactor]
  SR --> D[Discover complete staff/acceptance source pairs]
  AR --> D
  D --> P[Verify exact event IDs, offsets, hashes and staff identity]
  P --> I[Record exact reevaluation in ServiceEngagementIndependence]
  I --> W[Permanent user/practice independence wall remains authoritative]
```

Both trigger directions are required for status-before-acceptance and acceptance-before-status. Use complete source coverage and exact immutable source pairs. Reevaluation cannot reactivate a prior acceptance automatically. Test either event ordering, projection lag, late staff assignment and stale service epoch.

## 12. Significant change impact and description decisions — CHOREO-09

```mermaid
flowchart LR
  A[Impact assessment accepted] --> W[LinkImpactReviewWork command]
  D[System description approved or no-change decision] --> R[Decision reactor]
  R --> C[Record exact decision in SignificantChange or ComplianceIncident]
  W --> P[Owner verifies exact assessment version and source correlation]
  C --> P
  P -->|current source| F[Advance current assessment]
  P -->|dependency not ready| T[Transient retry; keep checkpoint]
  P -->|proven superseded source| N[Terminal no-op for that exact old event]
```

An obsolete source event must not become a permanent conflict that blocks the checkpoint, and it cannot decide a newer assessment. Test the race between impact work and description decision, new assessment superseding old work, dependency lag, and exact terminal replay.

## 13. Type II expected occurrence materialization — CHOREO-10

```mermaid
flowchart LR
  P[TypeIIPeriod selects due control windows] --> E[DueControlOccurrencesGenerated with exact manifest]
  E --> R[Occurrence materialization reactor]
  R --> C[Create ControlOccurrence per control and business window]
  C --> O[Record plan-bound attestation/review facts]
  O --> V[Reconcile authoritative expected versus materialized coverage]
  V --> X[Permit period close only on complete coverage]
```

Selected work and materialized work are distinct. All generators use one deterministic identity per control/window. Test partial materialization, replay from the generation event, two generators racing the same window, and close blocked by missing authoritative occurrences.

## 14. Atomic bulk access decisions

```mermaid
flowchart LR
  P[Exact personal preview and ExpectedCampaignRevision] --> A0[Campaign admits frozen complete decision batch]
  A0 --> B[Materialize independent decision batch]
  B --> H[Acquire every item hold in canonical order]
  H --> V[Revalidate complete preview]
  V --> A[Append provisional native item decisions]
  A --> C[Verify complete exact item proof manifest]
  C --> U[Publish one complete batch]
  U --> R[Expose all decisions and settle exact holds]
  V -->|definite mismatch| F[Fence every selected receiver]
  F --> X[Cancel batch and settle fenced holds]
```

No partial success or smaller item selection is allowed. Tests include single-item edits racing holds, crash after any provisional append, unknown publication and cancellation before an unacquired target's delayed apply.

## 15. Handoff feedback admission and materialization

```mermaid
flowchart LR
  I[Personal feedback for exact selected version] --> G[Handoff admits full immutable contribution]
  G --> M[Reactor materializes feedback thread]
  M --> C[Thread owns corrections responses and resolution]
  R[Handoff revoke or close] --> G
  G -->|already revoked or closed| X[Reject new contribution]
```

Admission and revocation serialize on Handoff. An admitted contribution still materializes after revocation; the receiver uses its accepted proof and does not require the handoff to remain ready.

## 16. Auditor response admission and request resolution

```mermaid
flowchart LR
  I[Draft comment or sample-selection intent] --> A[Open request admits exact complete intent]
  A -->|response| M[Materialize response]
  A -->|comment or selection| T[Materialize thread or sample selection]
  M --> E[Exact response review delivery and disposition]
  Q[Resolve with nonempty exact response selection] --> S[Seal all new admission]
  S --> D[Drain every earlier admitted materialization]
  M --> D
  T --> D
  D --> H[Grant complete exact response-state uses]
  E --> H
  H --> P[Prove every selected admitted response accepted]
  P --> C[Record request resolved]
  C --> R[Settle uses with parent closure fence]
```

Response selection is nonempty and complete. Comments and sample selections must materialize but have no invented external-disposition requirement. Amendment keeps population identity and advances version. Close cannot discard accepted work. Reopen changes request generation, retaining historical children; old callbacks cannot resolve a new generation.

## 17. Finding action admission and closure

```mermaid
flowchart LR
  A[Finding admits complete corrective action] --> M[Materialize independent action]
  M --> E[Action records completion and exact evidence]
  Q[Close finding] --> S[Seal action admission]
  S --> H[Acquire complete nonempty action proof set]
  E --> H
  H --> I[Check closer independence and exact waiver]
  I --> C[Append finding closure]
  C --> R[Settle action uses]
```

Reopen advances the finding generation. Exact replay of the old close returns its original result and cannot close the reopened finding. A requested-action finding atomically admits its default action with initial creation.

## 18. Readiness latest assessment and gap-plan seal

```mermaid
flowchart LR
  R[Run with ExpectedProgramRevision] --> A[Program admits exact snapshot and one adoption generation]
  A --> C[Child computes from frozen inputs]
  C --> L[Program adopts exact child proof and clears pending generation]
  D[Personal readiness or Type I decision] --> S[Seal latest adoption and gap-edit admission]
  L --> S
  S --> T[Drain earlier admitted gap edits]
  T --> H[Acquire exact complete gap-plan uses]
  H --> V[Validate frozen current rules plans and acknowledgements]
  V --> F[Append exact native decision]
  F --> E[Settle uses and unseal]
  V -->|source mismatch before hold| X[Cancel and fence pending decision]
  X --> E
```

A pending run blocks decisions on the previous latest. Program and child revisions differ. The single approved Type I pointer stays at program scope; no newer assessment/plan may slip between validation and append. Cancellation never substitutes newer inputs.

## 19. Name claim and record change

```mermaid
flowchart LR
  I[Record accepts complete create rename or lifecycle intent] --> H[Acquire desired and existing name claims canonically]
  H --> A[Apply exact record change with claim proof]
  A --> P[Record native revision is durable]
  P --> R[Release only the old inactive name claim]
  F[Definite pre-apply failure] --> X[Cancel and fence record apply atomically]
  X --> N[Release pending new name with exact fence]
```

Provider, Location and OperationalProcess use their existing normalization and uniqueness scopes. Unknown target append never releases a claim. A delayed release cannot remove a newer claimant.

## 20. Package build and personal delivery recording

```mermaid
flowchart LR
  K[BuildKey package version and ExpectedManifestRevision] --> M[Manifest admits complete immutable build]
  M --> B[Independent build process]
  B --> V[Write and verify archive]
  V --> F[Manifest records exact built fact]
  P[Personal attestation of external handoff] --> A[Manifest verifies exact archive validation recipient approval]
  F --> A
  A --> Z[Freeze delivered version and admit recording]
  Z --> D[Materialize independent AuditPackageDelivered]
  D --> R[Authorized status and original result]
```

RecordAuditPackageDelivery records a handoff performed by the personal actor. It freezes authored DeliveredAt/reference and server RecordedAt separately. Admission serializes validation/approval/amendment races and preserves version immutability; child replay uses accepted proof and sends nothing. DeliveryId and BuildId retain their existing deterministic algorithms. Test crash after each native append, personal restriction, stale archive/recipient/validation, exact retry after package changes and complete build output verification.
