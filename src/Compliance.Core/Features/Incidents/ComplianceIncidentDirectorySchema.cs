using System.Globalization;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Incidents;

static class ComplianceIncidentDirectorySchema
{
    public static readonly KvDirectoryIndex<ComplianceIncidentSummaryView> ByProgramUpdatedAt = new(
        "by_program_updated_at", 1, static incident => [incident.ProgramId.ToString(),
            UpdatedAtSortKey(incident.UpdatedAt), incident.ComplianceIncidentId.ToString()]);

    public static readonly KvDirectory<ComplianceIncidentView, Uuid> Details = new(
        "compliance_incident_details", ComplianceCoreJsonContext.Default.ComplianceIncidentView,
        static incident => incident.ComplianceIncidentId,
        static incidentId => [incidentId.ToString()], []);

    public static readonly KvDirectory<ComplianceIncidentSummaryView, Uuid> Summaries = new(
        "compliance_incident_summaries",
        ComplianceCoreJsonContext.Default.ComplianceIncidentSummaryView,
        static incident => incident.ComplianceIncidentId,
        static incidentId => [incidentId.ToString()], [ByProgramUpdatedAt]);

    static string UpdatedAtSortKey(DateTimeOffset updatedAt) =>
        (DateTime.MaxValue.Ticks - updatedAt.UtcDateTime.Ticks)
        .ToString("D20", CultureInfo.InvariantCulture);
}
