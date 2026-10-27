using Bdgrz.Compliance.Features.Providers.Models;
using System.Globalization;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers.Projections;

static class AssuranceDirectorySchema
{
    public static readonly KvDirectoryIndex<AssuranceReportView> ReportsByProvider = new("reports_by_provider", 1,
        static view => [view.ProviderId.ToString(), view.ReportId.ToString()]);

    public static readonly KvDirectory<AssuranceReportView, string> Reports = new("assurance_reports",
        ComplianceCoreJsonContext.Default.AssuranceReportView, static view => RecordKey(view.ProviderId, view.ReportId),
        static key => [key], [ReportsByProvider]);

    public static readonly KvDirectory<AssuranceReportView, string> ReportRevisions =
        new("assurance_report_revisions", ComplianceCoreJsonContext.Default.AssuranceReportView,
            static view => ReportRevisionKey(view.ProviderId, view.ReportId, view.Revision), static key => [key], []);

    /// <summary>A provider-scoped record key: two providers may never collide on a caller-chosen record id.</summary>
    public static string RecordKey(Uuid providerId, Uuid recordId) => $"{providerId}:{recordId}";

    public static string ReportRevisionKey(Uuid providerId, Uuid reportId, long revision) =>
        $"{RecordKey(providerId, reportId)}:{revision.ToString("D20", CultureInfo.InvariantCulture)}";

    public static readonly KvDirectoryIndex<ProviderReviewView> ReviewsByProvider = new("reviews_by_provider", 1,
        static view => [view.ProviderId.ToString(),
            view.Content.ReviewedAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), view.ReviewId.ToString()]);

    public static readonly KvDirectory<ProviderReviewView, Uuid> Reviews = new("provider_reviews",
        ComplianceCoreJsonContext.Default.ProviderReviewView, static view => view.ReviewId,
        static id => [id.ToString()], [ReviewsByProvider]);

    public static readonly KvDirectoryIndex<ProviderCoverageGapView> CoverageGapsByProvider =
        new("coverage_gaps_by_provider", 1, static view =>
        [
            view.ProviderId.ToString(),
            view.RecordedAt.UtcTicks.ToString("D20", CultureInfo.InvariantCulture),
            view.GapId.ToString(),
        ]);

    public static readonly KvDirectory<ProviderCoverageGapView, string> CoverageGaps =
        new("provider_coverage_gaps", ComplianceCoreJsonContext.Default.ProviderCoverageGapView,
            static view => RecordKey(view.ProviderId, view.GapId), static key => [key], [CoverageGapsByProvider]);
}
