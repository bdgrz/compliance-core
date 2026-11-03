# AccessExpectation

- Classification: `business_aggregate`
- Source area: `AccessReviews` (derived from source path; not a bounded-context ruling).
- Business aggregate: `true`
- Selected status: `SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED`


## Identity and invariant

**Selected identity:** Tenant realm and immutable system-instance binding plus root expectation-line identity. The first proposal roots the line; every successor shares the exact approved predecessor line conflict key. Existing physical identity is preserved.

**Invariant:** One exact system-instance expectation lineage owns predecessor/successor competition, independent approval and same-line exceptions. Different lines may coexist, including different policies on one system. No cross-line exclusion governor or ban is implied; a future ban requires a separately reviewed precise predicate.

**Additional decision rule:** An expectation is scoped to one system instance and carries one rule kind: forbidden_principal_kind (names only a principal kind), privileged_entitlement or requires_expiry, with parameters validated per kind, a named approver, and a monotonically increasing version on its line. Any other rule kind is refused.

**Must preserve:** Independent proposer/reviewer or exact waiver; exception references approved expectation and future expiry.

**Replay:** Current required authorization precedes replay; exact authorized retries resolve the original accepted result using the selected logical identity and immutable actor/intent/source binding. Fresh mutable feasibility, clock selection and body preparation cannot replace that original result. Expected revision is concurrency precondition, not a new identity.

**Lifecycle:** [Placeholder: a consolidated lifecycle sequence is not present in the selected owner record.]





## Commands

| Command | Kind | Identity field |
| --- | --- | --- |
| [ApproveAccessExpectation](../commands/access-expectation.md#approveaccessexpectation) | http | CommandId |
| [ExemptAccessExpectation](../commands/access-expectation.md#exemptaccessexpectation) | http | CommandId |
| [ProposeAccessExpectation](../commands/access-expectation.md#proposeaccessexpectation) | http | ExpectationId |

## Events

| Event | Discriminator | Version |
| --- | --- | --- |
| [AccessExpectationApproved](../events/access-expectation.md#accessexpectationapproved) | bdgrz.access_expectation.approved | 2 |
| [AccessExpectationExceptionRecorded](../events/access-expectation.md#accessexpectationexceptionrecorded) | bdgrz.access_expectation.exception_recorded | 2 |
| [AccessExpectationProposed](../events/access-expectation.md#accessexpectationproposed) | bdgrz.access_expectation.proposed | 2 |

## Cross-owner flows

[Placeholder: no choreography row names this owner.]

## Product rules

- **D07-08** — An access expectation is scoped to a system instance, has a rule kind (forbidden_principal_kind, privileged_entitlement, or requires_expiry) with parameters and an approver, and is versioned. Decision: [m0-d07-access-review-population.md](../decisions/m0-d07-access-review-population.md).

## Adopted architecture decisions

- `OWNER_AccessExpectationScopeGuard` — line_authority_explicit_scope_override.

## Evidence and complete record

- Baseline source: [Placeholder: source file is not present in this checkout]
- Aggregate review: [Placeholder: no owner-specific review file is present]
- Full selected record: [JSON](../data/aggregates/access-expectation.json)

> Target definition is separate from runtime qualification. Implementation status recorded by the source: `SEPARATE_VERIFICATION_REQUIRED_IMPLEMENTATION_HALTED`.
