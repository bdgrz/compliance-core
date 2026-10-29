using System.Globalization;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
namespace Bdgrz.Compliance.Features.Risks.History;

static class RiskDraftHistoryDirectoryV1Schema
{
    public static readonly KvDirectoryIndex<RiskDraftDirectoryIdentity> ByRiskRevision = new(
        "by_risk_revision", 1, static revision =>
            [revision.Header.RiskId.ToString(),
                revision.Header.Revision.ToString("D20", CultureInfo.InvariantCulture)]);

    public static readonly KvDirectory<RiskDraftDirectoryIdentity, string> Revisions = new(
        "risk_draft_history_revisions_v1",
        ComplianceCoreJsonContext.Default.RiskDraftDirectoryIdentity,
        static revision => RevisionKey(revision.Header.RiskId, revision.Header.Revision),
        static key => [key], [ByRiskRevision]);

    public static string RevisionKey(Uuid riskId, long revision) =>
        $"{riskId}:{revision.ToString("D20", CultureInfo.InvariantCulture)}";
}
