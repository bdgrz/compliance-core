using System.Globalization;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

static class RiskDraftDirectorySchema
{
    public static readonly KvDirectoryIndex<RiskDraftDirectoryIdentity> ByProgramIdentifier = new(
        "by_program_identifier", 1,
        static risk => [risk.Header.ProgramId.ToString(), risk.Header.Identifier]);

    public static readonly KvDirectory<RiskDraftDirectoryIdentity, Uuid> Risks = new(
        "risk_drafts", ComplianceCoreJsonContext.Default.RiskDraftDirectoryIdentity,
        static risk => risk.Header.RiskId,
        static riskId => [riskId.ToString()], [ByProgramIdentifier]);

    public static readonly KvDirectory<RiskDraftDirectoryIdentity, string> Revisions = new(
        "risk_draft_revisions", ComplianceCoreJsonContext.Default.RiskDraftDirectoryIdentity,
        static revision => RevisionKey(revision.Header.RiskId, revision.Header.Revision),
        static key => [key], []);

    public static string RevisionKey(Uuid riskId, long revision) =>
        $"{riskId}:{revision.ToString("D20", CultureInfo.InvariantCulture)}";
}
