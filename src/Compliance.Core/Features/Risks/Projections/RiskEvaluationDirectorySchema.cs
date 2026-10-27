using System.Globalization;
using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.Risks;

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
