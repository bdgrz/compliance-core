using Cntryl.Portia;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.Remediation.Aggregates;
namespace Bdgrz.Compliance.Features.Work.Projections;

static class FindingClosureWorkItemDirectorySchema
{
    public static readonly KvDirectoryIndex<FindingClosureWorkState> ByProgram =
        new("by_program", 1, static finding =>
            [finding.ProgramId.ToString(), finding.FindingId.ToString()]);

    public static readonly KvDirectory<FindingClosureWorkState, Uuid> Findings = new(
        "findings", ComplianceCoreJsonContext.Default.FindingClosureWorkState,
        static finding => FindingAggregate.StreamId(finding.ProgramId, finding.FindingId),
        static key => [key.ToString()], [ByProgram]);
}
