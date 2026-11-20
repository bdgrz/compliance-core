using Bdgrz.Compliance.Features.Criteria.Commands;
using Bdgrz.Compliance.Features.Criteria.Models;
using Bdgrz.Compliance.Features.Criteria.Queries;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria;

/// <summary>Maps platform-operator criteria authoring and exact operation receipt routes.</summary>
/// <remarks>
/// Pending receipts remain live reads while independently retried participant decisions settle;
/// they are not frozen as native first responses.
/// </remarks>
public static class CriteriaAuthoringEndpoints
{
    /// <summary>Maps the authoring endpoints.</summary>
    public static IEndpointRouteBuilder MapCriteriaAuthoringEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapPortiaPut<DraftCriteriaEdition, CriteriaOperationReceipt>(
                "/v1/system/criteria-editions/{edition_id}/draft-operations/{operation_id}")
            .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser).WithTags("Criteria authoring");
        endpoints.MapPortiaPut<ChooseInitialEditionLabel, CriteriaOperationReceipt>(
                "/v1/system/criteria-editions/{edition_id}/initial-label-operations/{operation_id}")
            .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser).WithTags("Criteria authoring");
        endpoints.MapPortiaPut<CancelCriteriaEditionDraftRequest, CriteriaOperationReceipt>(
                "/v1/system/criteria-editions/{edition_id}/draft-cancellations/{operation_id}")
            .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser).WithTags("Criteria authoring");
        endpoints.MapPortiaPut<BeginCriteriaEditionRename, CriteriaOperationReceipt>(
                "/v1/system/criteria-editions/{edition_id}/rename-operations/{operation_id}")
            .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser).WithTags("Criteria authoring");
        endpoints.MapPortiaPut<CancelCriteriaEditionRename, CriteriaOperationReceipt>(
                "/v1/system/criteria-editions/{edition_id}/rename-cancellations/{operation_id}")
            .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser).WithTags("Criteria authoring");
        endpoints.MapPortiaPut<AbandonCriteriaEditionDraft, CriteriaOperationReceipt>(
                "/v1/system/criteria-editions/{edition_id}/abandon-operations/{operation_id}")
            .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser).WithTags("Criteria authoring");
        endpoints.MapPortiaPut<BeginCriteriaPublication, CriteriaOperationReceipt>(
                "/v1/system/criteria-editions/{edition_id}/publication-attempts/{attempt_id}")
            .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser).WithTags("Criteria authoring");
        endpoints.MapPortiaPut<ProposeCriterionChange, CriteriaOperationReceipt>(
                "/v1/system/criteria-editions/{edition_id}/entries/{identifier}/change-operations/{operation_id}")
            .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser).WithTags("Criteria authoring");
        endpoints.MapPortiaPut<CorrectCriterionSummary, CriteriaOperationReceipt>(
                "/v1/system/criteria-editions/{edition_id}/entries/{identifier}/summary-operations/{operation_id}")
            .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser).WithTags("Criteria authoring");
        endpoints.MapPortiaPut<SetCriteriaEditionField, CriteriaOperationReceipt>(
                "/v1/system/criteria-editions/{edition_id}/fields/{field}/operations/{operation_id}")
            .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser).WithTags("Criteria authoring");
        endpoints.MapPortiaPut<SetCriteriaEditionDisclosure, CriteriaOperationReceipt>(
                "/v1/system/criteria-editions/{edition_id}/disclosures/{category}/{code}/operations/{operation_id}")
            .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser).WithTags("Criteria authoring");

        endpoints.MapPortiaGet<GetCriteriaEditionDraft, CriteriaEditionDraftView>(
                "/v1/system/criteria-editions/{edition_id}/draft")
            .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser).WithTags("Criteria authoring");
        endpoints.MapPortiaGet<GetCriteriaOperation, CriteriaOperationReceipt>(
                "/v1/system/criteria-editions/{edition_id}/operations/{operation_id}")
            .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser).WithTags("Criteria authoring");
        endpoints.MapPortiaGet<GetCriteriaEditionValueOperation, CriteriaOperationReceipt>(
                "/v1/system/criteria-editions/{edition_id}/value-operations/{operation_id}")
            .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser).WithTags("Criteria authoring");
        return endpoints;
    }
}
