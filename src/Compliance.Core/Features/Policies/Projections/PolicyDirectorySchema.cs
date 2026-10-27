using Cntryl.Portia;
using Bdgrz.Compliance.Features.Policies.Rules;
namespace Bdgrz.Compliance.Features.Policies.Projections;

static class PolicyDirectorySchema
{
    public static readonly KvDirectoryIndex<PolicyDirectoryRow> ByProgramIdentifier = new(
        "by_program_identifier", 1,
        static row => [row.ProgramId.ToString(), row.Identifier]);

    public static readonly KvDirectory<PolicyDirectoryRow, Uuid> Policies = new(
        "policies", ComplianceCoreJsonContext.Default.PolicyDirectoryRow,
        static row => row.PolicyId, static policyId => [policyId.ToString()],
        [ByProgramIdentifier]);
}
