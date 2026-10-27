using System.Globalization;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
namespace Bdgrz.Compliance.Features.Applications.Rules;

static class ApplicationPolicyReferenceSchema
{
    public static readonly KvDirectoryIndex<ApplicationPolicyReferenceView> ByRecord =
        new("by_governed_record", 1, static reference =>
        [reference.SubjectType, reference.GovernedRecordId.ToString(), reference.PolicyId.ToString(),
            VersionKey(reference.Version), reference.EntryId.ToString()]);

    public static readonly KvDirectoryIndex<ApplicationPolicyReferenceView> ByPolicyVersion =
        new("by_policy_version", 1, static reference =>
        [reference.PolicyId.ToString(), VersionKey(reference.Version),
            reference.EntryId.ToString()]);

    public static readonly KvDirectory<ApplicationPolicyReferenceView,
        (Uuid PolicyId, string VersionKey, Uuid EntryId)> References = new(
        "application_policy_references",
        ComplianceCoreJsonContext.Default.ApplicationPolicyReferenceView,
        static reference => (reference.PolicyId, VersionKey(reference.Version),
            reference.EntryId),
        static key => [key.PolicyId.ToString(), key.VersionKey, key.EntryId.ToString()],
        [ByRecord, ByPolicyVersion]);

    static string VersionKey(long? version) => version?.ToString(CultureInfo.InvariantCulture) ??
        "draft";

    public static readonly KvDirectory<ApplicationPolicyReferenceState, Uuid> States = new(
        "application_policy_reference_states",
        ComplianceCoreJsonContext.Default.ApplicationPolicyReferenceState,
        static state => state.PolicyId, static policyId => [policyId.ToString()], []);

    public static readonly KvDirectory<ApplicationPolicyReferenceCoverage, Uuid> Coverage = new(
        "application_policy_reference_coverage",
        ComplianceCoreJsonContext.Default.ApplicationPolicyReferenceCoverage,
        static state => state.TenantId, static tenantId => [tenantId.ToString()], []);
}
