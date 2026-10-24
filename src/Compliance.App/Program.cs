using Bdgrz.Compliance.Hosting.NativeResponses;
using Bdgrz.Compliance.Features.MethodologyTemplates;
using Bdgrz.Compliance.Features.NamedRecords;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.AuditPopulations;
using Bdgrz.Compliance.Features.AuditorRequests;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Retention;
using Bdgrz.Compliance.Hosting;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bdgrz.Compliance;
using Bdgrz.Compliance.Features.Evidence;
using Bdgrz.Compliance.Features.Responsibilities;
using Bdgrz.Compliance.Features.UserIdentities;
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
        .AddMcpTool<ReinstateMember>(tool => tool.Idempotent())
        .AddMcpTool<GetTenantMember>(tool => tool.ReadOnly())
        .AddMcpTool<ListMemberResponsibilities>(tool => tool.ReadOnly())
        .AddMcpTool<ListTeamMembers>(tool => tool.ReadOnly())
        .AddMcpTool<DefineRole>(tool => tool.Idempotent())
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
        .AddMcpTool<GrantAccess>(tool => tool.Idempotent())
        .AddMcpTool<RevokeAccessGrant>(tool => tool.Destructive())
        .AddMcpTool<ListAccessGrants>(tool => tool.ReadOnly())
        .AddMcpTool<ListMyTenants>(tool => tool.ReadOnly())
        .AddMcpTool<RegisterTenant>()
        .AddMcpTool<SuspendTenant>(tool => tool.Destructive())
        .AddMcpTool<ReactivateTenant>(tool => tool.Idempotent())
        .AddMcpTool<OffboardTenant>(tool => tool.Destructive())
        .AddMcpTool<GrantPlatformOperator>()
        .AddMcpTool<RevokePlatformOperator>(tool => tool.Destructive())
        .AddMcpTool<ListPlatformOperators>(tool => tool.ReadOnly())
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
        .AddMcpTool<RecordPerson>()
        .AddMcpTool<RevisePerson>(tool => tool.Idempotent())
        .AddMcpTool<CorrelatePersonMembership>(tool => tool.Idempotent())
        .AddMcpTool<GetPerson>(tool => tool.ReadOnly())
        .AddMcpTool<ListPeople>(tool => tool.ReadOnly())
        .AddMcpTool<RecordWorkRelationship>()
        .AddMcpTool<ReviseWorkRelationship>(tool => tool.Idempotent())
        .AddMcpTool<GetWorkRelationship>(tool => tool.ReadOnly())
        .AddMcpTool<ListWorkRelationships>(tool => tool.ReadOnly())
        .AddMcpTool<ListWorkforceObservations>(tool => tool.ReadOnly())
        .AddMcpTool<ListWorkforceReconciliationObservations>(tool => tool.ReadOnly())
        .AddMcpTool<RecordWorkforceSourceObservation>(tool => tool.Idempotent())
        .AddMcpTool<GetWorkforceSourceObservation>(tool => tool.ReadOnly())
        .AddMcpTool<ListWorkforceSourceObservations>(tool => tool.ReadOnly())
        .AddMcpTool<PreviewWorkforceSourceObservation>(tool => tool.ReadOnly())
        .AddMcpTool<RecordServiceIdentity>()
        .AddMcpTool<ReviseServiceIdentity>(tool => tool.Idempotent())
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
        .AddMcpTool<GetFinding>(tool => tool.ReadOnly())
        .AddMcpTool<ListFindings>(tool => tool.ReadOnly())
        .AddMcpTool<OpenEvidenceRequest>()
        .AddMcpTool<CancelEvidenceRequest>()
        .AddMcpTool<PrepareEvidenceRedaction>()
        .AddMcpTool<GetEvidenceRedaction>(tool => tool.ReadOnly())
        .AddMcpTool<GetEvidenceArtifactMetadata>(tool => tool.ReadOnly())
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
        .AddMcpTool<ClaimWorkItem>()
        .AddMcpTool<AssignWorkItem>()
        .AddMcpTool<DelegateWorkItem>()
        .AddMcpTool<EscalateWorkItem>()
        .AddMcpTool<ListWorkReminders>(tool => tool.ReadOnly())
        .AddMcpTool<GetWorkDigest>(tool => tool.ReadOnly())
        .AddMcpTool<GetWorkDigestPreference>(tool => tool.ReadOnly())
        .AddMcpTool<SetWorkDigestPreference>()
        .AddMcpHttp();
    builder.Services.AddScoped<CaptureEvidenceArtifactHandler>();
    builder.Services.ConfigureHttpJsonOptions(options =>
    {
        options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
        options.SerializerOptions.TypeInfoResolverChain.Insert(0, ComplianceJsonContext.Default);
    });

    if (hostMode.RunsWorkers())
    {
        portia.AddWorkers();
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
                "/api/v1/developer-user-sessions",
                config => config.OnNativeResult((http, result) => UserSessionCookie.Issue(http, result, sessionTokens))).WithNativeFirstResponse<ContinueWithDeveloperIdentity>()
            .AllowAnonymous()
            .WithTags("Users");
    }
    else
    {
        app.MapPortiaPost<ContinueWithOidcProvider, AuthenticatedUserIdentity>(
                "/api/v1/oidc-user-sessions",
                config => config.OnNativeResult((http, result) => UserSessionCookie.Issue(http, result, sessionTokens))).WithNativeFirstResponse<ContinueWithOidcProvider>()
            .RequireAuthorization(ComplianceAuthorizationPolicies.OidcContinuation)
            .WithTags("Users");
        app.MapPortiaPost<LinkOidcProviderIdentity, AuthenticatedUserIdentity>(
                "/api/v1/my/oidc-identity-links", config => config.OnNativeResult()).WithNativeFirstResponse<LinkOidcProviderIdentity>()
            .RequireAuthorization(ComplianceAuthorizationPolicies.IdentityLink)
            .WithTags("Users");
        app.MapPortiaPut<StartIdentityRecovery>("/api/v1/identity-recovery/challenges/{challenge_id}", config => config.OnNativeResult()).WithNativeFirstResponse<StartIdentityRecovery>()
            .AllowAnonymous()
            .WithTags("Users");
        app.MapPortiaPost<GetIdentityRecoveryOptions, IdentityRecoveryOptions>(
                "/api/v1/identity-recovery/options", config => config.OnNativeResult()).WithNativeFirstResponse<GetIdentityRecoveryOptions>()
            .RequireAuthorization(ComplianceAuthorizationPolicies.OidcContinuation)
            .WithTags("Users");
        app.MapPortiaPut<CompleteIdentityRecovery, OperationAcceptedResponse>(
                "/api/v1/identity-recovery/challenges/{challenge_id}/completion",
                config => config.OnNativeResult((_, result) => IdentityRecoveryHttp.Result(result))).WithNativeFirstResponse<CompleteIdentityRecovery>()
            .RequireAuthorization(ComplianceAuthorizationPolicies.OidcContinuation)
            .WithTags("Users");
    }
    app.MapPortiaPost<RegisterTenant, TenantRegistration>("/api/v1/tenants", config => config.OnNativeResult()).WithNativeFirstResponse<RegisterTenant>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Tenants");
    if (app.Environment.IsDevelopment())
    {
        // The registered content store is local; keep public uploads unavailable until production storage is qualified.
        app.MapEvidenceArtifactUpload();
    }
    app.MapPortiaPut<SuspendTenant, UnitResult>("/api/v1/tenants/{tenant_id}/suspensions/{command_id}", config => config.OnNativeResult()).WithNativeFirstResponse<SuspendTenant>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Tenants");
    app.MapPortiaDelete<ReactivateTenant, UnitResult>("/api/v1/tenants/{tenant_id}/suspensions", config => config.OnNativeResult()).WithNativeFirstResponse<ReactivateTenant>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Tenants");
    app.MapPortiaPut<OffboardTenant, UnitResult>("/api/v1/tenants/{tenant_id}/offboarding/{command_id}", config => config.OnNativeResult()).WithNativeFirstResponse<OffboardTenant>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Tenants");
    app.MapPortiaPut<GrantPlatformOperator>("/api/v1/platform/operator-grants/{command_id}", config => config.OnNativeResult()).WithNativeFirstResponse<GrantPlatformOperator>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Platform operators");
    app.MapPortiaPut<RevokePlatformOperator>("/api/v1/platform/operator-revocations/{command_id}", config => config.OnNativeResult()).WithNativeFirstResponse<RevokePlatformOperator>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Platform operators");
    app.MapPortiaGet<ListPlatformOperators, PlatformOperatorRosterView>("/api/v1/platform/operators")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Platform operators");
    app.MapPortiaGet<GetWorkDigestDispatchStatus, WorkDigestDispatchStatusView>(
            "/api/v1/platform/tenants/{tenant_id}/members/{member_id}/work-digest-dispatches/{week_of}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Platform operators");
    app.MapPortiaPost<AuthorizeWorkDigestUnknownRetry, WorkDigestDispatchStatusView>(
            "/api/v1/platform/tenants/{tenant_id}/members/{member_id}/work-digest-dispatches/{week_of}/retry-authorizations", config => config.OnNativeResult()).WithNativeFirstResponse<AuthorizeWorkDigestUnknownRetry>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Platform operators");
    app.MapPortiaPut<InviteTenantMember>("/api/v1/tenants/{tenant_id}/invitations/{invitation_id}", config => config.OnNativeResult()).WithNativeFirstResponse<InviteTenantMember>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Tenants");
    app.MapPortiaPut<InviteOrganizationMember>("/api/v1/tenants/{tenant_id}/member-invitations/{invitation_id}", config => config.OnNativeResult()).WithNativeFirstResponse<InviteOrganizationMember>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Tenants");
    app.MapPortiaPut<InviteGuestConsultant>("/api/v1/tenants/{tenant_id}/guest-invitations/{invitation_id}", config => config.OnNativeResult()).WithNativeFirstResponse<InviteGuestConsultant>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Tenants");
    app.MapPortiaGet<ListTenantInvitations, Page<TenantInvitationView>>(
            "/api/v1/tenants/{tenant_id}/member-invitations")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Tenants");
    app.MapPortiaGet<GetMemberAccess, MemberAccessView>(
            "/api/v1/tenants/{tenant_id}/members/{user_id}/access")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Access control");
    app.MapPortiaPut<SuspendMember, AdministratorTransitionResponse>(
            "/api/v1/tenants/{tenant_id}/members/{user_id}/suspensions/{command_id}",
            config => config.OnNativeResult((_, result) => AdministratorTransitionHttp.Result(result))).WithNativeFirstResponse<SuspendMember>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Access control");
    app.MapPortiaPut<ReinstateMember, AdministratorTransitionResponse>(
            "/api/v1/tenants/{tenant_id}/members/by-user/{user_id}/reinstatements/{command_id}",
            config => config.OnNativeResult((_, result) => AdministratorTransitionHttp.Result(result))).WithNativeFirstResponse<ReinstateMember>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Access control");
    app.MapPortiaPut<DeprovisionMember, AdministratorTransitionResponse>(
            "/api/v1/tenants/{tenant_id}/members/{user_id}/deprovisioning/{command_id}",
            config => config.OnNativeResult((_, result) => AdministratorTransitionHttp.Result(result))).WithNativeFirstResponse<DeprovisionMember>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Access control");
    app.MapPortiaGet<GetTenantMember, TenantMembershipView>(
            "/api/v1/tenants/{tenant_id}/members/{user_id}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Access control");
    app.MapPortiaGet<ListMemberResponsibilities, IReadOnlyList<ResponsibilityAssignmentView>>(
            "/api/v1/tenants/{tenant_id}/members/{user_id}/responsibilities")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Responsibilities");
    app.MapPortiaPut<AcceptTenantInvitation>("/api/v1/tenants/{tenant_id}/invitations/{invitation_id}/acceptance", config => config.OnNativeResult()).WithNativeFirstResponse<AcceptTenantInvitation>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Tenants");
    app.MapPortiaGet<GetTenant, TenantView>("/api/v1/tenants/{tenant_id}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Tenants");
    app.MapPortiaGet<GetTenantProvisioningStatus, TenantProvisioningStatusView>(
            "/api/v1/tenants/{tenant_id}/provisioning-status")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Tenants");
    app.MapPortiaGet<ListTenants, Page<TenantView>>("/api/v1/platform/tenants")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Tenants");
    app.MapPortiaGet<ListTenantMembers, Page<TenantMembershipView>>("/api/v1/tenants/{tenant_id}/members")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Tenants");
    app.MapPortiaPut<ChangeTenantSlug>("/api/v1/tenants/{tenant_id}/slug-changes/{command_id}", config => config.OnNativeResult()).WithNativeFirstResponse<ChangeTenantSlug>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Tenants");
    app.MapPortiaGet<ResolveMyTenantSlug, TenantSlugResolution>("/api/v1/tenant-slugs/{slug}/mine")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Tenants");
    app.MapPortiaPost<CreateProgram, ProgramRegistration>("/api/v1/tenants/{tenant_id}/programs", config => config.OnNativeResult()).WithNativeFirstResponse<CreateProgram>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Programs");
    app.MapPortiaGet<ListCriteriaCatalogEditions, IReadOnlyList<CriteriaCatalogEdition>>(
            "/api/v1/tenants/{tenant_id}/criteria-editions")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Criteria");
    app.MapPortiaGet<GetCriteriaCatalogEdition, CriteriaCatalogEdition>(
            "/api/v1/tenants/{tenant_id}/criteria-editions/{edition_id}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Criteria");
    app.MapPortiaGet<ListCriteriaCatalogEntries, Page<Criterion>>(
            "/api/v1/tenants/{tenant_id}/criteria-editions/{edition_id}/entries")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Criteria");
    app.MapPortiaGet<ExportCriteriaCatalogEntries, Page<Criterion>>(
            "/api/v1/tenants/{tenant_id}/criteria-editions/{edition_id}/entries/export")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Criteria");
    app.MapPortiaGet<GetCriteriaCatalogEntry, Criterion>(
            "/api/v1/tenants/{tenant_id}/criteria-editions/{edition_id}/entries/{identifier}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Criteria");
    app.MapPortiaPut<RegisterFirmStaff, OperationAcceptedResponse>(
            "/api/v1/platform/firm-staff/{staff_member_id}/register-firm-staff",
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
            "/api/v1/platform/firm-staff/{staff_member_id}/set-firm-staff-status/{command_id}", config => config.OnNativeResult()).WithNativeFirstResponse<SetFirmStaffStatus>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Service engagements");
    app.MapPortiaPut<RecordFirmProfessionalDutyDesignation, OperationAcceptedResponse>(
            "/api/v1/platform/firm-professional-duties/{designation_id}/assertions/{assertion_id}",
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
            "/api/v1/platform/firm-professional-duties/{designation_id}/assertion-revocations/{assertion_id}",
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
            "/api/v1/platform/firm-professional-duties")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Firm independence");
    app.MapPortiaGet<GetFirmStaffDirectory, FirmStaffDirectoryView>(
            "/api/v1/platform/firm-staff")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Service engagements");
    app.MapPortiaGet<ListAssignableFirmStaff, FirmStaffDirectoryView>(
            "/api/v1/tenants/{tenant_id}/firm-staff")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Service engagements");
    app.MapPortiaPut<CreateServiceEngagement, ServiceEngagementView>(
            "/api/v1/tenants/{tenant_id}/service-engagements/{engagement_id}/create-service-engagement", config => config.OnNativeResult()).WithNativeFirstResponse<CreateServiceEngagement>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Service engagements");
    app.MapPortiaPut<AmendServiceEngagement, ServiceEngagementView>(
            "/api/v1/tenants/{tenant_id}/service-engagements/{engagement_id}/amend-service-engagement/{command_id}", config => config.OnNativeResult()).WithNativeFirstResponse<AmendServiceEngagement>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Service engagements");
    app.MapPortiaPut<ProposeServiceEngagementStaff, ServiceEngagementView>(
            "/api/v1/tenants/{tenant_id}/service-engagements/{engagement_id}/propose-service-engagement-staff/{command_id}", config => config.OnNativeResult()).WithNativeFirstResponse<ProposeServiceEngagementStaff>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Service engagements");
    app.MapPortiaPut<WithdrawServiceEngagementStaffProposal, ServiceEngagementView>(
            "/api/v1/tenants/{tenant_id}/service-engagements/{engagement_id}/staff-proposals/{staff_member_id}/withdrawals/{command_id}", config => config.OnNativeResult()).WithNativeFirstResponse<WithdrawServiceEngagementStaffProposal>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Service engagements");
    app.MapPortiaPut<RevokeServiceEngagementActualStaff, ServiceEngagementAcceptanceView>(
            "/api/v1/tenants/{tenant_id}/service-engagements/{engagement_id}/actual-assignments/{staff_member_id}/revocations/{command_id}", config => config.OnNativeResult()).WithNativeFirstResponse<RevokeServiceEngagementActualStaff>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Service engagements");
    app.MapPortiaGet<GetServiceEngagementAcceptance, ServiceEngagementAcceptanceView>(
            "/api/v1/tenants/{tenant_id}/service-engagements/{engagement_id}/acceptance")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Service engagements");
    app.MapPortiaGet<GetServiceEngagementAcceptanceHistory, IReadOnlyList<ServiceEngagementAcceptanceView>>(
            "/api/v1/tenants/{tenant_id}/service-engagements/{engagement_id}/acceptance-history")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Service engagements");
    app.MapPortiaGet<GetEngagementManagementAcknowledgements, IReadOnlyList<EngagementManagementAcknowledgementView>>(
            "/api/v1/tenants/{tenant_id}/service-engagements/{engagement_id}/management-acknowledgements")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Service engagements");
    app.MapPortiaPut<AcknowledgeEngagementManagement, OperationAcceptedResponse>(
            "/api/v1/tenants/{tenant_id}/service-engagements/{engagement_id}/management-acknowledgements/{acknowledgement_id}",
            config => config
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
            "/api/v1/tenants/{tenant_id}/service-engagements/{engagement_id}/partner-evaluations/{evaluation_id}",
            config => config
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
            "/api/v1/tenants/{tenant_id}/service-engagements/{engagement_id}/acceptance/{command_id}",
            config => config
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
            "/api/v1/tenants/{tenant_id}/service-engagements/{engagement_id}/actual-assignments/{staff_member_id}/{command_id}",
            config => config
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
            "/api/v1/tenants/{tenant_id}/service-engagements/{engagement_id}/closure/{command_id}", config => config.OnNativeResult()).WithNativeFirstResponse<CloseServiceEngagement>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Service engagements");
    app.MapPortiaGet<GetServiceEngagement, ServiceEngagementView>(
            "/api/v1/tenants/{tenant_id}/service-engagements/{engagement_id}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Service engagements");
    app.MapPortiaGet<ListServiceEngagements, IReadOnlyList<ServiceEngagementView>>(
            "/api/v1/tenants/{tenant_id}/service-engagements")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Service engagements");
    app.MapPortiaGet<GetServiceEngagementHistory, IReadOnlyList<ServiceEngagementView>>(
            "/api/v1/tenants/{tenant_id}/service-engagements/{engagement_id}/history")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Service engagements");
    app.MapPortiaGet<GetClientIndependenceRules, IReadOnlyList<IndependenceRuleVersionView>>(
            "/api/v1/tenants/{tenant_id}/independence/rules")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Firm independence");
    app.MapPortiaGet<GetIndependenceRuleVersions, IReadOnlyList<IndependenceRuleVersionView>>(
            "/api/v1/platform/independence/rules")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Firm independence");
    app.MapPortiaPut<ReviseIndependenceRules, IndependenceRuleVersionView>(
            "/api/v1/platform/independence/rules", config => config.OnNativeResult()).WithNativeFirstResponse<ReviseIndependenceRules>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Firm independence");
    app.MapPortiaPut<RatifyIndependenceRuleVersion, IndependenceRuleRatificationView>(
            "/api/v1/platform/independence/rules/{rule_version}/ratification/{command_id}", config => config.OnNativeResult()).WithNativeFirstResponse<RatifyIndependenceRuleVersion>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Firm independence");
    app.MapPortiaPut<RecordNonattestService, NonattestServiceView>(
            "/api/v1/tenants/{tenant_id}/nonattest-services/record-nonattest-service/{service_record_id}", config => config.OnNativeResult()).WithNativeFirstResponse<RecordNonattestService>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Firm independence");
    app.MapPortiaPut<EvaluateClientIndependence, OperationAcceptedResponse>(
            "/api/v1/tenants/{tenant_id}/client-independence-evaluations/evaluate-client-independence/{evaluation_id}",
            config => config
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
            "/api/v1/tenants/{tenant_id}/independence/history")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Firm independence");
    app.MapPortiaPut<SetCriteriaTextOverlay, CriteriaTextOverlayRegistration>(
            "/api/v1/tenants/{tenant_id}/criteria-editions/{edition_id}/overlays/set-criteria-text-overlay/{command_id}", config => config.OnNativeResult()).WithNativeFirstResponse<SetCriteriaTextOverlay>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Criteria");
    app.MapPortiaPut<SelectProgramCriteriaEdition>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/criteria-edition", config => config.OnNativeResult()).WithNativeFirstResponse<SelectProgramCriteriaEdition>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Programs");
    app.MapPortiaPut<ReviseProgram>("/api/v1/tenants/{tenant_id}/programs/{program_id}", config => config.OnNativeResult()).WithNativeFirstResponse<ReviseProgram>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Programs");
    app.MapPortiaGet<GetProgram, ProgramView>("/api/v1/tenants/{tenant_id}/programs/{program_id}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Programs");
    app.MapPortiaGet<ListPrograms, Page<ProgramView>>("/api/v1/tenants/{tenant_id}/programs")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Programs");
    app.MapPortiaGet<ListProgramRevisions, Page<ProgramRevisionView>>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/revisions")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Programs");
    app.MapPortiaGet<GetProgramRevision, ProgramRevisionView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/revisions/{revision}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Programs");
    app.MapPortiaGet<GetProgramSetupWork, ProgramSetupWorkView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/setup-work")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Programs");
    app.MapPortiaPut<CreateControlDraft, ControlRegistration>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{identifier}", config => config.OnNativeResult()).WithNativeFirstResponse<CreateControlDraft>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Controls");
    app.MapPortiaPut<ReviseControlDraft>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/draft/revisions/{command_id}", config => config.OnNativeResult()).WithNativeFirstResponse<ReviseControlDraft>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Controls");
    app.MapPortiaPut<DiscardControlDraft>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/draft/discards/{command_id}", config => config.OnNativeResult()).WithNativeFirstResponse<DiscardControlDraft>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Controls");
    app.MapPortiaGet<GetControlDraft, ControlDraftView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/draft")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Controls");
    app.MapPortiaGet<ListControlDrafts, Page<ControlDraftView>>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/controls")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Controls");
    app.MapPortiaGet<ListControlDraftRevisions, Page<ControlDraftRevisionView>>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/draft/revisions")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Controls");
    app.MapPortiaGet<GetControlDraftRevision, ControlDraftRevisionView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/draft/revisions/{revision}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Controls");
    // Review and approval are human decisions and stay HTTP-only; no MCP tools are registered.
    app.MapPortiaPut<ReviewControl>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/draft/reviews/{command_id}", config => config.OnNativeResult()).WithNativeFirstResponse<ReviewControl>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Controls");
    app.MapPortiaPut<ApproveControl>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/draft/approvals/{command_id}", config => config.OnNativeResult()).WithNativeFirstResponse<ApproveControl>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Controls");
    app.MapPortiaGet<GetCurrentControlVersion, ControlVersionView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/current-version")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Controls");
    app.MapPortiaGet<GetEffectiveControlVersion, ControlVersionView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/effective-version")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Controls");
    app.MapPortiaGet<ListControlVersions, Page<ControlVersionView>>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/versions")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Controls");
    app.MapPortiaGet<GetControlVersion, ControlVersionView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/versions/{version_id}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Controls");
    app.MapPortiaGet<ListControlDecisions, Page<ControlDecisionView>>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/decisions")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Controls");
    app.MapPortiaGet<GetControlDecision, ControlDecisionView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/decisions/{decision_id}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Controls");
    // Mapping writes, including review, are human decisions and stay HTTP-only.
    app.MapPortiaPut<ProposeControlCriterionMapping, ControlCriterionMappingRegistration>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/control-mappings/{mapping_id}", config => config.OnNativeResult()).WithNativeFirstResponse<ProposeControlCriterionMapping>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Control mappings");
    app.MapPortiaPut<ReviewControlCriterionMapping>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/control-mappings/{mapping_id}/reviews/{command_id}", config => config.OnNativeResult()).WithNativeFirstResponse<ReviewControlCriterionMapping>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Control mappings");
    app.MapPortiaPut<RetireControlCriterionMapping>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/control-mappings/{mapping_id}/retirements/{command_id}", config => config.OnNativeResult()).WithNativeFirstResponse<RetireControlCriterionMapping>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Control mappings");
    app.MapPortiaGet<GetControlCriterionMapping, ControlCriterionMappingView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/control-mappings/{mapping_id}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Control mappings");
    app.MapPortiaGet<ListControlCriterionMappings, Page<ControlCriterionMappingView>>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/control-mappings")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Control mappings");
    app.MapPortiaGet<ListCriteriaCoverage, Page<CriterionCoverageView>>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/criteria-coverage")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Control mappings");
    app.MapPortiaPut<ProposeControlSuccessor, ControlSuccessorRegistration>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/successors/{command_id}", config => config.OnNativeResult()).WithNativeFirstResponse<ProposeControlSuccessor>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Controls");
    app.MapPortiaPut<ProposeControlRetirement, ControlRetirementRegistration>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/retirement-proposals/{command_id}", config => config.OnNativeResult()).WithNativeFirstResponse<ProposeControlRetirement>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Controls");
    app.MapPortiaGet<PreviewControlImpact, ControlImpactPreview>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/impact-preview")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Controls");
    // Retirement approval is a human decision and stays HTTP-only.
    app.MapPortiaPut<RetireControl>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/retirements/{command_id}", config => config.OnNativeResult()).WithNativeFirstResponse<RetireControl>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Controls");
    // Withdrawal and person-owner designation are human decisions and stay HTTP-only.
    app.MapPortiaPut<WithdrawControlProposal>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/proposal-withdrawals/{command_id}", config => config.OnNativeResult()).WithNativeFirstResponse<WithdrawControlProposal>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Controls");
    app.MapPortiaPut<DesignateControlOwnerPerson>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/draft/owner-person/{command_id}", config => config.OnNativeResult()).WithNativeFirstResponse<DesignateControlOwnerPerson>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Controls");
    app.MapPortiaGet<GetControlDraftOwnerPerson, ControlDraftOwnerPersonView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/draft/owner-person")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Controls");
    // Criterion applicability decisions are human decisions; writes stay HTTP-only.
    app.MapPortiaPut<ProposeCriterionNotApplicable, CriterionApplicabilityRegistration>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/criterion-applicability/{decision_id}", config => config.OnNativeResult()).WithNativeFirstResponse<ProposeCriterionNotApplicable>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Control mappings");
    app.MapPortiaPut<ReviewCriterionApplicability>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/criterion-applicability/{decision_id}/reviews/{command_id}", config => config.OnNativeResult()).WithNativeFirstResponse<ReviewCriterionApplicability>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Control mappings");
    app.MapPortiaPut<WithdrawCriterionNotApplicable>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/criterion-applicability/{decision_id}/withdrawals/{command_id}", config => config.OnNativeResult()).WithNativeFirstResponse<WithdrawCriterionNotApplicable>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Control mappings");
    app.MapPortiaGet<GetCriterionApplicability, CriterionApplicabilityView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/criterion-applicability/{decision_id}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Control mappings");
    app.MapPortiaGet<ListCriterionApplicability, Page<CriterionApplicabilityView>>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/criterion-applicability")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Control mappings");
    app.MapPortiaPut<CreateCommitmentDraft, CommitmentDraftRegistration>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/commitment-drafts/{draft_id}", config => config.OnNativeResult()).WithNativeFirstResponse<CreateCommitmentDraft>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Commitments");
    app.MapPortiaPut<ReviseCommitmentDraft>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/commitment-drafts/{draft_id}/draft-revisions/{command_id}", config => config.OnNativeResult()).WithNativeFirstResponse<ReviseCommitmentDraft>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Commitments");
    app.MapPortiaGet<GetCommitmentDraft, CommitmentDraftView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/commitment-drafts/{draft_id}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Commitments");
    app.MapPortiaGet<ListCommitmentDrafts, Page<CommitmentDraftView>>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/commitment-drafts")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Commitments");
    app.MapPortiaGet<ListCommitmentDraftRevisions, Page<CommitmentDraftRevisionView>>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/commitment-drafts/{draft_id}/revisions")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Commitments");
    app.MapPortiaGet<GetCommitmentDraftRevision, CommitmentDraftRevisionView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/commitment-drafts/{draft_id}/revisions/{revision}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Commitments");
    app.MapPortiaGet<PreviewCommitmentImpact, CommitmentImpactPreview>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/commitment-drafts/{draft_id}/impact-preview")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Commitments");
    app.MapPortiaPut<ReviewCommitmentDraft>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/commitment-drafts/{draft_id}/reviews/{command_id}", config => config.OnNativeResult()).WithNativeFirstResponse<ReviewCommitmentDraft>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Commitments");
    // Approval is a separate human decision after review and stays HTTP-only.
    app.MapPortiaPut<ApproveCommitmentDraft>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/commitment-drafts/{draft_id}/approvals/{command_id}", config => config.OnNativeResult()).WithNativeFirstResponse<ApproveCommitmentDraft>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Commitments");
    app.MapPortiaPut<RetireCommitment>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/commitments/{commitment_id}/retirements/{command_id}", config => config.OnNativeResult()).WithNativeFirstResponse<RetireCommitment>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Commitments");
    app.MapPortiaGet<ListCommitmentDecisions, Page<CommitmentDecisionView>>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/commitment-drafts/{draft_id}/decisions")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Commitments");
    app.MapPortiaGet<ListCommitmentVersions, Page<CommitmentVersionView>>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/commitment-drafts/{draft_id}/versions")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Commitments");
    app.MapPortiaGet<GetCommitmentVersion, CommitmentVersionView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/commitment-drafts/{draft_id}/versions/{version}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Commitments");
    app.MapPortiaGet<GetEffectiveCommitmentVersion, CommitmentVersionView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/commitment-drafts/{draft_id}/effective-version")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Commitments");
    app.MapPortiaPut<CreatePolicyDraft, PolicyRegistration>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/policies/{policy_id}", config => config.OnNativeResult()).WithNativeFirstResponse<CreatePolicyDraft>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Policies");
    app.MapPortiaGet<ListPolicies, Page<PolicySummaryView>>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/policies")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Policies");
    app.MapPortiaGet<GetPolicy, PolicyView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/policies/{policy_id}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Policies");
    app.MapPortiaPut<RevisePolicyDraft, PolicyRegistration>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/policies/{policy_id}/draft", config => config.OnNativeResult()).WithNativeFirstResponse<RevisePolicyDraft>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Policies");
    app.MapPortiaPut<DiscardPolicyDraft, UnitResult>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/policies/{policy_id}/draft/discards/{command_id}", config => config.OnNativeResult()).WithNativeFirstResponse<DiscardPolicyDraft>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Policies");
    app.MapPortiaPut<ProposePolicySuccessor, PolicyRegistration>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/policies/{policy_id}/successors/{command_id}", config => config.OnNativeResult()).WithNativeFirstResponse<ProposePolicySuccessor>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Policies");
    app.MapPortiaGet<PreviewPolicyImpact, PolicyImpactPreview>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/policies/{policy_id}/impact-preview")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Policies");
    // Reviews, approvals, periodic reviews, and retirement approvals are personal decisions and stay HTTP-only.
    app.MapPortiaPut<ReviewPolicyDraft, PolicyDecisionView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/policies/{policy_id}/reviews/{command_id}", config => config.OnNativeResult()).WithNativeFirstResponse<ReviewPolicyDraft>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Policies");
    app.MapPortiaPut<ApprovePolicy, PolicyVersionView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/policies/{policy_id}/approvals/{command_id}", config => config.OnNativeResult()).WithNativeFirstResponse<ApprovePolicy>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Policies");
    app.MapPortiaPut<ConfirmPolicyReview, PolicyDecisionView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/policies/{policy_id}/periodic-reviews/{command_id}", config => config.OnNativeResult()).WithNativeFirstResponse<ConfirmPolicyReview>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Policies");
    app.MapPortiaPut<ProposePolicyRetirement, PolicyRegistration>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/policies/{policy_id}/retirement-proposals/{command_id}", config => config.OnNativeResult()).WithNativeFirstResponse<ProposePolicyRetirement>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Policies");
    app.MapPortiaPut<ApprovePolicyRetirement, PolicyDecisionView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/policies/{policy_id}/retirements/{command_id}", config => config.OnNativeResult()).WithNativeFirstResponse<ApprovePolicyRetirement>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Policies");
    app.MapPortiaGet<ListPolicyVersions, Page<PolicyVersionView>>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/policies/{policy_id}/versions")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Policies");
    app.MapPortiaGet<GetPolicyVersion, PolicyVersionView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/policies/{policy_id}/versions/{version}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Policies");
    app.MapPortiaGet<GetEffectivePolicyVersion, PolicyVersionView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/policies/{policy_id}/effective-version")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Policies");
    app.MapPortiaGet<ListPolicyDecisions, Page<PolicyDecisionView>>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/policies/{policy_id}/decisions")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Policies");
    app.MapPortiaPut<DefineTrainingRequirement, TrainingRequirementRegistration>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/training-requirements/{requirement_id}", config => config.OnNativeResult()).WithNativeFirstResponse<DefineTrainingRequirement>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Policy distribution");
    app.MapPortiaGet<ListTrainingRequirements, Page<TrainingRequirementView>>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/training-requirements")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Policy distribution");
    app.MapPortiaGet<GetTrainingRequirement, TrainingRequirementView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/training-requirements/{requirement_id}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Policy distribution");
    app.MapPortiaPut<ReviseTrainingRequirement, TrainingRequirementRegistration>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/training-requirements/revise-training-requirement/{command_id}", config => config.OnNativeResult()).WithNativeFirstResponse<ReviseTrainingRequirement>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Policy distribution");
    app.MapPortiaPut<LaunchPolicyCampaign, CampaignRegistration>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/policy-campaigns/{campaign_id}", config => config.OnNativeResult()).WithNativeFirstResponse<LaunchPolicyCampaign>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Policy distribution");
    app.MapPortiaPut<LaunchTrainingCampaign, CampaignRegistration>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/training-campaigns/{campaign_id}", config => config.OnNativeResult()).WithNativeFirstResponse<LaunchTrainingCampaign>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Policy distribution");
    app.MapPortiaGet<ListCampaigns, Page<CampaignSummaryView>>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/campaigns")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Policy distribution");
    app.MapPortiaGet<GetCampaign, CampaignView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/campaigns/{campaign_id}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Policy distribution");
    app.MapPortiaGet<ListCampaignParticipants, Page<CampaignParticipantView>>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/campaigns/{campaign_id}/participants")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Policy distribution");
    app.MapPortiaGet<ListCampaignAmendments, Page<CampaignAmendmentView>>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/campaigns/{campaign_id}/amendments")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Policy distribution");
    app.MapPortiaPut<ReconcileCampaignAudience, CampaignReconciliationView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/campaigns/{campaign_id}/reconciliations/{command_id}", config => config.OnNativeResult()).WithNativeFirstResponse<ReconcileCampaignAudience>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Policy distribution");
    app.MapPortiaPut<RecordTrainingCompletion, OperationAcceptedResponse>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/campaigns/{campaign_id}/completions/{command_id}",
            config => config
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
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/campaigns/{campaign_id}/closure/{command_id}",
            config => config
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
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/campaigns/{campaign_id}/acknowledgements/{command_id}",
            config => config
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
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/campaigns/{campaign_id}/waivers/{command_id}",
            config => config
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
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/risks/{risk_id}", config => config.OnNativeResult()).WithNativeFirstResponse<CreateRiskDraft>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Risks");
    app.MapPortiaPut<ReviseRiskDraft>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/risks/{risk_id}/draft", config => config.OnNativeResult()).WithNativeFirstResponse<ReviseRiskDraft>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Risks");
    app.MapPortiaGet<GetRiskDraft, RiskDraftView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/risks/{risk_id}/draft")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Risks");
    app.MapPortiaGet<ListRiskDrafts, Page<RiskDraftView>>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/risks")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Risks");
    app.MapPortiaGet<ListRiskDraftRevisions, Page<RiskDraftRevisionView>>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/risks/{risk_id}/draft/revisions")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Risks");
    app.MapPortiaGet<GetRiskDraftRevision, RiskDraftRevisionView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/risks/{risk_id}/draft/revisions/{revision}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Risks");
    app.MapPortiaPost<PublishRiskMethodVersion, RiskMethodVersionView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/risk-method/versions", config => config.OnNativeResult()).WithNativeFirstResponse<PublishRiskMethodVersion>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Risks");
    app.MapPortiaGet<GetRiskMethod, RiskMethodVersionView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/risk-method")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Risks");
    app.MapPortiaGet<GetRiskMethodVersion, RiskMethodVersionView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/risk-method/versions/{version}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Risks");
    app.MapPortiaPut<RecordRiskAssessment, RiskAssessmentView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/risks/{risk_id}/assessments/{command_id}", config => config.OnNativeResult()).WithNativeFirstResponse<RecordRiskAssessment>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Risks");
    app.MapPortiaPut<ChooseRiskTreatment>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/risks/{risk_id}/treatment", config => config.OnNativeResult()).WithNativeFirstResponse<ChooseRiskTreatment>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Risks");
    app.MapPortiaPut<AcceptRisk, RiskAcceptanceView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/risks/{risk_id}/acceptances/{command_id}", config => config.OnNativeResult()).WithNativeFirstResponse<AcceptRisk>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Risks");
    app.MapPortiaGet<GetRiskEvaluation, RiskEvaluationView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/risks/{risk_id}/evaluation")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Risks");
    app.MapPortiaGet<ListRiskEvaluationHistory, Page<RiskEvaluationHistoryEntryView>>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/risks/{risk_id}/evaluation/history")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Risks");
    // Risk owner and control treatment writes are human decisions and stay HTTP-only.
    app.MapPortiaPut<AssignRiskOwner>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/risks/{risk_id}/assign-risk-owner/{command_id}", config => config.OnNativeResult()).WithNativeFirstResponse<AssignRiskOwner>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Risks");
    app.MapPortiaPut<ProposeRiskControlTreatment, RiskControlTreatmentRegistration>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/risks/{risk_id}/control-treatments/{command_id}", config => config.OnNativeResult()).WithNativeFirstResponse<ProposeRiskControlTreatment>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Risks");
    app.MapPortiaPut<ReviewRiskControlTreatment>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/risks/{risk_id}/control-treatments/{treatment_id}/reviews/{command_id}", config => config.OnNativeResult()).WithNativeFirstResponse<ReviewRiskControlTreatment>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Risks");
    app.MapPortiaPut<RetireRiskControlTreatment>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/risks/{risk_id}/control-treatments/{treatment_id}/retirements/{command_id}", config => config.OnNativeResult()).WithNativeFirstResponse<RetireRiskControlTreatment>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Risks");
    app.MapPortiaPut<AddRiskTreatmentAction, RiskTreatmentActionRegistration>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/risks/{risk_id}/treatment-actions/{action_id}", config => config.OnNativeResult()).WithNativeFirstResponse<AddRiskTreatmentAction>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Risks");
    app.MapPortiaPut<ReviseRiskTreatmentAction>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/risks/{risk_id}/treatment-actions/revise-risk-treatment-action/{command_id}", config => config.OnNativeResult()).WithNativeFirstResponse<ReviseRiskTreatmentAction>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Risks");
    app.MapPortiaPut<CancelRiskTreatmentAction>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/risks/{risk_id}/treatment-actions/{action_id}/cancellations/{command_id}", config => config.OnNativeResult()).WithNativeFirstResponse<CancelRiskTreatmentAction>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Risks");
    app.MapPortiaPut<SubmitRiskTreatmentActionCompletion,
            RiskTreatmentActionCompletionRegistration>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/risks/{risk_id}/treatment-actions/{action_id}/completions/{command_id}", config => config.OnNativeResult()).WithNativeFirstResponse<SubmitRiskTreatmentActionCompletion>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Risks");
    app.MapPortiaPut<ReviewRiskTreatmentActionCompletion, OperationAcceptedResponse>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/risks/{risk_id}/treatment-actions/{action_id}/completion-reviews/{command_id}",
            config => config
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
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/risks/{risk_id}/governance")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Risks");
    app.MapPortiaPut<RunReadinessAssessment, OperationAcceptedResponse>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/readiness/assessments/{assessment_id}",
            config => config
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
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/readiness/assessments")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Readiness");
    app.MapPortiaGet<GetReadinessAssessment, ReadinessAssessmentView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/readiness/assessments/{assessment_id}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Readiness");
    app.MapPortiaGet<ListReadinessGaps, Page<ReadinessGapView>>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/readiness/assessments/{assessment_id}/gaps")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Readiness");
    app.MapPortiaPut<DecideReadiness, OperationAcceptedResponse>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/readiness/assessments/{assessment_id}/decision/{command_id}",
            config => config
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
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/readiness/gaps/{gap_id}/plan-readiness-gap/{command_id}", config => config.OnNativeResult()).WithNativeFirstResponse<PlanReadinessGap>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Readiness");
    // The Type I entry decision is a personal sign-off and advisor feedback is attributed to
    // its author; both stay HTTP-only.
    app.MapPortiaPut<DecideTypeIEntry, OperationAcceptedResponse>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/readiness/assessments/{assessment_id}/type-i-entry-decision/{command_id}",
            config => config
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
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/readiness/type-i-entry-decisions")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Readiness");
    app.MapPortiaPut<AnnotateReadinessGap, ReadinessAnnotationView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/readiness/assessments/{assessment_id}/gaps/{gap_id}/annotations/{command_id}", config => config.OnNativeResult()).WithNativeFirstResponse<AnnotateReadinessGap>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Readiness");
    app.MapPortiaGet<ListReadinessAnnotations, Page<ReadinessAnnotationView>>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/readiness/assessments/{assessment_id}/annotations")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Readiness");
    app.MapPortiaPut<ProposeControlOperatingPlan, ControlOperatingPlanView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/operating-plan/proposals/{plan_version_id}", config => config.OnNativeResult()).WithNativeFirstResponse<ProposeControlOperatingPlan>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Control operations");
    app.MapPortiaPost<PreviewControlOperatingPlan, ControlOperatingPlanPreview>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/operating-plan/preview", config => config.OnNativeResult()).WithNativeFirstResponse<PreviewControlOperatingPlan>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Control operations");
    app.MapPortiaPut<ApproveControlOperatingPlan, ControlOperatingPlanView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/operating-plan/proposals/{plan_version_id}/approvals/{command_id}", config => config.OnNativeResult()).WithNativeFirstResponse<ApproveControlOperatingPlan>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Control operations");
    app.MapPortiaGet<GetControlOperatingPlan, ControlOperatingPlanSetView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/operating-plan")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Control operations");
    app.MapPortiaGet<ListControlOperatingBlockers, Page<ControlOperatingBlockerView>>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/operating-blockers")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Control operations");
    app.MapPortiaGet<ListMyControlWork, MyControlWorkView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/my-work")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Control operations");
    app.MapPortiaPut<OpenControlOccurrence, ControlOccurrenceView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/occurrences/{command_id}", config => config.OnNativeResult()).WithNativeFirstResponse<OpenControlOccurrence>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Control operations");
    app.MapPortiaGet<ListControlOccurrences, Page<ControlOccurrenceView>>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/occurrences")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Control operations");
    app.MapPortiaGet<GetControlOccurrence, ControlOccurrenceView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/occurrences/{occurrence_id}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Control operations");
    app.MapPortiaPut<AttestControlOccurrence, ControlOccurrenceView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/occurrences/{occurrence_id}/attestations/{command_id}", config => config.OnNativeResult()).WithNativeFirstResponse<AttestControlOccurrence>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Control operations");
    app.MapPortiaPut<CorrectControlAttestation, ControlOccurrenceView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/occurrences/{occurrence_id}/corrections/{command_id}", config => config.OnNativeResult()).WithNativeFirstResponse<CorrectControlAttestation>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Control operations");
    app.MapPortiaPut<ReviewControlOccurrence, ControlOccurrenceView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/occurrences/{occurrence_id}/reviews/{command_id}", config => config.OnNativeResult()).WithNativeFirstResponse<ReviewControlOccurrence>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Control operations");
    app.MapPortiaPut<StartControlEvaluation, ControlEvaluationView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/evaluations/{evaluation_id}", config => config.OnNativeResult()).WithNativeFirstResponse<StartControlEvaluation>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Control evaluations");
    app.MapPortiaPost<DefineControlEvaluationPlan, ControlEvaluationPlanVersionView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/evaluation-plan/versions", config => config.OnNativeResult()).WithNativeFirstResponse<DefineControlEvaluationPlan>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Control evaluations");
    app.MapPortiaGet<ListControlEvaluationPlanVersions,
            Page<ControlEvaluationPlanVersionView>>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/evaluation-plan/versions")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Control evaluations");
    app.MapPortiaGet<GetControlEvaluationPlanVersion, ControlEvaluationPlanVersionView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/evaluation-plan/versions/{plan_version_id}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Control evaluations");
    app.MapPortiaGet<ListControlEvaluations, Page<ControlEvaluationView>>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/evaluations")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Control evaluations");
    app.MapPortiaGet<GetControlEvaluation, ControlEvaluationView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/evaluations/{evaluation_id}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Control evaluations");
    app.MapPortiaPut<RecordControlEvaluationStep, ControlEvaluationView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/evaluations/{evaluation_id}/steps/{step_id}/results/{command_id}", config => config.OnNativeResult()).WithNativeFirstResponse<RecordControlEvaluationStep>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Control evaluations");
    app.MapPortiaPut<DisposeControlEvaluationDeviation, ControlEvaluationView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/evaluations/{evaluation_id}/deviations/{deviation_id}/dispositions/{command_id}", config => config.OnNativeResult()).WithNativeFirstResponse<DisposeControlEvaluationDeviation>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Control evaluations");
    app.MapPortiaPut<SubmitControlEvaluation, ControlEvaluationView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/evaluations/{evaluation_id}/submissions/{command_id}", config => config.OnNativeResult()).WithNativeFirstResponse<SubmitControlEvaluation>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Control evaluations");
    app.MapPortiaPut<ReviewControlEvaluation, ControlEvaluationView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/evaluations/{evaluation_id}/reviews/{command_id}", config => config.OnNativeResult()).WithNativeFirstResponse<ReviewControlEvaluation>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Control evaluations");
    app.MapPortiaPut<OpenEvidenceRequest, EvidenceRequestView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/evidence-requests/{evidence_request_id}", config => config.OnNativeResult()).WithNativeFirstResponse<OpenEvidenceRequest>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Evidence requests");
    app.MapPortiaGet<ListEvidenceRequests, Page<EvidenceRequestView>>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/evidence-requests")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Evidence requests");
    app.MapPortiaPost<PrepareEvidenceRedaction, EvidenceRedactionView>(
            "/api/v1/tenants/{tenant_id}/evidence-redactions/{redaction_id}/preparations", config => config.OnNativeResult()).WithNativeFirstResponse<PrepareEvidenceRedaction>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Evidence redactions");
    app.MapPortiaPost<ApproveEvidenceRedaction, EvidenceRedactionView>(
            "/api/v1/tenants/{tenant_id}/evidence-redactions/{redaction_id}/approvals", config => config.OnNativeResult()).WithNativeFirstResponse<ApproveEvidenceRedaction>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Evidence redactions");
    app.MapPortiaGet<GetEvidenceRedaction, EvidenceRedactionView>(
            "/api/v1/tenants/{tenant_id}/evidence-redactions/{redaction_id}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Evidence redactions");
    app.MapPortiaGet<GetEvidenceArtifactMetadata, EvidenceArtifactMetadataView>(
            "/api/v1/tenants/{tenant_id}/evidence-artifacts/{artifact_id}/metadata")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Evidence artifacts");
    app.MapPortiaPost<IssueArtifactDownload, EvidenceArtifactDownloadLink>(
            "/api/v1/tenants/{tenant_id}/evidence-artifacts/{artifact_id}/downloads", config => config.OnNativeResult()).WithNativeFirstResponse<IssueArtifactDownload>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Evidence artifacts");
    app.MapPortiaPost<ReleaseEvidenceArtifactQuarantine>(
            "/api/v1/tenants/{tenant_id}/evidence-artifacts/{artifact_id}/quarantine-releases", config => config.OnNativeResult()).WithNativeFirstResponse<ReleaseEvidenceArtifactQuarantine>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithSummary("Release malware-quarantined evidence after a clean rescan")
        .WithTags("Evidence artifacts");
    app.MapPortiaPut<LinkEvidenceSupport, EvidenceSupportView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/evidence-artifacts/{artifact_id}/supports/{command_id}", config => config.OnNativeResult()).WithNativeFirstResponse<LinkEvidenceSupport>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Evidence artifacts");
    app.MapPortiaGet<GetEvidenceRequest, EvidenceRequestView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/evidence-requests/{evidence_request_id}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Evidence requests");
    app.MapPortiaPut<FulfilEvidenceRequest, EvidenceRequestView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/evidence-requests/{evidence_request_id}/fulfilments/{command_id}", config => config.OnNativeResult()).WithNativeFirstResponse<FulfilEvidenceRequest>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Evidence requests");
    app.MapPortiaPut<CancelEvidenceRequest, EvidenceRequestView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/evidence-requests/{evidence_request_id}/cancellations/{command_id}", config => config.OnNativeResult()).WithNativeFirstResponse<CancelEvidenceRequest>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Evidence requests");
    app.MapPortiaPut<RaiseFinding, FindingRegistration>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/findings/{finding_id}", config => config.OnNativeResult()).WithNativeFirstResponse<RaiseFinding>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Findings");
    app.MapPortiaGet<ListFindings, Page<FindingView>>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/findings")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Findings");
    app.MapPortiaGet<GetFinding, FindingView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/findings/{finding_id}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Findings");
    app.MapPortiaPut<ReviseFinding, FindingView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/findings/{finding_id}/revisions/{command_id}", config => config.OnNativeResult()).WithNativeFirstResponse<ReviseFinding>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Findings");
    app.MapPortiaPut<AddCorrectiveAction, OperationAcceptedResponse>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/findings/{finding_id}/corrective-actions/{action_id}",
            config => config
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
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/findings/{finding_id}/corrective-actions/{action_id}/completions/{command_id}", config => config.OnNativeResult()).WithNativeFirstResponse<CompleteCorrectiveAction>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Findings");
    app.MapPortiaPut<LinkFindingAcceptance, FindingView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/findings/{finding_id}/acceptances/{command_id}", config => config.OnNativeResult()).WithNativeFirstResponse<LinkFindingAcceptance>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Findings");
    app.MapPortiaPut<CloseFinding, OperationAcceptedResponse>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/findings/{finding_id}/closures/{command_id}",
            config => config
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
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/findings/{finding_id}/reopenings/{command_id}", config => config.OnNativeResult()).WithNativeFirstResponse<ReopenFinding>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Findings");
    app.MapPortiaPut<RecordAuditorRequest>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/auditor-requests/{request_id}/record-auditor-request", config => config.OnNativeResult()).WithNativeFirstResponse<RecordAuditorRequest>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Auditor requests");
    app.MapPortiaPut<ReviseAuditorRequest>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/auditor-requests/{request_id}/revise-auditor-request/{command_id}", config => config.OnNativeResult()).WithNativeFirstResponse<ReviseAuditorRequest>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Auditor requests");
    app.MapPortiaPut<AssignAuditorRequest>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/auditor-requests/{request_id}/assign-auditor-request/{command_id}", config => config.OnNativeResult()).WithNativeFirstResponse<AssignAuditorRequest>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Auditor requests");
    app.MapPortiaPut<ReopenAuditorRequest>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/auditor-requests/{request_id}/reopen-auditor-request/{command_id}", config => config.OnNativeResult()).WithNativeFirstResponse<ReopenAuditorRequest>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Auditor requests");
    app.MapPortiaPut<ReviseAuditResponseDraft>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/auditor-requests/{request_id}/responses/{response_id}/revise-audit-response-draft/{command_id}", config => config.OnNativeResult()).WithNativeFirstResponse<ReviseAuditResponseDraft>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Auditor requests");
    app.MapPortiaPut<SubmitAuditResponse>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/auditor-requests/{request_id}/responses/{response_id}/submit-audit-response/{command_id}", config => config.OnNativeResult()).WithNativeFirstResponse<SubmitAuditResponse>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Auditor requests");
    app.MapPortiaPut<ReviewAuditResponse>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/auditor-requests/{request_id}/responses/{response_id}/review-audit-response/{command_id}", config => config.OnNativeResult()).WithNativeFirstResponse<ReviewAuditResponse>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Auditor requests");
    app.MapPortiaPut<DeliverAuditResponse>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/auditor-requests/{request_id}/responses/{response_id}/deliver-audit-response/{command_id}", config => config.OnNativeResult()).WithNativeFirstResponse<DeliverAuditResponse>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Auditor requests");
    app.MapPortiaPut<RecordAuditorResponseDisposition>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/auditor-requests/{request_id}/responses/{response_id}/record-auditor-response-disposition/{command_id}", config => config.OnNativeResult()).WithNativeFirstResponse<RecordAuditorResponseDisposition>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Auditor requests");
    app.MapPortiaPut<ResolveAuditorRequest, OperationAcceptedResponse>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/auditor-requests/{request_id}/resolve-auditor-request/{command_id}",
            config => config
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
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/auditor-requests/{request_id}/create-audit-response-draft/{response_id}",
            config => config
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
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/auditor-requests/{request_id}/record-auditor-request-comment/{comment_id}",
            config => config
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
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/auditor-requests/{auditor_request_id}/sample-selections/record-sample-selection/{selection_id}",
            config => config
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
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/auditor-requests/{auditor_request_id}/sample-selections/amend-sample-selection/{selection_id}",
            config => config
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
            "/api/v1/tenants/{tenant_id}/access-populations/{population_id}", config => config.OnNativeResult()).WithNativeFirstResponse<OpenAccessPopulation>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("AccessReviews");
    app.MapPortiaPut<RecordAccessPopulationFacts, AccessPopulationRegistration>(
            "/api/v1/tenants/{tenant_id}/access-populations/record-access-population-facts/{command_id}", config => config.OnNativeResult()).WithNativeFirstResponse<RecordAccessPopulationFacts>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("AccessReviews");
    app.MapPortiaGet<PreviewAccessPopulation, AccessPopulationPreview>(
            "/api/v1/tenants/{tenant_id}/access-populations/{population_id}/preview")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("AccessReviews");
    app.MapPortiaPut<AcceptAccessPopulation, AccessPopulationAcceptance>(
            "/api/v1/tenants/{tenant_id}/access-populations/{population_id}/acceptance/{command_id}", config => config.OnNativeResult()).WithNativeFirstResponse<AcceptAccessPopulation>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("AccessReviews");
    app.MapPortiaGet<GetAccessPopulation, AccessPopulationView>(
            "/api/v1/tenants/{tenant_id}/access-populations/{population_id}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("AccessReviews");
    app.MapPortiaGet<ListAccessPopulations, Page<AccessPopulationSummaryView>>(
            "/api/v1/tenants/{tenant_id}/system-instances/{system_instance_id}/access-populations")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("AccessReviews");
    app.MapPortiaPut<ExemptMissingAccessPopulation, AccessPopulationExceptionView>(
            "/api/v1/tenants/{tenant_id}/applications/{application_id}/system-instances/{system_instance_id}/access-population-exceptions/{exception_id}", config => config.OnNativeResult()).WithNativeFirstResponse<ExemptMissingAccessPopulation>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("AccessReviews");
    app.MapPortiaGet<GetAccessReviewCoverage, AccessReviewCoverageView>(
            "/api/v1/tenants/{tenant_id}/applications/{application_id}/access-review-coverage")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("AccessReviews");
    app.MapPortiaPut<ClassifyAccessPrincipal, AccessPrincipalClassificationView>(
            "/api/v1/tenants/{tenant_id}/access-populations/{population_id}/classifications/{command_id}", config => config.OnNativeResult()).WithNativeFirstResponse<ClassifyAccessPrincipal>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("AccessReviews");
    app.MapPortiaGet<ListAccessPrincipals, Page<AccessPrincipalView>>(
            "/api/v1/tenants/{tenant_id}/access-populations/{population_id}/principals")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("AccessReviews");
    app.MapPortiaGet<GetAccessVariance, AccessVarianceView>(
            "/api/v1/tenants/{tenant_id}/access-populations/{population_id}/variance")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("AccessReviews");
    app.MapPortiaPut<ProposeAccessExpectation, AccessExpectationView>(
            "/api/v1/tenants/{tenant_id}/applications/{application_id}/system-instances/{system_instance_id}/access-expectations/{expectation_id}", config => config.OnNativeResult()).WithNativeFirstResponse<ProposeAccessExpectation>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("AccessReviews");
    app.MapPortiaPut<ApproveAccessExpectation, AccessExpectationView>(
            "/api/v1/tenants/{tenant_id}/system-instances/{system_instance_id}/access-expectations/{expectation_id}/approval/{command_id}", config => config.OnNativeResult()).WithNativeFirstResponse<ApproveAccessExpectation>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("AccessReviews");
    app.MapPortiaPut<ExemptAccessExpectation, AccessExpectationExceptionView>(
            "/api/v1/tenants/{tenant_id}/system-instances/{system_instance_id}/access-expectations/{expectation_id}/exceptions/{command_id}", config => config.OnNativeResult()).WithNativeFirstResponse<ExemptAccessExpectation>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("AccessReviews");
    app.MapPortiaGet<ListAccessExpectations, AccessExpectationsView>(
            "/api/v1/tenants/{tenant_id}/system-instances/{system_instance_id}/access-expectations")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("AccessReviews");
    app.MapPortiaPut<LaunchAccessReviewCampaign, AccessReviewCampaignRegistration>(
            "/api/v1/tenants/{tenant_id}/access-review-campaigns/{campaign_id}", config => config.OnNativeResult()).WithNativeFirstResponse<LaunchAccessReviewCampaign>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("AccessReviews");
    app.MapPortiaGet<ListAccessReviewCampaigns, Page<AccessReviewCampaignSummaryView>>(
            "/api/v1/tenants/{tenant_id}/access-review-campaigns")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("AccessReviews");
    app.MapPortiaGet<GetAccessReviewCampaign, AccessReviewCampaignView>(
            "/api/v1/tenants/{tenant_id}/access-review-campaigns/{campaign_id}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("AccessReviews");
    app.MapPortiaPut<RecordAccessDecision, AccessDecisionView>(
            "/api/v1/tenants/{tenant_id}/access-review-campaigns/{campaign_id}/items/{item_id}/decisions/{command_id}", config => config.OnNativeResult()).WithNativeFirstResponse<RecordAccessDecision>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("AccessReviews");
    app.MapPortiaPost<PreviewBulkAccessDecision, BulkAccessDecisionPreview>(
            "/api/v1/tenants/{tenant_id}/access-review-campaigns/{campaign_id}/bulk-decision-previews", config => config.OnNativeResult()).WithNativeFirstResponse<PreviewBulkAccessDecision>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("AccessReviews");
    app.MapPortiaPut<RecordBulkAccessDecision, OperationAcceptedResponse>(
            "/api/v1/tenants/{tenant_id}/access-review-campaigns/{campaign_id}/bulk-decisions/{decision_batch_id}",
            config => config
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
            "/api/v1/tenants/{tenant_id}/access-review-campaigns/{campaign_id}/items/{item_id}/remediation-changes/{command_id}", config => config.OnNativeResult()).WithNativeFirstResponse<RecordAccessRemediationChange>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("AccessReviews");
    app.MapPortiaPut<ReassignAccessReviewResponsibility, AccessReviewResponsibilityReassignmentView>(
            "/api/v1/tenants/{tenant_id}/access-review-campaigns/{campaign_id}/items/{item_id}/responsibility-reassignments/{command_id}", config => config.OnNativeResult()).WithNativeFirstResponse<ReassignAccessReviewResponsibility>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("AccessReviews");
    app.MapPortiaPut<VerifyAccessRemediation, AccessRemediationVerificationView>(
            "/api/v1/tenants/{tenant_id}/access-review-campaigns/{campaign_id}/items/{item_id}/remediation-verifications/{command_id}", config => config.OnNativeResult()).WithNativeFirstResponse<VerifyAccessRemediation>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("AccessReviews");
    app.MapPortiaPut<ExemptAccessRemediation, AccessRemediationExceptionView>(
            "/api/v1/tenants/{tenant_id}/access-review-campaigns/{campaign_id}/items/{item_id}/remediation-exceptions/{command_id}", config => config.OnNativeResult()).WithNativeFirstResponse<ExemptAccessRemediation>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("AccessReviews");
    app.MapPortiaPut<CompleteAccessReviewCampaign, OperationAcceptedResponse>(
            "/api/v1/tenants/{tenant_id}/access-review-campaigns/{campaign_id}/completion/{command_id}",
            config => config
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
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/work")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Work");
    app.MapPortiaGet<ListWorkReminders, IReadOnlyList<WorkReminderView>>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/work/reminders")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Work");
    app.MapPortiaGet<GetWorkDigest, WorkDigestView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/work/digest")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Work");
    app.MapPortiaGet<GetWorkItem, WorkItemDetailView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/work/{work_item_id}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Work");
    app.MapPortiaPost<ClaimWorkItem, WorkItemDetailView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/work/{work_item_id}/claims", config => config.OnNativeResult()).WithNativeFirstResponse<ClaimWorkItem>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Work");
    app.MapPortiaPost<AssignWorkItem, WorkItemDetailView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/work/{work_item_id}/assignments", config => config.OnNativeResult()).WithNativeFirstResponse<AssignWorkItem>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Work");
    app.MapPortiaPost<DelegateWorkItem, WorkItemDetailView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/work/{work_item_id}/delegations", config => config.OnNativeResult()).WithNativeFirstResponse<DelegateWorkItem>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Work");
    app.MapPortiaPost<EscalateWorkItem, WorkItemDetailView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/work/{work_item_id}/escalations", config => config.OnNativeResult()).WithNativeFirstResponse<EscalateWorkItem>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Work");
    app.MapPortiaGet<GetWorkDigestPreference, WorkDigestPreferenceView>(
            "/api/v1/tenants/{tenant_id}/work-digest-preference")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Work");
    app.MapPortiaPut<SetWorkDigestPreference, WorkDigestPreferenceView>(
            "/api/v1/tenants/{tenant_id}/work-digest-preference", config => config.OnNativeResult()).WithNativeFirstResponse<SetWorkDigestPreference>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Work");
    app.MapPortiaPost<RecordPerson, PersonRegistration>(
            "/api/v1/tenants/{tenant_id}/people", config => config.OnNativeResult()).WithNativeFirstResponse<RecordPerson>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Workforce");
    app.MapPortiaPut<RevisePerson>(
            "/api/v1/tenants/{tenant_id}/people/{person_id}", config => config.OnNativeResult()).WithNativeFirstResponse<RevisePerson>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Workforce");
    app.MapPortiaPut<CorrelatePersonMembership>(
            "/api/v1/tenants/{tenant_id}/people/{person_id}/membership-correlation", config => config.OnNativeResult()).WithNativeFirstResponse<CorrelatePersonMembership>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Workforce");
    app.MapPortiaGet<GetPerson, PersonView>(
            "/api/v1/tenants/{tenant_id}/people/{person_id}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Workforce");
    app.MapPortiaGet<ListPeople, Page<PersonView>>(
            "/api/v1/tenants/{tenant_id}/people")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Workforce");
    app.MapPortiaPost<RecordWorkRelationship, WorkRelationshipRegistration>(
            "/api/v1/tenants/{tenant_id}/work-relationships", config => config.OnNativeResult()).WithNativeFirstResponse<RecordWorkRelationship>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Workforce");
    app.MapPortiaPut<ReviseWorkRelationship>(
            "/api/v1/tenants/{tenant_id}/work-relationships/{relationship_id}", config => config.OnNativeResult()).WithNativeFirstResponse<ReviseWorkRelationship>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Workforce");
    app.MapPortiaGet<GetWorkRelationship, WorkRelationshipView>(
            "/api/v1/tenants/{tenant_id}/work-relationships/{relationship_id}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Workforce");
    app.MapPortiaGet<ListWorkRelationships, Page<WorkRelationshipView>>(
            "/api/v1/tenants/{tenant_id}/work-relationships")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Workforce");
    app.MapPortiaGet<ListWorkforceObservations, Page<WorkforceObservationView>>(
            "/api/v1/tenants/{tenant_id}/workforce-observations")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Workforce");
    app.MapPortiaPut<ResolveWorkforceObservation>(
            "/api/v1/tenants/{tenant_id}/workforce-observations/{observation_id}/resolution", config => config.OnNativeResult()).WithNativeFirstResponse<ResolveWorkforceObservation>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Workforce");
    app.MapPortiaPost<RecordWorkforceSourceObservation, WorkforceSourceRegistration>(
            "/api/v1/tenants/{tenant_id}/workforce-source-observations", config => config.OnNativeResult()).WithNativeFirstResponse<RecordWorkforceSourceObservation>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Workforce");
    app.MapPortiaGet<GetWorkforceSourceObservation, WorkforceSourceView>(
            "/api/v1/tenants/{tenant_id}/workforce-source-observations/{observation_id}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Workforce");
    app.MapPortiaGet<ListWorkforceSourceObservations, Page<WorkforceSourceView>>(
            "/api/v1/tenants/{tenant_id}/workforce-source-observations")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Workforce");
    app.MapPortiaGet<PreviewWorkforceSourceObservation, WorkforceSourcePreview>(
            "/api/v1/tenants/{tenant_id}/workforce-source-observations/{observation_id}/preview")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Workforce");
    app.MapPortiaPut<ReconcileWorkforceSourceObservation>(
            "/api/v1/tenants/{tenant_id}/workforce-source-observations/{observation_id}/decision", config => config.OnNativeResult()).WithNativeFirstResponse<ReconcileWorkforceSourceObservation>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Workforce");
    app.MapPortiaGet<ListWorkforceReconciliationObservations,
            Page<WorkforceReconciliationObservationView>>(
            "/api/v1/tenants/{tenant_id}/workforce-reconciliation-observations")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Workforce");
    app.MapPortiaPost<RecordServiceIdentity, ServiceIdentityRegistration>(
            "/api/v1/tenants/{tenant_id}/service-identities", config => config.OnNativeResult()).WithNativeFirstResponse<RecordServiceIdentity>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Workforce");
    app.MapPortiaPut<ReviseServiceIdentity>(
            "/api/v1/tenants/{tenant_id}/service-identities/{service_identity_id}", config => config.OnNativeResult()).WithNativeFirstResponse<ReviseServiceIdentity>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Workforce");
    app.MapPortiaGet<GetServiceIdentity, ServiceIdentityView>(
            "/api/v1/tenants/{tenant_id}/service-identities/{service_identity_id}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Workforce");
    app.MapPortiaGet<ListServiceIdentities, Page<ServiceIdentityView>>(
            "/api/v1/tenants/{tenant_id}/service-identities")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Workforce");
    app.MapPortiaPut<RecordProvider, OperationAcceptedResponse>("/api/v1/tenants/{tenant_id}/providers/{provider_id}",
            config => config.OnNativeResult((_, result) => NamedRecordHttp.Result(result))).WithNativeFirstResponse<RecordProvider>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Providers");
    app.MapPortiaPut<ReviseProvider, OperationAcceptedResponse>(
            "/api/v1/tenants/{tenant_id}/providers/revise-provider/{command_id}",
            config => config.OnNativeResult((_, result) => NamedRecordHttp.Result(result))).WithNativeFirstResponse<ReviseProvider>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Providers");
    app.MapPortiaGet<GetProvider, ProviderView>("/api/v1/tenants/{tenant_id}/providers/{provider_id}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Providers");
    app.MapPortiaGet<ListProviders, Page<ProviderView>>("/api/v1/tenants/{tenant_id}/providers")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Providers");
    app.MapPortiaGet<GetProviderRevision, ProviderView>("/api/v1/tenants/{tenant_id}/providers/{provider_id}/revisions/{revision}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Providers");
    app.MapPortiaGet<ListProviderRevisions, Page<ProviderView>>("/api/v1/tenants/{tenant_id}/providers/{provider_id}/revisions")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Providers");
    app.MapPortiaPut<RecordAssuranceReport, AssuranceReportRegistration>(
            "/api/v1/tenants/{tenant_id}/providers/{provider_id}/assurance-reports/{report_id}", config => config.OnNativeResult()).WithNativeFirstResponse<RecordAssuranceReport>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Providers");
    app.MapPortiaPut<ReviseAssuranceReport, AssuranceReportRegistration>(
            "/api/v1/tenants/{tenant_id}/providers/{provider_id}/assurance-reports/revise-assurance-report/{command_id}", config => config.OnNativeResult()).WithNativeFirstResponse<ReviseAssuranceReport>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Providers");
    app.MapPortiaGet<ListProviderAssuranceReports, Page<AssuranceReportView>>(
            "/api/v1/tenants/{tenant_id}/providers/{provider_id}/assurance-reports")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Providers");
    // Personal sign-off: HTTP-only, deliberately not an MCP tool.
    app.MapPortiaPut<RecordProviderReview, OperationAcceptedResponse>(
            "/api/v1/tenants/{tenant_id}/providers/{provider_id}/reviews/{review_id}",
            config => config
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
            "/api/v1/tenants/{tenant_id}/providers/{provider_id}/reviews")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Providers");
    app.MapPortiaPut<RecordProviderCoverageGap, ProviderCoverageGapRegistration>(
            "/api/v1/tenants/{tenant_id}/providers/{provider_id}/coverage-gaps/{gap_id}", config => config.OnNativeResult()).WithNativeFirstResponse<RecordProviderCoverageGap>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Providers");
    // Coverage-restored closure is an attributed decision and remains HTTP-only.
    app.MapPortiaPut<CloseProviderCoverageGap, ProviderCoverageGapRegistration>(
            "/api/v1/tenants/{tenant_id}/providers/{provider_id}/coverage-gaps/close-provider-coverage-gap/{command_id}", config => config.OnNativeResult()).WithNativeFirstResponse<CloseProviderCoverageGap>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Providers");
    // Linking a personal R1-07 acceptance is an authenticated HTTP decision.
    app.MapPortiaPut<LinkProviderCoverageGapRiskAcceptance, OperationAcceptedResponse>(
            "/api/v1/tenants/{tenant_id}/providers/{provider_id}/coverage-gaps/{gap_id}/risk-acceptances/link-provider-coverage-gap-risk-acceptance/{command_id}",
            config => config
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
            "/api/v1/tenants/{tenant_id}/providers/{provider_id}/coverage-gaps/{gap_id}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Providers");
    app.MapPortiaGet<ListProviderCoverageGaps, Page<ProviderCoverageGapView>>(
            "/api/v1/tenants/{tenant_id}/providers/{provider_id}/coverage-gaps")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Providers");
    app.MapPortiaGet<GetProviderAssuranceCoverage, ProviderAssuranceCoverageView>(
            "/api/v1/tenants/{tenant_id}/providers/{provider_id}/assurance-coverage")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Providers");
    app.MapPortiaPost<PreviewProviderChange, ProviderChangeImpactPreview>(
            "/api/v1/tenants/{tenant_id}/providers/{provider_id}/change-impact-previews", config => config.OnNativeResult()).WithNativeFirstResponse<PreviewProviderChange>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithSummary("Preview provider change impact across systems, data, controls, evidence, and scope")
        .WithTags("Providers");
    app.MapPortiaPut<RecordTechnologyComponent, TechnologyComponentRegistration>(
            "/api/v1/tenants/{tenant_id}/technology-components/{component_id}", config => config.OnNativeResult()).WithNativeFirstResponse<RecordTechnologyComponent>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("TechnologyInventory");
    app.MapPortiaGet<ListTechnologyComponentBoundaryReferences,
            Page<ApplicationBoundaryReferenceView>>(
            "/api/v1/tenants/{tenant_id}/technology-components/{component_id}/boundary-references")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithSummary("List current draft and approved boundary references for a technology component")
        .WithTags("TechnologyInventory");
    app.MapPortiaGet<ListInformationAssetBoundaryReferences,
            Page<ApplicationBoundaryReferenceView>>(
            "/api/v1/tenants/{tenant_id}/information-assets/{information_asset_id}/boundary-references")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithSummary("List current draft and approved boundary references for an information asset")
        .WithTags("TechnologyInventory");
    app.MapPortiaGet<GetTechnologyComponent, TechnologyComponentView>(
            "/api/v1/tenants/{tenant_id}/technology-components/{component_id}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("TechnologyInventory");
    app.MapPortiaGet<ListTechnologyComponents, Page<TechnologyComponentView>>(
            "/api/v1/tenants/{tenant_id}/technology-components")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("TechnologyInventory");
    app.MapPortiaGet<ListTechnologyComponentRevisions, Page<TechnologyComponentView>>(
            "/api/v1/tenants/{tenant_id}/technology-components/{component_id}/revisions")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("TechnologyInventory");
    app.MapPortiaPut<RecordInformationAsset, InformationAssetRegistration>(
            "/api/v1/tenants/{tenant_id}/information-assets/{information_asset_id}", config => config.OnNativeResult()).WithNativeFirstResponse<RecordInformationAsset>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("TechnologyInventory");
    app.MapPortiaGet<GetInformationAsset, InformationAssetView>(
            "/api/v1/tenants/{tenant_id}/information-assets/{information_asset_id}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("TechnologyInventory");
    app.MapPortiaGet<ListInformationAssets, Page<InformationAssetView>>(
            "/api/v1/tenants/{tenant_id}/information-assets")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("TechnologyInventory");
    app.MapPortiaGet<ListInformationAssetRevisions, Page<InformationAssetView>>(
            "/api/v1/tenants/{tenant_id}/information-assets/{information_asset_id}/revisions")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("TechnologyInventory");
    app.MapPortiaPost<PreviewInformationAssetChange, InformationAssetChangePreview>(
            "/api/v1/tenants/{tenant_id}/information-assets/{information_asset_id}/change-previews", config => config.OnNativeResult()).WithNativeFirstResponse<PreviewInformationAssetChange>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithSummary("Preview the data-flow impact of reclassifying or retiring an information asset")
        .WithTags("TechnologyInventory");
    app.MapPortiaPut<RecordLocation, OperationAcceptedResponse>(
            "/api/v1/tenants/{tenant_id}/locations/{location_id}",
            config => config.OnNativeResult((_, result) => NamedRecordHttp.Result(result))).WithNativeFirstResponse<RecordLocation>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("TechnologyInventory");
    app.MapPortiaPut<ReviseLocation, OperationAcceptedResponse>(
            "/api/v1/tenants/{tenant_id}/locations/revise-location/{command_id}",
            config => config.OnNativeResult((_, result) => NamedRecordHttp.Result(result))).WithNativeFirstResponse<ReviseLocation>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("TechnologyInventory");
    app.MapPortiaGet<GetLocation, LocationView>(
            "/api/v1/tenants/{tenant_id}/locations/{location_id}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("TechnologyInventory");
    app.MapPortiaGet<ListLocations, Page<LocationView>>(
            "/api/v1/tenants/{tenant_id}/locations")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("TechnologyInventory");
    app.MapPortiaGet<ListLocationRevisions, Page<LocationView>>(
            "/api/v1/tenants/{tenant_id}/locations/{location_id}/revisions")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("TechnologyInventory");
    app.MapPortiaPut<RecordOperationalProcess, OperationAcceptedResponse>(
            "/api/v1/tenants/{tenant_id}/operational-processes/{operational_process_id}",
            config => config.OnNativeResult((_, result) => NamedRecordHttp.Result(result))).WithNativeFirstResponse<RecordOperationalProcess>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("TechnologyInventory");
    app.MapPortiaPut<ReviseOperationalProcess, OperationAcceptedResponse>(
            "/api/v1/tenants/{tenant_id}/operational-processes/revise-operational-process/{command_id}",
            config => config.OnNativeResult((_, result) => NamedRecordHttp.Result(result))).WithNativeFirstResponse<ReviseOperationalProcess>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("TechnologyInventory");
    app.MapPortiaGet<GetOperationalProcess, OperationalProcessView>(
            "/api/v1/tenants/{tenant_id}/operational-processes/{operational_process_id}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("TechnologyInventory");
    app.MapPortiaGet<ListOperationalProcesses, Page<OperationalProcessView>>(
            "/api/v1/tenants/{tenant_id}/operational-processes")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("TechnologyInventory");
    app.MapPortiaGet<ListOperationalProcessRevisions, Page<OperationalProcessView>>(
            "/api/v1/tenants/{tenant_id}/operational-processes/{operational_process_id}/revisions")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("TechnologyInventory");
    app.MapPortiaPut<RecordDataFlow, DataFlowRegistration>(
            "/api/v1/tenants/{tenant_id}/data-flows/{data_flow_id}", config => config.OnNativeResult()).WithNativeFirstResponse<RecordDataFlow>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("TechnologyInventory");
    app.MapPortiaGet<GetDataFlow, DataFlowView>(
            "/api/v1/tenants/{tenant_id}/data-flows/{data_flow_id}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("TechnologyInventory");
    app.MapPortiaGet<ListDataFlows, Page<DataFlowView>>(
            "/api/v1/tenants/{tenant_id}/data-flows")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("TechnologyInventory");
    app.MapPortiaGet<ListDataFlowRevisions, Page<DataFlowView>>(
            "/api/v1/tenants/{tenant_id}/data-flows/{data_flow_id}/revisions")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("TechnologyInventory");
    app.MapPortiaPut<DeclareApplication, ApplicationRegistration>(
            "/api/v1/tenants/{tenant_id}/applications/{application_id}", config => config.OnNativeResult()).WithNativeFirstResponse<DeclareApplication>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Applications");
    app.MapPortiaPut<ReviseApplication>(
            "/api/v1/tenants/{tenant_id}/applications/revise-application/{command_id}", endpoint => endpoint
                .OnBind(Program.BindApplicationRevisionAsync)
                .Accepts<ApplicationRevisionHttpBody>("application/json", isOptional: true, "*/*")
                .Parameter<Uuid>("tenant_id", PortiaHttpParameterLocation.Route, required: true)
                .Parameter<Uuid>("command_id", PortiaHttpParameterLocation.Route, required: true).OnNativeResult()).WithNativeFirstResponse<ReviseApplication>()
        .AddOpenApiOperationTransformer(Program.PreserveApplicationHttpSchemaAsync)
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Applications");
    app.MapPortiaGet<GetApplication, ApplicationView>(
            "/api/v1/tenants/{tenant_id}/applications/{application_id}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Applications");
    app.MapPortiaGet<ListApplications, Page<ApplicationView>>(
            "/api/v1/tenants/{tenant_id}/applications")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Applications");
    app.MapPortiaPost<AddApplicationRelationship, ApplicationRelationshipChange>(
            "/api/v1/tenants/{tenant_id}/applications/{application_id}/relationships/{relationship_id}", config => config.OnNativeResult()).WithNativeFirstResponse<AddApplicationRelationship>()
        .WithDescription("expected_relationship_revision uses the actor-visible relationship event count. " +
            "Each add or remove event counts only when its target is currently readable by the actor; " +
            "events for hidden targets do not create revision gaps. Stale writes return a retryable " +
            "conflict without exposing the canonical source sequence.")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Applications");
    app.MapPortiaDelete<RemoveApplicationRelationship, ApplicationRelationshipChange>(
            "/api/v1/tenants/{tenant_id}/applications/{application_id}/relationships/{relationship_id}", config => config.OnNativeResult()).WithNativeFirstResponse<RemoveApplicationRelationship>()
        .WithDescription("expected_relationship_revision uses the actor-visible relationship event count. " +
            "Each add or remove event counts only when its target is currently readable by the actor; " +
            "events for hidden targets do not create revision gaps. Stale writes return a retryable " +
            "conflict without exposing the canonical source sequence.")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Applications");
    app.MapPortiaGet<ListApplicationRelationships, ApplicationRelationshipPage>(
            "/api/v1/tenants/{tenant_id}/applications/{application_id}/relationships")
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
            "/api/v1/tenants/{tenant_id}/applications/{application_id}/revisions/{revision}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Applications");
    app.MapPortiaGet<ListApplicationRevisions, Page<ApplicationRevisionView>>(
            "/api/v1/tenants/{tenant_id}/applications/{application_id}/revisions")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Applications");
    app.MapPortiaPost<RecordApplicationRelationship, ApplicationRelationshipRegistration>(
            "/api/v1/tenants/{tenant_id}/applications/{source_application_id}/relationships", config => config.OnNativeResult()).WithNativeFirstResponse<RecordApplicationRelationship>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithSummary("Record a directed application dependency or proposed successor")
        .WithTags("Applications");
    app.MapPortiaDelete<RemoveGovernedApplicationRelationship>(
            "/api/v1/tenants/{tenant_id}/applications/{source_application_id}/relationships/{relationship_type}/{target_application_id}", config => config.OnNativeResult()).WithNativeFirstResponse<RemoveGovernedApplicationRelationship>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithSummary("Remove a directed application relationship")
        .WithTags("Applications");
    app.MapPortiaGet<ListDirectedApplicationRelationships, Page<ApplicationRelationshipView>>(
            "/api/v1/tenants/{tenant_id}/applications/{application_id}/relationships/directed")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithSummary("List incoming or outgoing application relationships")
        .WithTags("Applications");
    app.MapPortiaPost<ApproveApplicationSuccessor>(
            "/api/v1/tenants/{tenant_id}/applications/{predecessor_application_id}/successors/{successor_application_id}/approval", config => config.OnNativeResult()).WithNativeFirstResponse<ApproveApplicationSuccessor>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithSummary("Approve a proposed successor after a complete current impact review")
        .WithTags("Applications");
    app.MapPortiaPut<DeclareSystemInstance, SystemInstanceRegistration>(
            "/api/v1/tenants/{tenant_id}/applications/{application_id}/system-instances/{system_instance_id}", config => config.OnNativeResult()).WithNativeFirstResponse<DeclareSystemInstance>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("System instances");
    app.MapPortiaGet<GetSystemInstance, SystemInstanceView>(
            "/api/v1/tenants/{tenant_id}/applications/{application_id}/system-instances/{system_instance_id}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("System instances");
    app.MapPortiaGet<ListSystemInstances, Page<SystemInstanceView>>(
            "/api/v1/tenants/{tenant_id}/applications/{application_id}/system-instances")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("System instances");
    app.MapPortiaGet<ListApplicationBoundaryReferences,
            Page<ApplicationBoundaryReferenceView>>(
            "/api/v1/tenants/{tenant_id}/applications/{application_id}/boundary-references")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithSummary("List current draft and approved boundary references for an application")
        .WithTags("Applications");
    app.MapPortiaGet<ListSystemInstanceBoundaryReferences,
            Page<ApplicationBoundaryReferenceView>>(
            "/api/v1/tenants/{tenant_id}/applications/{application_id}/system-instances/{system_instance_id}/boundary-references")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithSummary("List current draft and approved boundary references for a system instance")
        .WithTags("System instances");
    app.MapPortiaPost<PreviewApplicationChange, ApplicationChangePreview>(
            "/api/v1/tenants/{tenant_id}/applications/{application_id}/change-previews", endpoint => endpoint
                .OnBind(Program.BindApplicationPreviewAsync)
                .Accepts<ApplicationPreviewHttpBody>("application/json", isOptional: true, "*/*")
                .Parameter<Uuid>("tenant_id", PortiaHttpParameterLocation.Route, required: true)
                .Parameter<Uuid>("application_id", PortiaHttpParameterLocation.Route, required: true).OnNativeResult()).WithNativeFirstResponse<PreviewApplicationChange>()
        .AddOpenApiOperationTransformer(Program.PreserveApplicationHttpSchemaAsync)
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithSummary("Preview the known impact of a proposed application change")
        .WithTags("Applications");
    app.MapPortiaPut<RetireApplication>(
            "/api/v1/tenants/{tenant_id}/applications/{application_id}/retirements/{command_id}", config => config.OnNativeResult()).WithNativeFirstResponse<RetireApplication>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithSummary("Retire an application from an effective date, optionally merged into a successor")
        .WithTags("Applications");
    app.MapPortiaPut<RetireSystemInstance>(
            "/api/v1/tenants/{tenant_id}/applications/{application_id}/system-instances/{system_instance_id}/retirements/{command_id}", config => config.OnNativeResult()).WithNativeFirstResponse<RetireSystemInstance>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithSummary("Retire a system instance from an effective date")
        .WithTags("System instances");
    app.MapPortiaPost<DecideAccessReviewScope, AccessReviewScopeDecisionView>(
            "/api/v1/tenants/{tenant_id}/applications/{application_id}/system-instances/{system_instance_id}/access-review-scope-decisions", config => config.OnNativeResult()).WithNativeFirstResponse<DecideAccessReviewScope>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithSummary("Approve an effective-dated access-review scope decision")
        .WithTags("System instances");
    app.MapPortiaGet<GetAccessReviewScope, AccessReviewScopeView>(
            "/api/v1/tenants/{tenant_id}/applications/{application_id}/system-instances/{system_instance_id}/access-review-scope")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithSummary("Read the effective access-review scope decision and its history")
        .WithTags("System instances");
    app.MapPortiaGet<ListAccessReviewScopes, Page<AccessReviewScopeStatusView>>(
            "/api/v1/tenants/{tenant_id}/applications/{application_id}/access-review-scopes")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithSummary("List the projected access-review scope status of an application's system instances")
        .WithTags("Applications");
    app.MapPortiaGet<GetArtifactRetention, ArtifactRetentionView>(
            "/api/v1/tenants/{tenant_id}/artifact-retention/{source_kind}/{source_id}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Artifact retention");
    app.MapPortiaGet<ListArtifactLegalHolds, Page<ArtifactLegalHoldView>>(
            "/api/v1/tenants/{tenant_id}/artifact-retention/{source_kind}/{source_id}/legal-holds")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Artifact retention");
    app.MapPortiaGet<GetApplicationImportRetention, ArtifactRetentionView>(
            "/api/v1/tenants/{tenant_id}/application-imports/{batch_id}/retention")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Artifact retention");
    app.MapPortiaPut<RecordArtifactRetentionBasis>(
            "/api/v1/tenants/{tenant_id}/artifact-retention/{source_kind}/{source_id}/basis/{command_id}", config => config.OnNativeResult()).WithNativeFirstResponse<RecordArtifactRetentionBasis>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Artifact retention");
    app.MapPortiaPut<PlaceArtifactLegalHold>(
            "/api/v1/tenants/{tenant_id}/artifact-retention/{source_kind}/{source_id}/legal-holds/{hold_id}", config => config.OnNativeResult()).WithNativeFirstResponse<PlaceArtifactLegalHold>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Artifact retention");
    app.MapPortiaPut<ReleaseArtifactLegalHold>(
            "/api/v1/tenants/{tenant_id}/artifact-retention/{source_kind}/{source_id}/legal-holds/{hold_id}/releases/{command_id}", config => config.OnNativeResult()).WithNativeFirstResponse<ReleaseArtifactLegalHold>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Artifact retention");
    app.MapPortiaPut<StageApplicationImport, ApplicationImportRegistration>(
            "/api/v1/tenants/{tenant_id}/application-imports/{submission_id}", config => config.OnNativeResult()).WithNativeFirstResponse<StageApplicationImport>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Application imports");
    app.MapPortiaGet<GetApplicationImportProgress, ApplicationImportProgress>(
            "/api/v1/tenants/{tenant_id}/application-imports/{batch_id}/progress")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithSummary("Read durable whole-batch import execution progress")
        .WithTags("Application imports");
    app.MapPortiaGet<GetApplicationImportRejectedReport, ApplicationImportRejectedReport>(
            "/api/v1/tenants/{tenant_id}/application-imports/{batch_id}/rejected-report")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithSummary("Read bounded rejected-item findings from retained import rows")
        .WithTags("Application imports");
    app.MapPortiaPut<AcceptApplicationImport, OperationAcceptedResponse>(
            "/api/v1/tenants/{tenant_id}/application-imports/{batch_id}/acceptances/{command_id}",
            config => config
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
            "/api/v1/tenants/{tenant_id}/application-imports/{batch_id}/cancellations/{command_id}",
            config => config
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
            "/api/v1/tenants/{tenant_id}/application-imports/{batch_id}/rows/{row_id}/correlations/{command_id}", config => config.OnNativeResult()).WithNativeFirstResponse<CorrelateApplicationImportRow>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithSummary("Record a personal source-row correlation decision before acceptance")
        .WithTags("Application imports");
    app.MapPortiaGet<GetApplicationImport, ApplicationImportView>(
            "/api/v1/tenants/{tenant_id}/application-imports/{batch_id}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Application imports");
    app.MapPortiaGet<ListApplicationImportRows, Page<ApplicationImportRowView>>(
            "/api/v1/tenants/{tenant_id}/application-imports/{batch_id}/rows")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Application imports");
    app.MapPortiaGet<PreviewApplicationImport, Page<ApplicationImportPreviewRow>>(
            "/api/v1/tenants/{tenant_id}/application-imports/{batch_id}/preview")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Application imports");
    app.MapPortiaGet<PreviewMissingApplicationImportRows, Page<ApplicationImportMissingRow>>(
            "/api/v1/tenants/{tenant_id}/application-imports/{batch_id}/preview/missing")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithSummary("Preview complete-source omissions as blocked retirement proposals")
        .WithTags("Application imports");
    app.MapPortiaGet<PreviewApplicationImportOmissionProposal, ApplicationImportOmissionPreview>(
            "/api/v1/tenants/{tenant_id}/application-imports/{batch_id}/omission-proposal/preview")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Application imports");
    app.MapPortiaPut<FreezeApplicationImportOmissionProposal>(
            "/api/v1/tenants/{tenant_id}/application-imports/{batch_id}/omission-proposal/{command_id}", config => config.OnNativeResult()).WithNativeFirstResponse<FreezeApplicationImportOmissionProposal>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithSummary("Confirm a source-bound omission proposal without approving retirement")
        .WithTags("Application imports");
    app.MapPortiaGet<GetApplicationImportOmissionProposal, ApplicationImportOmissionProposalView>(
            "/api/v1/tenants/{tenant_id}/application-imports/{batch_id}/omission-proposal")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Application imports");
    app.MapPortiaPost<CreateClientService, ClientServiceRegistration>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/client-services", config => config.OnNativeResult()).WithNativeFirstResponse<CreateClientService>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Client services");
    app.MapPortiaGet<ListProgramClientServices, Page<ClientServiceView>>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/client-services")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Client services");
    app.MapPortiaPut<ReviseClientService>(
            "/api/v1/tenants/{tenant_id}/client-services/{service_id}", config => config.OnNativeResult()).WithNativeFirstResponse<ReviseClientService>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Client services");
    app.MapPortiaPost<RetireClientService>(
            "/api/v1/tenants/{tenant_id}/client-services/{service_id}/retirements", config => config.OnNativeResult()).WithNativeFirstResponse<RetireClientService>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Client services");
    app.MapPortiaGet<GetClientService, ClientServiceView>(
            "/api/v1/tenants/{tenant_id}/client-services/{service_id}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Client services");
    app.MapPortiaGet<ListClientServices, Page<ClientServiceView>>(
            "/api/v1/tenants/{tenant_id}/client-services")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Client services");
    app.MapPortiaGet<ListClientServiceRevisions, Page<ClientServiceRevisionView>>(
            "/api/v1/tenants/{tenant_id}/client-services/{service_id}/revisions")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Client services");
    app.MapPortiaGet<GetClientServiceRevision, ClientServiceRevisionView>(
            "/api/v1/tenants/{tenant_id}/client-services/{service_id}/revisions/{revision}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Client services");
    app.MapPortiaPost<CreateBoundary, BoundaryRegistration>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/boundaries", config => config.OnNativeResult()).WithNativeFirstResponse<CreateBoundary>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Boundaries");
    app.MapPortiaGet<ListProgramBoundaries, Page<BoundaryView>>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/boundaries")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Boundaries");
    app.MapPortiaPut<ReviseBoundaryDraft>(
            "/api/v1/tenants/{tenant_id}/boundaries/{boundary_id}/drafts/{draft_version_id}", config => config.OnNativeResult()).WithNativeFirstResponse<ReviseBoundaryDraft>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Boundaries");
    app.MapPortiaPost<DiscardBoundaryDraft>(
            "/api/v1/tenants/{tenant_id}/boundaries/{boundary_id}/drafts/{draft_version_id}/discards", config => config.OnNativeResult()).WithNativeFirstResponse<DiscardBoundaryDraft>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Boundaries");
    app.MapPortiaGet<GetBoundary, BoundaryView>(
            "/api/v1/tenants/{tenant_id}/boundaries/{boundary_id}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Boundaries");
    app.MapPortiaPut<AssignResponsibility>(
            "/api/v1/tenants/{tenant_id}/responsibilities/{command_id}", config => config.OnNativeResult()).WithNativeFirstResponse<AssignResponsibility>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Responsibilities");
    app.MapPortiaPut<RevokeResponsibility>(
            "/api/v1/tenants/{tenant_id}/responsibilities/{assignment_id}/revocations/{command_id}", config => config.OnNativeResult()).WithNativeFirstResponse<RevokeResponsibility>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Responsibilities");
    app.MapPortiaGet<ListResponsibilities, ResponsibilitySetView>(
            "/api/v1/tenants/{tenant_id}/responsibilities")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Responsibilities");
    app.MapPortiaGet<PreviewResponsibilityConflicts, ResponsibilityConflictPreview>(
            "/api/v1/tenants/{tenant_id}/responsibilities/conflict-preview")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithDescription("The type query value must be control_owner, evidence_contributor, assigned_reviewer, access_reviewer, corrective_action_owner, or policy_approver.")
        .WithTags("Responsibilities");
    app.MapPortiaGet<GetBoundaryVersion, BoundaryVersionView>(
            "/api/v1/tenants/{tenant_id}/boundaries/{boundary_id}/versions/{version_id}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Boundaries");
    app.MapPortiaGet<ListBoundaryVersions, Page<BoundaryVersionView>>(
            "/api/v1/tenants/{tenant_id}/boundaries/{boundary_id}/versions")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Boundaries");
    app.MapPortiaGet<GetEffectiveBoundaryVersion, BoundaryVersionView>(
            "/api/v1/tenants/{tenant_id}/boundaries/{boundary_id}/effective-version")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Boundaries");
    app.MapPortiaGet<GetBoundaryDecision, BoundaryDecisionView>(
            "/api/v1/tenants/{tenant_id}/boundaries/{boundary_id}/decisions/{decision_id}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Boundaries");
    app.MapPortiaGet<ListBoundaryDecisions, Page<BoundaryDecisionView>>(
            "/api/v1/tenants/{tenant_id}/boundaries/{boundary_id}/decisions")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Boundaries");
    app.MapPortiaGet<GetSeparationOfDutiesWaiver, SeparationOfDutiesWaiverView>(
            "/api/v1/tenants/{tenant_id}/separation-of-duties-waivers/{waiver_id}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Access control");
    app.MapPortiaPost<FreezeProgramScopeSnapshot, SnapshotRegistration>(
            "/api/v1/tenants/{tenant_id}/scope-snapshots", config => config.OnNativeResult()).WithNativeFirstResponse<FreezeProgramScopeSnapshot>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Snapshots");
    app.MapPortiaPost<AmendProgramScopeSnapshot, SnapshotRegistration>(
            "/api/v1/tenants/{tenant_id}/scope-snapshots/{snapshot_id}/amendments", config => config.OnNativeResult()).WithNativeFirstResponse<AmendProgramScopeSnapshot>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Snapshots");
    app.MapPortiaGet<GetSnapshot, SnapshotView>(
            "/api/v1/tenants/{tenant_id}/scope-snapshots/{snapshot_id}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Snapshots");
    app.MapPortiaGet<VerifyProgramScopeSnapshot, ProgramScopeSnapshotVerification>(
            "/api/v1/tenants/{tenant_id}/scope-snapshots/{snapshot_id}/verification")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Snapshots");
    app.MapPortiaGet<RegenerateProgramScopeSnapshotManifest,
            ProgramScopeSnapshotManifestRegeneration>(
            "/api/v1/tenants/{tenant_id}/scope-snapshots/{snapshot_id}/manifest-regeneration")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Snapshots");
    app.MapPortiaGet<ListProgramSnapshots, Page<SnapshotView>>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/scope-snapshots")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Snapshots");
    app.MapPortiaPost<FreezeWorkforceRosterSnapshot, SnapshotRegistration>(
            "/api/v1/tenants/{tenant_id}/workforce-roster-snapshots", config => config.OnNativeResult()).WithNativeFirstResponse<FreezeWorkforceRosterSnapshot>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Snapshots");
    app.MapPortiaPost<AmendWorkforceRosterSnapshot, SnapshotRegistration>(
            "/api/v1/tenants/{tenant_id}/workforce-roster-snapshots/{snapshot_id}/amendments", config => config.OnNativeResult()).WithNativeFirstResponse<AmendWorkforceRosterSnapshot>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Snapshots");
    app.MapPortiaGet<GetWorkforceRosterSnapshot, WorkforceRosterSnapshotView>(
            "/api/v1/tenants/{tenant_id}/workforce-roster-snapshots/{snapshot_id}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Snapshots");
    app.MapPortiaGet<ListWorkforceRosterSnapshots, Page<PopulationSnapshotSummary>>(
            "/api/v1/tenants/{tenant_id}/workforce-roster-snapshots")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Snapshots");
    app.MapPortiaGet<RegenerateWorkforceRosterSnapshotManifest, PopulationSnapshotManifestRegeneration>(
            "/api/v1/tenants/{tenant_id}/workforce-roster-snapshots/{snapshot_id}/manifest-regeneration")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Snapshots");
    app.MapPortiaGet<GetWorkforceRosterSnapshotAsOf, WorkforceRosterSnapshotView>(
            "/api/v1/tenants/{tenant_id}/workforce-roster-snapshot-as-of")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Snapshots");
    app.MapPortiaGet<PreviewBoundaryImpact, BoundaryImpactPreview>(
            "/api/v1/tenants/{tenant_id}/boundaries/{boundary_id}/drafts/{draft_version_id}/impact-preview")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Boundaries");
    app.MapPortiaPost<ReviewBoundary>(
            "/api/v1/tenants/{tenant_id}/boundaries/{boundary_id}/drafts/{draft_version_id}/reviews", config => config.OnNativeResult()).WithNativeFirstResponse<ReviewBoundary>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Boundaries");
    app.MapPortiaPost<ApproveBoundary>(
            "/api/v1/tenants/{tenant_id}/boundaries/{boundary_id}/drafts/{draft_version_id}/approvals", config => config.OnNativeResult()).WithNativeFirstResponse<ApproveBoundary>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Boundaries");
    app.MapPortiaPost<RecordSeparationOfDutiesWaiver, SeparationOfDutiesWaiverView>(
            "/api/v1/tenants/{tenant_id}/separation-of-duties-waivers", config => config.OnNativeResult()).WithNativeFirstResponse<RecordSeparationOfDutiesWaiver>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Access control");
    app.MapPortiaPost<ApproveSeparationOfDutiesWaiver, SeparationOfDutiesWaiverView>(
            "/api/v1/tenants/{tenant_id}/separation-of-duties-waivers/{waiver_id}/approvals", config => config.OnNativeResult()).WithNativeFirstResponse<ApproveSeparationOfDutiesWaiver>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Access control");
    app.MapPortiaPost<ProposeBoundarySuccessor, BoundaryRegistration>(
            "/api/v1/tenants/{tenant_id}/boundaries/{boundary_id}/successors", config => config.OnNativeResult()).WithNativeFirstResponse<ProposeBoundarySuccessor>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Boundaries");
    app.MapPortiaPut<ReserveEmail>("/api/v1/users/{user_id}/email-addresses/{email_address}/{command_id}", config => config.OnNativeResult()).WithNativeFirstResponse<ReserveEmail>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Email addresses");
    app.MapPortiaPut<IssueEmailChallenge>("/api/v1/users/{user_id}/email-addresses/{email_address}/challenges/{challenge_id}", config => config.OnNativeResult()).WithNativeFirstResponse<IssueEmailChallenge>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Email addresses");
    app.MapPortiaGet<GetEmailChallengeStatus, EmailChallengeStatusView>(
            "/api/v1/users/{user_id}/email-addresses/{email_address}/challenges/status")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Email addresses");
    app.MapPortiaPut<CompleteEmailChallenge>("/api/v1/users/{user_id}/email-addresses/{email_address}/verifications/{challenge_id}", config => config.OnNativeResult()).WithNativeFirstResponse<CompleteEmailChallenge>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Email addresses");
    app.MapPortiaGet<GetEmailAddress, EmailAddressView>("/api/v1/users/{user_id}/email-addresses/{email_address}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Email addresses");
    app.MapPortiaGet<ListEmailAddresses, Page<EmailAddressView>>("/api/v1/users/{user_id}/email-addresses")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Email addresses");
    app.MapPortiaGet<ListMyTenants, Page<TenantMembershipSummary>>("/api/v1/tenants/mine")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Tenants");
    app.MapPortiaPut<DisableTenantIdentityProvider>(
            "/api/v1/tenants/{tenant_id}/identity-providers/{provider_connection_id}/disable/{command_id}", config => config.OnNativeResult()).WithNativeFirstResponse<DisableTenantIdentityProvider>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Identity federation");
    app.MapPortiaPut<DefineTeam>("/api/v1/tenants/{tenant_id}/teams/{team_id}", config => config.OnNativeResult()).WithNativeFirstResponse<DefineTeam>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Teams");
    app.MapPortiaPut<DeleteTeam>("/api/v1/tenants/{tenant_id}/teams/{team_id}/deletions/{command_id}", config => config.OnNativeResult()).WithNativeFirstResponse<DeleteTeam>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Teams");
    app.MapPortiaGet<GetTeam, TeamView>("/api/v1/tenants/{tenant_id}/teams/{team_id}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Teams");
    app.MapPortiaGet<ListTeams, Page<TeamView>>("/api/v1/tenants/{tenant_id}/teams")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Teams");
    app.MapPortiaPut<AssignTeamMember, AdministratorTransitionResponse>(
            "/api/v1/tenants/{tenant_id}/teams/{team_id}/members/{member_id}/{command_id}",
            config => config.OnNativeResult((_, result) => AdministratorTransitionHttp.Result(result))).WithNativeFirstResponse<AssignTeamMember>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Teams");
    app.MapPortiaPut<RemoveTeamMember, AdministratorTransitionResponse>(
            "/api/v1/tenants/{tenant_id}/members/{member_id}/team-removals/{command_id}",
            config => config.OnNativeResult((_, result) => AdministratorTransitionHttp.Result(result))).WithNativeFirstResponse<RemoveTeamMember>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Teams");
    app.MapPortiaGet<ListTeamMembers, Page<TeamMemberView>>("/api/v1/tenants/{tenant_id}/teams/{team_id}/members")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Teams");
    app.MapPortiaPut<AssignTeamRole>("/api/v1/tenants/{tenant_id}/teams/{team_id}/roles/{role_id}/assignments/{command_id}", config => config.OnNativeResult()).WithNativeFirstResponse<AssignTeamRole>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Teams");
    app.MapPortiaDelete<RemoveTeamRole>("/api/v1/tenants/{tenant_id}/teams/{team_id}/roles/{role_id}/removals/{command_id}", config => config.OnNativeResult()).WithNativeFirstResponse<RemoveTeamRole>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Teams");
    app.MapPortiaPut<DefineRole>("/api/v1/tenants/{tenant_id}/roles/{role_id}", config => config.OnNativeResult()).WithNativeFirstResponse<DefineRole>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Roles");
    app.MapPortiaPut<DeleteRole>("/api/v1/tenants/{tenant_id}/roles/{role_id}/deletions/{command_id}", config => config.OnNativeResult()).WithNativeFirstResponse<DeleteRole>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Roles");
    app.MapPortiaGet<GetRole, RoleView>("/api/v1/tenants/{tenant_id}/roles/{role_id}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Roles");
    app.MapPortiaGet<ListRoles, Page<RoleView>>("/api/v1/tenants/{tenant_id}/roles")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Roles");
    app.MapPortiaPut<AssignRolePermission>("/api/v1/tenants/{tenant_id}/roles/{role_id}/permissions/{permission}/assignments/{command_id}", config => config.OnNativeResult()).WithNativeFirstResponse<AssignRolePermission>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Roles");
    app.MapPortiaDelete<RemoveRolePermission>("/api/v1/tenants/{tenant_id}/roles/{role_id}/permissions/{permission}/removals/{command_id}", config => config.OnNativeResult()).WithNativeFirstResponse<RemoveRolePermission>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Roles");
    app.MapPortiaGet<ListRolePermissions, Page<RolePermissionView>>(
            "/api/v1/tenants/{tenant_id}/roles/{role_id}/permissions")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Roles");
    app.MapPortiaGet<ListRoleTeams, Page<RoleTeamView>>("/api/v1/tenants/{tenant_id}/roles/{role_id}/teams")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Roles");
    app.MapPortiaGet<ListTeamRoles, Page<TeamRoleView>>("/api/v1/tenants/{tenant_id}/teams/{team_id}/roles")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Teams");
    app.MapPortiaPost<GrantAccess>("/api/v1/tenants/{tenant_id}/access-grants/{grant_id}", config => config.OnNativeResult()).WithNativeFirstResponse<GrantAccess>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Access grants");
    app.MapPortiaDelete<RevokeAccessGrant>("/api/v1/tenants/{tenant_id}/access-grants/{grant_id}", config => config.OnNativeResult()).WithNativeFirstResponse<RevokeAccessGrant>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Access grants");
    app.MapPortiaGet<ListAccessGrants, AccessGrantSetView>("/api/v1/tenants/{tenant_id}/access-grants")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Access grants");
    app.MapPortiaPut<ConfigureTenantIdentityProvider>(
            "/api/v1/tenants/{tenant_id}/identity-providers/{provider_connection_id}/{command_id}", config => config.OnNativeResult()).WithNativeFirstResponse<ConfigureTenantIdentityProvider>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Identity federation");
    app.MapPortiaPut<TestTenantIdentityProvider>(
            "/api/v1/tenants/{tenant_id}/identity-providers/{provider_connection_id}/tests/{test_key}", config => config.OnNativeResult()).WithNativeFirstResponse<TestTenantIdentityProvider>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Identity federation");
    app.MapPortiaPut<RotateTenantIdentityProviderCredentials>(
            "/api/v1/tenants/{tenant_id}/identity-providers/{provider_connection_id}/credential-rotations/{command_id}", config => config.OnNativeResult()).WithNativeFirstResponse<RotateTenantIdentityProviderCredentials>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Identity federation");
    app.MapPortiaGet<GetAuditPackage, AuditPackageView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/audit-packages/{package_id}", endpoint => endpoint
                .OnBind(Program.BindAuditPackageRead)
                .Parameter<Uuid>("tenant_id", PortiaHttpParameterLocation.Route, required: true)
                .Parameter<Uuid>("program_id", PortiaHttpParameterLocation.Route, required: true)
                .Parameter<Uuid>("package_id", PortiaHttpParameterLocation.Route, required: true))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Audit packages");
    app.MapPortiaGet<ListAuditPackages, Page<AuditPackageSummaryView>>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/audit-packages")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Audit packages");
    app.MapPortiaGet<GetTemplateApplication, TemplateApplicationView>(
        "/api/v1/tenants/{tenant_id}/programs/{program_id}/template-applications/{template_application_id}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Methodology templates");

    app.MapPortiaGet<GetMethodologyTemplateVersion, MethodologyTemplateVersionView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/methodology-template-versions/{methodology_template_version_id}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Methodology templates");
    app.MapPortiaGet<ListMethodologyTemplates, Page<MethodologyTemplateSummaryView>>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/methodology-templates")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Methodology templates");
    app.MapPortiaPut<CreateAuditPackage>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/audit-packages/{package_id}", config => config.OnNativeResult()).WithNativeFirstResponse<CreateAuditPackage>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Audit packages");
    app.MapPortiaPut<AmendAuditPackage>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/audit-packages/{package_id}/amendments/{command_id}", config => config.OnNativeResult()).WithNativeFirstResponse<AmendAuditPackage>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Audit packages");
    app.MapPortiaPut<ValidateAuditPackage>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/audit-packages/{package_id}/versions/{package_version}/validations/{command_id}", config => config.OnNativeResult()).WithNativeFirstResponse<ValidateAuditPackage>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Audit packages");
    app.MapPortiaPut<ApproveAuditPackageDisclosure>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/audit-packages/{package_id}/versions/{package_version}/disclosures/{command_id}", config => config.OnNativeResult()).WithNativeFirstResponse<ApproveAuditPackageDisclosure>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Audit packages");
    app.MapPortiaPut<ApproveAuditPackageValidationOverride>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/audit-packages/{package_id}/versions/{package_version}/validation-overrides/{command_id}", config => config.OnNativeResult()).WithNativeFirstResponse<ApproveAuditPackageValidationOverride>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Audit packages");
    app.MapPortiaPut<RequestAuditPackageBuild, OperationAcceptedResponse>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/audit-packages/{package_id}/versions/{package_version}/builds/{build_key}",
            config => config
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
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/audit-packages/{package_id}/versions/{package_version}/deliveries/{delivery_id}",
            config => config
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
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/examinations/{examination_id}", config => config.OnNativeResult()).WithNativeFirstResponse<CreateExaminationEngagement>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Examinations");
    app.MapPortiaPut<ReviseExaminationEngagement>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/examinations/{examination_id}/revisions", config => config.OnNativeResult()).WithNativeFirstResponse<ReviseExaminationEngagement>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Examinations");
    app.MapPortiaGet<GetExaminationEngagement, ExaminationEngagementView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/examinations/{examination_id}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Examinations");
    app.MapPortiaGet<GetTypeIBaseline, TypeIBaselineView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/type-i-baselines/{type_i_baseline_id}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Examinations");
    app.MapPortiaGet<PreviewTypeIBaseline, TypeIBaselinePreviewView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/examinations/{examination_id}/type-i-baseline-preview")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Examinations");
    app.MapPortiaPost<FreezeTypeIBaseline>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/examinations/{examination_id}/type-i-baselines/{baseline_id}/freeze", config => config.OnNativeResult()).WithNativeFirstResponse<FreezeTypeIBaseline>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Examinations");
    app.MapPortiaPost<AmendTypeIBaseline>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/examinations/{examination_id}/type-i-baselines/{baseline_id}/amendments", config => config.OnNativeResult()).WithNativeFirstResponse<AmendTypeIBaseline>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Examinations");
    app.MapPortiaPost<RecordManagementAssertion>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/examinations/{examination_id}/assertions/{assertion_id}", config => config.OnNativeResult()).WithNativeFirstResponse<RecordManagementAssertion>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Examinations");
    app.MapPortiaPost<RecordManagementRepresentationLetter>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/examinations/{examination_id}/representation-letters/{letter_id}", config => config.OnNativeResult()).WithNativeFirstResponse<RecordManagementRepresentationLetter>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Examinations");
    app.MapPortiaPost<RecordExaminationOutcome>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/examinations/{examination_id}/outcomes/{outcome_id}", config => config.OnNativeResult()).WithNativeFirstResponse<RecordExaminationOutcome>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Examinations");
    app.MapPortiaPost<RecordManagementExaminationReview>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/examinations/{examination_id}/management-reviews/{sign_off_id}", config => config.OnNativeResult()).WithNativeFirstResponse<RecordManagementExaminationReview>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Examinations");
    app.MapPortiaPost<ProposeTypeIIOperatingPlan>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/examinations/{examination_id}/type-ii-plans/{plan_id}/proposals", config => config.OnNativeResult()).WithNativeFirstResponse<ProposeTypeIIOperatingPlan>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Examinations");
    app.MapPortiaPut<StartTypeIIObservationPeriod, OperationAcceptedResponse>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/type-ii-periods/{period_id}",
            config => config
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
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/type-ii-periods/{period_id}/amendments/{command_id}",
            config => config
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
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/type-ii-periods/{period_id}/amendments/{amendment_id}/cancellations/{command_id}", config => config.OnNativeResult()).WithNativeFirstResponse<CancelTypeIIPeriodAmendment>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Observation periods");
    app.MapPortiaPut<RecordTypeIIPeriodCloseValidation, UnitResult>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/observation-periods/{period_id}/close-validations/{command_id}", config => config.OnNativeResult()).WithNativeFirstResponse<RecordTypeIIPeriodCloseValidation>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Type II periods");
    app.MapPortiaPut<GenerateDueControlOccurrences, UnitResult>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/observation-periods/{period_id}/occurrence-generations/{generation_key}", config => config.OnNativeResult()).WithNativeFirstResponse<GenerateDueControlOccurrences>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Observation periods");
    app.MapPortiaGet<GetObservationPeriod, ObservationPeriodView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/observation-periods/{period_id}", endpoint => endpoint
                .OnBind(Program.BindObservationPeriodRead)
                .Parameter<Uuid>("tenant_id", PortiaHttpParameterLocation.Route, required: true)
                .Parameter<Uuid>("program_id", PortiaHttpParameterLocation.Route, required: true)
                .Parameter<Uuid>("period_id", PortiaHttpParameterLocation.Route, required: true))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Observation periods");
    app.MapPortiaGet<ListObservationPeriods, Page<ObservationPeriodSummaryView>>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/observation-periods")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Observation periods");
    app.MapPortiaGet<PreviewObservationPeriodClose, ObservationPeriodClosePreview>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/observation-periods/{period_id}/close-preview", endpoint => endpoint
                .OnBind(Program.BindObservationPeriodClosePreview)
                .Parameter<Uuid>("tenant_id", PortiaHttpParameterLocation.Route, required: true)
                .Parameter<Uuid>("program_id", PortiaHttpParameterLocation.Route, required: true)
                .Parameter<Uuid>("period_id", PortiaHttpParameterLocation.Route, required: true))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Observation periods");
    app.MapPortiaGet<GetObservationPeriodClose, ObservationPeriodCloseView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/observation-period-closes/{close_id}", endpoint => endpoint
                .OnBind(Program.BindObservationPeriodCloseRead)
                .Parameter<Uuid>("tenant_id", PortiaHttpParameterLocation.Route, required: true)
                .Parameter<Uuid>("program_id", PortiaHttpParameterLocation.Route, required: true)
                .Parameter<Uuid>("close_id", PortiaHttpParameterLocation.Route, required: true))
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Observation periods");
    app.MapPortiaPut<AmendTypeIIPeriodClose, UnitResult>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/observation-periods/{period_id}/close-snapshots/{close_snapshot_id}/amendments/{command_id}", config => config.OnNativeResult()).WithNativeFirstResponse<AmendTypeIIPeriodClose>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Observation periods");
    app.MapPortiaPost<ApproveTypeIIPeriodClose, UnitResult>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/observation-periods/{period_id}/close-approvals", config => config.OnNativeResult()).WithNativeFirstResponse<ApproveTypeIIPeriodClose>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Observation periods");
    app.MapPortiaPost<FreezeTypeIIPeriodClose, UnitResult>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/observation-periods/{period_id}/close-snapshots/{close_snapshot_id}/freezes", config => config.OnNativeResult()).WithNativeFirstResponse<FreezeTypeIIPeriodClose>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Observation periods");
    app.MapPortiaPost<ProposeProgramRollover>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/rollovers/{rollover_id}/proposals", config => config.OnNativeResult()).WithNativeFirstResponse<ProposeProgramRollover>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Programs");
    app.MapPortiaPost<ApproveProgramRollover>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/rollovers/{rollover_id}/decisions", config => config.OnNativeResult()).WithNativeFirstResponse<ApproveProgramRollover>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Programs");
    app.MapPortiaPut<CreateAdvisorHandoff>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/advisor-handoffs/{handoff_id}", config => config.OnNativeResult()).WithNativeFirstResponse<CreateAdvisorHandoff>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Advisor collaboration");
    app.MapPortiaPut<ReviseAdvisorHandoffSelection>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/advisor-handoffs/{handoff_id}/selection-revisions/{command_id}", config => config.OnNativeResult()).WithNativeFirstResponse<ReviseAdvisorHandoffSelection>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Advisor collaboration");
    app.MapPortiaPut<MarkAdvisorHandoffReadyForReview>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/advisor-handoffs/{handoff_id}/ready-for-review/{command_id}", config => config.OnNativeResult()).WithNativeFirstResponse<MarkAdvisorHandoffReadyForReview>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Advisor collaboration");
    app.MapPortiaPut<CloseAdvisorHandoff>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/advisor-handoffs/{handoff_id}/closures/{command_id}", config => config.OnNativeResult()).WithNativeFirstResponse<CloseAdvisorHandoff>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Advisor collaboration");
    app.MapPortiaPut<RevokeAdvisorHandoff>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/advisor-handoffs/{handoff_id}/revocations/{command_id}", config => config.OnNativeResult()).WithNativeFirstResponse<RevokeAdvisorHandoff>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Advisor collaboration");
    app.MapPortiaPut<RespondToAdvisorFeedback>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/advisor-handoffs/{handoff_id}/feedback/{feedback_id}/responses/{command_id}", config => config.OnNativeResult()).WithNativeFirstResponse<RespondToAdvisorFeedback>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Advisor collaboration");
    app.MapPortiaPut<ResolveAdvisorFeedback>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/advisor-handoffs/{handoff_id}/feedback/{feedback_id}/resolutions/{command_id}", config => config.OnNativeResult()).WithNativeFirstResponse<ResolveAdvisorFeedback>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Advisor collaboration");
    app.MapPortiaPut<RecordAdvisorFeedback, OperationAcceptedResponse>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/advisor-handoffs/{handoff_id}/feedback/{feedback_id}",
            config => config
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
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/advisor-handoffs/{handoff_id}/feedback/{feedback_id}/corrections/{correction_id}", config => config.OnNativeResult()).WithNativeFirstResponse<CorrectAdvisorFeedback>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Advisor collaboration");
    app.MapPortiaGet<GetAdvisorRequest, AdvisorRequestView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/advisor-handoffs/{advisor_request_id}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Advisor collaboration");
    app.MapPortiaGet<ListAdvisorRequests, Cntryl.Fitz.Extensions.Page<AdvisorRequestSummaryView>>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/advisor-handoffs")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Advisor collaboration");
    app.MapPortiaGet<GetAdvisorSharedSource, AdvisorSharedSourceView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/advisor-handoffs/{handoff_id}/sources/{source_family}/{record_id}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Advisor collaboration");
    app.MapPortiaGet<PreviewAdvisorHandoff, AdvisorHandoffPreviewView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/advisor-handoffs/{handoff_id}/preview")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Advisor collaboration");
    app.MapPortiaGet<ListAdvisorFeedback, Cntryl.Fitz.Extensions.Page<AdvisorFeedbackView>>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/advisor-handoffs/{handoff_id}/feedback")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Advisor collaboration");
    app.MapPortiaGet<GetSignificantChange, SignificantChangeView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/significant-changes/{significant_change_id}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Changes");
    app.MapPortiaGet<ListSignificantChanges, Page<SignificantChangeSummaryView>>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/significant-changes")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Changes");
    app.MapPortiaGet<GetComplianceImpactAssessment, ComplianceIncidentImpactAssessmentView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/incidents/{incident_id}/impact-assessment")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Incidents");
    app.MapPortiaGet<GetComplianceIncident, ComplianceIncidentView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/incidents/{incident_id}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Incidents");
    app.MapPortiaGet<ListComplianceIncidents, Page<ComplianceIncidentSummaryView>>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/incidents")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Incidents");
    app.MapPortiaPut<RecordSignificantChange>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/significant-changes/{change_id}", config => config.OnNativeResult()).WithNativeFirstResponse<RecordSignificantChange>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Changes");
    app.MapPortiaPost<ReviseSignificantChange>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/significant-changes/{change_id}/revisions", config => config.OnNativeResult()).WithNativeFirstResponse<ReviseSignificantChange>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Changes");
    app.MapPortiaPost<AssessSignificantChange>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/significant-changes/{change_id}/assessments", config => config.OnNativeResult()).WithNativeFirstResponse<AssessSignificantChange>()
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Changes");
    app.MapMethods(
        "/api/{**path}",
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

    static Uuid ApplicationRouteId(HttpContext context, string name) =>
        Uuid.TryParse(context.Request.RouteValues[name]?.ToString(), CultureInfo.InvariantCulture, out var value)
            ? value : throw new BadHttpRequestException("Invalid route value.");

    static T ReadApplicationValue<TRequest, T>(HttpContext context, JsonElement body, JsonSerializerOptions options,
        int index, string name, bool nullable, bool hasDefault, out bool supplied)
    {
        var wireName = options.PropertyNamingPolicy?.ConvertName(name) ?? name;
        supplied = PortiaHttpBinding.HasBodyMember<TRequest, T>(body, options, index, wireName);
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
