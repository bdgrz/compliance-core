using Bdgrz.Compliance.Features.AccessControl;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization.Metadata;

namespace Bdgrz.Compliance.Features.NativeOperations;

/// <summary>
/// Static allowlist from result schema name to the concrete source-generated serializer metadata. A schema string
/// supplied at runtime selects an entry here; it never instantiates a type by name.
/// </summary>
public static class ResultSchemaRegistry
{
    static readonly Dictionary<string, JsonTypeInfo> Entries = new(StringComparer.Ordinal)
    {
        [FirstResponses.Schema] = ComplianceCoreJsonContext.Default.FirstResponseDescriptor,
        ["operation-accepted-response.v1"] = ComplianceCoreJsonContext.Default.OperationAcceptedResponse,
        ["operation-status-response.v1"] = ComplianceCoreJsonContext.Default.OperationStatusResponse,
        ["mutation-result-content.v1"] = ComplianceCoreJsonContext.Default.MutationResultContent,
        [AdministratorTransitionResponse.Schema] = ComplianceCoreJsonContext.Default.AdministratorTransitionResponse,
        ["FirmStaffChangeRecorded.StaffContent.v1"] = ComplianceCoreJsonContext.Default.FirmStaffMemberView,
        ["FirmProfessionalDutyDesignationRecorded.DesignationContent.v1"] =
            ComplianceCoreJsonContext.Default.FirmProfessionalDutyDesignationView,
        ["FirmProfessionalDutyDesignationRevoked.DesignationContent.v1"] =
            ComplianceCoreJsonContext.Default.FirmProfessionalDutyDesignationView,
        [IndependenceContent.ServiceSchema] = ComplianceCoreJsonContext.Default.NonattestServiceView,
        [IndependenceContent.EvaluationSchema] = ComplianceCoreJsonContext.Default.IndependenceEvaluationView,
        [IndependenceContent.EngagementSchema] = ComplianceCoreJsonContext.Default.ServiceEngagementView,
        [IndependenceContent.RevocationSchema] = ComplianceCoreJsonContext.Default.ServiceEngagementAcceptanceView,
    };

    /// <summary>Registered schema names.</summary>
    public static IReadOnlyCollection<string> Schemas => Entries.Keys;

    /// <summary>Looks up the serializer metadata registered for a schema.</summary>
    public static bool TryGet(string schema, [NotNullWhen(true)] out JsonTypeInfo? info)
    {
        ArgumentNullException.ThrowIfNull(schema);
        return Entries.TryGetValue(schema, out info);
    }
}
