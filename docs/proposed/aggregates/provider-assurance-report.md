# ProviderAssuranceReport

- Classification: `business_aggregate`
- Source area: `Providers` (derived from source path; not a bounded-context ruling).
- Business aggregate: `true`
- Selected status: `SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED`

## Identity and invariant

**Selected identity:** Tenant-scoped (tenantId, providerId, reportId); `ReportId` on `RecordAssuranceReport` is the caller-supplied immutable creation identity (exact immutable creation retry) so an exact retry resolves the original result and a different id is a different record; later mutations use CommandId bound to this identity and the expected revision.

**Invariant:** Evidence report for one provider and exact provider revision.

**Additional decision rule:** A report records kind (soc2_type1, soc2_type2, iso27001, questionnaire), period or point-in-time date, issuing firm, opinion (unqualified, qualified, adverse, disclaimer), covered services, exceptions, CUECs, coverage gaps, a bridge letter when the report period ends before the need, and a governed artifact reference.

**Must preserve:** Risk adoption cannot close a gap or occur after closure; each review binds same provider/current report.

**Replay:** Current required authorization precedes replay; exact authorized retries resolve the original accepted result using the selected logical identity and immutable actor/intent/source binding. Fresh mutable feasibility, clock selection and body preparation cannot replace that original result. Expected revision is concurrency precondition, not a new identity.

**Lifecycle:** [Placeholder: a consolidated lifecycle sequence is not present in the selected owner record.]

## Commands

| Command | Kind | Identity field |
| --- | --- | --- |
| [RecordAssuranceReport](../commands/provider-assurance-report.md#recordassurancereport) | http | ReportId |
| [ReviseAssuranceReport](../commands/provider-assurance-report.md#reviseassurancereport) | http | CommandId |
| [AuthorizeProviderAssuranceReportProviderReviewUse](../commands/provider-assurance-report.md#authorizeproviderassurancereportproviderreviewuse) | receiver | CommandId |
| [SettleProviderAssuranceReportProviderReviewUse](../commands/provider-assurance-report.md#settleproviderassurancereportproviderreviewuse) | receiver | CommandId |

## Events

| Event | Discriminator | Version |
| --- | --- | --- |
| [AssuranceReportRecorded](../events/provider-assurance-report.md#assurancereportrecorded) | bdgrz.provider.assurance_report.recorded | 1 |
| [AssuranceReportRevised](../events/provider-assurance-report.md#assurancereportrevised) | bdgrz.provider.assurance_report.revised | 1 |
| [ProviderAssuranceReportProviderReviewUseAuthorized](../events/provider-assurance-report.md#providerassurancereportproviderreviewuseauthorized) | bdgrz.provider-assurance-report.provider-assurance-report-provider-review-use-authorized | 1 |
| [ProviderAssuranceReportProviderReviewUseSettled](../events/provider-assurance-report.md#providerassurancereportproviderreviewusesettled) | bdgrz.provider-assurance-report.provider-assurance-report-provider-review-use-settled | 1 |

## Cross-owner flows

| Row | Kind | Source owner | Target owner | Command |
| --- | --- | --- | --- | --- |
| [T069](../reactors/provider-review-reactor.md#t069) | reaction_or_regular_process_stage | ProviderAssuranceReview | ProviderAssuranceReport | AuthorizeProviderAssuranceReportProviderReviewUse |
| [T070](../reactors/provider-review-reactor.md#t070) | reaction_or_regular_process_stage | ProviderAssuranceReview | ProviderAssuranceReport | SettleProviderAssuranceReportProviderReviewUse |

## Product rules

- **D11-05** — An assurance report records kind, period or point-in-time date, issuing firm, opinion (unqualified, qualified, adverse, disclaimer), covered services, exceptions, relevant CUECs, coverage gaps, bridge letter when the report period ends before the need, and a governed artifact reference. Source: [m0-d11-vendors.md](../../product/decisions/m0-d11-vendors.md).

## Adopted architecture decisions

[Placeholder: no adopted architecture-decision reference is recorded.]

## Evidence and complete record

- Baseline source: [Placeholder: source file is not present in this checkout]
- Aggregate review: [Placeholder: no owner-specific review file is present]
- Full selected record: [JSON](../data/aggregates/provider-assurance-report.json)

> Target definition is separate from runtime qualification. Implementation status recorded by the source: `SEPARATE_VERIFICATION_REQUIRED_IMPLEMENTATION_HALTED`.
