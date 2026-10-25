using System.Globalization;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests;

static class AuditorRequestDirectorySchema
{
    // Identity rows retain original creation ordering; lifecycle/status bodies are always composed from native owners.
    public static readonly KvDirectoryIndex<AuditorRequestSummaryView> ByProgramCreated = new("by_program_created", 1,
        static row => [row.ProgramId.ToString(), row.UpdatedAt.UtcTicks.ToString("D20", CultureInfo.InvariantCulture),
            row.AuditorRequestId.ToString()]);
    // RequestId is the actual tenant-scoped first owner; ProgramId is verified immutable creation scope.
    public static readonly KvDirectory<AuditorRequestSummaryView, Uuid> Requests = new("request_identities",
        ComplianceCoreJsonContext.Default.AuditorRequestSummaryView, static row => row.AuditorRequestId,
        static id => [id.ToString()], [ByProgramCreated]);
    public static readonly KvDirectoryIndex<AuditorSampleSelectionLocator> ByProgramSelection = new("by_program_selection", 1,
        static row => [row.ProgramId.ToString(), row.SampleSelectionId.ToString(), row.AuditorRequestId.ToString()]);
    // A selection UUID is local to its request owner; never overwrite another admitted request's locator.
    public static readonly KvDirectory<AuditorSampleSelectionLocator, string> Selections = new("selection_identities",
        ComplianceCoreJsonContext.Default.AuditorSampleSelectionLocator,
        static row => $"{row.ProgramId}:{row.AuditorRequestId}:{row.SampleSelectionId}", static key => [key], [ByProgramSelection]);
}
