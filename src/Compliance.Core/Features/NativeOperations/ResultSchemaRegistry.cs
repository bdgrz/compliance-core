using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Features.Providers;
using Bdgrz.Compliance.Features.AccessReviews;
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
        [UnitResult.Schema] = ComplianceCoreJsonContext.Default.UnitResult,
        ["operation-status-response.v1"] = ComplianceCoreJsonContext.Default.OperationStatusResponse,
        ["mutation-result-content.v1"] = ComplianceCoreJsonContext.Default.MutationResultContent,
        [AdministratorTransitionResponse.Schema] = ComplianceCoreJsonContext.Default.AdministratorTransitionResponse,
        [ApplicationInventoryOperations.ApplicationRegistrationSchema] =
            ComplianceCoreJsonContext.Default.ApplicationRegistration,
        [ApplicationInventoryOperations.SystemInstanceRegistrationSchema] =
            ComplianceCoreJsonContext.Default.SystemInstanceRegistration,
        [ApplicationImportContent.RegistrationSchema] = ComplianceCoreJsonContext.Default.ApplicationImportRegistration,
        ["FirmStaffChangeRecorded.StaffContent.v1"] = ComplianceCoreJsonContext.Default.FirmStaffMemberView,
        ["FirmProfessionalDutyDesignationRecorded.DesignationContent.v1"] =
            ComplianceCoreJsonContext.Default.FirmProfessionalDutyDesignationView,
        ["FirmProfessionalDutyDesignationRevoked.DesignationContent.v1"] =
            ComplianceCoreJsonContext.Default.FirmProfessionalDutyDesignationView,
        [IndependenceContent.ServiceSchema] = ComplianceCoreJsonContext.Default.NonattestServiceView,
        [IndependenceContent.EvaluationSchema] = ComplianceCoreJsonContext.Default.IndependenceEvaluationView,
        [IndependenceContent.EngagementSchema] = ComplianceCoreJsonContext.Default.ServiceEngagementView,
        [IndependenceContent.RevocationSchema] = ComplianceCoreJsonContext.Default.ServiceEngagementAcceptanceView,
        [EngagementAcceptanceEffects.ResultSchema] = ComplianceCoreJsonContext.Default.ServiceEngagementAcceptanceView,
        [EngagementAcceptanceEffects.AcceptanceSchema] = ComplianceCoreJsonContext.Default.ServiceEngagementAcceptanceView,
        [EngagementAcceptanceEffects.AssignedStaffSchema] = ComplianceCoreJsonContext.Default.FirmStaffMemberView,
        [EngagementAcceptanceEffects.PartnerStaffSchema] = ComplianceCoreJsonContext.Default.FirmStaffMemberView,
        [EngagementAcceptanceEffects.PartnerDesignationSchema] =
            ComplianceCoreJsonContext.Default.FirmProfessionalDutyDesignationView,
        [EngagementPersonalDecisionEffects.AcknowledgementSchema] =
            ComplianceCoreJsonContext.Default.EngagementManagementAcknowledgementView,
        [EngagementPersonalDecisionEffects.EvaluationSchema] =
            ComplianceCoreJsonContext.Default.PartnerIndependenceEvaluationView,
        [EngagementPersonalDecisionEffects.RatifiedRulesSchema] =
            ComplianceCoreJsonContext.Default.IndependenceRuleVersionView,
        [EngagementPersonalDecisionEffects.RatificationSchema] =
            ComplianceCoreJsonContext.Default.IndependenceRuleRatificationView,
        ["provider-registration.v1"] = ComplianceCoreJsonContext.Default.ProviderRegistration,
        ["technology-component-registration.v1"] = ComplianceCoreJsonContext.Default.TechnologyComponentRegistration,
        ["information-asset-registration.v1"] = ComplianceCoreJsonContext.Default.InformationAssetRegistration,
        ["data-flow-registration.v1"] = ComplianceCoreJsonContext.Default.DataFlowRegistration,
        [UnitResult.Schema] = ComplianceCoreJsonContext.Default.UnitResult,
        ["location-registration.v1"] = ComplianceCoreJsonContext.Default.LocationRegistration,
        ["operational-process-registration.v1"] = ComplianceCoreJsonContext.Default.OperationalProcessRegistration,
        [ProviderAssuranceContent.ReportRegistrationSchema] = ComplianceCoreJsonContext.Default.AssuranceReportRegistration,
        [ProviderAssuranceContent.GapRegistrationSchema] = ComplianceCoreJsonContext.Default.ProviderCoverageGapRegistration,
        [ProviderAssuranceContent.ReviewRegistrationSchema] = ComplianceCoreJsonContext.Default.ProviderReviewRegistration,
        [AccessReviewContent.CampaignRegistrationSchema] = ComplianceCoreJsonContext.Default.AccessReviewCampaignRegistration,
        [AccessReviewContent.DecisionViewSchema] = ComplianceCoreJsonContext.Default.AccessDecisionView,
        [AccessReviewContent.ChangeViewSchema] = ComplianceCoreJsonContext.Default.AccessRemediationChangeView,
        [AccessReviewContent.VerificationViewSchema] = ComplianceCoreJsonContext.Default.AccessRemediationVerificationView,
        [AccessReviewContent.ExceptionViewSchema] = ComplianceCoreJsonContext.Default.AccessRemediationExceptionView,
        [AccessReviewContent.ReassignmentViewSchema] =
            ComplianceCoreJsonContext.Default.AccessReviewResponsibilityReassignmentView,
        [AccessReviewContent.CompletionViewSchema] = ComplianceCoreJsonContext.Default.AccessReviewCampaignCompletionView,
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
