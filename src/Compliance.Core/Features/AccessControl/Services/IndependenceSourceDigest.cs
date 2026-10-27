using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
namespace Bdgrz.Compliance.Features.AccessControl.Services;

static class IndependenceSourceDigest
{
    public static string Acceptance(ServiceEngagementAcceptanceView acceptance) =>
        Digest(acceptance, ComplianceCoreJsonContext.Default.ServiceEngagementAcceptanceView);

    public static string AcceptanceEvent(ServiceEngagementAcceptanceRecorded accepted) =>
        Digest(accepted, ComplianceCoreJsonContext.Default.ServiceEngagementAcceptanceRecorded);

    public static string AssignmentEvent(ServiceEngagementStaffAssigned assigned) =>
        Digest(assigned, ComplianceCoreJsonContext.Default.ServiceEngagementStaffAssigned);

    /// <summary>The digest of an acceptance source fact: the recorded acceptance or a recorded staff assignment.</summary>
    public static string AcceptanceSource(DomainEvent source) => source switch
    {
        ServiceEngagementAcceptanceRecorded accepted => AcceptanceEvent(accepted),
        ServiceEngagementStaffAssigned assigned => AssignmentEvent(assigned),
        _ => throw new ArgumentException("The fact is not an acceptance source.", nameof(source)),
    };

    public static string DirectorySource(DirectoryStatusSourceView source) =>
        Digest(source, ComplianceCoreJsonContext.Default.DirectoryStatusSourceView);

    public static string DirectoryEvent(FirmStaffChangeRecorded change) =>
        Digest(change, ComplianceCoreJsonContext.Default.FirmStaffChangeRecorded);

    public static string RuleContent(IndependenceRuleContent content) =>
        Digest(content, ComplianceCoreJsonContext.Default.IndependenceRuleContent);

    public static string Services(IReadOnlyList<NonattestServiceView> services) =>
        Digest(services, ComplianceCoreJsonContext.Default.IReadOnlyListNonattestServiceView);

    static string Digest<T>(T value, JsonTypeInfo<T> typeInfo) =>
        Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value, typeInfo)));
}
