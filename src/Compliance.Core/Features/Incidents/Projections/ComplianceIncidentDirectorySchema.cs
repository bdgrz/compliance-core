using System.Globalization;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.Incidents.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
namespace Bdgrz.Compliance.Features.Incidents.Projections;

static class ComplianceIncidentDirectorySchema
{
    public static readonly KvDirectoryIndex<ComplianceIncidentSummaryView> ByProgramUpdatedAt = new(
        "by_program_updated_at", 1, static change => [change.ProgramId.ToString(),
            UpdatedAtSortKey(change.UpdatedAt), change.ComplianceIncidentId.ToString()]);

    public static readonly KvDirectory<ComplianceIncidentDirectoryEntry, Uuid> Details = new(
        "compliance_incident_authority", ComplianceCoreJsonContext.Default.ComplianceIncidentDirectoryEntry,
        static change => change.ComplianceIncidentId,
        static changeId => [changeId.ToString()], []);

    public static readonly KvDirectory<ComplianceIncidentSummaryView, Uuid> Summaries = new(
        "compliance_incident_summaries", ComplianceCoreJsonContext.Default.ComplianceIncidentSummaryView,
        static change => change.ComplianceIncidentId,
        static changeId => [changeId.ToString()], [ByProgramUpdatedAt]);

    static string UpdatedAtSortKey(DateTimeOffset updatedAt) =>
        (DateTime.MaxValue.Ticks - updatedAt.UtcDateTime.Ticks)
        .ToString("D20", CultureInfo.InvariantCulture);
}
