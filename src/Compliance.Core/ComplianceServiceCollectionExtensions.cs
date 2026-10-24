using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.SourceFacts;
using Bdgrz.Compliance.Hosting.Persistence;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Criteria;
using Bdgrz.Compliance.Features.AuditPackages;
using Bdgrz.Compliance.Features.AuditPopulations;
using Bdgrz.Compliance.Features.AuditorRequests;
using Bdgrz.Compliance.Features.AdvisorCollaboration;
using Bdgrz.Compliance.Features.Changes;
using Bdgrz.Compliance.Features.Examinations;
using Bdgrz.Compliance.Features.Exports;
using Bdgrz.Compliance.Features.Incidents;
using Bdgrz.Compliance.Features.Retention;
using Bdgrz.Compliance.Features.Evidence;
using Bdgrz.Compliance.Features.MethodologyTemplates;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Responsibilities;
using Bdgrz.Compliance.Features.SystemDescription;
using Bdgrz.Compliance.Features.UserIdentities;
using Bdgrz.Compliance.Features.Work;
using Cntryl.Fitz;
using Cntryl.Portia;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bdgrz.Compliance;

/// <summary>
/// Composes the shared Compliance application for every deployment role.
/// </summary>
public static class ComplianceServiceCollectionExtensions
{
    /// <summary>
    /// Registers Compliance components and their Fitz-backed Portia infrastructure.
    /// </summary>
    public static PortiaBuilder AddCompliance(
        this IServiceCollection services,
        IConfiguration configuration,
        bool developerAuthentication = false,
        bool requireRealEmailDelivery = false)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddSingleton(new DeveloperUserRegistration(developerAuthentication));
        services.AddSingleton(PlatformOperatorAuthority.FromConfiguration(configuration, developerAuthentication));
        services.AddScoped<IPlatformOperatorAccess, EventSourcedPlatformOperatorAccess>();
        var professionalAuthorityTrust = FirmProfessionalAuthorityTrustSettings.FromConfiguration(configuration);
        services.TryAddScoped<IFirmProfessionalDutyAssertionVerifier>(_ =>
            new SignedFirmProfessionalDutyAssertionVerifier(professionalAuthorityTrust));
        services.AddSingleton(ControlDraftDiscardReleaseGate.FromConfiguration(configuration));
        services.AddSingleton(ControlActivationReleaseGate.FromConfiguration(configuration));
        services.AddSingleton(ControlLifecycleReleaseGate.FromConfiguration(configuration));
        services.AddSingleton(ApplicationGovernedDeclarationGate.FromConfiguration(configuration));
        services.AddSingleton(ApplicationGovernedRevisionGate.FromConfiguration(configuration));
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<ICriteriaCatalog>(CriteriaCatalog.Platform);
        services.AddScoped<FitzCriteriaTextOverlayDirectory>();
        services.AddScoped<ICriteriaTextOverlayDirectoryReader>(provider =>
            provider.GetRequiredService<FitzCriteriaTextOverlayDirectory>());
        services.AddScoped<ICriteriaTextOverlayDirectoryProjection>(provider =>
            provider.GetRequiredService<FitzCriteriaTextOverlayDirectory>());
        services.AddScoped<CriteriaTextOverlayReadConsistency>();
        services.AddScoped<ICriteriaTextOverlayReader, FitzCriteriaTextOverlayReader>();
        services.AddSingleton<IReactorPrincipalProvider, ComplianceReactorPrincipalProvider>();
        services.AddSingleton(ArtifactContentStoreOptions.FromConfiguration(configuration));
        services.AddSingleton<IArtifactContentStore, LocalArtifactContentStore>();
        services.AddSingleton<IArtifactInspector, UninspectedArtifactInspector>();
        services.AddScoped<EvidenceIntake>();
        services.AddScoped<EvidenceArtifactStorageReconciler>();
        services.AddScoped<ArtifactRetentionMutation>();
        services.AddScoped<ArtifactRetentionRead>();
        services.AddScoped<AuditPackageSourceBuilder>();
        var emailDeliverySettings = EmailChallengeDeliverySettings.FromConfiguration(configuration,
            requireRealEmailDelivery);
        services.AddSingleton(emailDeliverySettings);
        var workDigestDeliverySettings = WorkDigestDeliverySettings.FromConfiguration(configuration,
            requireRealEmailDelivery);
        services.AddSingleton(workDigestDeliverySettings);
        services.AddSingleton<MockWorkDigestDelivery>();
        services.AddSingleton<IWorkDigestDelivery>(provider => emailDeliverySettings.Mode == "smtp"
            ? new SmtpWorkDigestDelivery(emailDeliverySettings)
            : provider.GetRequiredService<MockWorkDigestDelivery>());
        services.AddScoped<WorkDigestContentReader>();
        services.AddScoped<IWorkDigestContentReader>(provider =>
            provider.GetRequiredService<WorkDigestContentReader>());
        services.AddScoped<WorkDigestDispatchProcessor>();
        services.AddScoped<WorkDigestDispatchSweep>();
        services.AddSingleton(EmailChallengeTokenKeys.FromConfiguration(configuration,
            emailDeliverySettings.Mode == "mock"));
        services.AddSingleton<MockEmailChallengeDelivery>();
        services.AddSingleton<IEmailChallengeDelivery>(provider =>
            emailDeliverySettings.Mode == "smtp"
                ? new SmtpEmailChallengeDelivery(emailDeliverySettings)
                : provider.GetRequiredService<MockEmailChallengeDelivery>());
        services.AddSingleton<MockTenantInvitationDelivery>();
        services.AddSingleton<ITenantInvitationDelivery>(provider =>
            emailDeliverySettings.Mode == "smtp"
                ? new SmtpTenantInvitationDelivery(emailDeliverySettings)
                : provider.GetRequiredService<MockTenantInvitationDelivery>());
        services.AddScoped<FitzEmailAddressDirectory>();
        services.AddScoped<IEmailAddressDirectoryProjection>(
            provider => provider.GetRequiredService<FitzEmailAddressDirectory>());
        services.AddScoped<IEmailAddressDirectoryReader>(
            provider => provider.GetRequiredService<FitzEmailAddressDirectory>());
        services.AddScoped<FitzUserIdentityDirectory>();
        services.AddScoped<IUserIdentityDirectoryProjection>(
            provider => provider.GetRequiredService<FitzUserIdentityDirectory>());
        services.AddScoped<IUserIdentityDirectoryReader>(
            provider => provider.GetRequiredService<FitzUserIdentityDirectory>());
        services.AddScoped<IUserIdentityDirectoryReadConsistency, IdentityDirectoryReadConsistency>();
        services.AddScoped<FitzPlatformUserDirectory>();
        services.AddScoped<IPlatformUserDirectoryProjection>(
            provider => provider.GetRequiredService<FitzPlatformUserDirectory>());
        services.AddScoped<IPlatformUserDirectoryReader>(
            provider => provider.GetRequiredService<FitzPlatformUserDirectory>());
        services.AddScoped<IUserDisplayNameReader>(
            provider => provider.GetRequiredService<FitzPlatformUserDirectory>());
        services.AddScoped<UserIdentityContinuation>();
        services.AddScoped<TenantInvitationIssuer>();
        services.AddScoped<FitzPermissionAuthorizer>();
        services.AddScoped<IPermissionProjection>(provider => provider.GetRequiredService<FitzPermissionAuthorizer>());
        services.AddScoped<IPermissionAuthorizer>(provider => provider.GetRequiredService<FitzPermissionAuthorizer>());
        services.AddScoped<IMemberAccessReader>(provider => provider.GetRequiredService<FitzPermissionAuthorizer>());
        services.AddScoped<IMemberAccessEligibility, EventSourcedMemberAccessEligibility>();
        services.AddScoped<MemberAuthorityCleanup>();
        services.AddScoped<FitzTeamDirectoryReader>();
        services.AddScoped<ITeamDirectoryProjection>(provider => provider.GetRequiredService<FitzTeamDirectoryReader>());
        services.AddScoped<ITeamDirectoryReader>(provider => provider.GetRequiredService<FitzTeamDirectoryReader>());
        services.AddScoped<FitzTeamMemberDirectoryReader>();
        services.AddScoped<ITeamMemberDirectoryProjection>(
            provider => provider.GetRequiredService<FitzTeamMemberDirectoryReader>());
        services.AddScoped<ITeamMemberDirectoryReader>(
            provider => provider.GetRequiredService<FitzTeamMemberDirectoryReader>());
        services.AddScoped<FitzTenantDirectoryReader>();
        services.AddScoped<ITenantDirectoryProjection>(provider => provider.GetRequiredService<FitzTenantDirectoryReader>());
        services.AddScoped<ITenantDirectoryReader>(provider => provider.GetRequiredService<FitzTenantDirectoryReader>());
        services.AddScoped<ITenantActivity, EventSourcedTenantActivity>();
        services.AddScoped<FitzTenantMembershipDirectoryReader>();
        services.AddScoped<ITenantMembershipDirectoryProjection>(
            provider => provider.GetRequiredService<FitzTenantMembershipDirectoryReader>());
        services.AddScoped<ITenantMembershipDirectoryReader>(
            provider => provider.GetRequiredService<FitzTenantMembershipDirectoryReader>());
        services.AddScoped<FitzTenantInvitationDirectory>();
        services.AddScoped<ITenantInvitationDirectoryProjection>(
            provider => provider.GetRequiredService<FitzTenantInvitationDirectory>());
        services.AddScoped<ITenantInvitationDirectoryReader>(
            provider => provider.GetRequiredService<FitzTenantInvitationDirectory>());
        services.AddScoped<FitzProgramDirectory>();
        services.AddScoped<ProgramDirectoryReadConsistency>();
        services.AddScoped<FitzApplicationDirectory>();
        services.AddScoped<IApplicationDirectoryProjection>(provider =>
            provider.GetRequiredService<FitzApplicationDirectory>());
        services.AddScoped<IApplicationDirectoryReader>(provider =>
            provider.GetRequiredService<FitzApplicationDirectory>());
        services.AddScoped<FitzApplicationImportDirectory>();
        services.AddScoped<IApplicationImportDirectoryProjection>(provider =>
            provider.GetRequiredService<FitzApplicationImportDirectory>());
        services.AddScoped<IApplicationImportDirectoryReader>(provider =>
            provider.GetRequiredService<FitzApplicationImportDirectory>());
        services.AddScoped<ApplicationImportReadConsistency>();
        services.AddScoped<ApplicationImportVisibilityReadConsistency>();
        services.AddScoped<FitzAccessReviewScopeDirectory>();
        services.AddScoped<IAccessReviewScopeDirectoryProjection>(provider =>
            provider.GetRequiredService<FitzAccessReviewScopeDirectory>());
        services.AddScoped<IAccessReviewScopeDirectoryReader>(provider =>
            provider.GetRequiredService<FitzAccessReviewScopeDirectory>());
        services.AddScoped<IApplicationInventoryActivity,
            EventSourcedApplicationInventoryActivity>();
        services.AddScoped<IControlApplicabilityReferenceValidator,
            GovernedControlApplicabilityReferenceValidator>();
        services.AddScoped<ApplicationHistoryReadConsistency>();
        services.AddScoped<SystemInstanceReadConsistency>();
        services.AddScoped<LegacySystemInstanceSource>();
        services.AddScoped<FitzApplicationRelationshipDirectory>();
        services.AddScoped<IApplicationRelationshipProjection>(provider =>
            provider.GetRequiredService<FitzApplicationRelationshipDirectory>());
        services.AddScoped<IApplicationRelationshipDirectory>(provider =>
            provider.GetRequiredService<FitzApplicationRelationshipDirectory>());
        services.AddScoped<ApplicationRelationshipReadConsistency>();
        services.AddScoped<FitzApplicationIncomingRelationshipDirectory>();
        services.AddScoped<IApplicationIncomingRelationshipProjection>(provider =>
            provider.GetRequiredService<FitzApplicationIncomingRelationshipDirectory>());
        services.AddScoped<IApplicationIncomingRelationshipDirectory>(provider =>
            provider.GetRequiredService<FitzApplicationIncomingRelationshipDirectory>());
        services.AddScoped<ApplicationIncomingRelationshipReadConsistency>();
        services.AddScoped<PreviewApplicationChangeHandler>();
        services.AddScoped<IApplicationChangeImpactReader>(provider =>
            provider.GetRequiredService<PreviewApplicationChangeHandler>());
        services.AddScoped<IApplicationImportRetirementImpactReader>(provider =>
            provider.GetRequiredService<PreviewApplicationChangeHandler>());
        services.AddScoped<FitzApplicationBoundaryReferenceDirectory>();
        services.AddScoped<IApplicationBoundaryReferenceProjection>(provider =>
            provider.GetRequiredService<FitzApplicationBoundaryReferenceDirectory>());
        services.AddScoped<IApplicationBoundaryReferenceDirectory>(provider =>
            provider.GetRequiredService<FitzApplicationBoundaryReferenceDirectory>());
        services.AddScoped<ApplicationBoundaryReferenceReadConsistency>();
        services.AddScoped<FitzApplicationControlDraftReferenceDirectory>();
        services.AddScoped<IApplicationControlDraftReferenceProjection>(provider =>
            provider.GetRequiredService<FitzApplicationControlDraftReferenceDirectory>());
        services.AddScoped<IApplicationControlDraftReferenceDirectory>(provider =>
            provider.GetRequiredService<FitzApplicationControlDraftReferenceDirectory>());
        services.AddScoped<ApplicationControlDraftReferenceReadConsistency>();
        services.AddScoped<FitzApplicationControlVersionReferenceDirectory>();
        services.AddScoped<IApplicationControlVersionReferenceProjection>(provider =>
            provider.GetRequiredService<FitzApplicationControlVersionReferenceDirectory>());
        services.AddScoped<IApplicationControlVersionReferenceDirectory>(provider =>
            provider.GetRequiredService<FitzApplicationControlVersionReferenceDirectory>());
        services.AddScoped<ApplicationControlVersionReferenceReadConsistency>();
        services.AddScoped<FitzApplicationPolicyReferenceDirectory>();
        services.AddScoped<IApplicationPolicyReferenceProjection>(provider =>
            provider.GetRequiredService<FitzApplicationPolicyReferenceDirectory>());
        services.AddScoped<IApplicationPolicyReferenceDirectory>(provider =>
            provider.GetRequiredService<FitzApplicationPolicyReferenceDirectory>());
        services.AddScoped<ApplicationPolicyReferenceReadConsistency>();
        services.AddScoped<FitzApplicationEvidenceSourceReferenceDirectory>();
        services.AddScoped<IApplicationEvidenceSourceReferenceProjection>(provider =>
            provider.GetRequiredService<FitzApplicationEvidenceSourceReferenceDirectory>());
        services.AddScoped<IApplicationEvidenceSourceReferenceDirectory>(provider =>
            provider.GetRequiredService<FitzApplicationEvidenceSourceReferenceDirectory>());
        services.AddScoped<ApplicationEvidenceSourceReferenceReadConsistency>();
        services.AddScoped<IApplicationEvidenceSourceAccess, ApplicationEvidenceSourceAccess>();
        services.AddScoped<IProgramDirectoryProjection>(
            provider => provider.GetRequiredService<FitzProgramDirectory>());
        services.AddScoped<IProgramDirectoryReader>(
            provider => provider.GetRequiredService<FitzProgramDirectory>());
        services.AddScoped<ProgramHistoryReadConsistency>();
        services.AddScoped<ProgramSetupWorkReadConsistency>();
        services.AddScoped<FitzControlDraftDirectoryV2>();
        services.AddScoped<IControlDraftDirectoryProjection>(provider =>
            provider.GetRequiredService<FitzControlDraftDirectoryV2>());
        services.AddScoped<IControlDraftDirectoryReader>(provider =>
            provider.GetRequiredService<FitzControlDraftDirectoryV2>());
        services.AddScoped<ControlDraftReadConsistency>();
        services.AddScoped<ControlDraftListReadConsistency>();
        services.AddScoped<FitzControlDraftHistoryDirectoryV1>();
        services.AddScoped<IControlDraftHistoryDirectoryProjection>(provider =>
            provider.GetRequiredService<FitzControlDraftHistoryDirectoryV1>());
        services.AddScoped<IControlDraftHistoryDirectoryReader>(provider =>
            provider.GetRequiredService<FitzControlDraftHistoryDirectoryV1>());
        services.AddScoped<ControlDraftHistoryReadConsistency>();
        services.AddScoped<FitzCommitmentDraftDirectory>();
        services.AddScoped<ICommitmentDraftDirectoryProjection>(provider =>
            provider.GetRequiredService<FitzCommitmentDraftDirectory>());
        services.AddScoped<ICommitmentDraftDirectoryReader>(provider =>
            provider.GetRequiredService<FitzCommitmentDraftDirectory>());
        services.AddScoped<CommitmentDraftReadConsistency>();
        services.AddScoped<CommitmentDraftListReadConsistency>();
        services.AddScoped<FitzCommitmentDraftHistoryDirectoryV1>();
        services.AddScoped<ICommitmentDraftHistoryDirectoryProjection>(provider =>
            provider.GetRequiredService<FitzCommitmentDraftHistoryDirectoryV1>());
        services.AddScoped<ICommitmentDraftHistoryDirectoryReader>(provider =>
            provider.GetRequiredService<FitzCommitmentDraftHistoryDirectoryV1>());
        services.AddScoped<CommitmentDraftHistoryReadConsistency>();
        services.AddScoped<CommitmentVersionReadConsistency>();
        services.AddScoped<CommitmentImpactService>();
        services.AddScoped<ICommitmentReferenceReader, EventSourcedCommitmentReferenceReader>();
        services.AddScoped<FitzPolicyDirectory>();
        services.AddScoped<IPolicyDirectoryProjection>(provider =>
            provider.GetRequiredService<FitzPolicyDirectory>());
        services.AddScoped<IPolicyDirectoryReader>(provider =>
            provider.GetRequiredService<FitzPolicyDirectory>());
        services.AddScoped<PolicyDirectoryReadConsistency>();
        services.AddScoped<FitzPolicyDecisionWorkItemDirectory>();
        services.AddScoped<IPolicyDecisionWorkItemProjection>(provider =>
            provider.GetRequiredService<FitzPolicyDecisionWorkItemDirectory>());
        services.AddScoped<IAccountableWorkItemDirectoryReader>(provider =>
            provider.GetRequiredService<FitzPolicyDecisionWorkItemDirectory>());
        services.AddScoped<FitzPolicyCampaignWorkItemDirectory>();
        services.AddScoped<IPolicyCampaignWorkItemProjection>(provider =>
            provider.GetRequiredService<FitzPolicyCampaignWorkItemDirectory>());
        services.AddScoped<IAccountableWorkItemDirectoryReader>(provider =>
            provider.GetRequiredService<FitzPolicyCampaignWorkItemDirectory>());
        services.AddScoped<FitzBoundaryDecisionWorkItemDirectory>();
        services.AddScoped<IBoundaryDecisionWorkItemProjection>(provider =>
            provider.GetRequiredService<FitzBoundaryDecisionWorkItemDirectory>());
        services.AddScoped<IAccountableWorkItemDirectoryReader>(provider =>
            provider.GetRequiredService<FitzBoundaryDecisionWorkItemDirectory>());
        services.AddScoped<FitzCommitmentDecisionWorkItemDirectory>();
        services.AddScoped<ICommitmentDecisionWorkItemProjection>(provider =>
            provider.GetRequiredService<FitzCommitmentDecisionWorkItemDirectory>());
        services.AddScoped<IAccountableWorkItemDirectoryReader>(provider =>
            provider.GetRequiredService<FitzCommitmentDecisionWorkItemDirectory>());
        services.AddScoped<FitzControlDecisionWorkItemDirectory>();
        services.AddScoped<IControlDecisionWorkItemProjection>(provider =>
            provider.GetRequiredService<FitzControlDecisionWorkItemDirectory>());
        services.AddScoped<IAccountableWorkItemDirectoryReader>(provider =>
            provider.GetRequiredService<FitzControlDecisionWorkItemDirectory>());
        services.AddScoped<PolicyImpactService>();
        services.AddScoped<FitzCampaignDirectory>();
        services.AddScoped<ICampaignDirectoryProjection>(provider =>
            provider.GetRequiredService<FitzCampaignDirectory>());
        services.AddScoped<ICampaignDirectoryReader>(provider =>
            provider.GetRequiredService<FitzCampaignDirectory>());
        services.AddScoped<CampaignDirectoryReadConsistency>();
        services.AddScoped<FitzPersonDirectory>();
        services.AddScoped<IPersonMemberDisplayReader>(
            provider => provider.GetRequiredService<FitzPersonDirectory>());
        services.AddScoped<IPersonDirectoryProjection>(provider =>
            provider.GetRequiredService<FitzPersonDirectory>());
        services.AddScoped<IPersonDirectoryReader>(provider =>
            provider.GetRequiredService<FitzPersonDirectory>());
        services.AddScoped<FitzWorkRelationshipDirectory>();
        services.AddScoped<IWorkRelationshipDirectoryProjection>(provider =>
            provider.GetRequiredService<FitzWorkRelationshipDirectory>());
        services.AddScoped<IWorkRelationshipDirectoryReader>(provider =>
            provider.GetRequiredService<FitzWorkRelationshipDirectory>());
        services.AddScoped<FitzTechnologyInventoryDirectory>();
        services.AddScoped<ITechnologyInventoryProjection>(provider =>
            provider.GetRequiredService<FitzTechnologyInventoryDirectory>());
        services.AddScoped<ITechnologyInventoryReader>(provider =>
            provider.GetRequiredService<FitzTechnologyInventoryDirectory>());
        services.AddScoped<ProviderReferences>();
        services.AddScoped<FitzProviderDirectory>();
        services.AddScoped<IProviderProjection>(provider => provider.GetRequiredService<FitzProviderDirectory>());
        services.AddScoped<IProviderReader>(provider => provider.GetRequiredService<FitzProviderDirectory>());
        services.AddScoped<ProviderReadConsistency>();
        services.AddScoped<ProviderChangeImpactService>();
        services.AddScoped<AssuranceReferences>();
        services.AddScoped<AssuranceDisclosure>();
        services.AddScoped<FitzAssuranceDirectory>();
        services.AddScoped<IAssuranceProjection>(provider => provider.GetRequiredService<FitzAssuranceDirectory>());
        services.AddScoped<IAssuranceReader>(provider => provider.GetRequiredService<FitzAssuranceDirectory>());
        services.AddScoped<AssuranceReadConsistency>();
        services.AddScoped<TechnologyInventoryReadConsistency>();
        services.AddScoped<FitzInventoryRegisterDirectory>();
        services.AddScoped<IInventoryRegisterProjection>(provider =>
            provider.GetRequiredService<FitzInventoryRegisterDirectory>());
        services.AddScoped<IInventoryRegisterReader>(provider =>
            provider.GetRequiredService<FitzInventoryRegisterDirectory>());
        services.AddScoped<InventoryRegisterReadConsistency>();
        services.AddScoped<TechnologyInventoryReferences>();
        services.AddScoped<ITechnologyInventoryActivity>(provider =>
            provider.GetRequiredService<TechnologyInventoryReferences>());
        services.AddScoped<PersonReadConsistency>();
        services.AddScoped<WorkRelationshipReadConsistency>();
        services.AddScoped<FitzWorkforceObservationDirectory>();
        services.AddScoped<IWorkforceObservationProjection>(provider =>
            provider.GetRequiredService<FitzWorkforceObservationDirectory>());
        services.AddScoped<IWorkforceObservationDirectoryReader>(provider =>
            provider.GetRequiredService<FitzWorkforceObservationDirectory>());
        services.AddScoped<WorkforceObservationReadConsistency>();
        services.AddScoped<FitzWorkforceObservationResolutionDirectory>();
        services.AddScoped<IWorkforceObservationResolutionProjection>(provider =>
            provider.GetRequiredService<FitzWorkforceObservationResolutionDirectory>());
        services.AddScoped<IWorkforceObservationResolutionReader>(provider =>
            provider.GetRequiredService<FitzWorkforceObservationResolutionDirectory>());
        services.AddScoped<WorkforceObservationResolutions>();
        services.AddScoped<FitzWorkforceSourceDirectory>();
        services.AddScoped<IWorkforceSourceDirectoryReader>(provider =>
            provider.GetRequiredService<FitzWorkforceSourceDirectory>());
        services.AddScoped<IWorkforceSourceProjection>(provider =>
            provider.GetRequiredService<FitzWorkforceSourceDirectory>());
        services.AddScoped<WorkforceSourceReadConsistency>();
        services.AddScoped<WorkforceSourceTargets>();
        services.AddScoped<WorkforceRosterReconciler>();
        services.AddScoped<FitzServiceIdentityDirectory>();
        services.AddScoped<IServiceIdentityDirectoryProjection>(provider =>
            provider.GetRequiredService<FitzServiceIdentityDirectory>());
        services.AddScoped<IServiceIdentityDirectoryReader>(provider =>
            provider.GetRequiredService<FitzServiceIdentityDirectory>());
        services.AddScoped<ServiceIdentityReadConsistency>();
        services.AddScoped<ServiceIdentityOwnership>();
        services.AddScoped<FitzRiskDraftDirectory>();
        services.AddScoped<IRiskDraftDirectoryProjection>(provider =>
            provider.GetRequiredService<FitzRiskDraftDirectory>());
        services.AddScoped<IRiskDraftDirectoryReader>(provider =>
            provider.GetRequiredService<FitzRiskDraftDirectory>());
        services.AddScoped<RiskDraftReadConsistency>();
        services.AddScoped<RiskDraftListReadConsistency>();
        services.AddScoped<FitzRiskDraftHistoryDirectoryV1>();
        services.AddScoped<IRiskDraftHistoryDirectoryProjection>(provider =>
            provider.GetRequiredService<FitzRiskDraftHistoryDirectoryV1>());
        services.AddScoped<IRiskDraftHistoryDirectoryReader>(provider =>
            provider.GetRequiredService<FitzRiskDraftHistoryDirectoryV1>());
        services.AddScoped<RiskDraftHistoryReadConsistency>();
        services.AddScoped<FitzRiskEvaluationDirectory>();
        services.AddScoped<IRiskEvaluationDirectoryProjection>(provider =>
            provider.GetRequiredService<FitzRiskEvaluationDirectory>());
        services.AddScoped<IRiskEvaluationDirectoryReader>(provider =>
            provider.GetRequiredService<FitzRiskEvaluationDirectory>());
        services.AddScoped<RiskEvaluationReadConsistency>();
        services.AddScoped<RiskAcceptanceWorkItemDirectory>();
        services.AddScoped<IAccountableWorkItemDirectoryReader>(provider =>
            provider.GetRequiredService<RiskAcceptanceWorkItemDirectory>());
        services.AddScoped<FitzControlMappingDirectoryV1>();
        services.AddScoped<IControlMappingDirectoryProjection>(provider =>
            provider.GetRequiredService<FitzControlMappingDirectoryV1>());
        services.AddScoped<IControlMappingDirectoryReader>(provider =>
            provider.GetRequiredService<FitzControlMappingDirectoryV1>());
        services.AddScoped<FitzCriterionApplicabilityDirectoryV1>();
        services.AddScoped<ICriterionApplicabilityDirectoryProjection>(provider =>
            provider.GetRequiredService<FitzCriterionApplicabilityDirectoryV1>());
        services.AddScoped<ICriterionApplicabilityDirectoryReader>(provider =>
            provider.GetRequiredService<FitzCriterionApplicabilityDirectoryV1>());
        services.AddScoped<CriteriaCoverageReadConsistency>();
        services.AddScoped<FitzClientServiceDirectory>();
        services.AddScoped<IClientServiceDirectoryProjection>(
            provider => provider.GetRequiredService<FitzClientServiceDirectory>());
        services.AddScoped<IClientServiceDirectoryReader>(
            provider => provider.GetRequiredService<FitzClientServiceDirectory>());
        services.AddScoped<ClientServiceHistoryReadConsistency>();
        services.AddScoped<IClientServiceActivity, EventSourcedClientServiceActivity>();
        services.AddScoped<FitzBoundaryDirectory>();
        services.AddScoped<IBoundaryDirectoryProjection>(
            provider => provider.GetRequiredService<FitzBoundaryDirectory>());
        services.AddScoped<IBoundaryDirectoryReader>(
            provider => provider.GetRequiredService<FitzBoundaryDirectory>());
        services.AddScoped<FitzEvidenceWorkItemDirectory>();
        services.AddScoped<IEvidenceWorkItemProjection>(
            provider => provider.GetRequiredService<FitzEvidenceWorkItemDirectory>());
        services.AddScoped<IAccountableWorkItemDirectoryReader>(
            provider => provider.GetRequiredService<FitzEvidenceWorkItemDirectory>());
        services.AddScoped<FitzFindingClosureWorkItemDirectory>();
        services.AddScoped<IFindingClosureWorkItemProjection>(
            provider => provider.GetRequiredService<FitzFindingClosureWorkItemDirectory>());
        services.AddScoped<IAccountableWorkItemDirectoryReader>(
            provider => provider.GetRequiredService<FitzFindingClosureWorkItemDirectory>());
        services.AddScoped<FitzCorrectiveActionWorkItemDirectory>();
        services.AddScoped<ICorrectiveActionWorkItemProjection>(
            provider => provider.GetRequiredService<FitzCorrectiveActionWorkItemDirectory>());
        services.AddScoped<IAccountableWorkItemDirectoryReader>(
            provider => provider.GetRequiredService<FitzCorrectiveActionWorkItemDirectory>());
        services.AddScoped<FitzRiskGovernanceWorkItemDirectory>();
        services.AddScoped<IRiskGovernanceWorkItemProjection>(
            provider => provider.GetRequiredService<FitzRiskGovernanceWorkItemDirectory>());
        services.AddScoped<IAccountableWorkItemDirectoryReader>(
            provider => provider.GetRequiredService<FitzRiskGovernanceWorkItemDirectory>());
        services.AddScoped<FitzControlEvaluationWorkItemDirectory>();
        services.AddScoped<IControlEvaluationWorkItemProjection>(
            provider => provider.GetRequiredService<FitzControlEvaluationWorkItemDirectory>());
        services.AddScoped<IAccountableWorkItemDirectoryReader>(
            provider => provider.GetRequiredService<FitzControlEvaluationWorkItemDirectory>());
        services.AddScoped<FitzControlOperatingPlanWorkItemDirectory>();
        services.AddScoped<IControlOperatingPlanWorkItemProjection>(
            provider => provider.GetRequiredService<FitzControlOperatingPlanWorkItemDirectory>());
        services.AddScoped<IAccountableWorkItemDirectoryReader>(
            provider => provider.GetRequiredService<FitzControlOperatingPlanWorkItemDirectory>());
        services.AddScoped<FitzControlOccurrenceWorkItemDirectory>();
        services.AddScoped<IControlOccurrenceWorkItemProjection>(
            provider => provider.GetRequiredService<FitzControlOccurrenceWorkItemDirectory>());
        services.AddScoped<IAccountableWorkItemDirectoryReader>(
            provider => provider.GetRequiredService<FitzControlOccurrenceWorkItemDirectory>());
        services.AddScoped<FitzControlMappingWorkItemDirectory>();
        services.AddScoped<IControlCriterionMappingWorkItemProjection>(
            provider => provider.GetRequiredService<FitzControlMappingWorkItemDirectory>());
        services.AddScoped<IAccountableWorkItemDirectoryReader>(
            provider => provider.GetRequiredService<FitzControlMappingWorkItemDirectory>());
        services.AddScoped<FitzCriterionApplicabilityWorkItemDirectory>();
        services.AddScoped<ICriterionApplicabilityWorkItemProjection>(
            provider => provider.GetRequiredService<FitzCriterionApplicabilityWorkItemDirectory>());
        services.AddScoped<IAccountableWorkItemDirectoryReader>(
            provider => provider.GetRequiredService<FitzCriterionApplicabilityWorkItemDirectory>());
        services.AddScoped<FitzSnapshotDirectory>();
        services.AddScoped<ISnapshotDirectoryProjection>(
            provider => provider.GetRequiredService<FitzSnapshotDirectory>());
        services.AddScoped<ISnapshotDirectoryReader>(
            provider => provider.GetRequiredService<FitzSnapshotDirectory>());
        services.AddScoped<ScopeSnapshotFreezer>();
        services.AddScoped<FitzAccessPopulationDirectory>();
        services.AddScoped<IAccessPopulationDirectoryProjection>(
            provider => provider.GetRequiredService<FitzAccessPopulationDirectory>());
        services.AddScoped<IAccessPopulationDirectoryReader>(
            provider => provider.GetRequiredService<FitzAccessPopulationDirectory>());
        services.AddScoped<ApplicationAccessPopulationReadConsistency>();
        services.AddScoped<FitzAccessReviewCampaignImpactDirectory>();
        services.AddScoped<IAccessReviewCampaignImpactProjection>(
            provider => provider.GetRequiredService<FitzAccessReviewCampaignImpactDirectory>());
        services.AddScoped<IAccessReviewCampaignImpactDirectoryReader>(
            provider => provider.GetRequiredService<FitzAccessReviewCampaignImpactDirectory>());
        services.AddScoped<ApplicationReviewCampaignReadConsistency>();
        services.AddScoped<FitzAccessReviewCampaignDirectory>();
        services.AddScoped<IAccessReviewCampaignDirectoryProjection>(
            provider => provider.GetRequiredService<FitzAccessReviewCampaignDirectory>());
        services.AddScoped<IAccessReviewCampaignDirectoryReader>(
            provider => provider.GetRequiredService<FitzAccessReviewCampaignDirectory>());
        services.AddScoped<FitzAccessReviewCampaignWorkItemDirectory>();
        services.AddScoped<IAccessReviewCampaignWorkItemProjection>(provider =>
            provider.GetRequiredService<FitzAccessReviewCampaignWorkItemDirectory>());
        services.AddScoped<IAccountableWorkItemDirectoryReader>(provider =>
            provider.GetRequiredService<FitzAccessReviewCampaignWorkItemDirectory>());
        services.AddScoped<IAccessReviewSources, GovernedAccessReviewSources>();
        services.AddScoped<AccessReviewQueueEligibility>();
        services.AddScoped<FitzPopulationSnapshotDirectory>();
        services.AddScoped<IPopulationSnapshotDirectoryProjection>(
            provider => provider.GetRequiredService<FitzPopulationSnapshotDirectory>());
        services.AddScoped<IPopulationSnapshotDirectoryReader>(
            provider => provider.GetRequiredService<FitzPopulationSnapshotDirectory>());
        services.AddScoped<PopulationSnapshotDirectoryReadConsistency>();
        services.AddScoped<PopulationSnapshotFreezer>();
        services.TryAddSingleton(TenantActivationPolicy.Default);
        services.AddScoped<TenantManagerGuardBootstrap>();
        services.AddScoped<TenantManagerGuardLoader>();
        services.AddScoped<AdministratorTransitionAdmission>();
        services.AddScoped<AdministratorReceiverSupport>();
        services.AddScoped<IdentityRecoveryReceiverSupport>();
        services.AddScoped<WorkforceRosterSnapshotter>();
        services.AddScoped<BoundaryHistoryReadConsistency>();
        services.AddScoped<WorkQueueReadConsistency>();
        services.AddScoped<IBoundaryImpactContributor, ProgramBoundaryImpactContributor>();
        services.AddScoped<IBoundaryImpactContributor, ControlBoundaryImpactContributor>();
        services.AddScoped<FitzResponsibilitySetDirectory>();
        services.AddScoped<FitzMemberResponsibilityIndex>();
        services.AddScoped<IMemberResponsibilityIndex>(provider =>
            provider.GetRequiredService<FitzMemberResponsibilityIndex>());
        services.AddScoped<IResponsibilitySetDirectory>(provider =>
            provider.GetRequiredService<FitzResponsibilitySetDirectory>());
        services.AddScoped<IResponsibilitySetProjection>(provider =>
            provider.GetRequiredService<FitzResponsibilitySetDirectory>());
        services.AddScoped<FitzAccessGrantDirectory>();
        services.AddScoped<IAccessGrantProposalValidator, AccessGrantProposalValidator>();
        services.AddScoped<IAccessGrantDirectory>(provider =>
            provider.GetRequiredService<FitzAccessGrantDirectory>());
        services.AddScoped<IAccessGrantProjection>(provider =>
            provider.GetRequiredService<FitzAccessGrantDirectory>());
        services.AddScoped<AccessGrantPermissionAuthorizer>();
        services.AddScoped<IAccessGrantPermissionAuthorizer>(provider =>
            provider.GetRequiredService<AccessGrantPermissionAuthorizer>());
        services.AddScoped<IAccessGrantScopePermissionAuthorizer>(provider =>
            provider.GetRequiredService<AccessGrantPermissionAuthorizer>());
        services.AddScoped<IAccessGrantRolePermissionAuthorizer>(provider =>
            provider.GetRequiredService<AccessGrantPermissionAuthorizer>());
        services.AddScoped<RestrictedApplicationVisibility>();
        services.AddScoped<TechnologyInventoryRestrictedVisibility>();
        services.AddScoped<OperatingAuthority>();
        services.AddScoped<ManagementExecutiveAuthority>();
        services.AddScoped<ProgramRolloverApproverAuthority>();
        services.AddScoped<ClientManagementIndependenceGuard>();
        services.AddScoped<ClientCompartmentIndependenceGuard>();
        services.AddScoped<ProfessionalAdvisoryReadAccess>();
        services.AddScoped<IAdvisorSharedSourceAuthorization>(provider =>
            provider.GetRequiredService<ProfessionalAdvisoryReadAccess>());
        services.AddScoped<IAdvisorHandoffListAccess>(provider =>
            provider.GetRequiredService<ProfessionalAdvisoryReadAccess>());
        services.AddScoped<PolicyAcknowledgementRecorderGuard>();
        services.AddScoped<WorkQueueReader>();
        services.AddScoped<FindingReadSource>();
        services.AddScoped<FindingHistoryMigrationStatusReader>();
        services.AddScoped<WeeklyWorkDigestRecipientResolver>();
        services.AddScoped<IProgramResourceScopeResolver, ProgramResourceScopeResolver>();
        services.AddScoped<BoundaryResponsibilityScopeValidator>();
        services.AddScoped<ControlResponsibilityScopeValidator>();
        services.AddScoped<CommitmentResponsibilityScopeValidator>();
        services.AddScoped<ControlActivationSource>();
        services.AddScoped<ControlImpactService>();
        services.AddScoped<IControlImpactContributor, ApplicabilityControlImpactContributor>();
        services.AddScoped<IControlImpactContributor, ResponsibilityControlImpactContributor>();
        services.AddScoped<IControlImpactContributor, MappingControlImpactContributor>();
        services.AddScoped<IControlImpactContributor, RiskTreatmentControlImpactContributor>();
        services.AddScoped<IControlImpactContributor, ReadinessControlImpactContributor>();
        services.AddScoped<IControlImpactContributor, EvidenceControlImpactContributor>();
        services.AddScoped<IControlImpactContributor, WorkControlImpactContributor>();
        services.AddScoped<FitzReadinessDirectory>();
        services.AddScoped<IReadinessDirectoryReader>(provider =>
            provider.GetRequiredService<FitzReadinessDirectory>());
        services.AddScoped<IReadinessDirectoryProjection>(provider =>
            provider.GetRequiredService<FitzReadinessDirectory>());
        services.AddScoped<FitzExaminationEngagementDirectory>();
        services.AddScoped<IExaminationEngagementDirectoryReader>(provider =>
            provider.GetRequiredService<FitzExaminationEngagementDirectory>());
        services.AddScoped<FitzSignificantChangeDirectory>();
        services.AddScoped<ISignificantChangeDirectoryProjection>(provider =>
            provider.GetRequiredService<FitzSignificantChangeDirectory>());
        services.AddScoped<ISignificantChangeDirectoryReader>(provider =>
            provider.GetRequiredService<FitzSignificantChangeDirectory>());
        services.AddScoped<IChangeImpactWorkOwnershipReader, AccountableWorkItemOwnershipReader>();
        services.AddScoped<ISystemDescriptionDecisionCorrelationReader,
            AggregateSystemDescriptionDecisionCorrelationReader>();
        services.AddScoped<RestrictedComplianceRecordVisibility>();
        services.AddScoped<FitzComplianceIncidentDirectory>();
        services.AddScoped<IComplianceIncidentDirectoryProjection>(provider =>
            provider.GetRequiredService<FitzComplianceIncidentDirectory>());
        services.AddScoped<IComplianceIncidentDirectoryReader>(provider =>
            provider.GetRequiredService<FitzComplianceIncidentDirectory>());
        services.AddScoped<IExaminationEngagementDirectoryProjection>(provider =>
            provider.GetRequiredService<FitzExaminationEngagementDirectory>());
        services.AddScoped<ITypeIBaselineSourceContributor, BoundaryTypeIBaselineSourceContributor>();
        services.AddScoped<ITypeIBaselineSourceContributor, CriteriaTypeIBaselineSourceContributor>();
        services.AddScoped<ITypeIBaselineSourceContributor, ApplicationTypeIBaselineSourceContributor>();
        services.AddScoped<ITypeIBaselineSourceContributor, ReviewedSystemTypeIBaselineSourceContributor>();
        services.AddScoped<ITypeIBaselineSourceContributor, TechnologyComponentTypeIBaselineSourceContributor>();
        services.AddScoped<ITypeIBaselineSourceContributor, InformationAssetTypeIBaselineSourceContributor>();
        services.AddScoped<ITypeIBaselineSourceContributor, DataFlowTypeIBaselineSourceContributor>();
        services.AddScoped<CommitmentTypeIBaselineSourceReader>();
        services.AddScoped<IProviderCsocBaselineReader>(provider =>
            provider.GetRequiredService<CommitmentTypeIBaselineSourceReader>());
        services.AddScoped<ITypeIBaselineSourceContributor, CommitmentTypeIBaselineSourceContributor>();
        services.AddScoped<ITypeIBaselineSourceContributor, SystemRequirementTypeIBaselineSourceContributor>();
        services.AddScoped<ITypeIBaselineSourceContributor, ControlTypeIBaselineSourceContributor>();
        services.AddScoped<ITypeIBaselineSourceContributor, ControlCriterionMappingTypeIBaselineSourceContributor>();
        services.AddScoped<ITypeIBaselineSourceContributor, ControlEvaluationTypeIBaselineSourceContributor>();
        services.AddScoped<ITypeIBaselineSourceContributor, PolicyTypeIBaselineSourceContributor>();
        services.AddScoped<ITypeIBaselineSourceContributor, PolicyCommunicationTypeIBaselineSourceContributor>();
        services.AddScoped<ITypeIBaselineSourceContributor, AccessExpectationsTypeIBaselineSourceContributor>();
        services.AddScoped<ITypeIBaselineSourceContributor, AccessReviewCampaignsTypeIBaselineSourceContributor>();
        services.AddScoped<ITypeIBaselineSourceContributor, RiskTypeIBaselineSourceContributor>();
        services.AddScoped<ITypeIBaselineSourceContributor, ProviderTypeIBaselineSourceContributor>();
        services.AddScoped<ITypeIBaselineSourceContributor, EvidenceGovernanceTypeIBaselineSourceContributor>();
        services.AddScoped<ITypeIBaselineSourceContributor, FindingTypeIBaselineSourceContributor>();
        services.AddScoped<ITypeIBaselineSourceContributor, WorkforceTypeIBaselineSourceContributor>();
        services.AddScoped<ITypeIBaselinePreviewReader, TypeIBaselinePreviewReader>();
        services.AddScoped<IReadinessReadModel, FitzReadinessReadModel>();
        services.AddScoped<ReadinessProjectionReadConsistency>();
        services.AddScoped<IApplicationReadinessImpactReader, ApplicationReadinessImpactReader>();
        services.AddScoped<IReadinessSourceReader, DirectoryReadinessSourceReader>();
        services.AddScoped<IResponsibilityScopeValidator, SourceRecordResponsibilityScopeValidator>();
        services.AddScoped<BoundaryImpactService>();
        services.AddScoped<IBoundaryReferenceValidator,
            GovernedBoundaryReferenceValidator>();
        services.AddScoped<FitzRoleDirectoryReader>();
        services.AddScoped<IRoleDirectoryProjection>(provider => provider.GetRequiredService<FitzRoleDirectoryReader>());
        services.AddScoped<IRoleDirectoryReader>(provider => provider.GetRequiredService<FitzRoleDirectoryReader>());
        services.AddScoped<FitzRolePermissionDirectoryReader>();
        services.AddScoped<IRolePermissionDirectoryProjection>(
            provider => provider.GetRequiredService<FitzRolePermissionDirectoryReader>());
        services.AddScoped<IRolePermissionDirectoryReader>(
            provider => provider.GetRequiredService<FitzRolePermissionDirectoryReader>());
        services.AddScoped<FitzRoleTeamDirectoryReader>();
        services.AddScoped<IRoleTeamDirectoryProjection>(provider => provider.GetRequiredService<FitzRoleTeamDirectoryReader>());
        services.AddScoped<IRoleTeamDirectoryReader>(provider => provider.GetRequiredService<FitzRoleTeamDirectoryReader>());
        services.AddScoped<FitzTeamRoleDirectoryReader>();
        services.AddScoped<ITeamRoleDirectoryProjection>(provider => provider.GetRequiredService<FitzTeamRoleDirectoryReader>());
        services.AddScoped<ITeamRoleDirectoryReader>(provider => provider.GetRequiredService<FitzTeamRoleDirectoryReader>());
        services.AddScoped<SeparationOfDutiesWaiverAuthorizer>();
        services.AddScoped<EvidenceRedactionWaiverRead>();
        services.AddScoped<EvidenceRedactionRead>();
        services.AddScoped<EvidenceRedactionSources>();
        services.AddScoped<EvidenceRedactionAccess>();
        services.AddScoped<EvidenceArtifactReadAccess>();
        services.AddScoped<EvidenceArtifactMetadataRead>();
        services.AddScoped<EvidenceSupportTargetSource>();
        services.AddScoped<IAdvisorRecordVersionResolver, AdvisorRecordVersionResolver>();
        services.AddScoped<IAdvisorSharedSourceReader, AdvisorSharedSourceReader>();
        services.AddScoped<FitzAdvisorHandoffDirectory>();
        services.AddScoped<IAdvisorHandoffDirectoryReader>(provider =>
            provider.GetRequiredService<FitzAdvisorHandoffDirectory>());
        services.AddScoped<IAdvisorHandoffDirectoryProjection>(provider =>
            provider.GetRequiredService<FitzAdvisorHandoffDirectory>());
        services.AddScoped<AdvisorHandoffListReadConsistency>();
        services.AddScoped<FitzAdvisorFeedbackWorkItemDirectory>();
        services.AddScoped<IAdvisorFeedbackWorkItemProjection>(provider =>
            provider.GetRequiredService<FitzAdvisorFeedbackWorkItemDirectory>());
        services.AddScoped<IAccountableWorkItemDirectoryReader>(provider =>
            provider.GetRequiredService<FitzAdvisorFeedbackWorkItemDirectory>());
        services.AddScoped<AuditorResponseEvidence>();
        services.AddScoped<DirectoryReevaluationDiscovery>();
        services.AddScoped<ProfessionalDutyAdministrationAuthorizer>();
        services.AddScoped<ProfessionalDutyAuthorityReader>();
        services.AddScoped<IndependenceRuleRatificationAuthorizer>();
        services.AddScoped<ProfessionalEngagementPartnerAuthorizer>();
        services.AddScoped<CurrentRatifiedIndependenceRulesReader>();
        services.TryAddScoped<IServiceEngagementAcceptanceEvidenceReader,
            EventSourcedServiceEngagementAcceptanceEvidenceReader>();
        services.AddSingleton<IImmutableContentStore>(provider =>
            new FitzKvImmutableContentStore(provider.GetRequiredService<IKvClient>()));
        services.AddScoped<NativeOperationExecutor>();
        services.AddSingleton<IProcessRecordStore>(provider =>
            new FitzKvProcessRecordStore(provider.GetRequiredService<IKvClient>()));
        services.AddSingleton<ProcessCatalog>(provider =>
            new ProcessCatalog(provider.GetServices<ProcessDefinition>()));
        services.AddScoped<ProcessDriver>();
        services.AddScoped<ProcessRootReaction>();
        // ProcessSweep is a hosted service of the worker roles (AddWorkers); it resumes overdue process records.
        services.AddSingleton(AdministratorTransitionProcess.Definition);
        services.AddSingleton(IdentityRecoveryProcess.Definition);
