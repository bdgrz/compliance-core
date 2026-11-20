using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bdgrz.Compliance;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Applications.Commands;
using Bdgrz.Compliance.Features.Applications.Queries;
using Bdgrz.Compliance.Features.AuditorRequests.Commands;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Queries;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditPopulations.Queries;
using Bdgrz.Compliance.Features.Criteria.Services;
using Bdgrz.Compliance.Features.Evidence.Commands;
using Bdgrz.Compliance.Features.Evidence.Handlers;
using Bdgrz.Compliance.Features.Evidence.Queries;
using Bdgrz.Compliance.Features.MethodologyTemplates.Commands;
using Bdgrz.Compliance.Features.MethodologyTemplates.Models;
using Bdgrz.Compliance.Features.MethodologyTemplates.Queries;
using Bdgrz.Compliance.Features.NamedRecords;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Responsibilities.Commands;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.Responsibilities.Queries;
using Bdgrz.Compliance.Features.Retention.Commands;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Retention.Queries;
using Bdgrz.Compliance.Features.SystemDescription.Commands;
using Bdgrz.Compliance.Features.SystemDescription.Models;
using Bdgrz.Compliance.Features.SystemDescription.Queries;
using Bdgrz.Compliance.Features.UserIdentities.Commands;
using Bdgrz.Compliance.Features.UserIdentities.Queries;
using Bdgrz.Compliance.Features.UserIdentities.Readers;
using Bdgrz.Compliance.Hosting;
using Bdgrz.Compliance.Hosting.NativeResponses;
using Cntryl.Portia;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

var hostMode = ComplianceHostModeParser.Parse(
    Environment.GetEnvironmentVariable("COMPLIANCE_HOST_MODE"));

if (hostMode.RunsApi())
{
    await RunApiAsync(args, hostMode);
}
else
{
    await RunWorkerAsync(args);
}

static async Task RunApiAsync(string[] args, ComplianceHostMode hostMode)
{
    var builder = WebApplication.CreateBuilder(args);
    // Configuration includes environment variables; reading it here also lets a test host set
    // its mode without mutating the process environment.
    hostMode = ComplianceHostModeParser.Parse(builder.Configuration["COMPLIANCE_HOST_MODE"]);
    builder.Services.AddProblemDetails(options =>
    {
        options.CustomizeProblemDetails = context =>
            context.ProblemDetails.Extensions["trace_id"] = context.HttpContext.TraceIdentifier;
    });
    var authentication = builder.Services.AddComplianceAuthentication(
        builder.Configuration,
        builder.Environment);
    var developerAuthentication = authentication is null;
    var portia = builder.Services.AddCompliance(builder.Configuration, developerAuthentication,
        requireRealEmailDelivery: !builder.Environment.IsDevelopment());
    // AddPortia returns the same builder and lets this host contribute its generated JSON context.
    builder.Services.AddPortia()
        .AddHttp()
        .AddRequestHandler<ReviseApplicationMcpInputHandler>()
        .AddRequestHandler<PreviewApplicationChangeMcpInputHandler>()
        .AddMcpTool<DefineTeam>(tool => tool.Idempotent())
        .AddMcpTool<DeleteTeam>(tool => tool.Destructive())
        .AddMcpTool<GetTeam>(tool => tool.ReadOnly())
        .AddMcpTool<ListTeams>(tool => tool.ReadOnly())
        .AddMcpTool<AssignTeamMember>(tool => tool.Idempotent())
        .AddMcpTool<RemoveTeamMember>(tool => tool.Destructive())
        .AddMcpTool<SuspendMember>(tool => tool.Destructive())
        .AddMcpTool<DeprovisionMember>(tool => tool.Destructive())
        .AddMcpTool<ReinstateMember>(tool => tool.Idempotent())
        .AddMcpTool<GetTenantMember>(tool => tool.ReadOnly())
        .AddMcpTool<ListMemberResponsibilities>(tool => tool.ReadOnly())
        .AddMcpTool<ListTeamMembers>(tool => tool.ReadOnly())
        .AddMcpTool<DefineRole>(tool => tool.Idempotent())
        .AddMcpTool<DesignatePrimaryOrgAdmin>(tool => tool.Idempotent())
        .AddMcpTool<ReleasePrimaryOrgAdmin>(tool => tool.Idempotent())
        .AddMcpTool<GetPrimaryOrgAdmins>(tool => tool.ReadOnly())
        .AddMcpTool<RenameRole>(tool => tool.Idempotent())
        .AddMcpTool<DeleteRole>(tool => tool.Destructive())
        .AddMcpTool<GetRole>(tool => tool.ReadOnly())
        .AddMcpTool<ListRoles>(tool => tool.ReadOnly())
        .AddMcpTool<AssignRolePermission>(tool => tool.Idempotent())
        .AddMcpTool<RemoveRolePermission>(tool => tool.Destructive())
        .AddMcpTool<ListRolePermissions>(tool => tool.ReadOnly())
        .AddMcpTool<AssignTeamRole>(tool => tool.Idempotent())
        .AddMcpTool<RemoveTeamRole>(tool => tool.Destructive())
        .AddMcpTool<ListRoleTeams>(tool => tool.ReadOnly())
        .AddMcpTool<ListTeamRoles>(tool => tool.ReadOnly())
        .AddMcpTool<ListMyTenants>(tool => tool.ReadOnly())
        .AddMcpTool<RegisterTenant>(tool => tool.Idempotent())
        .AddMcpTool<SuspendTenant>(tool => tool.Destructive())
        .AddMcpTool<ReactivateTenant>(tool => tool.Idempotent())
        .AddMcpTool<OffboardTenant>(tool => tool.Destructive())
        .AddMcpTool<GrantPlatformOperator>()
        .AddMcpTool<RevokePlatformOperator>(tool => tool.Destructive())
        .AddMcpTool<DesignatePlatformPrimary>(tool => tool.Idempotent())
        .AddMcpTool<ReleasePlatformPrimary>(tool => tool.Idempotent())
        .AddMcpTool<ListPlatformOperators>(tool => tool.ReadOnly())
        .AddMcpTool<GetPlatformOperatorState>(tool => tool.ReadOnly())
        .AddMcpTool<ReviseMethodologyTemplateDraftV3>(tool => tool.Idempotent())
        .AddMcpTool<DiscardMethodologyTemplateDraft>(tool => tool.Destructive())
        .AddMcpTool<StartMethodologyTemplateDraft>(tool => tool.Idempotent())
        .AddMcpTool<GetMethodologyTemplateLine>(tool => tool.ReadOnly())
        .AddMcpTool<GetMethodologyTemplateVersionV2>(tool => tool.ReadOnly())
        .AddMcpTool<ListMethodologyTemplatesV2>(tool => tool.ReadOnly())
        .AddMcpTool<GetWorkDigestDispatchStatus>(tool => tool.ReadOnly())
        .AddMcpTool<InviteTenantMember>(tool => tool.Idempotent())
        .AddMcpTool<InviteOrganizationMember>(tool => tool.Idempotent())
        .AddMcpTool<InviteGuestConsultant>(tool => tool.Idempotent())
        .AddMcpTool<ListTenantInvitations>(tool => tool.ReadOnly())
        .AddMcpTool<GetMemberAccess>(tool => tool.ReadOnly())
        .AddMcpTool<GetTenant>(tool => tool.ReadOnly())
        .AddMcpTool<GetTenantProvisioningStatus>(tool => tool.ReadOnly())
        .AddMcpTool<ListTenants>(tool => tool.ReadOnly())
        .AddMcpTool<ListTenantMembers>(tool => tool.ReadOnly())
        .AddMcpTool<ChangeTenantSlug>()
        .AddMcpTool<ResolveMyTenantSlug>(tool => tool.ReadOnly())
        .AddMcpTool<CreateProgram>()
        .AddMcpTool<ListCriteriaCatalogEditions>(tool => tool.ReadOnly())
        .AddMcpTool<GetCriteriaCatalogEdition>(tool => tool.ReadOnly())
        .AddMcpTool<ListCriteriaCatalogEntries>(tool => tool.ReadOnly())
        .AddMcpTool<GetCriteriaCatalogEntry>(tool => tool.ReadOnly())
        .AddMcpTool<ExportCriteriaCatalogEntries>(tool => tool.ReadOnly())
        .AddMcpTool<ListRuntimeCriteriaEditions>(tool => tool.ReadOnly())
        .AddMcpTool<GetRuntimeCriteriaEdition>(tool => tool.ReadOnly())
        .AddMcpTool<ListRuntimeCriteriaEntries>(tool => tool.ReadOnly())
        .AddMcpTool<GetRuntimeCriteriaEntry>(tool => tool.ReadOnly())
        .AddMcpTool<ListRuntimeCriteriaDisclosures>(tool => tool.ReadOnly())
        .AddMcpTool<RegisterFirmStaff>(tool => tool.Idempotent())
        .AddMcpTool<SetFirmStaffStatus>(tool => tool.Idempotent())
        .AddMcpTool<GetFirmStaffDirectory>(tool => tool.ReadOnly())
        .AddMcpTool<ListAssignableFirmStaff>(tool => tool.ReadOnly())
        .AddMcpTool<GetServiceEngagementAcceptance>(tool => tool.ReadOnly())
        .AddMcpTool<GetServiceEngagementAcceptanceHistory>(tool => tool.ReadOnly())
        .AddMcpTool<GetEngagementManagementAcknowledgements>(tool => tool.ReadOnly())
        .AddMcpTool<CreateServiceEngagement>(tool => tool.Idempotent())
        .AddMcpTool<AmendServiceEngagement>(tool => tool.Idempotent())
        .AddMcpTool<ProposeServiceEngagementStaff>(tool => tool.Idempotent())
        .AddMcpTool<WithdrawServiceEngagementStaffProposal>(tool => tool.Idempotent())
        .AddMcpTool<RevokeServiceEngagementActualStaff>(tool => tool.Idempotent())
        .AddMcpTool<CloseServiceEngagement>(tool => tool.Idempotent())
        .AddMcpTool<GetServiceEngagement>(tool => tool.ReadOnly())
        .AddMcpTool<ListServiceEngagements>(tool => tool.ReadOnly())
        .AddMcpTool<GetServiceEngagementHistory>(tool => tool.ReadOnly())
        .AddMcpTool<GetExaminationEngagement>(tool => tool.ReadOnly())
        .AddMcpTool<GetTypeIBaseline>(tool => tool.ReadOnly())
        .AddMcpTool<PreviewTypeIBaseline>(tool => tool.ReadOnly())
        .AddMcpTool<CreateExaminationEngagement>(tool => tool.Idempotent())
        .AddMcpTool<ReviseExaminationEngagement>(tool => tool.Idempotent())
        .AddMcpTool<FreezeTypeIBaseline>(tool => tool.Idempotent())
        .AddMcpTool<AmendTypeIBaseline>(tool => tool.Idempotent())
        .AddMcpTool<GetClientIndependenceRules>(tool => tool.ReadOnly())
        .AddMcpTool<GetIndependenceRuleVersions>(tool => tool.ReadOnly())
        .AddMcpTool<ReviseIndependenceRules>(tool => tool.Idempotent())
        .AddMcpTool<RecordNonattestService>(tool => tool.Idempotent())
        .AddMcpTool<EvaluateClientIndependence>(tool => tool.Idempotent())
        .AddMcpTool<GetClientIndependenceHistory>(tool => tool.ReadOnly())
        .AddMcpTool<SetCriteriaTextOverlay>(tool => tool.Idempotent())
        .AddMcpTool<SelectProgramCriteriaEdition>(tool => tool.Idempotent())
        .AddMcpTool<CreateControlDraft>()
        .AddMcpTool<ReviseControlDraft>(tool => tool.Idempotent())
        .AddMcpTool<DiscardControlDraft>(tool => tool.Destructive())
        .AddMcpTool<GetControlDraft>(tool => tool.ReadOnly())
        .AddMcpTool<ListControlDrafts>(tool => tool.ReadOnly())
        .AddMcpTool<ListControlDraftRevisions>(tool => tool.ReadOnly())
        .AddMcpTool<GetControlDraftRevision>(tool => tool.ReadOnly())
        .AddMcpTool<GetControlVersion>(tool => tool.ReadOnly())
        .AddMcpTool<GetCurrentControlVersion>(tool => tool.ReadOnly())
        .AddMcpTool<GetEffectiveControlVersion>(tool => tool.ReadOnly())
        .AddMcpTool<ListControlVersions>(tool => tool.ReadOnly())
        .AddMcpTool<GetControlDecision>(tool => tool.ReadOnly())
        .AddMcpTool<ListControlDecisions>(tool => tool.ReadOnly())
        .AddMcpTool<GetControlCriterionMapping>(tool => tool.ReadOnly())
        .AddMcpTool<ListControlCriterionMappings>(tool => tool.ReadOnly())
        .AddMcpTool<ListCriteriaCoverage>(tool => tool.ReadOnly())
        .AddMcpTool<ProposeControlSuccessor>()
        .AddMcpTool<ProposeControlRetirement>()
        .AddMcpTool<PreviewControlImpact>(tool => tool.ReadOnly())
        .AddMcpTool<GetCriterionApplicability>(tool => tool.ReadOnly())
        .AddMcpTool<ListCriterionApplicability>(tool => tool.ReadOnly())
        .AddMcpTool<CreateCommitmentDraft>(tool => tool.Idempotent())
        .AddMcpTool<ReviseCommitmentDraft>()
        .AddMcpTool<GetCommitmentDraft>(tool => tool.ReadOnly())
        .AddMcpTool<ListCommitmentDrafts>(tool => tool.ReadOnly())
        .AddMcpTool<ListCommitmentDraftRevisions>(tool => tool.ReadOnly())
        .AddMcpTool<GetCommitmentDraftRevision>(tool => tool.ReadOnly())
        .AddMcpTool<PreviewCommitmentImpact>(tool => tool.ReadOnly())
        .AddMcpTool<GetCommitmentVersion>(tool => tool.ReadOnly())
        .AddMcpTool<GetEffectiveCommitmentVersion>(tool => tool.ReadOnly())
        .AddMcpTool<ListCommitmentVersions>(tool => tool.ReadOnly())
        .AddMcpTool<ListCommitmentDecisions>(tool => tool.ReadOnly())
        .AddMcpTool<CreatePolicyDraft>()
        .AddMcpTool<RevisePolicyDraft>()
        .AddMcpTool<ProposePolicySuccessor>()
        .AddMcpTool<DiscardPolicyDraft>(tool => tool.Destructive())
        .AddMcpTool<ProposePolicyRetirement>()
        .AddMcpTool<GetPolicy>(tool => tool.ReadOnly())
        .AddMcpTool<ListPolicies>(tool => tool.ReadOnly())
        .AddMcpTool<GetPolicyVersion>(tool => tool.ReadOnly())
        .AddMcpTool<GetEffectivePolicyVersion>(tool => tool.ReadOnly())
        .AddMcpTool<ListPolicyVersions>(tool => tool.ReadOnly())
        .AddMcpTool<ListPolicyDecisions>(tool => tool.ReadOnly())
        .AddMcpTool<PreviewPolicyImpact>(tool => tool.ReadOnly())
        .AddMcpTool<DefineTrainingRequirement>()
        .AddMcpTool<ReviseTrainingRequirement>()
        .AddMcpTool<GetTrainingRequirement>(tool => tool.ReadOnly())
        .AddMcpTool<ListTrainingRequirements>(tool => tool.ReadOnly())
        .AddMcpTool<LaunchPolicyCampaign>()
        .AddMcpTool<LaunchTrainingCampaign>()
        .AddMcpTool<ReconcileCampaignAudience>()
        .AddMcpTool<RecordTrainingCompletion>()
        .AddMcpTool<CloseCampaign>()
        .AddMcpTool<GetCampaign>(tool => tool.ReadOnly())
        .AddMcpTool<ListCampaigns>(tool => tool.ReadOnly())
        .AddMcpTool<ListCampaignParticipants>(tool => tool.ReadOnly())
        .AddMcpTool<ListCampaignAmendments>(tool => tool.ReadOnly())
        .AddMcpTool<CreateRiskDraft>()
        .AddMcpTool<ReviseRiskDraft>(tool => tool.Idempotent())
        .AddMcpTool<GetRiskDraft>(tool => tool.ReadOnly())
        .AddMcpTool<ListRiskDrafts>(tool => tool.ReadOnly())
        .AddMcpTool<ListRiskDraftRevisions>(tool => tool.ReadOnly())
        .AddMcpTool<GetRiskDraftRevision>(tool => tool.ReadOnly())
        .AddMcpTool<GetRiskMethod>(tool => tool.ReadOnly())
        .AddMcpTool<GetRiskMethodVersion>(tool => tool.ReadOnly())
        .AddMcpTool<GetRiskEvaluation>(tool => tool.ReadOnly())
        .AddMcpTool<ListRiskEvaluationHistory>(tool => tool.ReadOnly())
        .AddMcpTool<GetRiskGovernance>(tool => tool.ReadOnly())
        .AddMcpTool<ReviseProgram>(tool => tool.Idempotent())
        .AddMcpTool<GetProgram>(tool => tool.ReadOnly())
        .AddMcpTool<ListPrograms>(tool => tool.ReadOnly())
        .AddMcpTool<ListProgramRevisions>(tool => tool.ReadOnly())
        .AddMcpTool<GetProgramRevision>(tool => tool.ReadOnly())
        .AddMcpTool<GetProgramSetupWork>(tool => tool.ReadOnly())
        .AddMcpTool<DeclareApplication>()
        .AddMcpTool<ReviseApplicationMcpInput>(tool => tool.Named("bdgrz.application.revise")
            .DescribedAs("Invokes the ReviseApplication request.").Idempotent())
        .AddMcpTool<DeclareSystemInstance>()
        .AddMcpTool<GetApplication>(tool => tool.ReadOnly())
        .AddMcpTool<ListApplications>(tool => tool.ReadOnly())
        .AddMcpTool<GetApplicationRevision>(tool => tool.ReadOnly())
        .AddMcpTool<ListApplicationRevisions>(tool => tool.ReadOnly())
        .AddMcpTool<GetSystemInstance>(tool => tool.ReadOnly())
        .AddMcpTool<ListSystemInstances>(tool => tool.ReadOnly())
        .AddMcpTool<RecordApplicationRelationship>(tool => tool.Idempotent())
        .AddMcpTool<RemoveApplicationRelationship>(tool => tool.Idempotent())
        .AddMcpTool<RemoveGovernedApplicationRelationship>(tool => tool.Idempotent())
        .AddMcpTool<ListApplicationRelationships>(tool => tool.ReadOnly())
        .AddMcpTool<ListDirectedApplicationRelationships>(tool => tool.ReadOnly())
        .AddMcpTool<ListApplicationBoundaryReferences>(tool => tool.ReadOnly())
        .AddMcpTool<ListSystemInstanceBoundaryReferences>(tool => tool.ReadOnly())
        .AddMcpTool<PreviewApplicationChangeMcpInput>(tool => tool.Named("bdgrz.application.change.preview")
            .DescribedAs("Invokes the PreviewApplicationChange request.").ReadOnly())
        .AddMcpTool<RetireApplication>(tool => tool.Idempotent())
        .AddMcpTool<RetireSystemInstance>(tool => tool.Idempotent())
        .AddMcpTool<GetAccessReviewScope>(tool => tool.ReadOnly())
        .AddMcpTool<ListAccessReviewScopes>(tool => tool.ReadOnly())
        .AddMcpTool<StageApplicationImport>(tool => tool.Idempotent())
        .AddMcpTool<GetApplicationImport>(tool => tool.ReadOnly())
        .AddMcpTool<ListApplicationImportRows>(tool => tool.ReadOnly())
        .AddMcpTool<PreviewApplicationImport>(tool => tool.ReadOnly())
        .AddMcpTool<PreviewMissingApplicationImportRows>(tool => tool.ReadOnly())
        .AddMcpTool<PreviewApplicationImportOmissionProposal>(tool => tool.ReadOnly())
        .AddMcpTool<GetApplicationImportOmissionProposal>(tool => tool.ReadOnly())
        .AddMcpTool<GetArtifactRetention>(tool => tool.ReadOnly())
        .AddMcpTool<ListArtifactLegalHolds>(tool => tool.ReadOnly())
        .AddMcpTool<GetApplicationImportRetention>(tool => tool.ReadOnly())
        .AddMcpTool<GetApplicationImportProgress>(tool => tool.ReadOnly())
        .AddMcpTool<GetApplicationImportRejectedReport>(tool => tool.ReadOnly())
        .AddMcpTool<RecordPerson>(tool => tool.Idempotent())
        .AddMcpTool<ChangePersonDetails>(tool => tool.Idempotent())
        .AddMcpTool<CorrelatePersonMembership>(tool => tool.Idempotent())
        .AddMcpTool<GetPerson>(tool => tool.ReadOnly())
        .AddMcpTool<ListPeople>(tool => tool.ReadOnly())
        .AddMcpTool<RecordWorkRelationship>()
        .AddMcpTool<StartWorkRelationship>(tool => tool.Idempotent())
        .AddMcpTool<StartLeave>(tool => tool.Idempotent())
        .AddMcpTool<EndLeave>(tool => tool.Idempotent())
        .AddMcpTool<EndWorkRelationship>(tool => tool.Idempotent())
        .AddMcpTool<ChangeManager>(tool => tool.Idempotent())
        .AddMcpTool<ChangeDepartment>(tool => tool.Idempotent())
        .AddMcpTool<ChangeSponsor>(tool => tool.Idempotent())
        .AddMcpTool<CorrectWorkRelationshipDates>(tool => tool.Idempotent())
        .AddMcpTool<GetWorkRelationship>(tool => tool.ReadOnly())
        .AddMcpTool<ListWorkRelationships>(tool => tool.ReadOnly())
        .AddMcpTool<ListWorkforceObservations>(tool => tool.ReadOnly())
        .AddMcpTool<ListWorkforceReconciliationObservations>(tool => tool.ReadOnly())
        .AddMcpTool<RecordWorkforceSourceObservation>(tool => tool.Idempotent())
        .AddMcpTool<GetWorkforceSourceObservation>(tool => tool.ReadOnly())
        .AddMcpTool<ListWorkforceSourceObservations>(tool => tool.ReadOnly())
        .AddMcpTool<PreviewWorkforceSourceObservation>(tool => tool.ReadOnly())
        .AddMcpTool<RecordServiceIdentity>()
        .AddMcpTool<ChangeServiceIdentityOwner>(tool => tool.Idempotent())
        .AddMcpTool<ChangeServiceIdentityPurpose>(tool => tool.Idempotent())
        .AddMcpTool<DisableServiceIdentity>(tool => tool.Idempotent())
        .AddMcpTool<EnableServiceIdentity>(tool => tool.Idempotent())
        .AddMcpTool<RetireServiceIdentity>(tool => tool.Idempotent())
        .AddMcpTool<GetServiceIdentity>(tool => tool.ReadOnly())
        .AddMcpTool<ListServiceIdentities>(tool => tool.ReadOnly())
        .AddMcpTool<RecordProvider>()
        .AddMcpTool<ReviseProvider>(tool => tool.Idempotent())
        .AddMcpTool<GetProvider>(tool => tool.ReadOnly().Idempotent())
        .AddMcpTool<ListProviders>(tool => tool.ReadOnly().Idempotent())
        .AddMcpTool<GetProviderRevision>(tool => tool.ReadOnly().Idempotent())
        .AddMcpTool<ListProviderRevisions>(tool => tool.ReadOnly().Idempotent())
        .AddMcpTool<RecordAssuranceReport>()
        .AddMcpTool<ReviseAssuranceReport>(tool => tool.Idempotent())
        .AddMcpTool<ListProviderAssuranceReports>(tool => tool.ReadOnly().Idempotent())
        .AddMcpTool<ListProviderReviews>(tool => tool.ReadOnly().Idempotent())
        .AddMcpTool<RecordProviderCoverageGap>()
        .AddMcpTool<GetProviderCoverageGap>(tool => tool.ReadOnly().Idempotent())
        .AddMcpTool<ListProviderCoverageGaps>(tool => tool.ReadOnly().Idempotent())
        .AddMcpTool<GetProviderAssuranceCoverage>(tool => tool.ReadOnly().Idempotent())
        .AddMcpTool<PreviewProviderChange>(tool => tool.ReadOnly().Idempotent())
        .AddMcpTool<RecordTechnologyComponent>()
        .AddMcpTool<ReviseTechnologyComponent>(tool => tool.Idempotent())
        .AddMcpTool<GetTechnologyComponent>(tool => tool.ReadOnly())
        .AddMcpTool<ListTechnologyComponentBoundaryReferences>(tool => tool.ReadOnly())
        .AddMcpTool<ListInformationAssetBoundaryReferences>(tool => tool.ReadOnly())
        .AddMcpTool<ListTechnologyComponents>(tool => tool.ReadOnly())
        .AddMcpTool<ListTechnologyComponentRevisions>(tool => tool.ReadOnly())
        .AddMcpTool<RecordInformationAsset>()
        .AddMcpTool<ReviseInformationAsset>(tool => tool.Idempotent())
        .AddMcpTool<GetInformationAsset>(tool => tool.ReadOnly())
        .AddMcpTool<ListInformationAssets>(tool => tool.ReadOnly())
        .AddMcpTool<ListInformationAssetRevisions>(tool => tool.ReadOnly())
        .AddMcpTool<PreviewInformationAssetChange>(tool => tool.ReadOnly())
        .AddMcpTool<RecordLocation>()
        .AddMcpTool<ReviseLocation>(tool => tool.Idempotent())
        .AddMcpTool<GetLocation>(tool => tool.ReadOnly())
        .AddMcpTool<ListLocations>(tool => tool.ReadOnly())
        .AddMcpTool<ListLocationRevisions>(tool => tool.ReadOnly())
        .AddMcpTool<RecordOperationalProcess>()
        .AddMcpTool<ReviseOperationalProcess>(tool => tool.Idempotent())
        .AddMcpTool<GetOperationalProcess>(tool => tool.ReadOnly())
        .AddMcpTool<ListOperationalProcesses>(tool => tool.ReadOnly())
        .AddMcpTool<ListOperationalProcessRevisions>(tool => tool.ReadOnly())
        .AddMcpTool<RecordDataFlow>()
        .AddMcpTool<ReviseDataFlow>(tool => tool.Idempotent())
        .AddMcpTool<GetDataFlow>(tool => tool.ReadOnly())
        .AddMcpTool<ListDataFlows>(tool => tool.ReadOnly())
        .AddMcpTool<ListDataFlowRevisions>(tool => tool.ReadOnly())
        .AddMcpTool<CreateClientService>()
        .AddMcpTool<ReviseClientService>(tool => tool.Idempotent())
        .AddMcpTool<RetireClientService>(tool => tool.Destructive())
        .AddMcpTool<GetClientService>(tool => tool.ReadOnly())
        .AddMcpTool<ListClientServices>(tool => tool.ReadOnly())
        .AddMcpTool<ListProgramClientServices>(tool => tool.ReadOnly())
        .AddMcpTool<ListClientServiceRevisions>(tool => tool.ReadOnly())
        .AddMcpTool<GetClientServiceRevision>(tool => tool.ReadOnly())
        .AddMcpTool<CreateBoundary>()
        .AddMcpTool<ReviseBoundaryDraft>(tool => tool.Idempotent())
        .AddMcpTool<DiscardBoundaryDraft>(tool => tool.Destructive())
        .AddMcpTool<GetBoundary>(tool => tool.ReadOnly())
        .AddMcpTool<ListResponsibilities>(tool => tool.ReadOnly())
        .AddMcpTool<PreviewResponsibilityConflicts>(tool => tool.ReadOnly())
        .AddMcpTool<ListProgramBoundaries>(tool => tool.ReadOnly())
        .AddMcpTool<GetBoundaryVersion>(tool => tool.ReadOnly())
        .AddMcpTool<ListBoundaryVersions>(tool => tool.ReadOnly())
        .AddMcpTool<GetEffectiveBoundaryVersion>(tool => tool.ReadOnly())
        .AddMcpTool<GetBoundaryDecision>(tool => tool.ReadOnly())
        .AddMcpTool<ListBoundaryDecisions>(tool => tool.ReadOnly())
        .AddMcpTool<PreviewBoundaryImpact>(tool => tool.ReadOnly())
        .AddMcpTool<ProposeBoundarySuccessor>()
        .AddMcpTool<FreezeProgramScopeSnapshot>()
        .AddMcpTool<AmendProgramScopeSnapshot>()
        .AddMcpTool<GetSnapshot>(tool => tool.ReadOnly())
        .AddMcpTool<VerifyProgramScopeSnapshot>(tool => tool.ReadOnly())
        .AddMcpTool<RegenerateProgramScopeSnapshotManifest>(tool => tool.ReadOnly())
        .AddMcpTool<ListProgramSnapshots>(tool => tool.ReadOnly())
        .AddMcpTool<FreezeWorkforceRosterSnapshot>()
        .AddMcpTool<AmendWorkforceRosterSnapshot>()
        .AddMcpTool<GetWorkforceRosterSnapshot>(tool => tool.ReadOnly())
        .AddMcpTool<GetWorkforceRosterSnapshotAsOf>(tool => tool.ReadOnly())
        .AddMcpTool<ListWorkforceRosterSnapshots>(tool => tool.ReadOnly())
        .AddMcpTool<RegenerateWorkforceRosterSnapshotManifest>(tool => tool.ReadOnly())
        .AddMcpTool<GetReadinessAssessment>(tool => tool.ReadOnly())
        .AddMcpTool<GetAdvisorRequest>(tool => tool.ReadOnly())
        .AddMcpTool<PreviewAdvisorHandoff>(tool => tool.ReadOnly())
        .AddMcpTool<GetAdvisorSharedSource>(tool => tool.ReadOnly())
        .AddMcpTool<ListAdvisorRequests>(tool => tool.ReadOnly())
        .AddMcpTool<ListAdvisorFeedback>(tool => tool.ReadOnly())
        .AddMcpTool<ListReadinessAssessments>(tool => tool.ReadOnly())
        .AddMcpTool<ListReadinessGaps>(tool => tool.ReadOnly())
        .AddMcpTool<ListTypeIEntryDecisions>(tool => tool.ReadOnly())
        .AddMcpTool<ListReadinessAnnotations>(tool => tool.ReadOnly())
        .AddMcpTool<ProposeControlOperatingPlan>(tool => tool.Idempotent())
        .AddMcpTool<PreviewControlOperatingPlan>(tool => tool.ReadOnly())
        .AddMcpTool<GetControlOperatingPlan>(tool => tool.ReadOnly())
        .AddMcpTool<ListControlOperatingBlockers>(tool => tool.ReadOnly())
        .AddMcpTool<ListMyControlWork>(tool => tool.ReadOnly())
        .AddMcpTool<GetControlOccurrence>(tool => tool.ReadOnly())
        .AddMcpTool<ListControlOccurrences>(tool => tool.ReadOnly())
        .AddMcpTool<DefineControlEvaluationPlan>()
        .AddMcpTool<GetControlEvaluationPlanVersion>(tool => tool.ReadOnly())
        .AddMcpTool<ListControlEvaluationPlanVersions>(tool => tool.ReadOnly())
        .AddMcpTool<StartControlEvaluation>(tool => tool.Idempotent())
        .AddMcpTool<RecordControlEvaluationStep>(tool => tool.Idempotent())
        .AddMcpTool<DisposeControlEvaluationDeviation>(tool => tool.Idempotent())
        .AddMcpTool<GetControlEvaluation>(tool => tool.ReadOnly())
        .AddMcpTool<ListControlEvaluations>(tool => tool.ReadOnly())
        .AddMcpTool<RaiseFinding>()
        .AddMcpTool<CancelCorrectiveAction>(tool => tool.Idempotent())
        .AddMcpTool<ChangeCorrectiveAction>(tool => tool.Idempotent())
        .AddMcpTool<RequestWaiver>(tool => tool.Idempotent())
        .AddMcpTool<Bdgrz.Compliance.Features.Remediation.Queries.GetWaiver>(tool => tool.ReadOnly())
        .AddMcpTool<GetFinding>(tool => tool.ReadOnly())
        .AddMcpTool<ListFindings>(tool => tool.ReadOnly())
        .AddMcpTool<OpenEvidenceRequest>()
        .AddMcpTool<CancelEvidenceRequest>()
        .AddMcpTool<PrepareEvidenceRedaction>()
        .AddMcpTool<GetEvidenceRedaction>(tool => tool.ReadOnly())
        .AddMcpTool<GetEvidenceArtifactMetadata>(tool => tool.ReadOnly())
        .AddMcpTool<WithdrawEvidenceSupport>(tool => tool.Idempotent())
        .AddMcpTool<GetEvidenceSupport>(tool => tool.ReadOnly())
        .AddMcpTool<ListEvidenceSupports>(tool => tool.ReadOnly())
        .AddMcpTool<GetEvidenceRequest>(tool => tool.ReadOnly())
        .AddMcpTool<ListEvidenceRequests>(tool => tool.ReadOnly())
        .AddMcpTool<OpenAccessPopulation>(tool => tool.Idempotent())
        .AddMcpTool<RecordAccessPopulationFacts>(tool => tool.Idempotent())
        .AddMcpTool<PreviewAccessPopulation>(tool => tool.ReadOnly())
        .AddMcpTool<GetAccessPopulation>(tool => tool.ReadOnly())
        .AddMcpTool<ListAccessPopulations>(tool => tool.ReadOnly())
        .AddMcpTool<GetAccessReviewCoverage>(tool => tool.ReadOnly())
        .AddMcpTool<ClassifyAccessPrincipal>(tool => tool.Idempotent())
        .AddMcpTool<ListAccessPrincipals>(tool => tool.ReadOnly())
        .AddMcpTool<GetAccessVariance>(tool => tool.ReadOnly())
        .AddMcpTool<ProposeAccessExpectation>(tool => tool.Idempotent())
        .AddMcpTool<ListAccessExpectations>(tool => tool.ReadOnly())
        .AddMcpTool<LaunchAccessReviewCampaign>(tool => tool.Idempotent())
        .AddMcpTool<GetAccessReviewCampaign>(tool => tool.ReadOnly())
        .AddMcpTool<ListAccessReviewCampaigns>(tool => tool.ReadOnly())
        .AddMcpTool<PreviewBulkAccessDecision>(tool => tool.ReadOnly())
        .AddMcpTool<RecordAccessRemediationChange>(tool => tool.Idempotent())
        .AddMcpTool<VerifyAccessRemediation>(tool => tool.Idempotent())
        .AddMcpTool<ReassignAccessReviewResponsibility>(tool => tool.Idempotent())
        .AddMcpTool<ListWork>(tool => tool.ReadOnly())
        .AddMcpTool<GetWorkItem>(tool => tool.ReadOnly())
        .AddMcpTool<ClaimWorkItem>(tool => tool.Idempotent())
        .AddMcpTool<AssignWorkItem>(tool => tool.Idempotent())
        .AddMcpTool<DelegateWorkItem>(tool => tool.Idempotent())
        .AddMcpTool<EscalateWorkItem>(tool => tool.Idempotent())
        .AddMcpTool<ListWorkReminders>(tool => tool.ReadOnly())
        .AddMcpTool<GetWorkDigest>(tool => tool.ReadOnly())
        .AddMcpTool<GetWorkDigestPreference>(tool => tool.ReadOnly())
        .AddMcpTool<SetWorkDigestPreference>()
        .AddMcpHttp();
    portia.AddCriteriaAuthoringMcpTools();
    builder.Services.AddScoped<HttpAdmissionCredentialResolver>();
    builder.Services.AddScoped<McpHttpAdmissionCredentialResolver>();
    builder.Services.AddScoped<ITrustedAdmissionCredentialResolver, TransportAdmissionCredentialResolver>();
    builder.Services.AddScoped<IAccessAdmissionAuthority, CodePolicyAdmissionAuthority>();
    builder.Services.AddScoped<CaptureEvidenceArtifactHandler>();
    builder.Services.ConfigureHttpJsonOptions(options =>
    {
        options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
        options.SerializerOptions.TypeInfoResolverChain.Insert(0, ComplianceJsonContext.Default);
    });

    if (hostMode.RunsWorkers())
    {
        portia.AddWorkers();
        builder.Services.AddHostedService<PlatformCriteriaSeedWorker>();
        builder.Services.AddHostedService<WorkDigestDispatchBackgroundService>();
        builder.Services.AddHostedService<ProcessSweep>();
    }

    builder.Services.AddHostedService<ReservedTenantRouteCollisionCheck>();
    builder.Services.Configure<OpenApiOptions>("v1", options =>
    {
        options.AddDocumentTransformer<ComplianceOpenApiSecurityTransformer>();
        options.AddOperationTransformer<ComplianceOpenApiSecurityTransformer>();
    });
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddSingleton<INativeOperationTransportObserver, NativeOperationHttpObserver>();
    builder.Services.AddHostedService<NativeFirstResponseEndpointValidation>();
    builder.Services.AddSingleton<AuthorizationDenialLog>();
    builder.Services.AddHostedService(services => services.GetRequiredService<AuthorizationDenialLog>());
    builder.Services.AddComplianceHealthChecks();
    builder.Services.AddTenantPathLogRedaction();

    var app = builder.Build();

    app.Use((context, next) =>
    {
        // Organization slugs may identify a client and must not be sent to
        // another origin through a browser's Referer header.
        context.Response.Headers["Referrer-Policy"] = "no-referrer";
        return next(context);
    });
    app.UseExceptionHandler();
    app.UseStatusCodePages();
    app.UseRouting();

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapPortiaOpenApi();
    app.MapPortiaMcp(AuthorizationDenialLog.McpPath).RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser);
    app.MapCriteriaAuthoringEndpoints();

    app.MapComplianceHealthChecks();
    app.MapGet(
            "/auth/config",
            () => authentication?.ToClientConfiguration() ??
                ComplianceAuthenticationClientConfiguration.Development)
        .AllowAnonymous()
        .ExcludeFromDescription();
    app.MapGet(
            "/auth/session",
            async (HttpContext context, IEmailAddressDirectoryReader directory, CancellationToken ct) =>
            {
                var id = context.User.FindFirst("sub")?.Value ?? string.Empty;
                var email = context.User.FindFirst("email")?.Value ?? string.Empty;
                var verified = false;
                if (email.Length > 0 && Uuid.TryParse(id, CultureInfo.InvariantCulture, out var userId))
                {
                    var address = await directory.GetAsync(email, ct).ConfigureAwait(false);
                    verified = address?.UserId == userId && address.Verified;
                }

                return Results.Ok(new BrowserSession(id, email, verified));
            })
        .RequireAuthorization(ComplianceAuthorizationPolicies.Session)
        .ExcludeFromDescription();
    app.MapPost(
            "/auth/logout",
            (HttpContext context) =>
            {
                UserSessionCookie.Clear(context);
                return Results.NoContent();
            })
        .RequireAuthorization(ComplianceAuthorizationPolicies.Session)
        .ExcludeFromDescription();

    var sessionTokens = app.Services.GetRequiredService<BdgrzSessionTokens>();
    if (developerAuthentication)
    {
        app.MapPortiaPost<ContinueWithDeveloperIdentity, AuthenticatedUserIdentity>(
                "/v1/developer-user-sessions",
                config => config.OnNativeResult((http, result) => UserSessionCookie.Issue(http, result, sessionTokens))).WithNativeFirstResponse<ContinueWithDeveloperIdentity>()
            .AllowAnonymous()
            .WithTags("Users");
    }
    else
    {
        app.MapPortiaPost<ContinueWithOidcProvider, AuthenticatedUserIdentity>(
                "/v1/oidc-user-sessions",
                config => config.OnNativeResult((http, result) => UserSessionCookie.Issue(http, result, sessionTokens))).WithNativeFirstResponse<ContinueWithOidcProvider>()
            .RequireAuthorization(ComplianceAuthorizationPolicies.OidcContinuation)
            .WithTags("Users");
        app.MapPortiaPost<LinkOidcProviderIdentity, AuthenticatedUserIdentity>(
                "/v1/my/oidc-identity-links", config => config.OnNativeResult()).WithNativeFirstResponse<LinkOidcProviderIdentity>()
            .RequireAuthorization(ComplianceAuthorizationPolicies.IdentityLink)
            .WithTags("Users");
        app.MapPortiaPut<StartIdentityRecovery>("/v1/identity-recovery/challenges/{challenge_id}", config => config.OnNativeResult()).WithNativeFirstResponse<StartIdentityRecovery>()
            .AllowAnonymous()
            .WithTags("Users");
        app.MapPortiaPost<GetIdentityRecoveryOptions, IdentityRecoveryOptions>(
                "/v1/identity-recovery/options", config => config.OnNativeResult()).WithNativeFirstResponse<GetIdentityRecoveryOptions>()
            .RequireAuthorization(ComplianceAuthorizationPolicies.OidcContinuation)
            .WithTags("Users");
        app.MapPortiaPut<CompleteIdentityRecovery, OperationAcceptedResponse>(
                "/v1/identity-recovery/challenges/{challenge_id}/completion",
                config => config.OnNativeResult((_, result) => IdentityRecoveryHttp.Result(result))).WithNativeFirstResponse<CompleteIdentityRecovery>()
            .RequireAuthorization(ComplianceAuthorizationPolicies.OidcContinuation)
            .WithTags("Users");
    }
    app.MapPortiaPost<RegisterTenant, PortiaCreateOutcome<TenantRegistration>>("/v1/tenants",
            config => config.Created("/v1/tenants/{TenantId}")).WithNativeFirstResponse<RegisterTenant>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Tenants");
    if (app.Environment.IsDevelopment())
    {
        // The registered content store is local; keep public uploads unavailable until production storage is qualified.
        app.MapEvidenceArtifactUpload();
    }
    app.MapPortiaPut<SuspendTenant, UnitResult>("/v1/suspensions/{command_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<SuspendTenant>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Tenants");
    app.MapPortiaDelete<ReactivateTenant, UnitResult>("/v1/suspensions", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<ReactivateTenant>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Tenants");
    app.MapPortiaPut<OffboardTenant, UnitResult>("/v1/offboarding/{command_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<OffboardTenant>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Tenants");
    app.MapPortiaPut<GrantPlatformOperator>("/v1/system/operator-grants/{command_id}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Platform operators");
    app.MapPortiaPut<RevokePlatformOperator>("/v1/system/operator-revocations/{command_id}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Platform operators");
    app.MapPortiaPut<DesignatePlatformPrimary>("/v1/system/operators/{user_id}/primary-designations/{command_id}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Platform operators");
    app.MapPortiaPut<ReleasePlatformPrimary>("/v1/system/operators/{user_id}/primary-releases/{command_id}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Platform operators");
    app.MapPortiaGet<ListPlatformOperators, Page<PlatformOperatorDirectoryEntry>>("/v1/system/operators")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Platform operators");
    app.MapPortiaGet<GetPlatformOperatorState, PlatformOperatorStateView>("/v1/system/operators/{user_id}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Platform operators");
    app.MapPortiaGet<GetMethodologyTemplateLine, MethodologyTemplateLineView>(
            "/v1/system/methodology-templates/{template_id}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Methodology templates");
    app.MapPortiaPut<ReviseMethodologyTemplateDraftV3>(
            "/v1/system/methodology-templates/{template_id}/drafts/{draft_id}/revisions/{command_id}", config => config.OnNativeResult()).WithNativeFirstResponse<ReviseMethodologyTemplateDraftV3>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Methodology templates");
    app.MapPortiaPut<StartMethodologyTemplateDraft>(
            "/v1/system/methodology-templates/{template_id}/drafts/{draft_id}/starts/{command_id}", config => config.OnNativeResult()).WithNativeFirstResponse<StartMethodologyTemplateDraft>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Methodology templates");
    app.MapPortiaPut<ReviewMethodologyTemplateDraft>(
            "/v1/system/methodology-templates/{template_id}/drafts/{draft_id}/reviews/{command_id}", config => config.OnNativeResult()).WithNativeFirstResponse<ReviewMethodologyTemplateDraft>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Methodology templates");
    app.MapPortiaPut<ApproveMethodologyTemplateDraft>(
            "/v1/system/methodology-templates/{template_id}/drafts/{draft_id}/approvals/{command_id}", config => config.OnNativeResult()).WithNativeFirstResponse<ApproveMethodologyTemplateDraft>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Methodology templates");
    app.MapPortiaPut<DiscardMethodologyTemplateDraft>(
            "/v1/system/methodology-templates/{template_id}/drafts/{draft_id}/discards/{command_id}", config => config.OnNativeResult()).WithNativeFirstResponse<DiscardMethodologyTemplateDraft>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Methodology templates");
    app.MapPortiaGet<GetWorkDigestDispatchStatus, WorkDigestDispatchStatusView>(
            "/v1/system/members/{member_id}/work-digest-dispatches", config => config
                .OnBind(WorkDigestHttp.BindStatus)
                .Parameter<Uuid>("tenant_id", PortiaHttpParameterLocation.Header, required: true)
                .Parameter<Uuid>("member_id", PortiaHttpParameterLocation.Route, required: true)
                .Parameter<DateOnly>("week_of", PortiaHttpParameterLocation.Query, required: true))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Platform operators");
    app.MapPortiaPut<AuthorizeWorkDigestUnknownRetry, WorkDigestDispatchStatusView>(
            "/v1/system/members/{member_id}/work-digest-dispatches/retry-authorizations/{command_id}", config => config
                .OnBind(WorkDigestHttp.BindRetryAsync)
                .Accepts<WorkDigestRetryHttpBody>("application/json", isOptional: false)
                .Parameter<Uuid>("tenant_id", PortiaHttpParameterLocation.Header, required: true)
                .Parameter<Uuid>("member_id", PortiaHttpParameterLocation.Route, required: true)
                .Parameter<DateOnly>("week_of", PortiaHttpParameterLocation.Query, required: true)
                .Parameter<Uuid>("command_id", PortiaHttpParameterLocation.Route, required: true)
                .OnNativeResult()).WithNativeFirstResponse<AuthorizeWorkDigestUnknownRetry>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Platform operators");
    app.MapPortiaPut<InviteTenantMember>("/v1/invitations/{invitation_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<InviteTenantMember>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Tenants");
    app.MapPortiaPut<InviteOrganizationMember>("/v1/member-invitations/{invitation_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<InviteOrganizationMember>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Tenants");
    app.MapPortiaPut<InviteGuestConsultant>("/v1/guest-invitations/{invitation_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<InviteGuestConsultant>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Tenants");
    app.MapPortiaGet<ListTenantInvitations, Page<TenantInvitationView>>(
            "/v1/member-invitations", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Tenants");
    app.MapPortiaGet<GetMemberAccess, MemberAccessView>(
            "/v1/members/{user_id}/access", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Access control");
    app.MapPortiaPut<SuspendMember, AdministratorTransitionResponse>(
            "/v1/members/{user_id}/suspensions/{command_id}",
            config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult((_, result) => AdministratorTransitionHttp.Result(result))).WithNativeFirstResponse<SuspendMember>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Access control");
    app.MapPortiaPut<ReinstateMember, AdministratorTransitionResponse>(
            "/v1/members/by-user/{user_id}/reinstatements/{command_id}",
            config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult((_, result) => AdministratorTransitionHttp.Result(result))).WithNativeFirstResponse<ReinstateMember>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Access control");
    app.MapPortiaPut<DeprovisionMember, AdministratorTransitionResponse>(
            "/v1/members/{user_id}/deprovisioning/{command_id}",
            config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult((_, result) => AdministratorTransitionHttp.Result(result))).WithNativeFirstResponse<DeprovisionMember>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Access control");
    app.MapPortiaGet<GetTenantMember, TenantMembershipView>(
            "/v1/members/{user_id}", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Access control");
    app.MapPortiaGet<ListMemberResponsibilities, IReadOnlyList<ResponsibilityAssignmentView>>(
            "/v1/members/{user_id}/responsibilities", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Responsibilities");
    app.MapPortiaPut<AcceptTenantInvitation>("/v1/invitations/{invitation_id}/acceptance", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<AcceptTenantInvitation>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Tenants");
    app.MapPortiaGet<GetTenant, TenantView>("/v1/tenants/{tenant_id}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Tenants");
    app.MapPortiaGet<GetTenantProvisioningStatus, TenantProvisioningStatusView>(
            "/v1/tenant/provisioning-status", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Tenants");
    app.MapPortiaGet<ListTenants, Page<TenantView>>("/v1/system/tenants")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Tenants");
    app.MapPortiaGet<ListTenantMembers, Page<TenantMembershipView>>("/v1/members", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Tenants");
    app.MapPortiaPut<ChangeTenantSlug>("/v1/slug-changes/{command_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<ChangeTenantSlug>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Tenants");
    app.MapPortiaGet<ResolveMyTenantSlug, TenantSlugResolution>("/v1/my/tenant")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Tenants");
    app.MapPortiaPut<CreateProgram, ProgramRegistration>("/v1/programs/{program_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<CreateProgram>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Programs");
    app.MapPortiaGet<ListCriteriaCatalogEditions, IReadOnlyList<CriteriaCatalogEdition>>(
            "/v1/criteria-catalog-editions", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Criteria");
    app.MapPortiaGet<GetCriteriaCatalogEdition, CriteriaCatalogEdition>(
            "/v1/criteria-catalog-editions/{edition_id}", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Criteria");
    app.MapPortiaGet<ListCriteriaCatalogEntries, Page<Criterion>>(
            "/v1/criteria-catalog-editions/{edition_id}/entries", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Criteria");
    app.MapPortiaGet<ExportCriteriaCatalogEntries, Page<Criterion>>(
            "/v1/criteria-catalog-editions/{edition_id}/entries/export", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Criteria");
    app.MapPortiaGet<GetCriteriaCatalogEntry, Criterion>(
            "/v1/criteria-catalog-entries", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Criteria");
    app.MapPortiaGet<ListRuntimeCriteriaEditions, RuntimeCriteriaEditionsPage>(
            "/v1/runtime-criteria-editions", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Criteria");
    app.MapPortiaGet<GetRuntimeCriteriaEdition, RuntimeCriteriaEdition>(
            "/v1/runtime-criteria-editions/{edition_id}", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Criteria");
    app.MapPortiaGet<ListRuntimeCriteriaEntries, RuntimeCriteriaPage>(
            "/v1/runtime-criteria-editions/{edition_id}/entries", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Criteria");
    app.MapPortiaGet<GetRuntimeCriteriaEntry, RuntimeCriteriaEntry>(
            "/v1/runtime-criteria-entries", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Criteria");
    app.MapPortiaGet<ListRuntimeCriteriaDisclosures, RuntimeCriteriaDisclosurePage>(
            "/v1/runtime-criteria-editions/{edition_id}/disclosures", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Criteria");
    app.MapPortiaPut<RegisterFirmStaff, OperationAcceptedResponse>(
            "/v1/system/firm-staff/{staff_member_id}/register-firm-staff",
            config => config
                .Produces<OperationAcceptedResponse>(StatusCodes.Status202Accepted, "application/json")
                .OnNativeResult((http, result) =>
                {
                    if (!result.IsSuccess)
                        return null;
                    http.Response.Headers.Location = result.Value.StatusUrl;
                    return Results.Json(result.Value, ComplianceCoreJsonContext.Default.OperationAcceptedResponse,
                        statusCode: StatusCodes.Status202Accepted);
                })).WithNativeFirstResponse<RegisterFirmStaff>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Service engagements");
    app.MapPortiaPut<SetFirmStaffStatus, FirmStaffMemberView>(
            "/v1/system/firm-staff/{staff_member_id}/set-firm-staff-status/{command_id}", config => config.OnNativeResult()).WithNativeFirstResponse<SetFirmStaffStatus>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Service engagements");
    app.MapPortiaPut<RecordFirmProfessionalDutyDesignation, OperationAcceptedResponse>(
            "/v1/system/firm-professional-duties/{designation_id}/assertions/{assertion_id}",
            config => config
                .Produces<OperationAcceptedResponse>(StatusCodes.Status202Accepted, "application/json")
                .OnNativeResult((http, result) =>
                {
                    if (!result.IsSuccess)
                        return null;
                    http.Response.Headers.Location = result.Value.StatusUrl;
                    return Results.Json(result.Value, ComplianceCoreJsonContext.Default.OperationAcceptedResponse,
                        statusCode: StatusCodes.Status202Accepted);
                })).WithNativeFirstResponse<RecordFirmProfessionalDutyDesignation>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Firm independence");
    app.MapPortiaPut<RevokeFirmProfessionalDutyDesignation, OperationAcceptedResponse>(
            "/v1/system/firm-professional-duties/{designation_id}/assertion-revocations/{assertion_id}",
            config => config
                .Produces<OperationAcceptedResponse>(StatusCodes.Status202Accepted, "application/json")
                .OnNativeResult((http, result) =>
                {
                    if (!result.IsSuccess)
                        return null;
                    http.Response.Headers.Location = result.Value.StatusUrl;
                    return Results.Json(result.Value, ComplianceCoreJsonContext.Default.OperationAcceptedResponse,
                        statusCode: StatusCodes.Status202Accepted);
                })).WithNativeFirstResponse<RevokeFirmProfessionalDutyDesignation>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Firm independence");
    app.MapPortiaGet<GetFirmProfessionalDutyCatalog, FirmProfessionalDutyCatalogView>(
            "/v1/system/firm-professional-duties")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Firm independence");
    app.MapPortiaGet<GetFirmStaffDirectory, FirmStaffDirectoryView>(
            "/v1/system/firm-staff")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Service engagements");
    app.MapPortiaGet<ListAssignableFirmStaff, FirmStaffDirectoryView>(
            "/v1/firm-staff", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Service engagements");
    app.MapPortiaPut<CreateServiceEngagement, ServiceEngagementView>(
            "/v1/service-engagements/{engagement_id}/create-service-engagement", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<CreateServiceEngagement>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Service engagements");
    app.MapPortiaPut<AmendServiceEngagement, ServiceEngagementView>(
            "/v1/service-engagements/{engagement_id}/amend-service-engagement/{command_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<AmendServiceEngagement>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Service engagements");
    app.MapPortiaPut<ProposeServiceEngagementStaff, ServiceEngagementView>(
            "/v1/service-engagements/{engagement_id}/propose-service-engagement-staff/{command_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<ProposeServiceEngagementStaff>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Service engagements");
    app.MapPortiaPut<WithdrawServiceEngagementStaffProposal, ServiceEngagementView>(
            "/v1/service-engagements/{engagement_id}/staff-proposals/{staff_member_id}/withdrawals/{command_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<WithdrawServiceEngagementStaffProposal>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Service engagements");
    app.MapPortiaPut<RevokeServiceEngagementActualStaff, ServiceEngagementAcceptanceView>(
            "/v1/service-engagements/{engagement_id}/actual-assignments/{staff_member_id}/revocations/{command_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<RevokeServiceEngagementActualStaff>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Service engagements");
    app.MapPortiaGet<GetServiceEngagementAcceptance, ServiceEngagementAcceptanceView>(
            "/v1/service-engagements/{engagement_id}/acceptance", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Service engagements");
    app.MapPortiaGet<GetServiceEngagementAcceptanceHistory, IReadOnlyList<ServiceEngagementAcceptanceView>>(
            "/v1/service-engagements/{engagement_id}/acceptance-history", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Service engagements");
    app.MapPortiaGet<GetEngagementManagementAcknowledgements, IReadOnlyList<EngagementManagementAcknowledgementView>>(
            "/v1/service-engagements/{engagement_id}/management-acknowledgements", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Service engagements");
    app.MapPortiaPut<AcknowledgeEngagementManagement, OperationAcceptedResponse>(
            "/v1/service-engagements/{engagement_id}/management-acknowledgements/{acknowledgement_id}",
            config => config.FromHeader(x => x.TenantId, "Tenant-Id")
                .Produces<OperationAcceptedResponse>(StatusCodes.Status202Accepted, "application/json")
                .OnNativeResult((http, result) =>
                {
                    if (!result.IsSuccess)
                        return null;
                    http.Response.Headers.Location = result.Value.StatusUrl;
                    return Results.Json(result.Value, ComplianceCoreJsonContext.Default.OperationAcceptedResponse,
                        statusCode: StatusCodes.Status202Accepted);
                })).WithNativeFirstResponse<AcknowledgeEngagementManagement>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Service engagements");
    app.MapPortiaPut<RecordPartnerIndependenceEvaluation, OperationAcceptedResponse>(
            "/v1/service-engagements/{engagement_id}/partner-evaluations/{evaluation_id}",
            config => config.FromHeader(x => x.TenantId, "Tenant-Id")
                .Produces<OperationAcceptedResponse>(StatusCodes.Status202Accepted, "application/json")
                .OnNativeResult((http, result) =>
                {
                    if (!result.IsSuccess)
                        return null;
                    http.Response.Headers.Location = result.Value.StatusUrl;
                    return Results.Json(result.Value, ComplianceCoreJsonContext.Default.OperationAcceptedResponse,
                        statusCode: StatusCodes.Status202Accepted);
                })).WithNativeFirstResponse<RecordPartnerIndependenceEvaluation>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Service engagements");
    app.MapPortiaPut<AcceptServiceEngagement, OperationAcceptedResponse>(
            "/v1/service-engagements/{engagement_id}/acceptance/{command_id}",
            config => config.FromHeader(x => x.TenantId, "Tenant-Id")
                .Produces<OperationAcceptedResponse>(StatusCodes.Status202Accepted, "application/json")
                .OnNativeResult((http, result) =>
                {
                    if (!result.IsSuccess)
                        return null;
                    http.Response.Headers.Location = result.Value.StatusUrl;
                    return Results.Json(result.Value, ComplianceCoreJsonContext.Default.OperationAcceptedResponse,
                        statusCode: StatusCodes.Status202Accepted);
                })).WithNativeFirstResponse<AcceptServiceEngagement>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Service engagements");
    app.MapPortiaPut<AssignServiceEngagementStaff, OperationAcceptedResponse>(
            "/v1/service-engagements/{engagement_id}/actual-assignments/{staff_member_id}/{command_id}",
            config => config.FromHeader(x => x.TenantId, "Tenant-Id")
                .Produces<OperationAcceptedResponse>(StatusCodes.Status202Accepted, "application/json")
                .OnNativeResult((http, result) =>
                {
                    if (!result.IsSuccess)
                        return null;
                    http.Response.Headers.Location = result.Value.StatusUrl;
                    return Results.Json(result.Value, ComplianceCoreJsonContext.Default.OperationAcceptedResponse,
                        statusCode: StatusCodes.Status202Accepted);
                })).WithNativeFirstResponse<AssignServiceEngagementStaff>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Service engagements");
    app.MapPortiaPut<CloseServiceEngagement, ServiceEngagementView>(
            "/v1/service-engagements/{engagement_id}/closure/{command_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<CloseServiceEngagement>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Service engagements");
    app.MapPortiaGet<GetServiceEngagement, ServiceEngagementView>(
            "/v1/service-engagements/{engagement_id}", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Service engagements");
    app.MapPortiaGet<ListServiceEngagements, IReadOnlyList<ServiceEngagementView>>(
            "/v1/service-engagements", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Service engagements");
    app.MapPortiaGet<GetServiceEngagementHistory, IReadOnlyList<ServiceEngagementView>>(
            "/v1/service-engagements/{engagement_id}/history", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Service engagements");
    app.MapPortiaGet<GetClientIndependenceRules, IReadOnlyList<IndependenceRuleVersionView>>(
            "/v1/independence/rules", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Firm independence");
    app.MapPortiaGet<GetIndependenceRuleVersions, IReadOnlyList<IndependenceRuleVersionView>>(
            "/v1/system/independence/rules")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Firm independence");
    app.MapPortiaPut<ReviseIndependenceRules, IndependenceRuleVersionView>(
            "/v1/system/independence/rules/{command_id}", config => config.OnNativeResult()).WithNativeFirstResponse<ReviseIndependenceRules>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Firm independence");
    app.MapPortiaPut<RatifyIndependenceRuleVersion, IndependenceRuleRatificationView>(
            "/v1/system/independence/rules/{rule_version}/ratification/{command_id}", config => config.OnNativeResult()).WithNativeFirstResponse<RatifyIndependenceRuleVersion>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Firm independence");
    app.MapPortiaPut<RecordNonattestService, NonattestServiceView>(
            "/v1/nonattest-services/record-nonattest-service/{service_record_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<RecordNonattestService>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Firm independence");
    app.MapPortiaPut<EvaluateClientIndependence, OperationAcceptedResponse>(
            "/v1/client-independence-evaluations/evaluate-client-independence/{evaluation_id}",
            config => config.FromHeader(x => x.TenantId, "Tenant-Id")
                .Produces<OperationAcceptedResponse>(StatusCodes.Status202Accepted, "application/json")
                .OnNativeResult((http, result) =>
                {
                    if (!result.IsSuccess)
                        return null;
                    http.Response.Headers.Location = result.Value.StatusUrl;
                    return Results.Json(result.Value, ComplianceCoreJsonContext.Default.OperationAcceptedResponse,
                        statusCode: StatusCodes.Status202Accepted);
                })).WithNativeFirstResponse<EvaluateClientIndependence>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Firm independence");
    app.MapPortiaGet<GetClientIndependenceHistory, IndependenceHistoryView>(
            "/v1/independence/history", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Firm independence");
    app.MapPortiaPut<SetCriteriaTextOverlay, CriteriaTextOverlayRegistration>(
            "/v1/criteria-editions/{edition_id}/overlays/set-criteria-text-overlay/{command_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<SetCriteriaTextOverlay>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Criteria");
    app.MapPortiaPut<SelectProgramCriteriaEdition>(
            "/v1/criteria-edition/selections/{command_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<SelectProgramCriteriaEdition>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Programs");
    app.MapPortiaPut<ReviseProgram>("/v1/revisions/{command_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<ReviseProgram>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Programs");
    app.MapPortiaGet<GetProgram, ProgramView>("/v1/programs/{program_id}", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Programs");
    app.MapPortiaGet<ListPrograms, Page<ProgramView>>("/v1/programs", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Programs");
    app.MapPortiaGet<ListProgramRevisions, Page<ProgramRevisionView>>(
            "/v1/revisions", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Programs");
    app.MapPortiaGet<GetProgramRevision, ProgramRevisionView>(
            "/v1/program-revision", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Programs");
    app.MapPortiaGet<GetProgramSetupWork, ProgramSetupWorkView>(
            "/v1/setup-work", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Programs");
    app.MapPortiaPut<CreateControlDraft, ControlRegistration>(
            "/v1/controls/{identifier}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<CreateControlDraft>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Controls");
    app.MapPortiaPut<ReviseControlDraft>(
            "/v1/controls/{control_id}/draft/revisions/{command_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<ReviseControlDraft>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Controls");
    app.MapPortiaPut<DiscardControlDraft>(
            "/v1/controls/{control_id}/draft/discards/{command_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<DiscardControlDraft>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Controls");
    app.MapPortiaGet<GetControlDraft, ControlDraftView>(
            "/v1/controls/{control_id}/draft", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Controls");
    app.MapPortiaGet<ListControlDrafts, Page<ControlDraftView>>(
            "/v1/controls", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Controls");
    app.MapPortiaGet<ListControlDraftRevisions, Page<ControlDraftRevisionView>>(
            "/v1/controls/{control_id}/draft/revisions", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Controls");
    app.MapPortiaGet<GetControlDraftRevision, ControlDraftRevisionView>(
            "/v1/control-draft-revision", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Controls");
    // Review and approval are human decisions and stay HTTP-only; no MCP tools are registered.
    app.MapPortiaPut<ReviewControl>(
            "/v1/controls/{control_id}/draft/reviews/{command_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<ReviewControl>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Controls");
    app.MapPortiaPut<ApproveControl>(
            "/v1/controls/{control_id}/draft/approvals/{command_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<ApproveControl>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Controls");
    app.MapPortiaGet<GetCurrentControlVersion, ControlVersionView>(
            "/v1/controls/{control_id}/current-version", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Controls");
    app.MapPortiaGet<GetEffectiveControlVersion, ControlVersionView>(
            "/v1/controls/{control_id}/effective-version", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Controls");
    app.MapPortiaGet<ListControlVersions, Page<ControlVersionView>>(
            "/v1/controls/{control_id}/versions", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Controls");
    app.MapPortiaGet<GetControlVersion, ControlVersionView>(
            "/v1/controls/{control_id}/versions/{version_id}", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Controls");
    app.MapPortiaGet<ListControlDecisions, Page<ControlDecisionView>>(
            "/v1/controls/{control_id}/decisions", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Controls");
    app.MapPortiaGet<GetControlDecision, ControlDecisionView>(
            "/v1/controls/{control_id}/decisions/{decision_id}", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Controls");
    // Mapping writes, including review, are human decisions and stay HTTP-only.
    app.MapPortiaPut<ProposeControlCriterionMapping, ControlCriterionMappingRegistration>(
            "/v1/control-mappings/{mapping_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<ProposeControlCriterionMapping>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Control mappings");
    app.MapPortiaPut<ReviewControlCriterionMapping>(
            "/v1/control-mappings/{mapping_id}/reviews/{command_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<ReviewControlCriterionMapping>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Control mappings");
    app.MapPortiaPut<RetireControlCriterionMapping>(
            "/v1/control-mappings/{mapping_id}/retirements/{command_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<RetireControlCriterionMapping>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Control mappings");
    app.MapPortiaGet<GetControlCriterionMapping, ControlCriterionMappingView>(
            "/v1/control-mappings/{mapping_id}", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Control mappings");
    app.MapPortiaGet<ListControlCriterionMappings, Page<ControlCriterionMappingView>>(
            "/v1/control-mappings", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Control mappings");
    app.MapPortiaGet<ListCriteriaCoverage, Page<CriterionCoverageView>>(
            "/v1/criteria-coverage", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Control mappings");
    app.MapPortiaPut<ProposeControlSuccessor, ControlSuccessorRegistration>(
            "/v1/controls/{control_id}/successors/{command_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<ProposeControlSuccessor>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Controls");
    app.MapPortiaPut<ProposeControlRetirement, ControlRetirementRegistration>(
            "/v1/controls/{control_id}/retirement-proposals/{command_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<ProposeControlRetirement>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Controls");
    app.MapPortiaGet<PreviewControlImpact, ControlImpactPreview>(
            "/v1/controls/{control_id}/impact-preview", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Controls");
    // Retirement approval is a human decision and stays HTTP-only.
    app.MapPortiaPut<RetireControl>(
            "/v1/controls/{control_id}/retirements/{command_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<RetireControl>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Controls");
    // Withdrawal and person-owner designation are human decisions and stay HTTP-only.
    app.MapPortiaPut<WithdrawControlProposal>(
            "/v1/controls/{control_id}/proposal-withdrawals/{command_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<WithdrawControlProposal>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Controls");
    app.MapPortiaPut<DesignateControlOwnerPerson>(
            "/v1/controls/{control_id}/draft/owner-person/{command_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<DesignateControlOwnerPerson>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Controls");
    app.MapPortiaGet<GetControlDraftOwnerPerson, ControlDraftOwnerPersonView>(
            "/v1/controls/{control_id}/draft/owner-person", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Controls");
    // Criterion applicability decisions are human decisions; writes stay HTTP-only.
    app.MapPortiaPut<ProposeCriterionNotApplicable, CriterionApplicabilityRegistration>(
            "/v1/criterion-applicability/{decision_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<ProposeCriterionNotApplicable>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Control mappings");
    app.MapPortiaPut<ReviewCriterionApplicability>(
            "/v1/criterion-applicability/{decision_id}/reviews/{command_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<ReviewCriterionApplicability>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Control mappings");
    app.MapPortiaPut<WithdrawCriterionNotApplicable>(
            "/v1/criterion-applicability/{decision_id}/withdrawals/{command_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<WithdrawCriterionNotApplicable>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Control mappings");
    app.MapPortiaGet<GetCriterionApplicability, CriterionApplicabilityView>(
            "/v1/criterion-applicability/{decision_id}", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Control mappings");
    app.MapPortiaGet<ListCriterionApplicability, Page<CriterionApplicabilityView>>(
            "/v1/criterion-applicability", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Control mappings");
    app.MapPortiaPut<CreateCommitmentDraft, CommitmentDraftRegistration>(
            "/v1/commitment-drafts/{draft_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<CreateCommitmentDraft>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Commitments");
    app.MapPortiaPut<ReviseCommitmentDraft>(
            "/v1/commitment-drafts/{draft_id}/draft-revisions/{command_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<ReviseCommitmentDraft>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Commitments");
    app.MapPortiaGet<GetCommitmentDraft, CommitmentDraftView>(
            "/v1/commitment-drafts/{draft_id}", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Commitments");
    app.MapPortiaGet<ListCommitmentDrafts, Page<CommitmentDraftView>>(
            "/v1/commitment-drafts", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Commitments");
    app.MapPortiaGet<ListCommitmentDraftRevisions, Page<CommitmentDraftRevisionView>>(
            "/v1/commitment-drafts/{draft_id}/revisions", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Commitments");
    app.MapPortiaGet<GetCommitmentDraftRevision, CommitmentDraftRevisionView>(
            "/v1/commitment-draft-revision", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Commitments");
    app.MapPortiaGet<PreviewCommitmentImpact, CommitmentImpactPreview>(
            "/v1/commitment-drafts/{draft_id}/impact-preview", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Commitments");
    app.MapPortiaPut<ReviewCommitmentDraft>(
            "/v1/commitment-drafts/{draft_id}/reviews/{command_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<ReviewCommitmentDraft>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Commitments");
    // Approval is a separate human decision after review and stays HTTP-only.
    app.MapPortiaPut<ApproveCommitmentDraft>(
            "/v1/commitment-drafts/{draft_id}/approvals/{command_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<ApproveCommitmentDraft>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Commitments");
    app.MapPortiaPut<RetireCommitment>(
            "/v1/commitments/{commitment_id}/retirements/{command_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<RetireCommitment>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Commitments");
    app.MapPortiaGet<ListCommitmentDecisions, Page<CommitmentDecisionView>>(
            "/v1/commitment-drafts/{draft_id}/decisions", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Commitments");
    app.MapPortiaGet<ListCommitmentVersions, Page<CommitmentVersionView>>(
            "/v1/commitment-drafts/{draft_id}/versions", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Commitments");
    app.MapPortiaGet<GetCommitmentVersion, CommitmentVersionView>(
            "/v1/commitment-version", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Commitments");
    app.MapPortiaGet<GetEffectiveCommitmentVersion, CommitmentVersionView>(
            "/v1/commitment-drafts/{draft_id}/effective-version", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Commitments");
    app.MapPortiaPut<CreatePolicyDraft, PolicyRegistration>(
            "/v1/policies/{policy_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<CreatePolicyDraft>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Policies");
    app.MapPortiaGet<ListPolicies, Page<PolicySummaryView>>(
            "/v1/policies", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Policies");
    app.MapPortiaGet<GetPolicy, PolicyView>(
            "/v1/policies/{policy_id}", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Policies");
    app.MapPortiaPut<RevisePolicyDraft, PolicyRegistration>(
            "/v1/policies/{policy_id}/draft", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<RevisePolicyDraft>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Policies");
    app.MapPortiaPut<DiscardPolicyDraft, UnitResult>(
            "/v1/policies/{policy_id}/draft/discards/{command_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<DiscardPolicyDraft>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Policies");
    app.MapPortiaPut<ProposePolicySuccessor, PolicyRegistration>(
            "/v1/policies/{policy_id}/successors/{command_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<ProposePolicySuccessor>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Policies");
    app.MapPortiaGet<PreviewPolicyImpact, PolicyImpactPreview>(
            "/v1/policies/{policy_id}/impact-preview", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Policies");
    // Reviews, approvals, periodic reviews, and retirement approvals are personal decisions and stay HTTP-only.
    app.MapPortiaPut<ReviewPolicyDraft, PolicyDecisionView>(
            "/v1/policies/{policy_id}/reviews/{command_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<ReviewPolicyDraft>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Policies");
    app.MapPortiaPut<ApprovePolicy, PolicyVersionView>(
            "/v1/policies/{policy_id}/approvals/{command_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<ApprovePolicy>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Policies");
    app.MapPortiaPut<ConfirmPolicyReview, PolicyDecisionView>(
            "/v1/policies/{policy_id}/periodic-reviews/{command_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<ConfirmPolicyReview>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Policies");
    app.MapPortiaPut<ProposePolicyRetirement, PolicyRegistration>(
            "/v1/policies/{policy_id}/retirement-proposals/{command_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<ProposePolicyRetirement>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Policies");
    app.MapPortiaPut<ApprovePolicyRetirement, PolicyDecisionView>(
            "/v1/policies/{policy_id}/retirements/{command_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<ApprovePolicyRetirement>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Policies");
    app.MapPortiaGet<ListPolicyVersions, Page<PolicyVersionView>>(
            "/v1/policies/{policy_id}/versions", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Policies");
    app.MapPortiaGet<GetPolicyVersion, PolicyVersionView>(
            "/v1/policy-version", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Policies");
    app.MapPortiaGet<GetEffectivePolicyVersion, PolicyVersionView>(
            "/v1/policies/{policy_id}/effective-version", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Policies");
    app.MapPortiaGet<ListPolicyDecisions, Page<PolicyDecisionView>>(
            "/v1/policies/{policy_id}/decisions", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Policies");
    app.MapPortiaPut<DefineTrainingRequirement, TrainingRequirementRegistration>(
            "/v1/training-requirements/{requirement_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<DefineTrainingRequirement>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Policy distribution");
    app.MapPortiaGet<ListTrainingRequirements, Page<TrainingRequirementView>>(
            "/v1/training-requirements", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Policy distribution");
    app.MapPortiaGet<GetTrainingRequirement, TrainingRequirementView>(
            "/v1/training-requirements/{requirement_id}", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Policy distribution");
    app.MapPortiaPut<ReviseTrainingRequirement, TrainingRequirementRegistration>(
            "/v1/training-requirements/revise-training-requirement/{command_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<ReviseTrainingRequirement>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Policy distribution");
    app.MapPortiaPut<LaunchPolicyCampaign, CampaignRegistration>(
            "/v1/policy-campaigns/{campaign_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<LaunchPolicyCampaign>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Policy distribution");
    app.MapPortiaPut<LaunchTrainingCampaign, CampaignRegistration>(
            "/v1/training-campaigns/{campaign_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<LaunchTrainingCampaign>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Policy distribution");
    app.MapPortiaGet<ListCampaigns, Page<CampaignSummaryView>>(
            "/v1/campaigns", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Policy distribution");
    app.MapPortiaGet<GetCampaign, CampaignView>(
            "/v1/campaigns/{campaign_id}", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Policy distribution");
    app.MapPortiaGet<ListCampaignParticipants, Page<CampaignParticipantView>>(
            "/v1/campaigns/{campaign_id}/participants", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Policy distribution");
    app.MapPortiaGet<ListCampaignAmendments, Page<CampaignAmendmentView>>(
            "/v1/campaigns/{campaign_id}/amendments", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Policy distribution");
    app.MapPortiaPut<ReconcileCampaignAudience, CampaignReconciliationView>(
            "/v1/campaigns/{campaign_id}/reconciliations/{command_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<ReconcileCampaignAudience>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Policy distribution");
    app.MapPortiaPut<RecordTrainingCompletion, OperationAcceptedResponse>(
            "/v1/campaigns/{campaign_id}/completions/{command_id}",
            config => config.FromHeader(x => x.TenantId, "Tenant-Id")
                .Produces<OperationAcceptedResponse>(StatusCodes.Status202Accepted, "application/json")
                .OnNativeResult((http, result) =>
                {
                    if (!result.IsSuccess)
                        return null;
                    http.Response.Headers.Location = result.Value.StatusUrl;
                    return Results.Json(result.Value, ComplianceCoreJsonContext.Default.OperationAcceptedResponse,
                        statusCode: StatusCodes.Status202Accepted);
                })).WithNativeFirstResponse<RecordTrainingCompletion>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Policy distribution");
    app.MapPortiaPut<CloseCampaign, OperationAcceptedResponse>(
            "/v1/campaigns/{campaign_id}/closure/{command_id}",
            config => config.FromHeader(x => x.TenantId, "Tenant-Id")
                .Produces<OperationAcceptedResponse>(StatusCodes.Status202Accepted, "application/json")
                .OnNativeResult((http, result) =>
                {
                    if (!result.IsSuccess)
                        return null;
                    http.Response.Headers.Location = result.Value.StatusUrl;
                    return Results.Json(result.Value, ComplianceCoreJsonContext.Default.OperationAcceptedResponse,
                        statusCode: StatusCodes.Status202Accepted);
                })).WithNativeFirstResponse<CloseCampaign>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Policy distribution");
    // Personal acknowledgements and exception approvals stay HTTP-only.
    app.MapPortiaPut<AcknowledgePolicy, OperationAcceptedResponse>(
            "/v1/campaigns/{campaign_id}/acknowledgements/{command_id}",
            config => config.FromHeader(x => x.TenantId, "Tenant-Id")
                .Produces<OperationAcceptedResponse>(StatusCodes.Status202Accepted, "application/json")
                .OnNativeResult((http, result) =>
                {
                    if (!result.IsSuccess)
                        return null;
                    http.Response.Headers.Location = result.Value.StatusUrl;
                    return Results.Json(result.Value, ComplianceCoreJsonContext.Default.OperationAcceptedResponse,
                        statusCode: StatusCodes.Status202Accepted);
                })).WithNativeFirstResponse<AcknowledgePolicy>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Policy distribution");
    app.MapPortiaPut<ApproveCampaignWaiver, OperationAcceptedResponse>(
            "/v1/campaigns/{campaign_id}/waivers/{command_id}",
            config => config.FromHeader(x => x.TenantId, "Tenant-Id")
                .Produces<OperationAcceptedResponse>(StatusCodes.Status202Accepted, "application/json")
                .OnNativeResult((http, result) =>
                {
                    if (!result.IsSuccess)
                        return null;
                    http.Response.Headers.Location = result.Value.StatusUrl;
                    return Results.Json(result.Value, ComplianceCoreJsonContext.Default.OperationAcceptedResponse,
                        statusCode: StatusCodes.Status202Accepted);
                })).WithNativeFirstResponse<ApproveCampaignWaiver>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Policy distribution");
    app.MapPortiaPut<CreateRiskDraft, RiskRegistration>(
            "/v1/risks/{risk_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<CreateRiskDraft>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Risks");
    app.MapPortiaPut<ReviseRiskDraft>(
            "/v1/risks/{risk_id}/draft", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<ReviseRiskDraft>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Risks");
    app.MapPortiaGet<GetRiskDraft, RiskDraftView>(
            "/v1/risks/{risk_id}/draft", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Risks");
    app.MapPortiaGet<ListRiskDrafts, Page<RiskDraftView>>(
            "/v1/risks", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Risks");
    app.MapPortiaGet<ListRiskDraftRevisions, Page<RiskDraftRevisionView>>(
            "/v1/risks/{risk_id}/draft/revisions", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Risks");
    app.MapPortiaGet<GetRiskDraftRevision, RiskDraftRevisionView>(
            "/v1/risk-draft-revision", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Risks");
    app.MapPortiaPost<PublishRiskMethodVersion, PortiaCreateOutcome<RiskMethodVersionView>>(
            "/v1/risk-method/versions",
            config => config.FromHeader(x => x.TenantId, "Tenant-Id").Created("/v1/risk-method/versions/{MethodVersionId}")).WithNativeFirstResponse<PublishRiskMethodVersion>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Risks");
    app.MapPortiaGet<GetRiskMethod, RiskMethodVersionView>(
            "/v1/risk-method", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Risks");
    app.MapPortiaGet<GetRiskMethodVersion, RiskMethodVersionView>(
            "/v1/risk-method-version", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Risks");
    app.MapPortiaPut<RecordRiskAssessment, RiskAssessmentView>(
            "/v1/risks/{risk_id}/assessments/{command_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<RecordRiskAssessment>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Risks");
    app.MapPortiaPut<ChooseRiskTreatment>(
            "/v1/risks/{risk_id}/treatment", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<ChooseRiskTreatment>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Risks");
    app.MapPortiaPut<AcceptRisk, RiskAcceptanceView>(
            "/v1/risks/{risk_id}/acceptances/{command_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<AcceptRisk>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Risks");
    app.MapPortiaGet<GetRiskEvaluation, RiskEvaluationView>(
            "/v1/risks/{risk_id}/evaluation", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Risks");
    app.MapPortiaGet<ListRiskEvaluationHistory, Page<RiskEvaluationHistoryEntryView>>(
            "/v1/risks/{risk_id}/evaluation/history", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Risks");
    // Risk owner and control treatment writes are human decisions and stay HTTP-only.
    app.MapPortiaPut<AssignRiskOwner>(
            "/v1/risks/{risk_id}/assign-risk-owner/{command_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<AssignRiskOwner>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Risks");
    app.MapPortiaPut<ProposeRiskControlTreatment, RiskControlTreatmentRegistration>(
            "/v1/risks/{risk_id}/control-treatments/{command_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<ProposeRiskControlTreatment>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Risks");
    app.MapPortiaPut<ReviewRiskControlTreatment>(
            "/v1/risks/{risk_id}/control-treatments/{treatment_id}/reviews/{command_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<ReviewRiskControlTreatment>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Risks");
    app.MapPortiaPut<RetireRiskControlTreatment>(
            "/v1/risks/{risk_id}/control-treatments/{treatment_id}/retirements/{command_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<RetireRiskControlTreatment>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Risks");
    app.MapPortiaPut<AddRiskTreatmentAction, RiskTreatmentActionRegistration>(
            "/v1/risks/{risk_id}/treatment-actions/{action_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<AddRiskTreatmentAction>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Risks");
    app.MapPortiaPut<ReviseRiskTreatmentAction>(
            "/v1/risks/{risk_id}/treatment-actions/revise-risk-treatment-action/{command_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<ReviseRiskTreatmentAction>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Risks");
    app.MapPortiaPut<CancelRiskTreatmentAction>(
            "/v1/risks/{risk_id}/treatment-actions/{action_id}/cancellations/{command_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<CancelRiskTreatmentAction>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Risks");
    app.MapPortiaPut<SubmitRiskTreatmentActionCompletion,
            RiskTreatmentActionCompletionRegistration>(
            "/v1/risks/{risk_id}/treatment-actions/{action_id}/completions/{command_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<SubmitRiskTreatmentActionCompletion>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Risks");
    app.MapPortiaPut<ReviewRiskTreatmentActionCompletion, OperationAcceptedResponse>(
            "/v1/risks/{risk_id}/treatment-actions/{action_id}/completion-reviews/{command_id}",
            config => config.FromHeader(x => x.TenantId, "Tenant-Id")
                .Produces<OperationAcceptedResponse>(StatusCodes.Status202Accepted, "application/json")
                .OnNativeResult((http, result) =>
                {
                    if (!result.IsSuccess)
                        return null;
                    http.Response.Headers.Location = result.Value.StatusUrl;
                    return Results.Json(result.Value, ComplianceCoreJsonContext.Default.OperationAcceptedResponse,
                        statusCode: StatusCodes.Status202Accepted);
                })).WithNativeFirstResponse<ReviewRiskTreatmentActionCompletion>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Risks");
    app.MapPortiaGet<GetRiskGovernance, RiskGovernanceView>(
            "/v1/risks/{risk_id}/governance", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Risks");
    app.MapPortiaPut<RunReadinessAssessment, OperationAcceptedResponse>(
            "/v1/readiness/assessments/{assessment_id}",
            config => config.FromHeader(x => x.TenantId, "Tenant-Id")
                .Produces<OperationAcceptedResponse>(StatusCodes.Status202Accepted, "application/json")
                .OnNativeResult((http, result) =>
                {
                    if (!result.IsSuccess)
                        return null;
                    http.Response.Headers.Location = result.Value.StatusUrl;
                    return Results.Json(result.Value, ComplianceCoreJsonContext.Default.OperationAcceptedResponse,
                        statusCode: StatusCodes.Status202Accepted);
                })).WithNativeFirstResponse<RunReadinessAssessment>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Readiness");
    app.MapPortiaGet<ListReadinessAssessments, Page<ReadinessAssessmentSummaryView>>(
            "/v1/readiness/assessments", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Readiness");
    app.MapPortiaGet<GetReadinessAssessment, ReadinessAssessmentView>(
            "/v1/readiness/assessments/{assessment_id}", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Readiness");
    app.MapPortiaGet<ListReadinessGaps, Page<ReadinessGapView>>(
            "/v1/readiness/assessments/{assessment_id}/gaps", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Readiness");
    app.MapPortiaPut<DecideReadiness, OperationAcceptedResponse>(
            "/v1/readiness/assessments/{assessment_id}/decision/{command_id}",
            config => config.FromHeader(x => x.TenantId, "Tenant-Id")
                .Produces<OperationAcceptedResponse>(StatusCodes.Status202Accepted, "application/json")
                .OnNativeResult((http, result) =>
                {
                    if (!result.IsSuccess)
                        return null;
                    http.Response.Headers.Location = result.Value.StatusUrl;
                    return Results.Json(result.Value, ComplianceCoreJsonContext.Default.OperationAcceptedResponse,
                        statusCode: StatusCodes.Status202Accepted);
                })).WithNativeFirstResponse<DecideReadiness>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Readiness");
    app.MapPortiaPut<PlanReadinessGap, ReadinessGapPlanView>(
            "/v1/readiness/gaps/{gap_id}/plan-readiness-gap/{command_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<PlanReadinessGap>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Readiness");
    // The Type I entry decision is a personal sign-off and advisor feedback is attributed to
    // its author; both stay HTTP-only.
    app.MapPortiaPut<DecideTypeIEntry, OperationAcceptedResponse>(
            "/v1/readiness/assessments/{assessment_id}/type-i-entry-decision/{command_id}",
            config => config.FromHeader(x => x.TenantId, "Tenant-Id")
                .Produces<OperationAcceptedResponse>(StatusCodes.Status202Accepted, "application/json")
                .OnNativeResult((http, result) =>
                {
                    if (!result.IsSuccess)
                        return null;
                    http.Response.Headers.Location = result.Value.StatusUrl;
                    return Results.Json(result.Value, ComplianceCoreJsonContext.Default.OperationAcceptedResponse,
                        statusCode: StatusCodes.Status202Accepted);
                })).WithNativeFirstResponse<DecideTypeIEntry>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Readiness");
    app.MapPortiaGet<ListTypeIEntryDecisions, Page<TypeIEntryDecisionView>>(
            "/v1/readiness/type-i-entry-decisions", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Readiness");
    app.MapPortiaPut<AnnotateReadinessGap, ReadinessAnnotationView>(
            "/v1/readiness/assessments/{assessment_id}/gaps/{gap_id}/annotations/{command_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<AnnotateReadinessGap>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Readiness");
    app.MapPortiaGet<ListReadinessAnnotations, Page<ReadinessAnnotationView>>(
            "/v1/readiness/assessments/{assessment_id}/annotations", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Readiness");
    app.MapPortiaPut<ProposeControlOperatingPlan, ControlOperatingPlanView>(
            "/v1/controls/{control_id}/operating-plan/proposals/{plan_version_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<ProposeControlOperatingPlan>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Control operations");
    app.MapPortiaPost<PreviewControlOperatingPlan, ControlOperatingPlanPreview>(
            "/v1/controls/{control_id}/operating-plan/preview", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<PreviewControlOperatingPlan>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Control operations");
    app.MapPortiaPut<ApproveControlOperatingPlan, ControlOperatingPlanView>(
            "/v1/controls/{control_id}/operating-plan/proposals/{plan_version_id}/approvals/{command_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<ApproveControlOperatingPlan>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Control operations");
    app.MapPortiaGet<GetControlOperatingPlan, ControlOperatingPlanSetView>(
            "/v1/controls/{control_id}/operating-plan", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Control operations");
    app.MapPortiaGet<ListControlOperatingBlockers, Page<ControlOperatingBlockerView>>(
            "/v1/operating-blockers", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Control operations");
    app.MapPortiaGet<ListMyControlWork, MyControlWorkView>(
            "/v1/my-work", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Control operations");
    app.MapPortiaPut<OpenControlOccurrence, ControlOccurrenceView>(
            "/v1/controls/{control_id}/occurrences/{command_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<OpenControlOccurrence>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Control operations");
    app.MapPortiaGet<ListControlOccurrences, Page<ControlOccurrenceView>>(
            "/v1/controls/{control_id}/occurrences", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Control operations");
    app.MapPortiaGet<GetControlOccurrence, ControlOccurrenceView>(
            "/v1/controls/{control_id}/occurrences/{occurrence_id}", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Control operations");
    app.MapPortiaPut<AttestControlOccurrence, ControlOccurrenceView>(
            "/v1/controls/{control_id}/occurrences/{occurrence_id}/attestations/{command_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<AttestControlOccurrence>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Control operations");
    app.MapPortiaPut<CorrectControlAttestation, ControlOccurrenceView>(
            "/v1/controls/{control_id}/occurrences/{occurrence_id}/corrections/{command_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<CorrectControlAttestation>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Control operations");
    app.MapPortiaPut<ReviewControlOccurrence, ControlOccurrenceView>(
            "/v1/controls/{control_id}/occurrences/{occurrence_id}/reviews/{command_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<ReviewControlOccurrence>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Control operations");
    app.MapPortiaPut<StartControlEvaluation, ControlEvaluationView>(
            "/v1/controls/{control_id}/evaluations/{evaluation_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<StartControlEvaluation>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Control evaluations");
    app.MapPortiaPost<DefineControlEvaluationPlan, PortiaCreateOutcome<ControlEvaluationPlanVersionView>>(
            "/v1/controls/{control_id}/evaluation-plan/versions",
            config => config.FromHeader(x => x.TenantId, "Tenant-Id").Created("/v1/controls/{control_id}/evaluation-plan/versions/{PlanVersionId}")).WithNativeFirstResponse<DefineControlEvaluationPlan>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Control evaluations");
    app.MapPortiaGet<ListControlEvaluationPlanVersions,
            Page<ControlEvaluationPlanVersionView>>(
            "/v1/controls/{control_id}/evaluation-plan/versions", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Control evaluations");
    app.MapPortiaGet<GetControlEvaluationPlanVersion, ControlEvaluationPlanVersionView>(
            "/v1/controls/{control_id}/evaluation-plan/versions/{plan_version_id}", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Control evaluations");
    app.MapPortiaGet<ListControlEvaluations, Page<ControlEvaluationView>>(
            "/v1/controls/{control_id}/evaluations", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Control evaluations");
    app.MapPortiaGet<GetControlEvaluation, ControlEvaluationView>(
            "/v1/controls/{control_id}/evaluations/{evaluation_id}", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Control evaluations");
    app.MapPortiaPut<RecordControlEvaluationStep, ControlEvaluationView>(
            "/v1/controls/{control_id}/evaluations/{evaluation_id}/steps/{step_id}/results/{command_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<RecordControlEvaluationStep>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Control evaluations");
    app.MapPortiaPut<DisposeControlEvaluationDeviation, ControlEvaluationView>(
            "/v1/controls/{control_id}/evaluations/{evaluation_id}/deviations/{deviation_id}/dispositions/{command_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<DisposeControlEvaluationDeviation>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Control evaluations");
    app.MapPortiaPut<SubmitControlEvaluation, ControlEvaluationView>(
            "/v1/controls/{control_id}/evaluations/{evaluation_id}/submissions/{command_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<SubmitControlEvaluation>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Control evaluations");
    app.MapPortiaPut<ReviewControlEvaluation, ControlEvaluationView>(
            "/v1/controls/{control_id}/evaluations/{evaluation_id}/reviews/{command_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<ReviewControlEvaluation>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Control evaluations");
    app.MapPortiaPut<OpenEvidenceRequest, EvidenceRequestView>(
            "/v1/evidence-requests/{evidence_request_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<OpenEvidenceRequest>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Evidence requests");
    app.MapPortiaGet<ListEvidenceRequests, Page<EvidenceRequestView>>(
            "/v1/evidence-requests", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Evidence requests");
    app.MapPortiaPut<PrepareEvidenceRedaction, EvidenceRedactionView>(
            "/v1/evidence-redactions/{redaction_id}/preparations/{command_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<PrepareEvidenceRedaction>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Evidence redactions");
    app.MapPortiaPut<ApproveEvidenceRedaction, EvidenceRedactionView>(
            "/v1/evidence-redactions/{redaction_id}/approvals/{command_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<ApproveEvidenceRedaction>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Evidence redactions");
    app.MapPortiaGet<GetEvidenceRedaction, EvidenceRedactionView>(
            "/v1/evidence-redactions/{redaction_id}", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Evidence redactions");
    app.MapPortiaGet<GetEvidenceArtifactMetadata, EvidenceArtifactMetadataView>(
            "/v1/evidence-artifacts/{artifact_id}/metadata", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Evidence artifacts");
    app.MapPortiaPost<IssueArtifactDownload, EvidenceArtifactDownloadLink>(
            "/v1/evidence-artifacts/{artifact_id}/downloads", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<IssueArtifactDownload>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Evidence artifacts");
    app.MapPortiaPost<ReleaseEvidenceArtifactQuarantine>(
            "/v1/evidence-artifacts/{artifact_id}/quarantine-releases", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<ReleaseEvidenceArtifactQuarantine>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithSummary("Release malware-quarantined evidence after a clean rescan")
        .WithTags("Evidence artifacts");
    app.MapPortiaPut<LinkEvidenceSupport, EvidenceSupportView>(
            "/v1/evidence-artifacts/{artifact_id}/supports/{command_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<LinkEvidenceSupport>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Evidence artifacts");
    app.MapPortiaGet<GetEvidenceSupport, EvidenceSupportStatusView>(
            "/v1/evidence-supports/{evidence_support_id}", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Evidence supports");
    app.MapPortiaGet<ListEvidenceSupports, Page<EvidenceSupportStatusView>>(
            "/v1/evidence-supports", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Evidence supports");
    app.MapPortiaPut<WithdrawEvidenceSupport>(
            "/v1/evidence-supports/{support_id}/withdrawals/{command_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<WithdrawEvidenceSupport>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Evidence supports");
    app.MapPortiaGet<GetEvidenceRequest, EvidenceRequestView>(
            "/v1/evidence-requests/{evidence_request_id}", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Evidence requests");
    app.MapPortiaPut<FulfilEvidenceRequest, EvidenceRequestView>(
            "/v1/evidence-requests/{evidence_request_id}/fulfilments/{command_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<FulfilEvidenceRequest>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Evidence requests");
    app.MapPortiaPut<CancelEvidenceRequest, EvidenceRequestView>(
            "/v1/evidence-requests/{evidence_request_id}/cancellations/{command_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<CancelEvidenceRequest>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Evidence requests");
    app.MapPortiaPut<RaiseFinding, FindingRegistration>(
            "/v1/findings/{finding_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<RaiseFinding>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Findings");
    app.MapPortiaGet<ListFindings, Page<FindingView>>(
            "/v1/findings", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Findings");
    app.MapPortiaGet<GetFinding, FindingView>(
            "/v1/findings/{finding_id}", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Findings");
    app.MapPortiaPut<ReviseFinding, FindingView>(
            "/v1/findings/{finding_id}/revisions/{command_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<ReviseFinding>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Findings");
    app.MapPortiaPut<AddCorrectiveAction, OperationAcceptedResponse>(
            "/v1/findings/{finding_id}/corrective-actions/{action_id}",
            config => config.FromHeader(x => x.TenantId, "Tenant-Id")
                .Produces<OperationAcceptedResponse>(StatusCodes.Status202Accepted, "application/json")
                .OnNativeResult((http, result) =>
                {
                    if (!result.IsSuccess)
                        return null;
                    http.Response.Headers.Location = result.Value.StatusUrl;
                    return Results.Json(result.Value, ComplianceCoreJsonContext.Default.OperationAcceptedResponse,
                        statusCode: StatusCodes.Status202Accepted);
                })).WithNativeFirstResponse<AddCorrectiveAction>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Findings");
    app.MapPortiaPut<CompleteCorrectiveAction, FindingView>(
            "/v1/findings/{finding_id}/corrective-actions/{action_id}/completions/{command_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<CompleteCorrectiveAction>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Findings");
    app.MapPortiaPut<ChangeCorrectiveAction, FindingView>(
            "/v1/findings/{finding_id}/corrective-actions/{action_id}/changes/{command_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<ChangeCorrectiveAction>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Findings");
    app.MapPortiaPut<CancelCorrectiveAction, FindingView>(
            "/v1/findings/{finding_id}/corrective-actions/{action_id}/cancellations/{command_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<CancelCorrectiveAction>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Findings");
    app.MapPortiaPut<LinkFindingAcceptance, FindingView>(
            "/v1/findings/{finding_id}/acceptances/{command_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<LinkFindingAcceptance>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Findings");
    app.MapPortiaPut<CloseFinding, OperationAcceptedResponse>(
            "/v1/findings/{finding_id}/closures/{command_id}",
            config => config.FromHeader(x => x.TenantId, "Tenant-Id")
                .Produces<OperationAcceptedResponse>(StatusCodes.Status202Accepted, "application/json")
                .OnNativeResult((http, result) =>
                {
                    if (!result.IsSuccess)
                        return null;
                    http.Response.Headers.Location = result.Value.StatusUrl;
                    return Results.Json(result.Value, ComplianceCoreJsonContext.Default.OperationAcceptedResponse,
                        statusCode: StatusCodes.Status202Accepted);
                })).WithNativeFirstResponse<CloseFinding>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Findings");
    app.MapPortiaPut<ReopenFinding, FindingView>(
            "/v1/findings/{finding_id}/reopenings/{command_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<ReopenFinding>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Findings");
    app.MapPortiaGet<GetAuditorRequest, AuditorRequestView>(
            "/v1/auditor-requests/{auditor_request_id}", endpoint => endpoint
                .OnBind(Program.BindAuditorRequestRead)
                .Parameter<Uuid>("tenant_id", PortiaHttpParameterLocation.Header, required: true)
                .Parameter<Uuid>("program_id", PortiaHttpParameterLocation.Query, required: true)
                .Parameter<Uuid>("auditor_request_id", PortiaHttpParameterLocation.Route, required: true))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Auditor requests");
    app.MapPortiaGet<ListAuditorRequests, Page<AuditorRequestSummaryView>>(
            "/v1/auditor-requests", endpoint => endpoint
                .OnBind(Program.BindAuditorRequestsRead)
                .Parameter<Uuid>("tenant_id", PortiaHttpParameterLocation.Header, required: true)
                .Parameter<Uuid>("program_id", PortiaHttpParameterLocation.Query, required: true)
                .Parameter<int?>("limit", PortiaHttpParameterLocation.Query, required: false)
                .Parameter<string?>("cursor", PortiaHttpParameterLocation.Query, required: false))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Auditor requests");
    app.MapPortiaGet<ListRequestResponses, Page<AuditorRequestResponseView>>(
            "/v1/auditor-requests/{auditor_request_id}/responses", endpoint => endpoint
                .OnBind(Program.BindAuditorRequestResponsesRead)
                .Parameter<Uuid>("tenant_id", PortiaHttpParameterLocation.Header, required: true)
                .Parameter<Uuid>("program_id", PortiaHttpParameterLocation.Query, required: true)
                .Parameter<Uuid>("auditor_request_id", PortiaHttpParameterLocation.Route, required: true)
                .Parameter<int?>("limit", PortiaHttpParameterLocation.Query, required: false)
                .Parameter<string?>("cursor", PortiaHttpParameterLocation.Query, required: false))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Auditor requests");
    app.MapPortiaGet<GetSampleSelection, SampleSelectionView>(
            "/v1/sample-selections/{sample_selection_id}", endpoint => endpoint
                .OnBind(Program.BindAuditorSampleSelectionRead)
                .Parameter<Uuid>("tenant_id", PortiaHttpParameterLocation.Header, required: true)
                .Parameter<Uuid>("program_id", PortiaHttpParameterLocation.Query, required: true)
                .Parameter<Uuid>("sample_selection_id", PortiaHttpParameterLocation.Route, required: true))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Auditor requests");
    app.MapPortiaPut<RecordAuditorRequest>(
            "/v1/auditor-requests/{request_id}/record-auditor-request", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<RecordAuditorRequest>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Auditor requests");
    app.MapPortiaPut<ReviseAuditorRequest>(
            "/v1/auditor-requests/{request_id}/revise-auditor-request/{command_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<ReviseAuditorRequest>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Auditor requests");
    app.MapPortiaPut<AssignAuditorRequest>(
            "/v1/auditor-requests/{request_id}/assign-auditor-request/{command_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<AssignAuditorRequest>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Auditor requests");
    app.MapPortiaPut<ReopenAuditorRequest>(
            "/v1/auditor-requests/{request_id}/reopen-auditor-request/{command_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<ReopenAuditorRequest>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Auditor requests");
    app.MapPortiaPut<ReviseAuditResponseDraft>(
            "/v1/auditor-requests/{request_id}/responses/{response_id}/revise-audit-response-draft/{command_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<ReviseAuditResponseDraft>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Auditor requests");
    app.MapPortiaPut<SubmitAuditResponse>(
            "/v1/auditor-requests/{request_id}/responses/{response_id}/submit-audit-response/{command_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<SubmitAuditResponse>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Auditor requests");
    app.MapPortiaPut<ReviewAuditResponse>(
            "/v1/auditor-requests/{request_id}/responses/{response_id}/review-audit-response/{command_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<ReviewAuditResponse>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Auditor requests");
    app.MapPortiaPut<DeliverAuditResponse>(
            "/v1/auditor-requests/{request_id}/responses/{response_id}/deliver-audit-response/{command_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<DeliverAuditResponse>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Auditor requests");
    app.MapPortiaPut<RecordAuditorResponseDisposition>(
            "/v1/auditor-requests/{request_id}/responses/{response_id}/record-auditor-response-disposition/{command_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<RecordAuditorResponseDisposition>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Auditor requests");
    app.MapPortiaPut<ResolveAuditorRequest, OperationAcceptedResponse>(
            "/v1/auditor-requests/{request_id}/resolve-auditor-request/{command_id}",
            config => config.FromHeader(x => x.TenantId, "Tenant-Id")
                .Produces<OperationAcceptedResponse>(StatusCodes.Status202Accepted, "application/json")
                .OnNativeResult((http, result) =>
                {
                    if (!result.IsSuccess)
                        return null;
                    http.Response.Headers.Location = result.Value.StatusUrl;
                    return Results.Json(result.Value, ComplianceCoreJsonContext.Default.OperationAcceptedResponse,
                        statusCode: StatusCodes.Status202Accepted);
                })).WithNativeFirstResponse<ResolveAuditorRequest>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Auditor requests");
    app.MapPortiaPut<CreateAuditResponseDraft, OperationAcceptedResponse>(
            "/v1/auditor-requests/{request_id}/create-audit-response-draft/{response_id}",
            config => config.FromHeader(x => x.TenantId, "Tenant-Id")
                .Produces<OperationAcceptedResponse>(StatusCodes.Status202Accepted, "application/json")
                .OnNativeResult((http, result) =>
                {
                    if (!result.IsSuccess)
                        return null;
                    http.Response.Headers.Location = result.Value.StatusUrl;
                    return Results.Json(result.Value, ComplianceCoreJsonContext.Default.OperationAcceptedResponse,
                        statusCode: StatusCodes.Status202Accepted);
                })).WithNativeFirstResponse<CreateAuditResponseDraft>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Auditor requests");
    app.MapPortiaPut<RecordAuditorRequestComment, OperationAcceptedResponse>(
            "/v1/auditor-requests/{request_id}/record-auditor-request-comment/{comment_id}",
            config => config.FromHeader(x => x.TenantId, "Tenant-Id")
                .Produces<OperationAcceptedResponse>(StatusCodes.Status202Accepted, "application/json")
                .OnNativeResult((http, result) =>
                {
                    if (!result.IsSuccess)
                        return null;
                    http.Response.Headers.Location = result.Value.StatusUrl;
                    return Results.Json(result.Value, ComplianceCoreJsonContext.Default.OperationAcceptedResponse,
                        statusCode: StatusCodes.Status202Accepted);
                })).WithNativeFirstResponse<RecordAuditorRequestComment>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Auditor requests");
    app.MapPortiaPut<RecordSampleSelection, OperationAcceptedResponse>(
            "/v1/auditor-requests/{auditor_request_id}/sample-selections/record-sample-selection/{selection_id}",
            config => config.FromHeader(x => x.TenantId, "Tenant-Id")
                .Produces<OperationAcceptedResponse>(StatusCodes.Status202Accepted, "application/json")
                .OnNativeResult((http, result) =>
                {
                    if (!result.IsSuccess)
                        return null;
                    http.Response.Headers.Location = result.Value.StatusUrl;
                    return Results.Json(result.Value, ComplianceCoreJsonContext.Default.OperationAcceptedResponse,
                        statusCode: StatusCodes.Status202Accepted);
                })).WithNativeFirstResponse<RecordSampleSelection>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Auditor requests");
    app.MapPortiaPut<AmendSampleSelection, OperationAcceptedResponse>(
            "/v1/auditor-requests/{auditor_request_id}/sample-selections/amend-sample-selection/{selection_id}",
            config => config.FromHeader(x => x.TenantId, "Tenant-Id")
                .Produces<OperationAcceptedResponse>(StatusCodes.Status202Accepted, "application/json")
                .OnNativeResult((http, result) =>
                {
                    if (!result.IsSuccess)
                        return null;
                    http.Response.Headers.Location = result.Value.StatusUrl;
                    return Results.Json(result.Value, ComplianceCoreJsonContext.Default.OperationAcceptedResponse,
                        statusCode: StatusCodes.Status202Accepted);
                })).WithNativeFirstResponse<AmendSampleSelection>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Auditor requests");
    app.MapPortiaPut<OpenAccessPopulation, AccessPopulationRegistration>(
            "/v1/access-populations/{population_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<OpenAccessPopulation>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("AccessReviews");
    app.MapPortiaPut<RecordAccessPopulationFacts, AccessPopulationRegistration>(
            "/v1/access-populations/record-access-population-facts/{command_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<RecordAccessPopulationFacts>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("AccessReviews");
    app.MapPortiaGet<PreviewAccessPopulation, AccessPopulationPreview>(
            "/v1/access-populations/{population_id}/preview", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("AccessReviews");
    app.MapPortiaPut<AcceptAccessPopulation, AccessPopulationAcceptance>(
            "/v1/access-populations/{population_id}/acceptance/{command_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<AcceptAccessPopulation>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("AccessReviews");
    app.MapPortiaGet<GetAccessPopulation, AccessPopulationView>(
            "/v1/access-populations/{population_id}", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("AccessReviews");
    app.MapPortiaGet<ListAccessPopulations, Page<AccessPopulationSummaryView>>(
            "/v1/system-instances/{system_instance_id}/access-populations", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("AccessReviews");
    app.MapPortiaPut<ExemptMissingAccessPopulation, AccessPopulationExceptionView>(
            "/v1/applications/{application_id}/system-instances/{system_instance_id}/access-population-exceptions/{exception_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<ExemptMissingAccessPopulation>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("AccessReviews");
    app.MapPortiaGet<GetAccessReviewCoverage, AccessReviewCoverageView>(
            "/v1/applications/{application_id}/access-review-coverage", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("AccessReviews");
    app.MapPortiaPut<ClassifyAccessPrincipal, AccessPrincipalClassificationView>(
            "/v1/access-populations/{population_id}/classifications/{command_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<ClassifyAccessPrincipal>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("AccessReviews");
    app.MapPortiaGet<ListAccessPrincipals, Page<AccessPrincipalView>>(
            "/v1/access-populations/{population_id}/principals", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("AccessReviews");
    app.MapPortiaGet<GetAccessVariance, AccessVarianceView>(
            "/v1/access-populations/{population_id}/variance", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("AccessReviews");
    app.MapPortiaPut<ProposeAccessExpectation, AccessExpectationView>(
            "/v1/applications/{application_id}/system-instances/{system_instance_id}/access-expectations/{expectation_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<ProposeAccessExpectation>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("AccessReviews");
    app.MapPortiaPut<ApproveAccessExpectation, AccessExpectationView>(
            "/v1/system-instances/{system_instance_id}/access-expectations/{expectation_id}/approval/{command_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<ApproveAccessExpectation>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("AccessReviews");
    app.MapPortiaPut<ExemptAccessExpectation, AccessExpectationExceptionView>(
            "/v1/system-instances/{system_instance_id}/access-expectations/{expectation_id}/exceptions/{command_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<ExemptAccessExpectation>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("AccessReviews");
    app.MapPortiaGet<ListAccessExpectations, AccessExpectationsView>(
            "/v1/system-instances/{system_instance_id}/access-expectations", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("AccessReviews");
    app.MapPortiaPut<LaunchAccessReviewCampaign, AccessReviewCampaignRegistration>(
            "/v1/access-review-campaigns/{campaign_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<LaunchAccessReviewCampaign>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("AccessReviews");
    app.MapPortiaGet<ListAccessReviewCampaigns, Page<AccessReviewCampaignSummaryView>>(
            "/v1/access-review-campaigns", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("AccessReviews");
    app.MapPortiaGet<GetAccessReviewCampaign, AccessReviewCampaignView>(
            "/v1/access-review-campaigns/{campaign_id}", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("AccessReviews");
    app.MapPortiaPut<RecordAccessDecision, AccessDecisionView>(
            "/v1/access-review-campaigns/{campaign_id}/items/{item_id}/decisions/{command_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<RecordAccessDecision>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("AccessReviews");
    app.MapPortiaPost<PreviewBulkAccessDecision, BulkAccessDecisionPreview>(
            "/v1/access-review-campaigns/{campaign_id}/bulk-decision-previews", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<PreviewBulkAccessDecision>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("AccessReviews");
    app.MapPortiaPut<RecordBulkAccessDecision, OperationAcceptedResponse>(
            "/v1/access-review-campaigns/{campaign_id}/bulk-decisions/{decision_batch_id}",
            config => config.FromHeader(x => x.TenantId, "Tenant-Id")
                .Produces<OperationAcceptedResponse>(StatusCodes.Status202Accepted, "application/json")
                .OnNativeResult((http, result) =>
                {
                    if (!result.IsSuccess)
                        return null;
                    http.Response.Headers.Location = result.Value.StatusUrl;
                    return Results.Json(result.Value, ComplianceCoreJsonContext.Default.OperationAcceptedResponse,
                        statusCode: StatusCodes.Status202Accepted);
                })).WithNativeFirstResponse<RecordBulkAccessDecision>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("AccessReviews");
    app.MapPortiaPut<RecordAccessRemediationChange, AccessRemediationChangeView>(
            "/v1/access-review-campaigns/{campaign_id}/items/{item_id}/remediation-changes/{command_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<RecordAccessRemediationChange>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("AccessReviews");
    app.MapPortiaPut<ReassignAccessReviewResponsibility, AccessReviewResponsibilityReassignmentView>(
            "/v1/access-review-campaigns/{campaign_id}/items/{item_id}/responsibility-reassignments/{command_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<ReassignAccessReviewResponsibility>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("AccessReviews");
    app.MapPortiaPut<VerifyAccessRemediation, AccessRemediationVerificationView>(
            "/v1/access-review-campaigns/{campaign_id}/items/{item_id}/remediation-verifications/{command_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<VerifyAccessRemediation>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("AccessReviews");
    app.MapPortiaPut<ExemptAccessRemediation, AccessRemediationExceptionView>(
            "/v1/access-review-campaigns/{campaign_id}/items/{item_id}/remediation-exceptions/{command_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<ExemptAccessRemediation>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("AccessReviews");
    app.MapPortiaPut<CompleteAccessReviewCampaign, OperationAcceptedResponse>(
            "/v1/access-review-campaigns/{campaign_id}/completion/{command_id}",
            config => config.FromHeader(x => x.TenantId, "Tenant-Id")
                .Produces<OperationAcceptedResponse>(StatusCodes.Status202Accepted, "application/json")
                .OnNativeResult((http, result) =>
                {
                    if (!result.IsSuccess)
                        return null;
                    http.Response.Headers.Location = result.Value.StatusUrl;
                    return Results.Json(result.Value, ComplianceCoreJsonContext.Default.OperationAcceptedResponse,
                        statusCode: StatusCodes.Status202Accepted);
                })).WithNativeFirstResponse<CompleteAccessReviewCampaign>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("AccessReviews");
    app.MapPortiaGet<ListWork, WorkQueueView>(
            "/v1/work", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Work");
    app.MapPortiaGet<ListWorkReminders, IReadOnlyList<WorkReminderView>>(
            "/v1/work/reminders", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Work");
    app.MapPortiaGet<GetWorkDigest, WorkDigestView>(
            "/v1/work/digest", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Work");
    app.MapPortiaGet<GetWorkItem, WorkItemDetailView>(
            "/v1/work/{work_item_id}", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Work");
    app.MapPortiaPut<ClaimWorkItem, WorkItemDetailView>(
            "/v1/work/{work_item_id}/claims/{command_id}", config => config
                .OnBind(WorkAssignmentHttp.BindClaimAsync)
                .Accepts<WorkAssignmentHttp.ClaimBody>("application/json", isOptional: false)
                .Parameter<Uuid>("tenant_id", PortiaHttpParameterLocation.Header, required: true)
                .Parameter<Uuid>("program_id", PortiaHttpParameterLocation.Query, required: true)
                .Parameter<Uuid>("work_item_id", PortiaHttpParameterLocation.Route, required: true)
                .Parameter<Uuid>("command_id", PortiaHttpParameterLocation.Route, required: true)
                .OnNativeResult()).WithNativeFirstResponse<ClaimWorkItem>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Work");
    app.MapPortiaPut<AssignWorkItem, WorkItemDetailView>(
            "/v1/work/{work_item_id}/assignments/{command_id}", config => config
                .OnBind(WorkAssignmentHttp.BindAssignAsync)
                .Accepts<WorkAssignmentHttp.AssignBody>("application/json", isOptional: false)
                .Parameter<Uuid>("tenant_id", PortiaHttpParameterLocation.Header, required: true)
                .Parameter<Uuid>("program_id", PortiaHttpParameterLocation.Query, required: true)
                .Parameter<Uuid>("work_item_id", PortiaHttpParameterLocation.Route, required: true)
                .Parameter<Uuid>("command_id", PortiaHttpParameterLocation.Route, required: true)
                .OnNativeResult()).WithNativeFirstResponse<AssignWorkItem>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Work");
    app.MapPortiaPut<DelegateWorkItem, WorkItemDetailView>(
            "/v1/work/{work_item_id}/delegations/{command_id}", config => config
                .OnBind(WorkAssignmentHttp.BindDelegateAsync)
                .Accepts<WorkAssignmentHttp.DelegateBody>("application/json", isOptional: false)
                .Parameter<Uuid>("tenant_id", PortiaHttpParameterLocation.Header, required: true)
                .Parameter<Uuid>("program_id", PortiaHttpParameterLocation.Query, required: true)
                .Parameter<Uuid>("work_item_id", PortiaHttpParameterLocation.Route, required: true)
                .Parameter<Uuid>("command_id", PortiaHttpParameterLocation.Route, required: true)
                .OnNativeResult()).WithNativeFirstResponse<DelegateWorkItem>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Work");
    app.MapPortiaPut<EscalateWorkItem, WorkItemDetailView>(
            "/v1/work/{work_item_id}/escalations/{command_id}", config => config
                .OnBind(WorkAssignmentHttp.BindEscalateAsync)
                .Accepts<WorkAssignmentHttp.EscalateBody>("application/json", isOptional: false)
                .Parameter<Uuid>("tenant_id", PortiaHttpParameterLocation.Header, required: true)
                .Parameter<Uuid>("program_id", PortiaHttpParameterLocation.Query, required: true)
                .Parameter<Uuid>("work_item_id", PortiaHttpParameterLocation.Route, required: true)
                .Parameter<Uuid>("command_id", PortiaHttpParameterLocation.Route, required: true)
                .OnNativeResult()).WithNativeFirstResponse<EscalateWorkItem>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Work");
    app.MapPortiaGet<GetWorkDigestPreference, WorkDigestPreferenceView>(
            "/v1/work-digest-preference", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Work");
    app.MapPortiaPut<SetWorkDigestPreference, WorkDigestPreferenceView>(
            "/v1/work-digest-preference/changes/{command_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<SetWorkDigestPreference>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Work");
    app.MapPortiaPut<RecordPerson, PersonRegistration>(
            "/v1/people/{person_id}", config => config
                .OnBind(PersonHttp.BindRecordAsync)
                .Accepts<PersonHttp.RecordBody>("application/json", isOptional: false)
                .Parameter<Uuid>("tenant_id", PortiaHttpParameterLocation.Header, required: true)
                .Parameter<Uuid>("person_id", PortiaHttpParameterLocation.Route, required: true)
                .OnNativeResult()).WithNativeFirstResponse<RecordPerson>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Workforce");
    app.MapPortiaPost<ChangePersonDetails, UnitResult>(
            "/v1/people/{person_id}/detail-changes/{command_id}", config => config
                .OnBind(PersonHttp.BindChangeDetailsAsync)
                .Accepts<PersonHttp.ChangeDetailsBody>("application/json", isOptional: false)
                .Parameter<Uuid>("tenant_id", PortiaHttpParameterLocation.Header, required: true)
                .Parameter<Uuid>("person_id", PortiaHttpParameterLocation.Route, required: true)
                .Parameter<Uuid>("command_id", PortiaHttpParameterLocation.Route, required: true)
                .OnNativeResult()).WithNativeFirstResponse<ChangePersonDetails>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Workforce");
    app.MapPortiaPut<CorrelatePersonMembership, UnitResult>(
            "/v1/people/{person_id}/membership-correlations/{command_id}", config => config
                .OnBind(PersonHttp.BindCorrelateAsync)
                .Accepts<PersonHttp.CorrelateBody>("application/json", isOptional: false)
                .Parameter<Uuid>("tenant_id", PortiaHttpParameterLocation.Header, required: true)
                .Parameter<Uuid>("person_id", PortiaHttpParameterLocation.Route, required: true)
                .Parameter<Uuid>("command_id", PortiaHttpParameterLocation.Route, required: true)
                .OnNativeResult()).WithNativeFirstResponse<CorrelatePersonMembership>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Workforce");
    app.MapPortiaGet<GetPerson, PersonView>(
            "/v1/people/{person_id}", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Workforce");
    app.MapPortiaGet<ListPeople, Page<PersonView>>(
            "/v1/people", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Workforce");
    app.MapPortiaPost<RequestWaiver, PortiaCreateOutcome<Bdgrz.Compliance.Features.Remediation.Models.WaiverRegistration>>(
            "/v1/waivers",
            config => config.FromHeader(x => x.TenantId, "Tenant-Id").Created("/v1/waivers/{WaiverId}")).WithNativeFirstResponse<RequestWaiver>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Assurance work");
    app.MapPortiaPost<ReviewWaiver>(
            "/v1/waivers/{waiver_id}/review", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<ReviewWaiver>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Assurance work");
    app.MapPortiaPost<RevokeWaiver>(
            "/v1/waivers/{waiver_id}/revoke", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<RevokeWaiver>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Assurance work");
    app.MapPortiaGet<Bdgrz.Compliance.Features.Remediation.Queries.GetWaiver, Bdgrz.Compliance.Features.Remediation.Models.WaiverView>(
            "/v1/waivers/{waiver_id}", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Assurance work");
    app.MapPortiaPost<RecordWorkRelationship, PortiaCreateOutcome<WorkRelationshipRegistration>>(
            "/v1/work-relationships",
            config => config.FromHeader(x => x.TenantId, "Tenant-Id").Created("/v1/work-relationships/{RelationshipId}")).WithNativeFirstResponse<RecordWorkRelationship>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Workforce");
    app.MapPortiaPost<StartWorkRelationship>(
            "/v1/work-relationships/{relationship_id}/start", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<StartWorkRelationship>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Workforce");
    app.MapPortiaPost<StartLeave>(
            "/v1/work-relationships/{relationship_id}/leave/start", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<StartLeave>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Workforce");
    app.MapPortiaPost<EndLeave>(
            "/v1/work-relationships/{relationship_id}/leave/end", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<EndLeave>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Workforce");
    app.MapPortiaPost<EndWorkRelationship>(
            "/v1/work-relationships/{relationship_id}/end", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<EndWorkRelationship>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Workforce");
    app.MapPortiaPost<ChangeManager>(
            "/v1/work-relationships/{relationship_id}/manager", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<ChangeManager>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Workforce");
    app.MapPortiaPost<ChangeDepartment>(
            "/v1/work-relationships/{relationship_id}/department", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<ChangeDepartment>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Workforce");
    app.MapPortiaPost<ChangeSponsor>(
            "/v1/work-relationships/{relationship_id}/sponsor", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<ChangeSponsor>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Workforce");
    app.MapPortiaPost<CorrectWorkRelationshipDates>(
            "/v1/work-relationships/{relationship_id}/dates/correction", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<CorrectWorkRelationshipDates>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Workforce");
    app.MapPortiaGet<GetWorkRelationship, WorkRelationshipView>(
            "/v1/work-relationships/{relationship_id}", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Workforce");
    app.MapPortiaGet<ListWorkRelationships, Page<WorkRelationshipView>>(
            "/v1/work-relationships", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Workforce");
    app.MapPortiaGet<ListWorkforceObservations, Page<WorkforceObservationView>>(
            "/v1/workforce-observations", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Workforce");
    app.MapPortiaPut<ResolveWorkforceObservation>(
            "/v1/workforce-observations/{observation_id}/resolution", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<ResolveWorkforceObservation>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Workforce");
    app.MapPortiaPost<RecordWorkforceSourceObservation, PortiaCreateOutcome<WorkforceSourceRegistration>>(
            "/v1/workforce-source-observations",
            config => config.FromHeader(x => x.TenantId, "Tenant-Id").Created("/v1/workforce-source-observations/{ObservationId}")).WithNativeFirstResponse<RecordWorkforceSourceObservation>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Workforce");
    app.MapPortiaGet<GetWorkforceSourceObservation, WorkforceSourceView>(
            "/v1/workforce-source-observations/{observation_id}", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Workforce");
    app.MapPortiaGet<ListWorkforceSourceObservations, Page<WorkforceSourceView>>(
            "/v1/workforce-source-observations", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Workforce");
    app.MapPortiaGet<PreviewWorkforceSourceObservation, WorkforceSourcePreview>(
            "/v1/workforce-source-observations/{observation_id}/preview", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Workforce");
    app.MapPortiaPut<ReconcileWorkforceSourceObservation>(
            "/v1/workforce-source-observations/{observation_id}/decision", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<ReconcileWorkforceSourceObservation>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Workforce");
    app.MapPortiaGet<ListWorkforceReconciliationObservations,
            Page<WorkforceReconciliationObservationView>>(
            "/v1/workforce-reconciliation-observations", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Workforce");
    app.MapPortiaPost<RecordServiceIdentity, PortiaCreateOutcome<ServiceIdentityRegistration>>(
            "/v1/service-identities",
            config => config.FromHeader(x => x.TenantId, "Tenant-Id").Created("/v1/service-identities/{ServiceIdentityId}")).WithNativeFirstResponse<RecordServiceIdentity>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Workforce");
    app.MapPortiaGet<GetServiceIdentity, ServiceIdentityView>(
            "/v1/service-identities/{service_identity_id}", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Workforce");
    app.MapPortiaPost<ChangeServiceIdentityOwner>(
            "/v1/service-identities/{service_identity_id}/owner/change", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<ChangeServiceIdentityOwner>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Workforce");
    app.MapPortiaPost<ChangeServiceIdentityPurpose>(
            "/v1/service-identities/{service_identity_id}/purpose/change", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<ChangeServiceIdentityPurpose>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Workforce");
    app.MapPortiaPost<ReviewServiceIdentity>(
            "/v1/service-identities/{service_identity_id}/review", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<ReviewServiceIdentity>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Workforce");
    app.MapPortiaPost<DisableServiceIdentity>(
            "/v1/service-identities/{service_identity_id}/disable", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<DisableServiceIdentity>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Workforce");
    app.MapPortiaPost<EnableServiceIdentity>(
            "/v1/service-identities/{service_identity_id}/enable", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<EnableServiceIdentity>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Workforce");
    app.MapPortiaPost<RetireServiceIdentity>(
            "/v1/service-identities/{service_identity_id}/retire", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<RetireServiceIdentity>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Workforce");
    app.MapPortiaGet<ListServiceIdentities, Page<ServiceIdentityView>>(
            "/v1/service-identities", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Workforce");
    app.MapPortiaPut<RecordProvider, OperationAcceptedResponse>("/v1/providers/{provider_id}",
            config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult((_, result) => NamedRecordHttp.Result(result))).WithNativeFirstResponse<RecordProvider>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Providers");
    app.MapPortiaPut<ReviseProvider, OperationAcceptedResponse>(
            "/v1/providers/revise-provider/{command_id}",
            config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult((_, result) => NamedRecordHttp.Result(result))).WithNativeFirstResponse<ReviseProvider>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Providers");
    app.MapPortiaGet<GetProvider, ProviderView>("/v1/providers/{provider_id}", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Providers");
    app.MapPortiaGet<ListProviders, Page<ProviderView>>("/v1/providers", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Providers");
    app.MapPortiaGet<GetProviderRevision, ProviderView>("/v1/provider-revision", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Providers");
    app.MapPortiaGet<ListProviderRevisions, Page<ProviderView>>("/v1/providers/{provider_id}/revisions", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Providers");
    app.MapPortiaPut<RecordAssuranceReport, AssuranceReportRegistration>(
            "/v1/providers/{provider_id}/assurance-reports/{report_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<RecordAssuranceReport>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Providers");
    app.MapPortiaPut<ReviseAssuranceReport, AssuranceReportRegistration>(
            "/v1/providers/{provider_id}/assurance-reports/revise-assurance-report/{command_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<ReviseAssuranceReport>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Providers");
    app.MapPortiaGet<ListProviderAssuranceReports, Page<AssuranceReportView>>(
            "/v1/providers/{provider_id}/assurance-reports", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Providers");
    // Personal sign-off: HTTP-only, deliberately not an MCP tool.
    app.MapPortiaPut<RecordProviderReview, OperationAcceptedResponse>(
            "/v1/providers/{provider_id}/reviews/{review_id}",
            config => config.FromHeader(x => x.TenantId, "Tenant-Id")
                .Produces<OperationAcceptedResponse>(StatusCodes.Status202Accepted, "application/json")
                .OnNativeResult((http, result) =>
                {
                    if (!result.IsSuccess)
                        return null;
                    http.Response.Headers.Location = result.Value.StatusUrl;
                    return Results.Json(result.Value, ComplianceCoreJsonContext.Default.OperationAcceptedResponse,
                        statusCode: StatusCodes.Status202Accepted);
                })).WithNativeFirstResponse<RecordProviderReview>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Providers");
    app.MapPortiaGet<ListProviderReviews, Page<ProviderReviewView>>(
            "/v1/providers/{provider_id}/reviews", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Providers");
    app.MapPortiaPut<RecordProviderCoverageGap, ProviderCoverageGapRegistration>(
            "/v1/providers/{provider_id}/coverage-gaps/{gap_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<RecordProviderCoverageGap>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Providers");
    // Coverage-restored closure is an attributed decision and remains HTTP-only.
    app.MapPortiaPut<CloseProviderCoverageGap, ProviderCoverageGapRegistration>(
            "/v1/providers/{provider_id}/coverage-gaps/close-provider-coverage-gap/{command_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<CloseProviderCoverageGap>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Providers");
    // Linking a personal R1-07 acceptance is an authenticated HTTP decision.
    app.MapPortiaPut<LinkProviderCoverageGapRiskAcceptance, OperationAcceptedResponse>(
            "/v1/providers/{provider_id}/coverage-gaps/{gap_id}/risk-acceptances/link-provider-coverage-gap-risk-acceptance/{command_id}",
            config => config.FromHeader(x => x.TenantId, "Tenant-Id")
                .Produces<OperationAcceptedResponse>(StatusCodes.Status202Accepted, "application/json")
                .OnNativeResult((http, result) =>
                {
                    if (!result.IsSuccess)
                        return null;
                    http.Response.Headers.Location = result.Value.StatusUrl;
                    return Results.Json(result.Value, ComplianceCoreJsonContext.Default.OperationAcceptedResponse,
                        statusCode: StatusCodes.Status202Accepted);
                })).WithNativeFirstResponse<LinkProviderCoverageGapRiskAcceptance>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Providers");
    app.MapPortiaGet<GetProviderCoverageGap, ProviderCoverageGapView>(
            "/v1/providers/{provider_id}/coverage-gaps/{gap_id}", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Providers");
    app.MapPortiaGet<ListProviderCoverageGaps, Page<ProviderCoverageGapView>>(
            "/v1/providers/{provider_id}/coverage-gaps", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Providers");
    app.MapPortiaGet<GetProviderAssuranceCoverage, ProviderAssuranceCoverageView>(
            "/v1/providers/{provider_id}/assurance-coverage", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Providers");
    app.MapPortiaPost<PreviewProviderChange, ProviderChangeImpactPreview>(
            "/v1/providers/{provider_id}/change-impact-previews", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<PreviewProviderChange>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithSummary("Preview provider change impact across systems, data, controls, evidence, and scope")
        .WithTags("Providers");
    app.MapPortiaPut<RecordTechnologyComponent, TechnologyComponentRegistration>(
            "/v1/technology-components/{component_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<RecordTechnologyComponent>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("TechnologyInventory");
    app.MapPortiaGet<ListTechnologyComponentBoundaryReferences,
            Page<ApplicationBoundaryReferenceView>>(
            "/v1/technology-components/{component_id}/boundary-references", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithSummary("List current draft and approved boundary references for a technology component")
        .WithTags("TechnologyInventory");
    app.MapPortiaGet<ListInformationAssetBoundaryReferences,
            Page<ApplicationBoundaryReferenceView>>(
            "/v1/information-assets/{information_asset_id}/boundary-references", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithSummary("List current draft and approved boundary references for an information asset")
        .WithTags("TechnologyInventory");
    app.MapPortiaGet<GetTechnologyComponent, TechnologyComponentView>(
            "/v1/technology-components/{component_id}", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("TechnologyInventory");
    app.MapPortiaGet<ListTechnologyComponents, Page<TechnologyComponentView>>(
            "/v1/technology-components", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("TechnologyInventory");
    app.MapPortiaGet<ListTechnologyComponentRevisions, Page<TechnologyComponentView>>(
            "/v1/technology-components/{component_id}/revisions", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("TechnologyInventory");
    app.MapPortiaPut<RecordInformationAsset, InformationAssetRegistration>(
            "/v1/information-assets/{information_asset_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<RecordInformationAsset>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("TechnologyInventory");
    app.MapPortiaGet<GetInformationAsset, InformationAssetView>(
            "/v1/information-assets/{information_asset_id}", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("TechnologyInventory");
    app.MapPortiaGet<ListInformationAssets, Page<InformationAssetView>>(
            "/v1/information-assets", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("TechnologyInventory");
    app.MapPortiaGet<ListInformationAssetRevisions, Page<InformationAssetView>>(
            "/v1/information-assets/{information_asset_id}/revisions", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("TechnologyInventory");
    app.MapPortiaPost<PreviewInformationAssetChange, InformationAssetChangePreview>(
            "/v1/information-assets/{information_asset_id}/change-previews", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<PreviewInformationAssetChange>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithSummary("Preview the data-flow impact of reclassifying or retiring an information asset")
        .WithTags("TechnologyInventory");
    app.MapPortiaPut<RecordLocation, OperationAcceptedResponse>(
            "/v1/locations/{location_id}",
            config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult((_, result) => NamedRecordHttp.Result(result))).WithNativeFirstResponse<RecordLocation>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("TechnologyInventory");
    app.MapPortiaPut<ReviseLocation, OperationAcceptedResponse>(
            "/v1/locations/revise-location/{command_id}",
            config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult((_, result) => NamedRecordHttp.Result(result))).WithNativeFirstResponse<ReviseLocation>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("TechnologyInventory");
    app.MapPortiaGet<GetLocation, LocationView>(
            "/v1/locations/{location_id}", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("TechnologyInventory");
    app.MapPortiaGet<ListLocations, Page<LocationView>>(
            "/v1/locations", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("TechnologyInventory");
    app.MapPortiaGet<ListLocationRevisions, Page<LocationView>>(
            "/v1/locations/{location_id}/revisions", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("TechnologyInventory");
    app.MapPortiaPut<RecordOperationalProcess, OperationAcceptedResponse>(
            "/v1/operational-processes/{operational_process_id}",
            config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult((_, result) => NamedRecordHttp.Result(result))).WithNativeFirstResponse<RecordOperationalProcess>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("TechnologyInventory");
    app.MapPortiaPut<ReviseOperationalProcess, OperationAcceptedResponse>(
            "/v1/operational-processes/revise-operational-process/{command_id}",
            config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult((_, result) => NamedRecordHttp.Result(result))).WithNativeFirstResponse<ReviseOperationalProcess>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("TechnologyInventory");
    app.MapPortiaGet<GetOperationalProcess, OperationalProcessView>(
            "/v1/operational-processes/{operational_process_id}", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("TechnologyInventory");
    app.MapPortiaGet<ListOperationalProcesses, Page<OperationalProcessView>>(
            "/v1/operational-processes", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("TechnologyInventory");
    app.MapPortiaGet<ListOperationalProcessRevisions, Page<OperationalProcessView>>(
            "/v1/operational-processes/{operational_process_id}/revisions", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("TechnologyInventory");
    app.MapPortiaPut<RecordDataFlow, DataFlowRegistration>(
            "/v1/data-flows/{data_flow_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<RecordDataFlow>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("TechnologyInventory");
    app.MapPortiaGet<GetDataFlow, DataFlowView>(
            "/v1/data-flows/{data_flow_id}", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("TechnologyInventory");
    app.MapPortiaGet<ListDataFlows, Page<DataFlowView>>(
            "/v1/data-flows", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("TechnologyInventory");
    app.MapPortiaGet<ListDataFlowRevisions, Page<DataFlowView>>(
            "/v1/data-flows/{data_flow_id}/revisions", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("TechnologyInventory");
    app.MapPortiaPut<DeclareApplication, ApplicationRegistration>(
            "/v1/applications/{application_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<DeclareApplication>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Applications");
    app.MapPortiaPut<ReviseApplication>(
            "/v1/applications/revise-application/{command_id}", endpoint => endpoint
                .OnBind(Program.BindApplicationRevisionAsync)
                .Accepts<ApplicationRevisionHttpBody>("application/json", isOptional: true, "*/*")
                .Parameter<Uuid>("tenant_id", PortiaHttpParameterLocation.Header, required: true)
                .Parameter<Uuid>("command_id", PortiaHttpParameterLocation.Route, required: true).OnNativeResult()).WithNativeFirstResponse<ReviseApplication>()
        .AddOpenApiOperationTransformer(Program.PreserveApplicationHttpSchemaAsync)
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Applications");
    app.MapPortiaGet<GetApplication, ApplicationView>(
            "/v1/applications/{application_id}", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Applications");
    app.MapPortiaGet<ListApplications, Page<ApplicationView>>(
            "/v1/applications", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Applications");
    app.MapPortiaPost<AddApplicationRelationship, ApplicationRelationshipChange>(
            "/v1/applications/{application_id}/relationships/{relationship_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<AddApplicationRelationship>()
        .WithDescription("expected_relationship_revision uses the actor-visible relationship event count. " +
            "Each add or remove event counts only when its target is currently readable by the actor; " +
            "events for hidden targets do not create revision gaps. Stale writes return a retryable " +
            "conflict without exposing the canonical source sequence.")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Applications");
    app.MapPortiaDelete<RemoveApplicationRelationship, ApplicationRelationshipChange>(
            "/v1/applications/{application_id}/relationships/{relationship_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<RemoveApplicationRelationship>()
        .WithDescription("expected_relationship_revision uses the actor-visible relationship event count. " +
            "Each add or remove event counts only when its target is currently readable by the actor; " +
            "events for hidden targets do not create revision gaps. Stale writes return a retryable " +
            "conflict without exposing the canonical source sequence.")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Applications");
    app.MapPortiaGet<ListApplicationRelationships, ApplicationRelationshipPage>(
            "/v1/applications/{application_id}/relationships", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithSummary("List directed relationships from an application")
        .WithDescription("relationship_revision, expected_relationship_revision, and minimum_revision " +
            "use actor-visible relationship event counts: each add or remove event counts only when " +
            "its target is currently readable by the actor. Hidden target events do not create revision " +
            "gaps. minimum_revision is a nonnegative read-your-own-writes lower bound; the endpoint " +
            "returns the current actor-visible relationship snapshot when its revision meets the " +
            "requested minimum. Otherwise it returns a retryable 409 with the requested and current " +
            "actor-visible relationship revisions. It never returns a lower snapshot or reconstructs " +
            "an exact historical revision. Continuation cursors bind tenant, source, actor, page size, " +
            "visible revision, and the filtered active-row fingerprint; hidden-only updates preserve " +
            "the cursor, while visible add/remove activity invalidates it.")
        .WithTags("Applications");
    app.MapPortiaGet<GetApplicationRevision, ApplicationRevisionView>(
            "/v1/application-revision", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Applications");
    app.MapPortiaGet<ListApplicationRevisions, Page<ApplicationRevisionView>>(
            "/v1/applications/{application_id}/revisions", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Applications");
    app.MapPortiaPost<RecordApplicationRelationship, PortiaCreateOutcome<ApplicationRelationshipRegistration>>(
            "/v1/applications/{source_application_id}/relationships",
            config => config.FromHeader(x => x.TenantId, "Tenant-Id").Created("/v1/applications/{source_application_id}/relationships/{RelationshipId}")).WithNativeFirstResponse<RecordApplicationRelationship>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithSummary("Record a directed application dependency or proposed successor")
        .WithTags("Applications");
    app.MapPortiaDelete<RemoveGovernedApplicationRelationship>(
            "/v1/applications/{source_application_id}/relationships/{relationship_type}/{target_application_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<RemoveGovernedApplicationRelationship>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithSummary("Remove a directed application relationship")
        .WithTags("Applications");
    app.MapPortiaGet<ListDirectedApplicationRelationships, Page<ApplicationRelationshipView>>(
            "/v1/applications/{application_id}/relationships/directed", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithSummary("List incoming or outgoing application relationships")
        .WithTags("Applications");
    app.MapPortiaPost<ApproveApplicationSuccessor>(
            "/v1/applications/{predecessor_application_id}/successors/{successor_application_id}/approval", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<ApproveApplicationSuccessor>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithSummary("Approve a proposed successor after a complete current impact review")
        .WithTags("Applications");
    app.MapPortiaPut<DeclareSystemInstance, SystemInstanceRegistration>(
            "/v1/applications/{application_id}/system-instances/{system_instance_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<DeclareSystemInstance>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("System instances");
    app.MapPortiaGet<GetSystemInstance, SystemInstanceView>(
            "/v1/applications/{application_id}/system-instances/{system_instance_id}", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("System instances");
    app.MapPortiaGet<ListSystemInstances, Page<SystemInstanceView>>(
            "/v1/applications/{application_id}/system-instances", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("System instances");
    app.MapPortiaGet<ListApplicationBoundaryReferences,
            Page<ApplicationBoundaryReferenceView>>(
            "/v1/applications/{application_id}/boundary-references", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithSummary("List current draft and approved boundary references for an application")
        .WithTags("Applications");
    app.MapPortiaGet<ListSystemInstanceBoundaryReferences,
            Page<ApplicationBoundaryReferenceView>>(
            "/v1/applications/{application_id}/system-instances/{system_instance_id}/boundary-references", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithSummary("List current draft and approved boundary references for a system instance")
        .WithTags("System instances");
    app.MapPortiaPost<PreviewApplicationChange, ApplicationChangePreview>(
            "/v1/applications/{application_id}/change-previews", endpoint => endpoint
                .OnBind(Program.BindApplicationPreviewAsync)
                .Accepts<ApplicationPreviewHttpBody>("application/json", isOptional: true, "*/*")
                .Parameter<Uuid>("tenant_id", PortiaHttpParameterLocation.Header, required: true)
                .Parameter<Uuid>("application_id", PortiaHttpParameterLocation.Route, required: true).OnNativeResult()).WithNativeFirstResponse<PreviewApplicationChange>()
        .AddOpenApiOperationTransformer(Program.PreserveApplicationHttpSchemaAsync)
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithSummary("Preview the known impact of a proposed application change")
        .WithTags("Applications");
    app.MapPortiaPut<RetireApplication>(
            "/v1/applications/{application_id}/retirements/{command_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<RetireApplication>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithSummary("Retire an application from an effective date, optionally merged into a successor")
        .WithTags("Applications");
    app.MapPortiaPut<RetireSystemInstance>(
            "/v1/applications/{application_id}/system-instances/{system_instance_id}/retirements/{command_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<RetireSystemInstance>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithSummary("Retire a system instance from an effective date")
        .WithTags("System instances");
    app.MapPortiaPost<DecideAccessReviewScope, AccessReviewScopeDecisionView>(
            "/v1/applications/{application_id}/system-instances/{system_instance_id}/access-review-scope-decisions", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<DecideAccessReviewScope>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithSummary("Approve an effective-dated access-review scope decision")
        .WithTags("System instances");
    app.MapPortiaGet<GetAccessReviewScope, AccessReviewScopeView>(
            "/v1/applications/{application_id}/system-instances/{system_instance_id}/access-review-scope", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithSummary("Read the effective access-review scope decision and its history")
        .WithTags("System instances");
    app.MapPortiaGet<ListAccessReviewScopes, Page<AccessReviewScopeStatusView>>(
            "/v1/applications/{application_id}/access-review-scopes", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithSummary("List the projected access-review scope status of an application's system instances")
        .WithTags("Applications");
    app.MapPortiaGet<GetArtifactRetention, ArtifactRetentionView>(
            "/v1/artifact-retention/{source_kind}/{source_id}", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Artifact retention");
    app.MapPortiaGet<ListArtifactLegalHolds, Page<ArtifactLegalHoldView>>(
            "/v1/artifact-retention/{source_kind}/{source_id}/legal-holds", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Artifact retention");
    app.MapPortiaGet<GetApplicationImportRetention, ArtifactRetentionView>(
            "/v1/application-imports/{batch_id}/retention", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Artifact retention");
    app.MapPortiaPut<RecordArtifactRetentionBasis>(
            "/v1/artifact-retention/{source_kind}/{source_id}/basis/{command_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<RecordArtifactRetentionBasis>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Artifact retention");
    app.MapPortiaPut<PlaceArtifactLegalHold>(
            "/v1/artifact-retention/{source_kind}/{source_id}/legal-holds/{hold_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<PlaceArtifactLegalHold>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Artifact retention");
    app.MapPortiaPut<ReleaseArtifactLegalHold>(
            "/v1/artifact-retention/{source_kind}/{source_id}/legal-holds/{hold_id}/releases/{command_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<ReleaseArtifactLegalHold>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Artifact retention");
    app.MapPortiaPut<StageApplicationImport, ApplicationImportRegistration>(
            "/v1/application-imports/{submission_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<StageApplicationImport>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Application imports");
    app.MapPortiaGet<GetApplicationImportProgress, ApplicationImportProgress>(
            "/v1/application-imports/{batch_id}/progress", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithSummary("Read durable whole-batch import execution progress")
        .WithTags("Application imports");
    app.MapPortiaGet<GetApplicationImportRejectedReport, ApplicationImportRejectedReport>(
            "/v1/application-imports/{batch_id}/rejected-report", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithSummary("Read bounded rejected-item findings from retained import rows")
        .WithTags("Application imports");
    app.MapPortiaPut<AcceptApplicationImport, OperationAcceptedResponse>(
            "/v1/application-imports/{batch_id}/acceptances/{command_id}",
            config => config.FromHeader(x => x.TenantId, "Tenant-Id")
                .Produces<OperationAcceptedResponse>(StatusCodes.Status202Accepted, "application/json")
                .OnNativeResult((http, result) =>
                {
                    if (!result.IsSuccess)
                        return null;
                    http.Response.Headers.Location = result.Value.StatusUrl;
                    return Results.Json(result.Value, ComplianceCoreJsonContext.Default.OperationAcceptedResponse,
                        statusCode: StatusCodes.Status202Accepted);
                })).WithNativeFirstResponse<AcceptApplicationImport>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithSummary("Personally accept a resolved whole application import batch")
        .WithTags("Application imports");
    app.MapPortiaPut<CancelApplicationImport, OperationAcceptedResponse>(
            "/v1/application-imports/{batch_id}/cancellations/{command_id}",
            config => config.FromHeader(x => x.TenantId, "Tenant-Id")
                .Produces<OperationAcceptedResponse>(StatusCodes.Status202Accepted, "application/json")
                .OnNativeResult((http, result) =>
                {
                    if (!result.IsSuccess)
                        return null;
                    http.Response.Headers.Location = result.Value.StatusUrl;
                    return Results.Json(result.Value, ComplianceCoreJsonContext.Default.OperationAcceptedResponse,
                        statusCode: StatusCodes.Status202Accepted);
                })).WithNativeFirstResponse<CancelApplicationImport>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Application imports");
    app.MapPortiaPut<CorrelateApplicationImportRow>(
            "/v1/application-imports/{batch_id}/rows/{row_id}/correlations/{command_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<CorrelateApplicationImportRow>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithSummary("Record a personal source-row correlation decision before acceptance")
        .WithTags("Application imports");
    app.MapPortiaGet<GetApplicationImport, ApplicationImportView>(
            "/v1/application-imports/{batch_id}", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Application imports");
    app.MapPortiaGet<ListApplicationImportRows, Page<ApplicationImportRowView>>(
            "/v1/application-imports/{batch_id}/rows", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Application imports");
    app.MapPortiaGet<PreviewApplicationImport, Page<ApplicationImportPreviewRow>>(
            "/v1/application-imports/{batch_id}/preview", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Application imports");
    app.MapPortiaGet<PreviewMissingApplicationImportRows, Page<ApplicationImportMissingRow>>(
            "/v1/application-imports/{batch_id}/preview/missing", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithSummary("Preview complete-source omissions as blocked retirement proposals")
        .WithTags("Application imports");
    app.MapPortiaGet<PreviewApplicationImportOmissionProposal, ApplicationImportOmissionPreview>(
            "/v1/application-imports/{batch_id}/omission-proposal/preview", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Application imports");
    app.MapPortiaPut<FreezeApplicationImportOmissionProposal>(
            "/v1/application-imports/{batch_id}/omission-proposal/{command_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<FreezeApplicationImportOmissionProposal>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithSummary("Confirm a source-bound omission proposal without approving retirement")
        .WithTags("Application imports");
    app.MapPortiaGet<GetApplicationImportOmissionProposal, ApplicationImportOmissionProposalView>(
            "/v1/application-imports/{batch_id}/omission-proposal", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Application imports");
    app.MapPortiaPut<CreateClientService, ClientServiceRegistration>(
            "/v1/client-services/{service_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<CreateClientService>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Client services");
    app.MapPortiaGet<ListProgramClientServices, Page<ClientServiceView>>(
            "/v1/program-client-services", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Client services");
    app.MapPortiaPut<ReviseClientService>(
            "/v1/client-services/{service_id}/revisions/{command_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<ReviseClientService>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Client services");
    app.MapPortiaPut<RetireClientService>(
            "/v1/client-services/{service_id}/retirements/{command_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<RetireClientService>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Client services");
    app.MapPortiaGet<GetClientService, ClientServiceView>(
            "/v1/client-services/{service_id}", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Client services");
    app.MapPortiaGet<ListClientServices, Page<ClientServiceView>>(
            "/v1/client-services", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Client services");
    app.MapPortiaGet<ListClientServiceRevisions, Page<ClientServiceRevisionView>>(
            "/v1/client-services/{service_id}/revisions", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Client services");
    app.MapPortiaGet<GetClientServiceRevision, ClientServiceRevisionView>(
            "/v1/client-service-revision", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Client services");
    app.MapPortiaPut<CreateBoundary, BoundaryRegistration>(
            "/v1/boundaries/{boundary_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<CreateBoundary>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Boundaries");
    app.MapPortiaGet<ListProgramBoundaries, Page<BoundaryView>>(
            "/v1/boundaries", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Boundaries");
    app.MapPortiaPut<ReviseBoundaryDraft>(
            "/v1/boundaries/{boundary_id}/drafts/{draft_version_id}/revisions/{command_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<ReviseBoundaryDraft>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Boundaries");
    app.MapPortiaPut<DiscardBoundaryDraft>(
            "/v1/boundaries/{boundary_id}/drafts/{draft_version_id}/discards/{command_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<DiscardBoundaryDraft>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Boundaries");
    app.MapPortiaGet<GetBoundary, BoundaryView>(
            "/v1/boundaries/{boundary_id}", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Boundaries");
    app.MapPortiaPut<AssignResponsibility>(
            "/v1/responsibilities/{command_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<AssignResponsibility>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Responsibilities");
    app.MapPortiaPut<RevokeResponsibility>(
            "/v1/responsibilities/{assignment_id}/revocations/{command_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<RevokeResponsibility>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Responsibilities");
    app.MapPortiaGet<ListResponsibilities, ResponsibilitySetView>(
            "/v1/responsibilities", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Responsibilities");
    app.MapPortiaGet<PreviewResponsibilityConflicts, ResponsibilityConflictPreview>(
            "/v1/responsibilities/conflict-preview", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithDescription("The type query value must be control_owner, evidence_contributor, assigned_reviewer, access_reviewer, corrective_action_owner, or policy_approver.")
        .WithTags("Responsibilities");
    app.MapPortiaGet<GetBoundaryVersion, BoundaryVersionView>(
            "/v1/boundaries/{boundary_id}/versions/{version_id}", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Boundaries");
    app.MapPortiaGet<ListBoundaryVersions, Page<BoundaryVersionView>>(
            "/v1/boundaries/{boundary_id}/versions", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Boundaries");
    app.MapPortiaGet<GetEffectiveBoundaryVersion, BoundaryVersionView>(
            "/v1/boundaries/{boundary_id}/effective-version", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Boundaries");
    app.MapPortiaGet<GetBoundaryDecision, BoundaryDecisionView>(
            "/v1/boundaries/{boundary_id}/decisions/{decision_id}", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Boundaries");
    app.MapPortiaGet<ListBoundaryDecisions, Page<BoundaryDecisionView>>(
            "/v1/boundaries/{boundary_id}/decisions", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Boundaries");
    app.MapPortiaGet<GetSeparationOfDutiesWaiver, SeparationOfDutiesWaiverView>(
            "/v1/separation-of-duties-waivers/{waiver_id}", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Access control");
    app.MapPortiaPost<FreezeProgramScopeSnapshot, SnapshotRegistration>(
            "/v1/scope-snapshots", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<FreezeProgramScopeSnapshot>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Snapshots");
    app.MapPortiaPost<AmendProgramScopeSnapshot, SnapshotRegistration>(
            "/v1/scope-snapshots/{snapshot_id}/amendments", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<AmendProgramScopeSnapshot>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Snapshots");
    app.MapPortiaGet<GetSnapshot, SnapshotView>(
            "/v1/scope-snapshots/{snapshot_id}", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Snapshots");
    app.MapPortiaGet<VerifyProgramScopeSnapshot, ProgramScopeSnapshotVerification>(
            "/v1/scope-snapshots/{snapshot_id}/verification", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Snapshots");
    app.MapPortiaGet<RegenerateProgramScopeSnapshotManifest,
            ProgramScopeSnapshotManifestRegeneration>(
            "/v1/scope-snapshots/{snapshot_id}/manifest-regeneration", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Snapshots");
    app.MapPortiaGet<ListProgramSnapshots, Page<SnapshotView>>(
            "/v1/scope-snapshots", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Snapshots");
    app.MapPortiaPost<FreezeWorkforceRosterSnapshot, SnapshotRegistration>(
            "/v1/workforce-roster-snapshots", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<FreezeWorkforceRosterSnapshot>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Snapshots");
    app.MapPortiaPost<AmendWorkforceRosterSnapshot, SnapshotRegistration>(
            "/v1/workforce-roster-snapshots/{snapshot_id}/amendments", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<AmendWorkforceRosterSnapshot>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Snapshots");
    app.MapPortiaGet<GetWorkforceRosterSnapshot, WorkforceRosterSnapshotView>(
            "/v1/workforce-roster-snapshots/{snapshot_id}", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Snapshots");
    app.MapPortiaGet<ListWorkforceRosterSnapshots, Page<PopulationSnapshotSummary>>(
            "/v1/workforce-roster-snapshots", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Snapshots");
    app.MapPortiaGet<RegenerateWorkforceRosterSnapshotManifest, PopulationSnapshotManifestRegeneration>(
            "/v1/workforce-roster-snapshots/{snapshot_id}/manifest-regeneration", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Snapshots");
    app.MapPortiaGet<GetWorkforceRosterSnapshotAsOf, WorkforceRosterSnapshotView>(
            "/v1/workforce-roster-snapshot-as-of", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Snapshots");
    app.MapPortiaGet<PreviewBoundaryImpact, BoundaryImpactPreview>(
            "/v1/boundaries/{boundary_id}/drafts/{draft_version_id}/impact-preview", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Boundaries");
    app.MapPortiaPut<ReviewBoundary>(
            "/v1/boundaries/{boundary_id}/drafts/{draft_version_id}/reviews/{command_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<ReviewBoundary>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Boundaries");
    app.MapPortiaPut<ApproveBoundary>(
            "/v1/boundaries/{boundary_id}/drafts/{draft_version_id}/approvals/{command_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<ApproveBoundary>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Boundaries");
    app.MapPortiaPost<RecordSeparationOfDutiesWaiver, PortiaCreateOutcome<SeparationOfDutiesWaiverView>>(
            "/v1/separation-of-duties-waivers",
            config => config.FromHeader(x => x.TenantId, "Tenant-Id").Created("/v1/separation-of-duties-waivers/{WaiverId}")).WithNativeFirstResponse<RecordSeparationOfDutiesWaiver>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Access control");
    app.MapPortiaPost<ApproveSeparationOfDutiesWaiver, SeparationOfDutiesWaiverView>(
            "/v1/separation-of-duties-waivers/{waiver_id}/approvals", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<ApproveSeparationOfDutiesWaiver>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Access control");
    app.MapPortiaPut<ProposeBoundarySuccessor, BoundaryRegistration>(
            "/v1/boundaries/{boundary_id}/successors/{command_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<ProposeBoundarySuccessor>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Boundaries");
    app.MapPortiaPut<ReserveEmail>("/v1/users/{user_id}/email-address-reservations/{command_id}", config => config.OnNativeResult()).WithNativeFirstResponse<ReserveEmail>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Email addresses");
    app.MapPortiaPut<IssueEmailChallenge>("/v1/email-challenges/{challenge_id}", config => config.OnNativeResult()).WithNativeFirstResponse<IssueEmailChallenge>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Email addresses");
    app.MapPortiaGet<GetEmailChallengeStatus, EmailChallengeStatusView>(
            "/v1/email-challenges")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Email addresses");
    app.MapPortiaPut<CompleteEmailChallenge>("/v1/email-challenges/{challenge_id}/verification", config => config.OnNativeResult()).WithNativeFirstResponse<CompleteEmailChallenge>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Email addresses");
    app.MapPortiaGet<GetEmailAddress, EmailAddressView>("/v1/user-email-addresses")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Email addresses");
    app.MapPortiaGet<ListEmailAddresses, Page<EmailAddressView>>("/v1/users/{user_id}/email-addresses")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Email addresses");
    app.MapPortiaGet<ListMyTenants, Page<TenantMembershipSummary>>("/v1/tenants/mine")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Tenants");
    app.MapPortiaPut<DisableTenantIdentityProvider>(
            "/v1/identity-providers/{provider_connection_id}/disable/{command_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<DisableTenantIdentityProvider>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Identity federation");
    app.MapPortiaPut<DefineTeam>("/v1/teams/{team_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<DefineTeam>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Teams");
    app.MapPortiaDelete<DeleteTeam>("/v1/teams/{team_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<DeleteTeam>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Teams");
    app.MapPortiaGet<GetTeam, TeamView>("/v1/teams/{team_id}", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Teams");
    app.MapPortiaGet<ListTeams, Page<TeamView>>("/v1/teams", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Teams");
    app.MapPortiaPut<AssignTeamMember, AdministratorTransitionResponse>(
            "/v1/teams/{team_id}/members/{member_id}",
            config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult((_, result) => AdministratorTransitionHttp.Result(result))).WithNativeFirstResponse<AssignTeamMember>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Teams");
    app.MapPortiaDelete<RemoveTeamMember, AdministratorTransitionResponse>(
            "/v1/teams/{team_id}/members/{member_id}",
            config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult((_, result) => AdministratorTransitionHttp.Result(result))).WithNativeFirstResponse<RemoveTeamMember>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Teams");
    app.MapPortiaGet<ListTeamMembers, Page<TeamMemberView>>("/v1/teams/{team_id}/members", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Teams");
    app.MapPortiaPut<AssignTeamRole>("/v1/teams/{team_id}/role-assignments/{role_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<AssignTeamRole>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Teams");
    app.MapPortiaDelete<RemoveTeamRole>("/v1/teams/{team_id}/role-assignments/{role_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<RemoveTeamRole>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Teams");
    app.MapPortiaPut<DefineRole>("/v1/roles/{role_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<DefineRole>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Roles");
    app.MapPortiaPut<DesignatePrimaryOrgAdmin>("/v1/primary-org-admins/{user_id}/designations/{command_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<DesignatePrimaryOrgAdmin>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Organization access");
    app.MapPortiaPut<ReleasePrimaryOrgAdmin>("/v1/primary-org-admins/{user_id}/releases/{command_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<ReleasePrimaryOrgAdmin>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Organization access");
    app.MapPortiaGet<GetPrimaryOrgAdmins, PrimaryOrgAdminsView>("/v1/primary-org-admins", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Organization access");

    app.MapPortiaPut<RenameRole>("/v1/roles/{role_id}/name", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<RenameRole>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Roles");
    app.MapPortiaDelete<DeleteRole>("/v1/roles/{role_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<DeleteRole>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Roles");
    app.MapPortiaGet<GetRole, RoleView>("/v1/roles/{role_id}", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Roles");
    app.MapPortiaGet<ListRoles, Page<RoleView>>("/v1/roles", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Roles");
    app.MapPortiaPut<AssignRolePermission>("/v1/roles/{role_id}/permissions/{permission}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<AssignRolePermission>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Roles");
    app.MapPortiaDelete<RemoveRolePermission>("/v1/roles/{role_id}/permissions/{permission}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<RemoveRolePermission>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Roles");
    app.MapPortiaGet<ListRolePermissions, Page<RolePermissionView>>(
            "/v1/roles/{role_id}/permissions", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Roles");
    app.MapPortiaGet<ListRoleTeams, Page<RoleTeamView>>("/v1/roles/{role_id}/teams", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Roles");
    app.MapPortiaGet<ListTeamRoles, Page<TeamRoleView>>("/v1/teams/{team_id}/roles", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Teams");
    app.MapPortiaPut<ConfigureTenantIdentityProvider>(
            "/v1/identity-providers/{provider_connection_id}/{command_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<ConfigureTenantIdentityProvider>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Identity federation");
    app.MapPortiaPut<TestTenantIdentityProvider>(
            "/v1/identity-providers/{provider_connection_id}/tests/{test_key}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<TestTenantIdentityProvider>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Identity federation");
    app.MapPortiaPut<RotateTenantIdentityProviderCredentials>(
            "/v1/identity-providers/{provider_connection_id}/credential-rotations/{command_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<RotateTenantIdentityProviderCredentials>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Identity federation");
    app.MapPortiaGet<GetAuditPackage, AuditPackageView>(
            "/v1/audit-packages/{package_id}", endpoint => endpoint
                .OnBind(Program.BindAuditPackageRead)
                .Parameter<Uuid>("tenant_id", PortiaHttpParameterLocation.Header, required: true)
                .Parameter<Uuid>("program_id", PortiaHttpParameterLocation.Query, required: true)
                .Parameter<Uuid>("package_id", PortiaHttpParameterLocation.Route, required: true))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Audit packages");
    app.MapPortiaGet<ListAuditPackages, Page<AuditPackageSummaryView>>(
            "/v1/audit-packages", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Audit packages");
    app.MapPortiaGet<GetTemplateApplication, TemplateApplicationView>(
        "/v1/template-applications/{template_application_id}", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Methodology templates");

    app.MapPortiaGet<GetMethodologyTemplateVersionV2, MethodologyTemplatePublishedVersionView>(
            "/v1/methodology-template-versions/{methodology_template_version_id}", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Methodology templates");
    app.MapPortiaGet<ListMethodologyTemplatesV2, Page<MethodologyTemplateSummaryView>>(
            "/v1/methodology-templates", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Methodology templates");
    app.MapPortiaPut<CreateAuditPackage>(
            "/v1/audit-packages/{package_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<CreateAuditPackage>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Audit packages");
    app.MapPortiaPut<AmendAuditPackage>(
            "/v1/audit-packages/{package_id}/amendments/{command_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<AmendAuditPackage>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Audit packages");
    app.MapPortiaPut<ValidateAuditPackage>(
            "/v1/audit-packages/{package_id}/versions/{package_version}/validations/{command_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<ValidateAuditPackage>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Audit packages");
    app.MapPortiaPut<ApproveAuditPackageDisclosure>(
            "/v1/audit-packages/{package_id}/versions/{package_version}/disclosures/{command_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<ApproveAuditPackageDisclosure>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Audit packages");
    app.MapPortiaPut<ApproveAuditPackageValidationOverride>(
            "/v1/audit-packages/{package_id}/versions/{package_version}/validation-overrides/{command_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<ApproveAuditPackageValidationOverride>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Audit packages");
    app.MapPortiaPut<RequestAuditPackageBuild, OperationAcceptedResponse>(
            "/v1/audit-packages/{package_id}/versions/{package_version}/builds/{build_key}",
            config => config.FromHeader(x => x.TenantId, "Tenant-Id")
                .Produces<OperationAcceptedResponse>(StatusCodes.Status202Accepted, "application/json")
                .OnNativeResult((http, result) =>
                {
                    if (!result.IsSuccess)
                        return null;
                    http.Response.Headers.Location = result.Value.StatusUrl;
                    return Results.Json(result.Value, ComplianceCoreJsonContext.Default.OperationAcceptedResponse,
                        statusCode: StatusCodes.Status202Accepted);
                })).WithNativeFirstResponse<RequestAuditPackageBuild>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Audit packages");
    app.MapPortiaPut<RecordAuditPackageDelivery, OperationAcceptedResponse>(
            "/v1/audit-packages/{package_id}/versions/{package_version}/deliveries/{delivery_id}",
            config => config.FromHeader(x => x.TenantId, "Tenant-Id")
                .Produces<OperationAcceptedResponse>(StatusCodes.Status202Accepted, "application/json")
                .OnNativeResult((http, result) =>
                {
                    if (!result.IsSuccess)
                        return null;
                    http.Response.Headers.Location = result.Value.StatusUrl;
                    return Results.Json(result.Value, ComplianceCoreJsonContext.Default.OperationAcceptedResponse,
                        statusCode: StatusCodes.Status202Accepted);
                })).WithNativeFirstResponse<RecordAuditPackageDelivery>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Audit packages");
    app.MapPortiaPut<CreateExaminationEngagement>(
            "/v1/examinations/{examination_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<CreateExaminationEngagement>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Examinations");
    app.MapPortiaPut<ReviseExaminationEngagement>(
            "/v1/examinations/{examination_id}/revisions", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<ReviseExaminationEngagement>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Examinations");
    app.MapPortiaGet<GetExaminationEngagement, ExaminationEngagementView>(
            "/v1/examinations/{examination_id}", endpoint => endpoint
                .OnBind(Program.BindExaminationEngagementRead)
                .Parameter<Uuid>("tenant_id", PortiaHttpParameterLocation.Header, required: true)
                .Parameter<Uuid>("program_id", PortiaHttpParameterLocation.Query, required: true)
                .Parameter<Uuid>("examination_id", PortiaHttpParameterLocation.Route, required: true))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Examinations");
    app.MapPortiaGet<GetTypeIBaseline, TypeIBaselineView>(
            "/v1/type-i-baselines/{type_i_baseline_id}", endpoint => endpoint
                .OnBind(Program.BindTypeIBaselineRead)
                .Parameter<Uuid>("tenant_id", PortiaHttpParameterLocation.Header, required: true)
                .Parameter<Uuid>("program_id", PortiaHttpParameterLocation.Query, required: true)
                .Parameter<Uuid>("type_i_baseline_id", PortiaHttpParameterLocation.Route, required: true))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Examinations");
    app.MapPortiaGet<PreviewTypeIBaseline, TypeIBaselinePreviewView>(
            "/v1/examinations/{examination_id}/type-i-baseline-preview", endpoint => endpoint
                .OnBind(Program.BindTypeIBaselinePreviewRead)
                .Parameter<Uuid>("tenant_id", PortiaHttpParameterLocation.Header, required: true)
                .Parameter<Uuid>("program_id", PortiaHttpParameterLocation.Query, required: true)
                .Parameter<Uuid>("examination_id", PortiaHttpParameterLocation.Route, required: true))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Examinations");
    app.MapPortiaPost<FreezeTypeIBaseline>(
            "/v1/examinations/{examination_id}/type-i-baselines/{baseline_id}/freeze", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<FreezeTypeIBaseline>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Examinations");
    app.MapPortiaPost<AmendTypeIBaseline>(
            "/v1/examinations/{examination_id}/type-i-baselines/{baseline_id}/amendments", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<AmendTypeIBaseline>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Examinations");
    app.MapPortiaPost<RecordManagementAssertion>(
            "/v1/examinations/{examination_id}/assertions/{assertion_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<RecordManagementAssertion>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Examinations");
    app.MapPortiaPost<RecordManagementRepresentationLetter>(
            "/v1/examinations/{examination_id}/representation-letters/{letter_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<RecordManagementRepresentationLetter>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Examinations");
    app.MapPortiaPost<RecordExaminationOutcome>(
            "/v1/examinations/{examination_id}/outcomes/{outcome_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<RecordExaminationOutcome>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Examinations");
    app.MapPortiaPost<RecordManagementExaminationReview>(
            "/v1/examinations/{examination_id}/management-reviews/{sign_off_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<RecordManagementExaminationReview>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Examinations");
    app.MapPortiaPost<ProposeTypeIIOperatingPlan>(
            "/v1/examinations/{examination_id}/type-ii-plans/{plan_id}/proposals", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<ProposeTypeIIOperatingPlan>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Examinations");
    app.MapPortiaPut<StartTypeIIObservationPeriod, OperationAcceptedResponse>(
            "/v1/type-ii-periods/{period_id}",
            config => config.FromHeader(x => x.TenantId, "Tenant-Id")
                .Produces<OperationAcceptedResponse>(StatusCodes.Status202Accepted, "application/json")
                .OnNativeResult((http, result) =>
                {
                    if (!result.IsSuccess)
                        return null;
                    http.Response.Headers.Location = result.Value.StatusUrl;
                    return Results.Json(result.Value, ComplianceCoreJsonContext.Default.OperationAcceptedResponse,
                        statusCode: StatusCodes.Status202Accepted);
                })).WithNativeFirstResponse<StartTypeIIObservationPeriod>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Observation periods");
    app.MapPortiaPut<AmendTypeIIObservationPeriod, OperationAcceptedResponse>(
            "/v1/type-ii-periods/{period_id}/amendments/{command_id}",
            config => config.FromHeader(x => x.TenantId, "Tenant-Id")
                .Produces<OperationAcceptedResponse>(StatusCodes.Status202Accepted, "application/json")
                .OnNativeResult((http, result) =>
                {
                    if (!result.IsSuccess)
                        return null;
                    http.Response.Headers.Location = result.Value.StatusUrl;
                    return Results.Json(result.Value, ComplianceCoreJsonContext.Default.OperationAcceptedResponse,
                        statusCode: StatusCodes.Status202Accepted);
                })).WithNativeFirstResponse<AmendTypeIIObservationPeriod>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Observation periods");
    app.MapPortiaPut<CancelTypeIIPeriodAmendment, UnitResult>(
            "/v1/type-ii-periods/{period_id}/amendments/{amendment_id}/cancellations/{command_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<CancelTypeIIPeriodAmendment>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Observation periods");
    app.MapPortiaPut<RecordTypeIIPeriodCloseValidation, UnitResult>(
            "/v1/observation-periods/{period_id}/close-validations/{command_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<RecordTypeIIPeriodCloseValidation>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Type II periods");
    app.MapPortiaPut<GenerateDueControlOccurrences, UnitResult>(
            "/v1/observation-periods/{period_id}/occurrence-generations/{generation_key}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<GenerateDueControlOccurrences>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Observation periods");
    app.MapPortiaGet<GetObservationPeriod, ObservationPeriodView>(
            "/v1/observation-periods/{period_id}", endpoint => endpoint
                .OnBind(Program.BindObservationPeriodRead)
                .Parameter<Uuid>("tenant_id", PortiaHttpParameterLocation.Header, required: true)
                .Parameter<Uuid>("program_id", PortiaHttpParameterLocation.Query, required: true)
                .Parameter<Uuid>("period_id", PortiaHttpParameterLocation.Route, required: true))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Observation periods");
    app.MapPortiaGet<ListObservationPeriods, Page<ObservationPeriodSummaryView>>(
            "/v1/observation-periods", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Observation periods");
    app.MapPortiaGet<PreviewObservationPeriodClose, ObservationPeriodClosePreview>(
            "/v1/observation-periods/{period_id}/close-preview", endpoint => endpoint
                .OnBind(Program.BindObservationPeriodClosePreview)
                .Parameter<Uuid>("tenant_id", PortiaHttpParameterLocation.Header, required: true)
                .Parameter<Uuid>("program_id", PortiaHttpParameterLocation.Query, required: true)
                .Parameter<Uuid>("period_id", PortiaHttpParameterLocation.Route, required: true))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Observation periods");
    app.MapPortiaGet<GetObservationPeriodClose, ObservationPeriodCloseView>(
            "/v1/observation-period-closes/{close_id}", endpoint => endpoint
                .OnBind(Program.BindObservationPeriodCloseRead)
                .Parameter<Uuid>("tenant_id", PortiaHttpParameterLocation.Header, required: true)
                .Parameter<Uuid>("program_id", PortiaHttpParameterLocation.Query, required: true)
                .Parameter<Uuid>("close_id", PortiaHttpParameterLocation.Route, required: true))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Observation periods");
    app.MapPortiaPut<AmendTypeIIPeriodClose, UnitResult>(
            "/v1/observation-periods/{period_id}/close-snapshots/{close_snapshot_id}/amendments/{command_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<AmendTypeIIPeriodClose>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Observation periods");
    app.MapPortiaPost<ApproveTypeIIPeriodClose, UnitResult>(
            "/v1/observation-periods/{period_id}/close-approvals", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<ApproveTypeIIPeriodClose>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Observation periods");
    app.MapPortiaPost<FreezeTypeIIPeriodClose, UnitResult>(
            "/v1/observation-periods/{period_id}/close-snapshots/{close_snapshot_id}/freezes", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<FreezeTypeIIPeriodClose>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Observation periods");
    app.MapPortiaPost<ProposeProgramRollover>(
            "/v1/rollovers/{rollover_id}/proposals", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<ProposeProgramRollover>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Programs");
    app.MapPortiaPost<ApproveProgramRollover>(
            "/v1/rollovers/{rollover_id}/decisions", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<ApproveProgramRollover>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Programs");
    app.MapPortiaPut<CreateAdvisorHandoff>(
            "/v1/advisor-handoffs/{handoff_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<CreateAdvisorHandoff>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Advisor collaboration");
    app.MapPortiaPut<ReviseAdvisorHandoffSelection>(
            "/v1/advisor-handoffs/{handoff_id}/selection-revisions/{command_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<ReviseAdvisorHandoffSelection>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Advisor collaboration");
    app.MapPortiaPut<MarkAdvisorHandoffReadyForReview>(
            "/v1/advisor-handoffs/{handoff_id}/ready-for-review/{command_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<MarkAdvisorHandoffReadyForReview>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Advisor collaboration");
    app.MapPortiaPut<CloseAdvisorHandoff>(
            "/v1/advisor-handoffs/{handoff_id}/closures/{command_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<CloseAdvisorHandoff>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Advisor collaboration");
    app.MapPortiaPut<RevokeAdvisorHandoff>(
            "/v1/advisor-handoffs/{handoff_id}/revocations/{command_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<RevokeAdvisorHandoff>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Advisor collaboration");
    app.MapPortiaPut<RespondToAdvisorFeedback>(
            "/v1/advisor-handoffs/{handoff_id}/feedback/{feedback_id}/responses/{command_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<RespondToAdvisorFeedback>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Advisor collaboration");
    app.MapPortiaPut<ResolveAdvisorFeedback>(
            "/v1/advisor-handoffs/{handoff_id}/feedback/{feedback_id}/resolutions/{command_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<ResolveAdvisorFeedback>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Advisor collaboration");
    app.MapPortiaPut<RecordAdvisorFeedback, OperationAcceptedResponse>(
            "/v1/advisor-handoffs/{handoff_id}/feedback/{feedback_id}",
            config => config.FromHeader(x => x.TenantId, "Tenant-Id")
                .Produces<OperationAcceptedResponse>(StatusCodes.Status202Accepted, "application/json")
                .OnNativeResult((http, result) =>
                {
                    if (!result.IsSuccess)
                        return null;
                    http.Response.Headers.Location = result.Value.StatusUrl;
                    return Results.Json(result.Value, ComplianceCoreJsonContext.Default.OperationAcceptedResponse,
                        statusCode: StatusCodes.Status202Accepted);
                })).WithNativeFirstResponse<RecordAdvisorFeedback>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Advisor collaboration");
    app.MapPortiaPut<CorrectAdvisorFeedback>(
            "/v1/advisor-handoffs/{handoff_id}/feedback/{feedback_id}/corrections/{correction_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<CorrectAdvisorFeedback>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Advisor collaboration");
    app.MapPortiaGet<GetAdvisorRequest, AdvisorRequestView>(
            "/v1/advisor-handoffs/{advisor_request_id}", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Advisor collaboration");
    app.MapPortiaGet<ListAdvisorRequests, Bdgrz.Compliance.Page<AdvisorRequestSummaryView>>(
            "/v1/advisor-handoffs", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Advisor collaboration");
    app.MapPortiaGet<GetAdvisorSharedSource, AdvisorSharedSourceView>(
            "/v1/advisor-handoffs/{handoff_id}/sources/{source_family}/{record_id}", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Advisor collaboration");
    app.MapPortiaGet<PreviewAdvisorHandoff, AdvisorHandoffPreviewView>(
            "/v1/advisor-handoffs/{handoff_id}/preview", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Advisor collaboration");
    app.MapPortiaGet<ListAdvisorFeedback, Bdgrz.Compliance.Page<AdvisorFeedbackView>>(
            "/v1/advisor-handoffs/{handoff_id}/feedback", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Advisor collaboration");
    app.MapPortiaGet<GetSignificantChange, SignificantChangeView>(
            "/v1/significant-changes/{change_id}", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Changes");
    app.MapPortiaGet<ListSignificantChanges, Page<SignificantChangeSummaryView>>(
            "/v1/significant-changes", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Changes");
    app.MapPortiaPut<RecordComplianceIncident>("/v1/incidents/{incident_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<RecordComplianceIncident>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Incidents");
    app.MapPortiaPut<ReviseComplianceIncident>("/v1/incidents/{incident_id}/revisions/{command_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<ReviseComplianceIncident>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Incidents");
    app.MapPortiaPut<AssessComplianceIncident>("/v1/incidents/{incident_id}/assessments/{command_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<AssessComplianceIncident>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Incidents");
    app.MapPortiaPut<RecordComplianceIncidentResolution>("/v1/incidents/{incident_id}/resolutions/{command_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<RecordComplianceIncidentResolution>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Incidents");
    app.MapPortiaGet<GetComplianceImpactAssessment, ComplianceIncidentImpactAssessmentView>(
            "/v1/incidents/{incident_id}/impact-assessment", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Incidents");
    app.MapPortiaGet<GetComplianceIncident, ComplianceIncidentView>(
            "/v1/incidents/{incident_id}", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Incidents");
    app.MapPortiaGet<ListComplianceIncidents, Page<ComplianceIncidentSummaryView>>(
            "/v1/incidents", endpoint => endpoint.FromHeader(x => x.TenantId, "Tenant-Id"))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Incidents");
    app.MapPortiaPut<RecordSignificantChange, UnitResult>(
            "/v1/significant-changes/{change_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<RecordSignificantChange>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Changes");
    app.MapPortiaPut<ReviseSignificantChange, UnitResult>(
            "/v1/significant-changes/{change_id}/revisions/{command_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<ReviseSignificantChange>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Changes");
    app.MapPortiaPut<AssessSignificantChange, UnitResult>(
            "/v1/significant-changes/{change_id}/assessments/{command_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<AssessSignificantChange>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Changes");
    app.MapPortiaPut<ApproveSystemDescription>(
            "/v1/system-descriptions/{description_id}/approve-system-description/{command_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<ApproveSystemDescription>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("System descriptions");
    app.MapPortiaPut<BindSystemDescriptionSources>(
            "/v1/system-descriptions/{description_id}/bind-system-description-sources/{command_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<BindSystemDescriptionSources>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("System descriptions");
    app.MapPortiaPut<BindSystemDescriptionToExamination>(
            "/v1/system-descriptions/{description_id}/bind-system-description-to-examination/{command_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<BindSystemDescriptionToExamination>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("System descriptions");
    app.MapPortiaPut<CreateSystemDescriptionDraft>(
            "/v1/system-descriptions/{description_id}/create-system-description-draft", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<CreateSystemDescriptionDraft>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("System descriptions");
    app.MapPortiaPut<ProposeSystemDescriptionSuccessor>(
            "/v1/system-descriptions/{description_id}/propose-system-description-successor/{command_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<ProposeSystemDescriptionSuccessor>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("System descriptions");
    app.MapPortiaPut<RecordSystemDescriptionNoChangeDecision>(
            "/v1/system-descriptions/{description_id}/record-system-description-no-change-decision/{command_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<RecordSystemDescriptionNoChangeDecision>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("System descriptions");
    app.MapPortiaPut<ReviewSystemDescription>(
            "/v1/system-descriptions/{description_id}/review-system-description/{command_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<ReviewSystemDescription>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("System descriptions");
    app.MapPortiaPut<ReviseSystemDescriptionDraft>(
            "/v1/system-descriptions/{description_id}/revise-system-description-draft/{command_id}", config => config.FromHeader(x => x.TenantId, "Tenant-Id").OnNativeResult()).WithNativeFirstResponse<ReviseSystemDescriptionDraft>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("System descriptions");
    app.MapPortiaGet<GetSystemDescription, SystemDescriptionView>(
            "/v1/system-descriptions/{description_id}", endpoint => endpoint.OnBind(Program.BindSystemDescriptionRead)
                .Parameter<Uuid>("tenant_id", PortiaHttpParameterLocation.Header, required: true)
                .Parameter<Uuid>("program_id", PortiaHttpParameterLocation.Query, required: true)
                .Parameter<Uuid>("description_id", PortiaHttpParameterLocation.Route, required: true))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("System descriptions");
    app.MapPortiaGet<ListSystemDescriptionVersions, Page<SystemDescriptionVersionView>>(
            "/v1/system-descriptions/{description_id}/versions", endpoint => endpoint.OnBind(Program.BindSystemDescriptionVersions)
                .Parameter<Uuid>("tenant_id", PortiaHttpParameterLocation.Header, required: true)
                .Parameter<Uuid>("program_id", PortiaHttpParameterLocation.Query, required: true)
                .Parameter<Uuid>("description_id", PortiaHttpParameterLocation.Route, required: true)
                .Parameter<int?>("limit", PortiaHttpParameterLocation.Query, required: false)
                .Parameter<string?>("cursor", PortiaHttpParameterLocation.Query, required: false))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("System descriptions");
    app.MapPortiaGet<PreviewSystemDescriptionImpact, SystemDescriptionImpactPreview>(
            "/v1/system-descriptions/{description_id}/impact-preview", endpoint => endpoint.OnBind(Program.BindSystemDescriptionImpact)
                .Parameter<Uuid>("tenant_id", PortiaHttpParameterLocation.Header, required: true)
                .Parameter<Uuid>("program_id", PortiaHttpParameterLocation.Query, required: true)
                .Parameter<Uuid>("description_id", PortiaHttpParameterLocation.Route, required: true))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("System descriptions");
    app.MapMethods(
        "/v1/{**path}",
        ["DELETE", "GET", "HEAD", "OPTIONS", "PATCH", "POST", "PUT"],
        () => Results.NotFound())
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .ExcludeFromDescription();

    await app.RunAsync();
}

static async Task RunWorkerAsync(string[] args)
{
    var builder = Host.CreateApplicationBuilder(args);
    var developerAuthentication = builder.Configuration.GetValue("BDGRZ_DEVELOPER_AUTH", false) ||
        string.Equals(
            builder.Configuration["Compliance:Authentication:Mode"],
            "Development",
            StringComparison.OrdinalIgnoreCase);
    if (developerAuthentication && !builder.Environment.IsDevelopment())
    {
        throw new InvalidOperationException(
            "BDGRZ_DEVELOPER_AUTH is only available in the Development environment.");
    }

    builder.Services
        .AddCompliance(builder.Configuration, developerAuthentication,
            requireRealEmailDelivery: !builder.Environment.IsDevelopment())
        .AddWorkers();
    builder.Services.AddHostedService<PlatformCriteriaSeedWorker>();
    builder.Services.AddHostedService<WorkDigestDispatchBackgroundService>();
    builder.Services.AddHostedService<ProcessSweep>();
    builder.Services.AddComplianceHealthChecks();
    builder.Services.AddTenantPathLogRedaction();

    await builder.Build().RunAsync();
}

/// <summary>
/// Exposes the application entry point to integration tests.
/// </summary>
public partial class Program
{
    internal static GetSystemDescription BindSystemDescriptionRead(HttpContext context) => new(
        ApplicationRouteId(context, "tenant_id"), ApplicationRouteId(context, "program_id"), ApplicationRouteId(context, "description_id"));
    internal static PreviewSystemDescriptionImpact BindSystemDescriptionImpact(HttpContext context) => new(
        ApplicationRouteId(context, "tenant_id"), ApplicationRouteId(context, "program_id"), ApplicationRouteId(context, "description_id"));
    internal static ListSystemDescriptionVersions BindSystemDescriptionVersions(HttpContext context)
    {
        var raw = PortiaHttpBinding.ReadQuery(context, "limit");
        int? limit = null;
        if (raw is not null)
            limit = int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed :
                throw new BadHttpRequestException("The description version limit is invalid.");
        return new ListSystemDescriptionVersions(ApplicationRouteId(context, "tenant_id"), ApplicationRouteId(context, "program_id"),
            ApplicationRouteId(context, "description_id"), limit, PortiaHttpBinding.ReadQuery(context, "cursor"));
    }

    internal static GetExaminationEngagement BindExaminationEngagementRead(HttpContext context) => new(
        ApplicationRouteId(context, "tenant_id"), ApplicationRouteId(context, "program_id"),
        ApplicationRouteId(context, "examination_id"));

    internal static GetTypeIBaseline BindTypeIBaselineRead(HttpContext context) => new(
        ApplicationRouteId(context, "tenant_id"), ApplicationRouteId(context, "program_id"),
        ApplicationRouteId(context, "type_i_baseline_id"));

    internal static PreviewTypeIBaseline BindTypeIBaselinePreviewRead(HttpContext context) => new(
        ApplicationRouteId(context, "tenant_id"), ApplicationRouteId(context, "program_id"),
        ApplicationRouteId(context, "examination_id"));

    internal static GetAuditorRequest BindAuditorRequestRead(HttpContext context) => new(
        ApplicationRouteId(context, "tenant_id"), ApplicationRouteId(context, "program_id"),
        ApplicationRouteId(context, "auditor_request_id"));

    internal static ListAuditorRequests BindAuditorRequestsRead(HttpContext context) => new(
        ApplicationRouteId(context, "tenant_id"), ApplicationRouteId(context, "program_id"),
        AuditorReadLimit(context), PortiaHttpBinding.ReadQuery(context, "cursor"));

    internal static ListRequestResponses BindAuditorRequestResponsesRead(HttpContext context) => new(
        ApplicationRouteId(context, "tenant_id"), ApplicationRouteId(context, "program_id"),
        ApplicationRouteId(context, "auditor_request_id"), AuditorReadLimit(context), PortiaHttpBinding.ReadQuery(context, "cursor"));

    internal static GetSampleSelection BindAuditorSampleSelectionRead(HttpContext context) => new(
        ApplicationRouteId(context, "tenant_id"), ApplicationRouteId(context, "program_id"),
        ApplicationRouteId(context, "sample_selection_id"));

    static int? AuditorReadLimit(HttpContext context)
    {
        var raw = PortiaHttpBinding.ReadQuery(context, "limit");
        if (raw is null)
            return null;
        return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value :
            throw new BadHttpRequestException("The auditor-request read limit is invalid.");
    }

    internal static GetObservationPeriod BindObservationPeriodRead(HttpContext context) => new(
        ApplicationRouteId(context, "tenant_id"), ApplicationRouteId(context, "program_id"), ApplicationRouteId(context, "period_id"));

    internal static PreviewObservationPeriodClose BindObservationPeriodClosePreview(HttpContext context) => new(
        ApplicationRouteId(context, "tenant_id"), ApplicationRouteId(context, "program_id"), ApplicationRouteId(context, "period_id"));

    internal static GetObservationPeriodClose BindObservationPeriodCloseRead(HttpContext context) => new(
        ApplicationRouteId(context, "tenant_id"), ApplicationRouteId(context, "program_id"), ApplicationRouteId(context, "close_id"));

    internal static GetAuditPackage BindAuditPackageRead(HttpContext context) => new(
        ApplicationRouteId(context, "tenant_id"), ApplicationRouteId(context, "program_id"),
        ApplicationRouteId(context, "package_id"));

    internal static Task PreserveApplicationHttpSchemaAsync(OpenApiOperation operation,
        OpenApiOperationTransformerContext context, CancellationToken ct)
    {
        // Let the binder enforce JSON media types (including missing headers) so the fallback
        // route cannot turn Portia's 415 into a 404. Documentation retains the original contract.
        var body = new OpenApiRequestBody { Required = true, Content = operation.RequestBody!.Content };
        operation.RequestBody = body;
        body.Content!.Remove("*/*");
        return Task.CompletedTask;
    }

    internal static async ValueTask<ReviseApplication> BindApplicationRevisionAsync(HttpContext context,
        CancellationToken ct)
    {
        using var document = await PortiaHttpBinding.ReadJsonBodyAsync(context, true, ct).ConfigureAwait(false);
        var body = document.RootElement;
        var options = PortiaHttpBinding.GetJsonOptions(context);
        var classification = ReadApplicationValue<ReviseApplication, string?>(context, body, options,
            7, "Classification", true, true, out var classificationSupplied);
        var systemOwner = ReadApplicationValue<ReviseApplication, Uuid?>(context, body, options,
            8, "SystemOwnerPersonId", true, true, out var systemOwnerSupplied);
        var accessOwner = ReadApplicationValue<ReviseApplication, Uuid?>(context, body, options,
            9, "AccessOwnerPersonId", true, true, out var accessOwnerSupplied);
        // The command id and tenant come from the route; the application is named by the body, so the
        // application-scoped declaration route and this revision route never share a template.
        return new ReviseApplication(ApplicationRouteId(context, "command_id"), ApplicationRouteId(context, "tenant_id"),
            Read<Uuid>(2, "ApplicationId", false, false),
            Read<long>(3, "ExpectedRevision", false, false), Read<string>(4, "Name", false, false),
            Read<string>(5, "Purpose", false, false), Read<string?>(6, "OwnerReference", true, false),
            classification, systemOwner, accessOwner, Read<bool?>(10, "IsRestricted", true, true),
            Read<string?>(11, "ApplicationKind", true, true), Read<string?>(12, "Criticality", true, true))
        {
            ClassificationIntent = new(classificationSupplied),
            SystemOwnerIntent = new(systemOwnerSupplied),
            AccessOwnerIntent = new(accessOwnerSupplied),
        };

        T Read<T>(int index, string name, bool nullable, bool hasDefault) =>
            ReadApplicationValue<ReviseApplication, T>(context, body, options, index, name, nullable, hasDefault, out _);
    }

    internal static async ValueTask<PreviewApplicationChange> BindApplicationPreviewAsync(HttpContext context,
        CancellationToken ct)
    {
        using var document = await PortiaHttpBinding.ReadJsonBodyAsync(context, true, ct).ConfigureAwait(false);
        var body = document.RootElement;
        var options = PortiaHttpBinding.GetJsonOptions(context);
        var classification = ReadApplicationValue<PreviewApplicationChange, string?>(context, body, options,
            7, "Classification", true, true, out var classificationSupplied);
        var systemOwner = ReadApplicationValue<PreviewApplicationChange, Uuid?>(context, body, options,
            8, "SystemOwnerPersonId", true, true, out var systemOwnerSupplied);
        var accessOwner = ReadApplicationValue<PreviewApplicationChange, Uuid?>(context, body, options,
            9, "AccessOwnerPersonId", true, true, out var accessOwnerSupplied);
        return new PreviewApplicationChange(ApplicationRouteId(context, "tenant_id"), ApplicationRouteId(context, "application_id"),
            Read<long>(2, "ExpectedApplicationRevision", false, false), Read<string>(3, "ChangeKind", false, false),
            Read<string?>(4, "Name", true, true), Read<string?>(5, "Purpose", true, true),
            Read<string?>(6, "OwnerReference", true, true), classification, systemOwner, accessOwner,
            Read<string?>(10, "ApplicationKind", true, true), Read<string?>(11, "Criticality", true, true))
        {
            ClassificationIntent = new(classificationSupplied),
            SystemOwnerIntent = new(systemOwnerSupplied),
            AccessOwnerIntent = new(accessOwnerSupplied),
        };

        T Read<T>(int index, string name, bool nullable, bool hasDefault) =>
            ReadApplicationValue<PreviewApplicationChange, T>(context, body, options, index, name, nullable, hasDefault, out _);
    }

    static Uuid ApplicationRouteId(HttpContext context, string name)
    {
        var raw = name switch
        {
            "tenant_id" => PortiaHttpBinding.ReadHeader(context, "Tenant-Id"),
            "program_id" => PortiaHttpBinding.ReadQuery(context, "program_id"),
            _ => context.Request.RouteValues[name]?.ToString(),
        };
        return Uuid.TryParse(raw, CultureInfo.InvariantCulture, out var value) && value != Uuid.Empty
            ? value : throw new BadHttpRequestException($"Invalid request identity '{name}'.");
    }

    static T ReadApplicationValue<TRequest, T>(HttpContext context, JsonElement body, JsonSerializerOptions options,
        int index, string name, bool nullable, bool hasDefault, out bool supplied)
    {
        var wireName = options.PropertyNamingPolicy?.ConvertName(name) ?? name;
        supplied = PortiaHttpBinding.HasBodyMember<TRequest, T>(body, options, index, name, wireName);
        if (!supplied && PortiaHttpBinding.ReadQuery(context, wireName) is { } raw)
        {
            // Preserve generated binding's body-before-query scalar fallback and parsing.
            supplied = true;
            object value;
            if (typeof(T) == typeof(string))
                value = raw;
            else if (typeof(T) == typeof(long) && long.TryParse(raw, CultureInfo.InvariantCulture, out var number))
                value = number;
            else if (typeof(T) == typeof(Uuid?) && Uuid.TryParse(raw, CultureInfo.InvariantCulture, out var id))
                value = id;
            else if (typeof(T) == typeof(Uuid) && Uuid.TryParse(raw, CultureInfo.InvariantCulture, out var required))
                value = required;
            else if (typeof(T) == typeof(bool?) && bool.TryParse(raw, out var boolean))
                value = boolean;
            else
                throw new BadHttpRequestException("Invalid scalar value.");
            return (T)value;
        }
        return PortiaHttpBinding.ReadBody<TRequest, T>(body, options, index, name, wireName,
            nullable, hasDefault, default!);
    }
}

// These types describe only the existing HTTP body. Binding still uses the request's scalar
// metadata above. Nonnullable UUID/bool documentation preserves Portia's current export shape.
readonly record struct ApplicationRevisionHttpBody
{
    [JsonRequired]
    public Uuid ApplicationId { get; init; }
    [JsonRequired]
    public long ExpectedRevision { get; init; }
    [JsonRequired]
    public string? Name { get; init; }
    [JsonRequired]
    public string? Purpose { get; init; }
    public string? OwnerReference { get; init; }
    public string? Classification { get; init; }
    public Uuid SystemOwnerPersonId { get; init; }
    public Uuid AccessOwnerPersonId { get; init; }
    public bool IsRestricted { get; init; }
    public string? ApplicationKind { get; init; }
    public string? Criticality { get; init; }
}

readonly record struct ApplicationPreviewHttpBody
{
    [JsonRequired]
    public long ExpectedApplicationRevision { get; init; }
    [JsonRequired]
    public string? ChangeKind { get; init; }
    public string? Name { get; init; }
    public string? Purpose { get; init; }
    public string? OwnerReference { get; init; }
    public string? Classification { get; init; }
    public Uuid SystemOwnerPersonId { get; init; }
    public Uuid AccessOwnerPersonId { get; init; }
    public string? ApplicationKind { get; init; }
    public string? Criticality { get; init; }
}

[PortiaJsonContext]
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web, PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
[JsonSerializable(typeof(ApplicationRevisionHttpBody))]
[JsonSerializable(typeof(ApplicationPreviewHttpBody))]
[JsonSerializable(typeof(ReviseApplicationMcpInput))]
[JsonSerializable(typeof(PreviewApplicationChangeMcpInput))]
sealed partial class ApplicationRevisionHttpJsonContext : JsonSerializerContext;
