# Additional admission and independence choreography

These diagrams supplement the [20 source/lifecycle flows](choreography-flows.md) with newly explicit admission protocols. They use the exact [receiver catalog](target-reactor-contracts.md), [durable payloads](durable-intent-schemas.md) and [ordered test traces](design-review-traces.json). Content references are complete and verified before admission. A committed source plus durable process recreates every continuation after a crash.

## Professional duty and active staff

```mermaid
flowchart LR
  A[Designation admits exact verified signed change] --> K{Record or revoke}
  K -->|record| U[Grant exact current active canonical staff use]
  K -->|revoke| C[Claim signed assertion identity]
  U --> C
  C --> P[Apply exact predecessor and signed scope]
  P --> F[Native designation recorded or revoked]
  F --> S[Settle any granted staff use]
  U -->|definite conflict| X[Reject change and fence claim continuation and apply]
  X --> S
```

Record requires the exact assertion user to match an active staff fact. Revocation keeps its existing independent signed authority. Acquire required staff use before the permanent assertion claim; keep it through native recording. A used assertion identity claim is permanent. Signed validity is checked at the declared frozen decision instant, and a use cannot extend that interval. LOCK-19 defines stale staff, delayed commands and revocation acceptance.

## Permanent canonical staff registration

```mermaid
flowchart LR
  R[Staff record admits immutable registration] --> C[Permanent canonical user identity claim]
  C -->|exact compatible claim| P[Complete same staff registration]
  C -->|owned by another staff record| X[Reject and fence pending registration]
  P --> S[Independent staff status mutations]
  S --> I[Canonical user and practice association remain immutable]
```

StaffMemberId is the creation identity; initial expected staff revision is zero. A canonical identity claim never migrates to another staff record because of deactivation. Exact claim proof resumes registration without requiring a newly emitted event.

## Personal engagement decisions

```mermaid
flowchart LR
  A[Personal request with exact engagement revision and service epoch] --> P[Engagement admits and pins exact content]
  P --> U[Acquire complete required current source uses canonically]
  U --> V[Authorize irreversible personal decision]
  V --> M[Materialize management acknowledgement or partner decision]
  V --> S[Settle current source uses]
  M --> C[Settle materialization without regressing newer pointer]
  U -->|definite stale source| X[Cancel on engagement and fence authorization]
  X --> S
```

Management acknowledgement does not invent partner-duty prerequisites. Partner evaluation requires its exact current staff/duty/rule/ratification and service facts. All temporal intervals must hold at irreversible DecisionAt. Admission alone cannot materialize the personal decision. Authorized historical work finishes forward. LOCK-05 defines the epoch/cancellation race.

## Engagement acceptance and actual staff assignment

```mermaid
flowchart LR
  A[Admit complete exact acceptance or assignment intent] --> D[Seal and drain earlier personal materializations]
  D --> U[Acquire complete eligibility source uses and temporary practice reservations]
  U --> V[Verify all exact grants and current temporal eligibility]
  V --> I[Authorize irreversible practice commitments]
  I --> W[Commit every permanent staff and canonical user wall]
  W --> F[Record exact acceptance or actual assignment]
  F --> S[Settle temporary practice and service epoch reservations]
  I --> E[Settle historical decision source uses]
  U -->|definite preauthorization conflict| X[Reject and fence all delayed receiving operations]
  X --> S
  X --> E
```

Freeze the whole final reservation set before acquiring it, using canonical stream-key order without upgrades. Every temporary acquire, including practice reservations, derives from the original immutable ServiceEngagementAcceptanceRequested or ServiceEngagementStaffAssignmentRequested, so no acquire requires another grant event first. Process dispatch follows the recorded complete key manifest, not source row order. EngagementPracticeCommitmentsAuthorized requires all exact epoch/source/practice grants and current temporal validity before any permanent wall. LOCK-20 deliberately places a practice key before the epoch key to prove the ordering.  No permanent commitment can be erased after irreversible authorization. Source changes invalidate old eligibility authoritatively; reevaluation cannot reactivate acceptance. Permanent walls survive removal and closure.

## Historical client independence preview

```mermaid
flowchart LR
  A[Client service owner checks ExpectedServiceEpoch] --> S[Freeze complete historical service rule and practice snapshot]
  S --> E[Admit immutable evaluation]
  E --> C[Child computes only from accepted snapshot]
  C --> R[Record historical preview with production blocked]
```

An infrastructure gap is not an empty service history. Preview cannot reserve practice authority or satisfy engagement acceptance. Its own EvaluationId identifies the immutable operation and result.

## Provider review and accepted-risk linkage

```mermaid
flowchart LR
  R[Personal review admits exact report and authored conclusion] --> U[Grant exact current report use]
  U --> V[Record review and settle use]
  G[Open gap checks ExpectedGapRevision and unique pair] --> L[Admit exact active accepted risk at linkage instant]
  L --> C[Materialize independent historical risk link]
  N[Permitted newer supporting source closes gap] --> G
```

Review and gap lifecycle have different owners. Review source uses hold through native decision. A link admitted before later closure still materializes; closure first prevents new linkage. Risk linkage never closes a gap. LOCK-03 fixes this ordering.

## Campaign participant admission and closure

```mermaid
flowchart LR
  A[Personal acknowledgement completion or allowed waiver] --> P[Campaign admits complete exact participant intent]
  P --> M[Materialize independent participant result]
  C[Close campaign] --> S[Seal new audience and participant admission]
  S --> D[Drain all earlier admitted materializations]
  M --> D
  D --> U[Grant exact complete participant closure uses]
  U --> F[Verify existing closure and independence gates]
  F --> N[Record campaign closed]
  N --> R[Settle uses and install exact parent closure fences]
```

Required ExpectedCampaignRevision belongs to the campaign, not a new participant. Preserve current acknowledgement, training and waiver actor/evidence rules. Existing admitted work cannot disappear because close won a later append. A temporary use release must retain the closed-parent generation fence.
