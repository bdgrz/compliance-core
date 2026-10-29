using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Providers.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.Providers.Services;

/// <summary>
///     Pure evaluation of recorded assurance facts at an as-of date. A report is current for one
///     annual cycle after its period ends (M0-D11); a bridge letter extends only the interval it
///     names and is never an auditor opinion, so neither a stale report nor a bridge letter can
///     imply current or complete coverage.
/// </summary>
public static class ProviderAssuranceCoverage
{
    public static string Currency(AssuranceReportContent content, DateOnly asOf) =>
        asOf > content.PeriodEnd.AddMonths(AssuranceRules.AnnualMonths) ? "stale" : "current";

    public static string PeriodCoverage(AssuranceReportContent content, DateOnly asOf)
    {
        if (asOf < (content.PeriodStart ?? content.PeriodEnd))
            return "not_yet_effective";
        if (asOf <= content.PeriodEnd)
            return "covers_as_of";
        if (content.BridgeLetter is not { } bridge || asOf < bridge.CoversFrom)
            return "uncovered_after_period";
        return asOf <= bridge.CoversThrough ? "bridged_only" : "bridge_expired";
    }

    public static AssuranceReportCoverageView Evaluate(AssuranceReportView report, DateOnly asOf)
    {
        var content = report.Content;
        var currency = Currency(content, asOf);
        var coverage = PeriodCoverage(content, asOf);
        var reasons = new List<string>();
        if (currency == "stale")
            reasons.Add("report_stale");
        if (coverage != "covers_as_of")
            reasons.Add(coverage);
        if (content.Opinion is "qualified" or "adverse" or "disclaimer")
            reasons.Add($"opinion_{content.Opinion}");
        if (report.ExceptionCount > 0)
            reasons.Add("exceptions_noted");
        if (report.CoverageGapCount > 0)
            reasons.Add("coverage_gaps_declared");
        return new AssuranceReportCoverageView(report.ReportId, report.Revision, content.ReportKind, content.Issuer,
            content.PeriodStart, content.PeriodEnd, content.Opinion, report.ExceptionCount, report.CoverageGapCount,
            content.BridgeLetter?.CoversThrough, currency, coverage, reasons.Count == 0, reasons.AsReadOnly());
    }

    public static ProviderAssuranceCoverageView Evaluate(ProviderView provider,
        IReadOnlyCollection<AssuranceReportView> reports, IReadOnlyCollection<ProviderReviewView> reviews, DateOnly asOf)
    {
        var coverage = reports.OrderBy(static report => report.Content.PeriodEnd).ThenBy(static report => report.ReportId)
            .Select(report => Evaluate(report, asOf)).ToArray();
        var latest = reviews.Where(review => review.Content.ReviewedAt <= asOf)
            .OrderByDescending(static review => review.Content.ReviewedAt).ThenByDescending(static review => review.RecordedAt)
            .ThenByDescending(static review => review.ReviewId).FirstOrDefault();
        var reasons = new List<string>();
        string status;
        var complete = false;
        var materiality = provider.Content.Materiality;
        if (materiality == "not_material")
            status = "not_required";
        else if (materiality is null)
        {
            status = "unresolved_materiality";
            reasons.Add("materiality_unresolved");
        }
        else if (latest is null)
        {
            status = "no_review";
            reasons.Add("no_review_recorded");
        }
        else if (latest.Content.Conclusion == "not_acceptable")
        {
            status = "not_acceptable";
            reasons.Add("latest_review_not_acceptable");
        }
        else if (asOf > latest.Content.NextReviewDue)
        {
            status = "review_overdue";
            reasons.Add("next_review_due_passed");
        }
        else if (latest.Content.AssuranceReportId is not { } reportId ||
                 coverage.FirstOrDefault(item => item.ReportId == reportId) is not { } evidence)
        {
            status = "evidence_unrecorded";
            reasons.Add("evidence_report_not_recorded");
        }
        else if (evidence.Currency == "stale")
        {
            status = "evidence_stale";
            reasons.Add("report_stale");
        }
        else
        {
            // Annual currency is what keeps a review in force; whether the assurance period (or an
            // unexpired bridge letter) reaches as-of stays visible as a reason and blocks completeness.
            status = "current";
            reasons.AddRange(evidence.IncompleteReasons);
            if (latest.AssuranceReportRevision != evidence.Revision)
                reasons.Add("report_revised_since_review");
            if (latest.Content.Conclusion != "acceptable")
                reasons.Add("review_has_exceptions");
            complete = reasons.Count == 0;
        }
        return new ProviderAssuranceCoverageView(provider.TenantId, provider.ProviderId, provider.Revision, asOf,
            materiality, status, complete, reasons.AsReadOnly(), latest?.ReviewId, latest?.Content.ReviewedAt,
            latest?.Content.NextReviewDue, latest?.Content.Conclusion, coverage);
    }
}
