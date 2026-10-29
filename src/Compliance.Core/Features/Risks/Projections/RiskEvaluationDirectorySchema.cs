using System.Globalization;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
namespace Bdgrz.Compliance.Features.Risks.Projections;

static class RiskEvaluationDirectorySchema
{
    public static readonly KvDirectory<RiskEvaluationDirectoryIdentity, Uuid> Evaluations = new("risk_evaluation_identities_v2",
        ComplianceCoreJsonContext.Default.RiskEvaluationDirectoryIdentity, static value => value.RiskId, static id => [id.ToString()], []);
    public static readonly KvDirectoryIndex<RiskEvaluationDirectoryIdentity> ByRiskRevision = new("by_risk_revision", 1,
        static value => [value.RiskId.ToString(), value.Revision.ToString("D20", CultureInfo.InvariantCulture)]);
    public static readonly KvDirectory<RiskEvaluationDirectoryIdentity, string> History = new("risk_evaluation_history_identities_v2",
        ComplianceCoreJsonContext.Default.RiskEvaluationDirectoryIdentity, static value => HistoryKey(value.RiskId, value.Revision),
        static key => [key], [ByRiskRevision]);
    public static string HistoryKey(Uuid riskId, long revision) => $"{riskId}:{revision.ToString("D20", CultureInfo.InvariantCulture)}";
}
