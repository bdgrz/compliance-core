using System.Globalization;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.Changes.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
namespace Bdgrz.Compliance.Features.Changes.Projections;

static class SignificantChangeDirectorySchema
{
    public static readonly KvDirectoryIndex<SignificantChangeSummaryView> ByProgramUpdatedAt = new(
        "by_program_updated_at", 1, static change => [change.ProgramId.ToString(),
            UpdatedAtSortKey(change.UpdatedAt), change.SignificantChangeId.ToString()]);

    public static readonly KvDirectory<SignificantChangeDirectoryEntry, Uuid> Details = new(
        "significant_change_authority", ComplianceCoreJsonContext.Default.SignificantChangeDirectoryEntry,
        static change => change.SignificantChangeId,
        static changeId => [changeId.ToString()], []);

    public static readonly KvDirectory<SignificantChangeSummaryView, Uuid> Summaries = new(
        "significant_change_summaries", ComplianceCoreJsonContext.Default.SignificantChangeSummaryView,
        static change => change.SignificantChangeId,
        static changeId => [changeId.ToString()], [ByProgramUpdatedAt]);

    static string UpdatedAtSortKey(DateTimeOffset updatedAt) =>
        (DateTime.MaxValue.Ticks - updatedAt.UtcDateTime.Ticks)
        .ToString("D20", CultureInfo.InvariantCulture);
}
