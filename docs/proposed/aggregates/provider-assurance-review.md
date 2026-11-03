# ProviderAssuranceReview

- Classification: `business_aggregate`
- Source area: `Providers` (derived from source path; not a bounded-context ruling).
- Business aggregate: `true`
- Selected status: `SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED`

## Identity and invariant

**Selected identity:** Tenant-scoped tenant / reviewId; `ReviewId` on `RecordProviderReview` is the caller-supplied immutable creation identity (exact immutable creation retry) so an exact retry resolves the original result and a different id is a different record; later mutations use CommandId bound to this identity and the expected revision.

**Invariant:** One immutable personal same-provider due-diligence review bound to exact report revision, evidence kind, validity window and cadence.

**Additional decision rule:** A review records evidence kind soc2_type1, soc2_type2, iso27001 or questionnaire, with the questionnaire accepted only when no SOC 2 report or ISO/IEC 27001 certificate exists; its next review due date is after the review date and, for a material provider, no more than 12 months after it. A non-material provider needs only its recorded classification rationale.

**Must preserve:** Risk adoption cannot close a gap or occur after closure; each review binds same provider/current report.

**Replay:** Current required authorization precedes replay; exact authorized retries resolve the original accepted result using the selected logical identity and immutable actor/intent/source binding. Fresh mutable feasibility, clock selection and body preparation cannot replace that original result. Expected revision is concurrency precondition, not a new identity.

**Lifecycle:** [Placeholder: a consolidated lifecycle sequence is not present in the selected owner record.]

## Commands

| Command | Kind | Identity field |
| --- | --- | --- |
| [RecordProviderReview](../commands/provider-assurance-review.md#recordproviderreview) | http | ReviewId |
| [AbortProviderReview](../commands/provider-assurance-review.md#abortproviderreview) | abort | CommandId |
| [CompleteProviderAssuranceReview](../commands/provider-assurance-review.md#completeproviderassurancereview) | receiver | CommandId |

## Events

| Event | Discriminator | Version |
| --- | --- | --- |
| [ProviderReviewCanceled](../events/provider-assurance-review.md#providerreviewcanceled) | bdgrz.provider-assurance-review.provider-review-canceled | 1 |
| [ProviderReviewRecorded](../events/provider-assurance-review.md#providerreviewrecorded) | bdgrz.provider.review.recorded | 1 |
| [ProviderReviewRequested](../events/provider-assurance-review.md#providerreviewrequested) | bdgrz.provider-assurance-review.provider-review-requested | 1 |

## Cross-owner flows

| Row | Kind | Source owner | Target owner | Command |
| --- | --- | --- | --- | --- |
| [T069](../reactors/provider-review-reactor.md#t069) | reaction_or_regular_process_stage | ProviderAssuranceReview | ProviderAssuranceReport | AuthorizeProviderAssuranceReportProviderReview |
| [T070](../reactors/provider-review-reactor.md#t070) | reaction_or_regular_process_stage | ProviderAssuranceReview | ProviderAssuranceReport | CloseProviderAssuranceReportProviderReviewAuthorization |
| [T077](../reactors/provider-review-reactor.md#t077) | reaction_or_regular_process_stage | ProviderAssuranceReview | ProviderAssuranceReview | CompleteProviderAssuranceReview |
| [A011](../reactors/unassigned-architecture-rows.md#a011) | process_abort_binding | ProviderAssuranceReview | ProviderAssuranceReview | AbortProviderReview |

## Product rules

- **D11-03** — A provider review's next review due date must be no later than one year after the review date for a material provider. Source: [m0-d11-vendors.md](../../product/decisions/m0-d11-vendors.md).
- **D11-04** — A provider review records the provider, review time, evidence kind (soc2_type1, soc2_type2, iso27001, questionnaire), optional assurance report, conclusion, and reviewer. Source: [m0-d11-vendors.md](../../product/decisions/m0-d11-vendors.md).
- **D11-02** — A material provider requires a current SOC 2 report, ISO/IEC 27001 certificate, or, if neither exists, a completed security questionnaire, reviewed at onboarding and at least annually; a non-material provider needs only a recorded classification rationale. Source: [m0-d11-vendors.md](../../product/decisions/m0-d11-vendors.md).

## Adopted architecture decisions

[Placeholder: no adopted architecture-decision reference is recorded.]

## Evidence and complete record

- Baseline source: [Placeholder: source file is not present in this checkout]
- Aggregate review: [Placeholder: no owner-specific review file is present]
- Full selected record: [JSON](../data/aggregates/provider-assurance-review.json)

> Target definition is separate from runtime qualification. Implementation status recorded by the source: `SEPARATE_VERIFICATION_REQUIRED_IMPLEMENTATION_HALTED`.
