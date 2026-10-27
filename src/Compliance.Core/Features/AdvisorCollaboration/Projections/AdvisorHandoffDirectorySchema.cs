using System.Globalization;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Models;
namespace Bdgrz.Compliance.Features.AdvisorCollaboration.Projections;

static class AdvisorHandoffDirectorySchema
{
    public static readonly KvDirectoryIndex<AdvisorHandoffDirectoryEntry> ByProgramUpdatedAt = new(
        "by_program_updated_at", 1, static handoff => [handoff.ProgramId.ToString(),
            UpdatedAtSortKey(handoff.UpdatedAt), handoff.AdvisorRequestId.ToString()]);

    public static readonly KvDirectoryIndex<AdvisorHandoffDirectoryEntry> ByProgramStatusUpdatedAt = new(
        "by_program_status_updated_at", 1, static handoff => [handoff.ProgramId.ToString(),
            handoff.Status, UpdatedAtSortKey(handoff.UpdatedAt), handoff.AdvisorRequestId.ToString()]);

    public static readonly KvDirectory<AdvisorHandoffDirectoryEntry, Uuid> Handoffs = new(
        "advisor_handoffs", ComplianceCoreJsonContext.Default.AdvisorHandoffDirectoryEntry,
        static handoff => handoff.AdvisorRequestId,
        static handoffId => [handoffId.ToString()], [ByProgramUpdatedAt, ByProgramStatusUpdatedAt]);

    static string UpdatedAtSortKey(DateTimeOffset updatedAt) =>
        (DateTime.MaxValue.Ticks - updatedAt.UtcDateTime.Ticks)
        .ToString("D20", CultureInfo.InvariantCulture);
}
