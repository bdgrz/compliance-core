using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NamedRecords;
using Bdgrz.Compliance.Features.Independence;
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
        services.AddScoped<PlatformOperatorRosterOperations>();
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
        services.AddScoped<AuditPackageReader>();
        services.AddScoped<FitzAuditPackageDirectory>();
        services.AddScoped<IAuditPackageDirectoryReader>(provider => provider.GetRequiredService<FitzAuditPackageDirectory>());
        services.AddScoped<IAuditPackageDirectoryProjection>(provider => provider.GetRequiredService<FitzAuditPackageDirectory>());
        services.AddScoped<AuditPackageDirectoryReadConsistency>();
        services.AddScoped<AuditPackageRootOperation>();
        services.AddScoped<AuditPackageSourceVerifier>();
        services.AddScoped<AuditPackageArchiveBuilder>();
        services.AddSingleton<IAuditPackageBuildOutputs, FitzKvAuditPackageBuildOutputs>();
        services.AddSingleton(AuditPackageBuildProcess.Definition());
        services.AddSingleton(AuditPackageDeliveryRecordingProcess.Definition());
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
        services.AddScoped<WorkDigestHistoryReader>();
        services.AddScoped<WorkDigestRetryOperations>();
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
        services.AddScoped<TenantInvitationAdmission>();
        services.AddScoped<TenantInvitationMailboxes>();
        services.AddScoped<TenantInvitationDeliverySupport>();
        services.AddSingleton<ITenantInvitationDeliveryStore>(provider =>
            new FitzKvTenantInvitationDeliveryStore(provider.GetRequiredService<IKvClient>()));
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
        services.AddScoped<ControlReader>();
        services.AddScoped<ControlOperations>();
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
        services.AddScoped<CommitmentReader>();
        services.AddScoped<CommitmentOperations>();
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
        services.AddScoped<PolicyReader>();
        services.AddScoped<PolicyOperations>();
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
        services.AddScoped<ProviderSource>();
        services.AddScoped<ProviderGapReader>();
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
        services.AddScoped<AssuranceHistoryReader>();
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
        services.AddScoped<RiskDraftReader>();
        services.AddScoped<RiskDraftOperations>();
        services.AddScoped<RiskEvaluationReader>();
        services.AddScoped<RiskEvaluationOperations>();
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
        services.AddScoped<BoundaryReader>();
        services.AddScoped<BoundaryOperations>();
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
        services.AddScoped<AccessExpectationsReader>();
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
        services.AddScoped<NamedRecordAdmission>();
        services.AddScoped<NamedRecordReceivers>();
        services.AddScoped<NamedRecordReactions>();
        services.AddScoped<ReceiverAppend>();
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
        services.AddScoped<FindingReader>();
        services.AddScoped<WeeklyWorkDigestRecipientResolver>();
        services.AddScoped<WeeklyDigestOperations>();
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
        services.AddScoped<ExaminationEngagementReader>();
        services.AddScoped<ExaminationEngagementOperations>();
        services.AddScoped<FitzExaminationEngagementDirectory>();
        services.AddScoped<IExaminationEngagementDirectoryReader>(provider =>
            provider.GetRequiredService<FitzExaminationEngagementDirectory>());
        services.AddScoped<SignificantChangeReader>();
        services.AddScoped<SignificantChangeOperations>();
        services.AddScoped<SignificantChangeDirectoryReadConsistency>();
        services.AddScoped<FitzSignificantChangeDirectory>();
        services.AddScoped<ISignificantChangeDirectoryProjection>(provider =>
            provider.GetRequiredService<FitzSignificantChangeDirectory>());
        services.AddScoped<ISignificantChangeDirectoryReader>(provider =>
            provider.GetRequiredService<FitzSignificantChangeDirectory>());
        services.AddScoped<IChangeImpactWorkOwnershipReader, AccountableWorkItemOwnershipReader>();
        services.AddScoped<SystemDescriptionReader>();
        services.AddScoped<SystemDescriptionOperations>();
        services.AddScoped<SystemDescriptionSnapshotSources>();
        services.AddScoped<SystemDescriptionSourceResolver>();
        services.AddScoped<SystemDescriptionReadConsistency>();
        services.AddSingleton<ISystemDescriptionExportProcesses, FitzKvSystemDescriptionExportProcesses>();
        services.AddScoped<SystemDescriptionExportProcess>();
        services.AddScoped<ISystemDescriptionDecisionCorrelationReader,
            AggregateSystemDescriptionDecisionCorrelationReader>();
        services.AddScoped<RestrictedComplianceRecordVisibility>();
        services.AddScoped<ComplianceIncidentReader>();
        services.AddScoped<IncidentOperations>();
        services.AddScoped<ComplianceIncidentReadConsistency>();
        services.AddScoped<ComplianceIncidentDirectoryReadConsistency>();
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
        services.AddScoped<TypeIBaselinePreviewReader>();
        services.AddScoped<ITypeIBaselinePreviewReader, PublishedTypeIBaselinePreviewReader>();
        services.AddScoped<PublishedTypeIBaselinePreviewReader>();
        services.AddScoped<TypeIIPeriodCloseHistory>();
        services.AddScoped<TypeIIPeriodCloseSourcePublication>();
        services.AddScoped<ITypeIIPeriodCloseReconciliation, TypeIIPeriodCloseReconciliation>();
        services.AddScoped<ReadinessReader>();
        services.AddScoped<ReadinessVerifier>();
        services.AddScoped<ReadinessDecisionFences>();
        services.AddScoped<ReadinessDecisionAdmission>();
        services.AddScoped<ReadinessDecisionCompleter>();
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
        services.AddScoped<AdvisorHandoffReader>();
        services.AddScoped<AdvisorFeedbackReader>();
        services.AddScoped<AdvisorUnitOperation>();
        services.AddSingleton<IChildMaterializationReceiver, AdvisorFeedbackMaterializationReceiver>();
        services.AddScoped<FitzAdvisorFeedbackWorkItemDirectory>();
        services.AddScoped<IAdvisorFeedbackWorkItemProjection>(provider =>
            provider.GetRequiredService<FitzAdvisorFeedbackWorkItemDirectory>());
        services.AddScoped<IAccountableWorkItemDirectoryReader>(provider =>
            provider.GetRequiredService<FitzAdvisorFeedbackWorkItemDirectory>());
        services.AddScoped<AuditorResponseEvidence>();
        services.AddScoped<DirectoryReevaluationDiscovery>();
        services.AddScoped<ProfessionalDutyAdministrationAuthorizer>();
        services.AddScoped<ProfessionalDutyAuthorityReader>();
        services.AddScoped<ProfessionalDutyReader>();
        services.AddScoped<IndependenceRuleRatificationAuthorizer>();
        services.AddScoped<ProfessionalEngagementPartnerAuthorizer>();
        services.AddScoped<IndependenceRuleCatalogReader>();
        services.AddScoped<IndependenceRuleRatificationReader>();
        services.AddScoped<IndependenceRuleCatalogOperations>();
        services.AddScoped<IndependenceRuleRatificationOperations>();
        services.AddScoped<CurrentRatifiedIndependenceRulesReader>();
        services.TryAddScoped<IServiceEngagementAcceptanceEvidenceReader,
            EventSourcedServiceEngagementAcceptanceEvidenceReader>();
        services.AddSingleton<IImmutableContentStore>(provider =>
            new FitzKvImmutableContentStore(provider.GetRequiredService<IKvClient>()));
        services.AddSingleton(ProtectedResponseKeyRing.FromConfiguration(configuration));
        services.AddSingleton<IProtectedResponseContentStore, AesProtectedResponseContentStore>();
        services.AddScoped<NativeOperationExecutor>();
        services.AddScoped<TenantLifecycleOperations>();
        services.AddScoped<MethodologyTemplateContributionOperations>();
        services.AddScoped<FitzMethodologyTemplateDirectory>();
        services.AddScoped<IMethodologyTemplateDirectoryReader>(provider => provider.GetRequiredService<FitzMethodologyTemplateDirectory>());
        services.AddScoped<IMethodologyTemplateDirectoryProjection>(provider => provider.GetRequiredService<FitzMethodologyTemplateDirectory>());
        services.AddScoped<MethodologyTemplateDirectoryReadConsistency>();
        services.AddScoped<TemplateApplicationReader>()
            .AddScoped<TemplateApplicationOperations>()
            .AddScoped<MethodologyTemplateReader>();
        services.AddScoped<MethodologyTemplateOperations>();
        services.AddScoped<MethodologyTemplateContributionReader>();
        services.AddScoped<FollowUpCompletionVerifier>();
        services.AddSingleton<IProcessRecordStore>(provider =>
            new FitzKvProcessRecordStore(provider.GetRequiredService<IKvClient>()));
        services.AddSingleton<ProcessCatalog>(provider =>
            new ProcessCatalog(provider.GetServices<ProcessDefinition>()));
        services.AddScoped<ProcessDriver>();
        services.AddScoped<ProcessRootReaction>();
        // ProcessSweep is a hosted service of the worker roles (AddWorkers); it resumes overdue process records.
        services.AddSingleton(AdministratorTransitionProcess.Definition);
        services.AddSingleton(IdentityRecoveryProcess.Definition);
        services.AddSingleton(EmailChallengeDeliveryProcess.Definition);
        services.AddSingleton<IEmailChallengeDeliveryLedger>(provider =>
            new FitzKvEmailChallengeDeliveryLedger(provider.GetRequiredService<IKvClient>()));
        services.AddScoped<EmailChallengeDeliveryWorker>();
        services.AddSingleton(FirmStaffRegistrationProcess.Definition());
        services.AddScoped<FirmStaffViews>();
        services.AddSingleton(ClientIndependencePreviewProcess.Definition());
        services.AddSingleton(EngagementPersonalDecisionProcess.Definition());
        services.AddScoped<EngagementPersonalDecisionAdmission>();
        services.AddScoped<EngagementPersonalDecisionVerifier>();
        services.AddSingleton(EngagementAcceptanceProcess.Definition());
        services.AddScoped<EngagementAcceptanceAdmission>();
        services.AddScoped<EngagementAcceptanceVerifier>();
        services.AddScoped<EngagementUseVerifier>();
        services.AddScoped<EngagementSourcePins>();
        services.AddScoped<EngagementCompletion>();
        services.AddScoped<EngagementRejection>();
        services.AddScoped<PracticeIndependenceWall>();
        services.AddScoped<EngagementPersonalDecisionViews>();
        services.AddScoped<ServiceEngagementCommands>();
        services.AddScoped<ServiceEngagementViews>();
        services.AddScoped<FirmStaffRegistrationReceivers>();
        services.AddSingleton(ProfessionalDutyChangeProcess.Definition());
        services.AddSingleton(TypeIIRangeProcess.Definition());
        services.AddSingleton(ControlOccurrenceFanOut.Definition());
        services.AddSingleton(ReadinessRunProcess.Definition());
        services.AddSingleton(ReadinessDecisionProcess.Definition());
        services.AddSingleton(ApplicationImportExecutionProcess.Definition());
        services.AddScoped<ApplicationImportVerifier>();
        services.AddScoped<ApplicationImportReceivers>();
        services.AddSingleton(AccessReviewLaunchFanOut.Definition());
        services.AddSingleton(AccessReviewCompletionProcess.Definition());
        services.AddSingleton(BulkAccessDecisionProcess.Definition());
        services.AddScoped<AccessReviewBulkVerifier>();
        services.AddScoped<AccessReviewBulkFences>();
        services.AddSingleton<AccessReviewManifests>();
        services.AddScoped<AccessReviewReader>();
        services.AddScoped<AccessReviewCompletionVerifier>();
        services.AddScoped<AccessReviewCompletionFences>();
        services.AddSingleton(FindingClosureProcess.Definition());
        services.AddSingleton<IChildMaterializationReceiver, Features.Remediation.IndependentChildMaterialization>();
        services.AddSingleton<IChildMaterializationReceiver, Features.PolicyDistribution.IndependentChildMaterialization>();
        services.AddScoped<AuditorRequestReader>();
        services.AddScoped<FitzAuditorRequestDirectory>();
        services.AddScoped<IAuditorRequestDirectoryReader>(provider => provider.GetRequiredService<FitzAuditorRequestDirectory>());
        services.AddScoped<IAuditorRequestDirectoryProjection>(provider => provider.GetRequiredService<FitzAuditorRequestDirectory>());
        services.AddScoped<AuditorRequestDirectoryReadConsistency>();
        services.AddSingleton(AuditorRequestResolutionProcess.Definition());
        services.AddSingleton<IChildMaterializationReceiver, AuditorResponseMaterializationReceiver>();
        services.AddSingleton<IChildMaterializationReceiver, AuditorCommentMaterializationReceiver>();
        services.AddSingleton<IChildMaterializationReceiver, AuditorSampleSelectionMaterializationReceiver>();
        services.AddScoped<AuditorRequestResolutionVerifier>();
        services.AddScoped<AuditorRequestResolutionFences>();
        services.AddScoped<SampleSelectionAdmission>();
        services.AddScoped<FindingClosureVerifier>();
        services.AddScoped<FindingClosureFences>();
        services.AddSingleton(PolicyCampaignClosureProcess.Definition());
        services.AddScoped<CampaignSource>();
        services.AddScoped<PolicyCampaignReader>();
        services.AddScoped<CampaignLaunch>();
        services.AddScoped<PolicyParticipantAdmission>();
        services.AddScoped<PolicyCampaignClosureVerifier>();
        services.AddScoped<PolicyCampaignClosureFences>();
        services.AddScoped<FitzTrainingRequirementDirectory>();
        services.AddScoped<ITrainingRequirementDirectoryProjection>(provider =>
            provider.GetRequiredService<FitzTrainingRequirementDirectory>());
        services.AddScoped<ITrainingRequirementDirectoryReader>(provider =>
            provider.GetRequiredService<FitzTrainingRequirementDirectory>());
        services.AddSingleton(RiskActionCompletionReviewProcess.Definition());
        services.AddScoped<RiskActionReviewVerifier>();
        services.AddScoped<RiskActionReviewFences>();
        services.AddScoped<RiskGovernanceReader>();
        services.AddScoped<RiskMethodReader>();
        services.AddScoped<Bdgrz.Compliance.Features.Evidence.EvidenceRequestReader>();
        services.AddScoped<ControlOperationsReader>();
        services.AddScoped<ControlMappingReader>();
        services.AddScoped<ApplicabilityReader>();
        services.AddScoped<ControlEvaluationReader>();
        services.AddScoped<TypeIIOccurrenceCoverage>();
        services.AddScoped<TypeIICalendarReader>();
        services.AddScoped<TypeIIPeriodReader>();
        services.AddScoped<TypeIIPeriodOperations>();
        services.AddScoped<TypeIIPeriodCloseMaterialReader>();
        services.AddScoped<FitzObservationPeriodDirectory>();
        services.AddScoped<IObservationPeriodDirectoryReader>(provider => provider.GetRequiredService<FitzObservationPeriodDirectory>());
        services.AddScoped<IObservationPeriodCloseDirectoryReader>(provider => provider.GetRequiredService<FitzObservationPeriodDirectory>());
        services.AddScoped<IObservationPeriodDirectoryProjection>(provider => provider.GetRequiredService<FitzObservationPeriodDirectory>());
        services.AddScoped<ObservationPeriodDirectoryReadConsistency>();
        services.AddScoped<TypeIIRangeVerifier>();
        services.AddScoped<TypeIIRangeReceivers>();
        services.AddScoped<ProfessionalDutyAdmission>();
        services.AddScoped<ProfessionalDutyChangeVerifier>();
        services.AddScoped<ProfessionalDutyFences>();
        services.AddScoped<ProfessionalDutyChangeReceivers>();
        services.AddScoped<FitzFirmProfessionalDutyDirectory>();
        services.AddScoped<IFirmProfessionalDutyDirectoryProjection>(
            provider => provider.GetRequiredService<FitzFirmProfessionalDutyDirectory>());
        services.AddScoped<IFirmProfessionalDutyDirectoryReader>(
            provider => provider.GetRequiredService<FitzFirmProfessionalDutyDirectory>());
        services.AddScoped<FitzFirmStaffDirectory>();
        services.AddScoped<IFirmStaffDirectoryProjection>(
            provider => provider.GetRequiredService<FitzFirmStaffDirectory>());
        services.AddScoped<IFirmStaffDirectoryReader>(
            provider => provider.GetRequiredService<FitzFirmStaffDirectory>());
        services.AddSingleton(TenantInvitationDeliveryProcess.Definition);
        services.AddSingleton(ProviderNameChangeProcess.Definition);
        services.AddSingleton(LocationNameChangeProcess.Definition);
        services.AddSingleton(OperationalProcessNameChangeProcess.Definition);
        services.AddSingleton(ProviderReviewProcess.Definition());
        services.AddSingleton(IndependentChildMaterializationProcess.Definition());
        services.AddSingleton<IChildMaterializationReceiver, ProviderRiskAcceptanceLinkMaterializer>();
        services.AddScoped<ProviderReviewVerifier>();
        // Portia 0.9.1 registers the shared connection's IStreamClient for source-fact verification.
        services.TryAddScoped<ISourceFactResolver>(provider => new FitzSourceFactResolver(
            provider.GetRequiredService<IStreamClient>(), provider.GetRequiredService<IDomainEventSerializer>()));
        services.TryAddScoped<INativeEventReceiptReader, FitzNativeEventReceiptReader>();
        services.AddScoped<RecordedSourceReceiptCutFactory>();
        // The fixed-cut reader shares the Portia stream client.
        services.TryAddScoped<IAuthoritativeSnapshotReader>(provider => new FitzAuthoritativeSnapshotReader(
            provider.GetRequiredService<IStreamClient>(), provider.GetRequiredService<IDomainEventSerializer>(),
            provider.GetService<TimeProvider>()));
        services.AddScoped<ApplicationPublicationReader>();
        // Waiver proofs use the same stream-backed source-fact resolver.
        services.AddScoped<ISeparationOfDutiesWaiverProofResolver>(provider =>
            new DeferredSeparationOfDutiesWaiverProofResolver(() =>
                ActivatorUtilities.CreateInstance<SeparationOfDutiesWaiverProofResolver>(provider)));
        services.AddScoped<FitzActualStaffEngagementLocator>();
        services.AddScoped<IActualStaffEngagementLocatorProjection>(provider =>
            provider.GetRequiredService<FitzActualStaffEngagementLocator>());
        services.AddScoped<IActualStaffEngagementLocatorReader>(provider =>
            provider.GetRequiredService<FitzActualStaffEngagementLocator>());
        services.AddSingleton<ITenantDirectory>(provider =>
            new EventSourcedTenantDirectory<TenantRegistered, TenantRegistered>(
                provider.GetRequiredService<IDomainEventReader>(),
                EventStreamPattern.ForPattern("bdgrz", "tenants"),
                domainEvent => new TenantId(((TenantRegistered)domainEvent).TenantId.ToString()),
                timeProvider: provider.GetRequiredService<TimeProvider>(),
                notifier: provider.GetService<IDomainEventNotifier>() ??
                          provider.GetService<IEventStore>() as IDomainEventNotifier));

        var portia = services
            .AddPortia()
            .AddRequestHandler<ContinueWithDeveloperIdentityHandler>()
            .AddRequestAuthorizer<ContinueWithDeveloperIdentityAuthorizer>()
            .AddRequestHandler<ContinueWithOidcProviderHandler>()
            .AddRequestAuthorizer<ContinueWithOidcProviderAuthorizer>()
            .AddRequestHandler<LinkOidcProviderIdentityHandler>()
            .AddRequestAuthorizer<LinkOidcProviderIdentityAuthorizer>()
            .AddRequestHandler<StartIdentityRecoveryHandler>()
            .AddRequestHandler<CompleteIdentityRecoveryHandler>()
            .AddRequestAuthorizer<IdentityRecoveryAuthorizer>()
            .AddRequestHandler<MaterializeIdentityRecoveryPreparationHandler>()
            .AddRequestHandler<ProtectRecoveryIdentityHandler>()
            .AddRequestHandler<AcceptProtectedIdentityRecoveryHandler>()
            .AddRequestHandler<RegisterRecoveredIdentityHandler>()
            .AddRequestHandler<RevokeRecoveredOldIdentityHandler>()
            .AddRequestHandler<CompleteAcceptedIdentityRecoveryHandler>()
            .AddRequestHandler<SettleRecoveryIdentityProtectionHandler>()
            .AddRequestHandler<CompleteEmailRecoveryChallengeHandler>()
            .AddRequestHandler<SendIdentityRecoveryNoticeHandler>()
            .AddRequestHandler<RejectIdentityRecoveryPreparationHandler>()
            .AddRequestAuthorizer<IdentityRecoveryReceiverAuthorizer>()
            .AddRequestHandler<GetIdentityRecoveryOptionsHandler>()
            .AddRequestAuthorizer<IdentityRecoveryOptionsAuthorizer>()
            .AddRequestHandler<DeliverEmailChallengeHandler>()
            .AddRequestHandler<DeliverRecoveryChallengeHandler>()
            .AddRequestAuthorizer<EmailChallengeDeliveryReceiverAuthorizer>()
            .AddRequestHandler<RecordEmailChallengeDeliverySentHandler>()
            .AddRequestHandler<RecordEmailChallengeDeliveryFailedHandler>()
            .AddRequestAuthorizer<EmailChallengeDeliveryOutcomeAuthorizer>()
            .AddRequestHandler<ReserveEmailHandler>()
            .AddRequestHandler<IssueEmailChallengeHandler>()
            .AddRequestHandler<CompleteEmailChallengeHandler>()
            .AddRequestHandler<GetEmailChallengeStatusHandler>()
            .AddRequestHandler<GetEmailAddressHandler>()
            .AddRequestHandler<ListEmailAddressesHandler>()
            .AddRequestHandler<ConfigureTenantIdentityProviderHandler>()
            .AddRequestHandler<TestTenantIdentityProviderHandler>()
            .AddRequestHandler<RecordTenantIdentityProviderTestResultHandler>()
            .AddRequestHandler<RotateTenantIdentityProviderCredentialsHandler>()
            .AddRequestHandler<DisableTenantIdentityProviderHandler>()
            .AddRequestAuthorizer<RecordTenantIdentityProviderTestResultAuthorizer>()
            .AddRequestAuthorizer<EmailOwnershipAuthorizer>()
            .AddRequestHandler<ReviseIndependenceRulesHandler>()
            .AddRequestHandler<RatifyIndependenceRuleVersionHandler>()
            .AddRequestHandler<GetIndependenceRuleVersionsHandler>()
            .AddRequestHandler<GetClientIndependenceRulesHandler>()
            .AddRequestHandler<RecordNonattestServiceHandler>()
            .AddRequestHandler<EvaluateClientIndependenceHandler>()
            .AddRequestHandler<GetClientIndependenceHistoryHandler>()
            .AddRequestHandler<RecordDirectoryIndependenceReevaluationHandler>()
            .AddRequestHandler<RegisterFirmStaffHandler>()
            .AddRequestHandler<SetFirmStaffStatusHandler>()
            .AddRequestHandler<ClaimFirmStaffIdentityHandler>()
            .AddRequestHandler<CompleteFirmStaffRegistrationHandler>()
            .AddRequestHandler<RejectFirmStaffRegistrationHandler>()
            .AddRequestHandler<GetFirmStaffDirectoryHandler>()
            .AddRequestHandler<RecordFirmProfessionalDutyDesignationHandler>()
            .AddRequestHandler<RevokeFirmProfessionalDutyDesignationHandler>()
            .AddRequestHandler<GetFirmProfessionalDutyCatalogHandler>()
            .AddRequestHandler<AuthorizeFirmStaffProfessionalDutyUseHandler>()
            .AddRequestHandler<SettleFirmStaffProfessionalDutyUseHandler>()
            .AddRequestHandler<ClaimProfessionalAssertionIdentityHandler>()
            .AddRequestHandler<ApplyProfessionalDutyChangeHandler>()
            .AddRequestHandler<MaterializeClientIndependencePreviewHandler>()
            .AddRequestHandler<AcknowledgeEngagementManagementHandler>()
            .AddRequestHandler<RecordPartnerIndependenceEvaluationHandler>()
            .AddRequestHandler<AuthorizeFirmStaffRecordEngagementUseHandler>()
            .AddRequestHandler<SettleFirmStaffRecordEngagementUseHandler>()
            .AddRequestHandler<AuthorizeFirmProfessionalDutyDesignationEngagementUseHandler>()
            .AddRequestHandler<SettleFirmProfessionalDutyDesignationEngagementUseHandler>()
            .AddRequestHandler<AuthorizeIndependenceRuleCatalogEngagementUseHandler>()
            .AddRequestHandler<SettleIndependenceRuleCatalogEngagementUseHandler>()
            .AddRequestHandler<AuthorizeIndependenceRuleRatificationCatalogEngagementUseHandler>()
            .AddRequestHandler<SettleIndependenceRuleRatificationCatalogEngagementUseHandler>()
            .AddRequestHandler<AuthorizeClientServicePersonalDecisionUseHandler>()
            .AddRequestHandler<SettleClientServicePersonalDecisionUseHandler>()
            .AddRequestHandler<AuthorizeEngagementPersonalDecisionHandler>()
            .AddRequestHandler<MaterializeEngagementManagementAcknowledgementHandler>()
            .AddRequestHandler<MaterializePartnerIndependenceDecisionHandler>()
            .AddRequestHandler<SettleEngagementPersonalDecisionHandler>()
            .AddRequestHandler<CancelEngagementPersonalDecisionHandler>()
            .AddRequestHandler<AcceptServiceEngagementHandler>()
            .AddRequestHandler<AssignServiceEngagementStaffHandler>()
            .AddRequestHandler<AcquireEngagementServiceEpochHandler>()
            .AddRequestHandler<ReleaseEngagementServiceEpochHandler>()
            .AddRequestHandler<ReservePracticeIndependenceHandler>()
            .AddRequestHandler<SettlePracticeIndependenceReservationHandler>()
            .AddRequestHandler<AuthorizeEngagementPracticeCommitmentsHandler>()
            .AddRequestHandler<CommitPracticeIndependenceHandler>()
            .AddRequestHandler<CompleteServiceEngagementAcceptanceHandler>()
            .AddRequestHandler<CompleteServiceEngagementStaffAssignmentHandler>()
            .AddRequestHandler<RejectServiceEngagementAcceptanceHandler>()
            .AddRequestHandler<RejectServiceEngagementStaffAssignmentHandler>()
            .AddRequestHandler<RejectProfessionalDutyChangeHandler>()
            .AddRequestHandler<ListAssignableFirmStaffHandler>()
            .AddRequestHandler<CreateServiceEngagementHandler>()
            .AddRequestHandler<AmendServiceEngagementHandler>()
            .AddRequestHandler<ProposeServiceEngagementStaffHandler>()
            .AddRequestHandler<WithdrawServiceEngagementStaffProposalHandler>()
            .AddRequestHandler<RevokeServiceEngagementActualStaffHandler>()
            .AddRequestHandler<CloseServiceEngagementHandler>()
            .AddRequestHandler<GetServiceEngagementAcceptanceHandler>()
            .AddRequestHandler<GetServiceEngagementAcceptanceHistoryHandler>()
            .AddRequestHandler<GetServiceEngagementHandler>()
            .AddRequestHandler<ListServiceEngagementsHandler>()
            .AddRequestHandler<GetServiceEngagementHistoryHandler>()
            .AddRequestHandler<GetEngagementManagementAcknowledgementsHandler>()
            .AddRequestAuthorizer<IndependenceRuleAdministrationAuthorizer>()
            .AddRequestAuthorizer<IndependenceRuleRatificationAuthorizer>()
            .AddRequestAuthorizer<ProfessionalEngagementPartnerAuthorizer>()
            .AddRequestAuthorizer<FirmStaffAdministrationAuthorizer>()
            .AddRequestAuthorizer<FirmStaffRegistrationReceiverAuthorizer>()
            .AddRequestAuthorizer<ProfessionalDutyChangeReceiverAuthorizer>()
            .AddRequestAuthorizer<ClientIndependencePreviewReceiverAuthorizer>()
            .AddRequestAuthorizer<EngagementPersonalDecisionReceiverAuthorizer>()
            .AddRequestAuthorizer<EngagementAcceptanceReceiverAuthorizer>()
            .AddRequestAuthorizer<TypeIIRangeReceiverAuthorizer>()
            .AddRequestAuthorizer<PolicyCampaignReceiverAuthorizer>()
            .AddRequestAuthorizer<ControlOccurrenceReceiverAuthorizer>()
            .AddRequestAuthorizer<AccessReviewReceiverAuthorizer>()
            .AddRequestAuthorizer<ProfessionalDutyAdministrationAuthorizer>()
            .AddRequestAuthorizer<ServiceEngagementAcceptanceAuthorizer>()
            .AddRequestAuthorizer<IndependenceAdministrationAuthorizer>()
            .AddRequestAuthorizer<RecordDirectoryIndependenceReevaluationAuthorizer>()
            .AddRequestHandler<RegisterMemberHandler>()
            .AddRequestHandler<CompleteMemberRetirementHandler>()
            .AddRequestHandler<CleanupRetiredMembershipRelationshipHandler>()
            .AddRequestAuthorizer<MembershipRetirementAuthorizer>()
            .AddRequestHandler<SuspendMemberHandler>()
            .AddRequestHandler<ReinstateMemberHandler>()
            .AddRequestHandler<DeprovisionMemberHandler>()
            .AddRequestHandler<ApplyMemberWithdrawalHandler>()
            .AddRequestHandler<ApplyAdministratorAssignmentWithdrawalHandler>()
            .AddRequestHandler<ApplyMemberRestorationHandler>()
            .AddRequestHandler<ApplyAdministratorAssignmentRestorationHandler>()
            .AddRequestHandler<ApplyNeutralMemberSourceMutationHandler>()
            .AddRequestHandler<ApplyNeutralAdministratorAssignmentMutationHandler>()
            .AddRequestHandler<CompleteAdministratorWithdrawalHandler>()
            .AddRequestHandler<CompleteAdministratorRestorationHandler>()
            .AddRequestAuthorizer<AdministratorTransitionReceiverAuthorizer>()
            .AddRequestHandler<GetTenantMemberHandler>()
            .AddRequestHandler<DefineTeamHandler>()
            .AddRequestHandler<DeleteTeamHandler>()
            .AddRequestHandler<DefineRoleHandler>()
            .AddRequestHandler<RenameRoleHandler>()
            .AddRequestHandler<DeleteRoleHandler>()
            .AddRequestHandler<AssignTeamMemberHandler>()
            .AddRequestHandler<RemoveTeamMemberHandler>()
            .AddRequestHandler<AssignTeamRoleHandler>()
            .AddRequestHandler<RemoveTeamRoleHandler>()
            .AddRequestHandler<AssignRolePermissionHandler>()
            .AddRequestHandler<RemoveRolePermissionHandler>()
            .AddRequestHandler<GrantAccessHandler>()
            .AddRequestHandler<RevokeAccessGrantHandler>()
            .AddRequestHandler<ListAccessGrantsHandler>()
            .AddRequestAuthorizer<RbacManagementAuthorizer>()
            .AddRequestHandler<GetTeamHandler>()
            .AddRequestHandler<ListTeamsHandler>()
            .AddRequestHandler<ListTeamMembersHandler>()
            .AddRequestHandler<GetRoleHandler>()
            .AddRequestHandler<ListRolesHandler>()
            .AddRequestHandler<ListRolePermissionsHandler>()
            .AddRequestHandler<ListRoleTeamsHandler>()
            .AddRequestHandler<ListTeamRolesHandler>()
            .AddRequestAuthorizer<TenantAccessAuthorizer>()
            .AddRequestHandler<DeclareApplicationHandler>()
            .AddRequestHandler<ReviseApplicationHandler>()
            .AddRequestHandler<DeclareSystemInstanceHandler>()
            .AddRequestHandler<ReviseSystemInstanceHandler>()
            .AddRequestHandler<GetApplicationHandler>()
            .AddRequestHandler<ListApplicationsHandler>()
            .AddRequestHandler<AddApplicationRelationshipHandler>()
            .AddRequestHandler<RemoveApplicationRelationshipHandler>()
            .AddRequestHandler<ListApplicationRelationshipsHandler>()
            .AddRequestPipelineBehavior<ApplicationImportVisibilityBehavior>()
            .AddRequestHandler<GetApplicationRevisionHandler>()
            .AddRequestHandler<ListApplicationRevisionsHandler>()
            .AddRequestHandler<GetSystemInstanceHandler>()
            .AddRequestHandler<ListSystemInstancesHandler>()
            .AddRequestHandler<RecordApplicationRelationshipHandler>()
            .AddRequestHandler<RemoveGovernedApplicationRelationshipHandler>()
            .AddRequestHandler<ListDirectedApplicationRelationshipsHandler>()
            .AddRequestHandler<ListApplicationBoundaryReferencesHandler>()
            .AddRequestHandler<ListTechnologyComponentBoundaryReferencesHandler>()
            .AddRequestHandler<ListInformationAssetBoundaryReferencesHandler>()
            .AddRequestHandler<ListSystemInstanceBoundaryReferencesHandler>()
            .AddRequestHandler<PreviewApplicationChangeHandler>()
            .AddRequestHandler<ApproveApplicationSuccessorHandler>()
            .AddRequestHandler<RetireApplicationHandler>()
            .AddRequestHandler<RetireSystemInstanceHandler>()
            .AddRequestHandler<DecideAccessReviewScopeHandler>()
            .AddRequestHandler<GetAccessReviewScopeHandler>()
            .AddRequestHandler<ListAccessReviewScopesHandler>()
            .AddRequestHandler<PreviewInformationAssetChangeHandler>()
            .AddRequestAuthorizer<ArtifactRetentionAdminAuthorizer>()
            .AddRequestAuthorizer<ArtifactDisclosureAccess>()
            .AddRequestHandler<ApproveArtifactDisclosureHandler>()
            .AddRequestHandler<RecordArtifactDisclosureDeliveryHandler>()
            .AddRequestHandler<RevokeArtifactDisclosureHandler>()
            .AddRequestHandler<AssignArtifactHandlingPolicyHandler>()
            .AddRequestHandler<PlaceArtifactEngagementHoldHandler>()
            .AddRequestHandler<RecordEngagementReportIssuedHandler>()
            .AddRequestHandler<ReleaseArtifactEngagementHoldHandler>()
            .AddRequestHandler<RecordArtifactRetentionBasisHandler>()
            .AddRequestHandler<PlaceArtifactLegalHoldHandler>()
            .AddRequestHandler<ReleaseArtifactLegalHoldHandler>()
            .AddRequestHandler<RecordArtifactDispositionResultHandler>()
            .AddRequestAuthorizer<RecordArtifactDispositionResultAuthorizer>()
            .AddRequestHandler<GetArtifactRetentionHandler>()
            .AddRequestHandler<GetApplicationImportRetentionHandler>()
            .AddRequestHandler<ListArtifactLegalHoldsHandler>()
            .AddRequestHandler<StageApplicationImportHandler>()
            .AddRequestHandler<CancelApplicationImportHandler>()
            .AddRequestHandler<AcceptApplicationImportHandler>()
            .AddRequestHandler<GetApplicationImportProgressHandler>()
            .AddRequestHandler<GetApplicationImportRejectedReportHandler>()
            .AddRequestHandler<ExecuteApplicationImportHandler>()
            .AddRequestAuthorizer<ExecuteApplicationImportAuthorizer>()
            .AddRequestHandler<GetApplicationImportHandler>()
            .AddRequestHandler<ListApplicationImportRowsHandler>()
            .AddRequestHandler<PreviewApplicationImportHandler>()
            .AddRequestHandler<PreviewMissingApplicationImportRowsHandler>()
            .AddRequestHandler<PreviewApplicationImportOmissionProposalHandler>()
            .AddRequestHandler<FreezeApplicationImportOmissionProposalHandler>()
            .AddRequestHandler<GetApplicationImportOmissionProposalHandler>()
            .AddRequestHandler<CorrelateApplicationImportRowHandler>()
            .AddRequestHandler<ClaimApplicationImportSourceHandler>()
            .AddRequestHandler<ReserveApplicationImportTargetHandler>()
            .AddRequestHandler<ApplyApplicationImportEffectHandler>()
            .AddRequestHandler<CommitApplicationImportBatchHandler>()
            .AddRequestHandler<FenceApplicationImportTargetHandler>()
            .AddRequestHandler<CompleteApplicationImportCancellationHandler>()
            .AddRequestHandler<ReleaseApplicationImportSourceHandler>()
            .AddRequestHandler<ReleaseApplicationImportTargetHandler>()
            .AddRequestAuthorizer<ApplicationImportReceiverAuthorizer>()
            .AddRequestAuthorizer<ApplicationInventoryAuthorizer>()
            .AddRequestHandler<RecordProviderHandler>()
            .AddRequestHandler<ReviseProviderHandler>()
            .AddRequestHandler<ClaimProviderNameHandler>()
            .AddRequestHandler<ApplyProviderChangeHandler>()
            .AddRequestHandler<ReleaseProviderNameHandler>()
            .AddRequestHandler<CancelProviderChangeHandler>()
            .AddRequestHandler<ClaimLocationNameHandler>()
            .AddRequestHandler<ApplyLocationChangeHandler>()
            .AddRequestHandler<ReleaseLocationNameHandler>()
            .AddRequestHandler<CancelLocationChangeHandler>()
            .AddRequestHandler<ClaimOperationalProcessNameHandler>()
            .AddRequestHandler<ApplyOperationalProcessChangeHandler>()
            .AddRequestHandler<ReleaseOperationalProcessNameHandler>()
            .AddRequestHandler<CancelOperationalProcessChangeHandler>()
            .AddRequestAuthorizer<NamedRecordReceiverAuthorizer>()
            .AddRequestHandler<GetProviderHandler>()
            .AddRequestHandler<ListProvidersHandler>()
            .AddRequestHandler<GetProviderRevisionHandler>()
            .AddRequestHandler<ListProviderRevisionsHandler>()
            .AddRequestHandler<RecordAssuranceReportHandler>()
            .AddRequestHandler<ReviseAssuranceReportHandler>()
            .AddRequestHandler<RecordProviderReviewHandler>()
            .AddRequestHandler<ListProviderAssuranceReportsHandler>()
            .AddRequestHandler<ListProviderReviewsHandler>()
            .AddRequestHandler<RecordProviderCoverageGapHandler>()
            .AddRequestHandler<CloseProviderCoverageGapHandler>()
            .AddRequestHandler<LinkProviderCoverageGapRiskAcceptanceHandler>()
            .AddRequestHandler<AuthorizeProviderAssuranceReportProviderReviewUseHandler>()
            .AddRequestHandler<SettleProviderAssuranceReportProviderReviewUseHandler>()
            .AddRequestHandler<CompleteProviderAssuranceReviewHandler>()
            .AddRequestHandler<AbortProviderReviewHandler>()
            .AddRequestHandler<MaterializeProviderRiskAcceptanceLinkHandler>()
            .AddRequestAuthorizer<ProviderAssuranceReceiverAuthorizer>()
            .AddRequestHandler<GetProviderCoverageGapHandler>()
            .AddRequestHandler<ListProviderCoverageGapsHandler>()
            .AddRequestHandler<GetProviderAssuranceCoverageHandler>()
            .AddRequestHandler<PreviewProviderChangeHandler>()
            .AddRequestAuthorizer<ProviderAuthorizer>()
            .AddRequestHandler<RecordPersonHandler>()
            .AddRequestHandler<RevisePersonHandler>()
            .AddRequestHandler<CorrelatePersonMembershipHandler>()
            .AddRequestHandler<GetPersonHandler>()
            .AddRequestHandler<ListPeopleHandler>()
            .AddRequestHandler<RecordWorkRelationshipHandler>()
            .AddRequestHandler<ReviseWorkRelationshipHandler>()
            .AddRequestHandler<GetWorkRelationshipHandler>()
            .AddRequestHandler<ListWorkRelationshipsHandler>()
            .AddRequestHandler<ListWorkforceObservationsHandler>()
            .AddRequestHandler<ListWorkforceReconciliationObservationsHandler>()
            .AddRequestHandler<ResolveWorkforceObservationHandler>()
            .AddRequestHandler<RecordWorkforceSourceObservationHandler>()
            .AddRequestHandler<GetWorkforceSourceObservationHandler>()
            .AddRequestHandler<ListWorkforceSourceObservationsHandler>()
            .AddRequestHandler<PreviewWorkforceSourceObservationHandler>()
            .AddRequestHandler<ReconcileWorkforceSourceObservationHandler>()
            .AddRequestHandler<RecordServiceIdentityHandler>()
            .AddRequestHandler<ReviseServiceIdentityHandler>()
            .AddRequestHandler<GetServiceIdentityHandler>()
            .AddRequestHandler<ListServiceIdentitiesHandler>()
            .AddRequestAuthorizer<WorkforceAuthorizer>()
            .AddRequestHandler<RecordTechnologyComponentHandler>()
            .AddRequestHandler<ReviseTechnologyComponentHandler>()
            .AddRequestHandler<GetTechnologyComponentHandler>()
            .AddRequestHandler<ListTechnologyComponentsHandler>()
            .AddRequestHandler<ListTechnologyComponentRevisionsHandler>()
            .AddRequestHandler<RecordInformationAssetHandler>()
            .AddRequestHandler<ReviseInformationAssetHandler>()
            .AddRequestHandler<GetInformationAssetHandler>()
            .AddRequestHandler<ListInformationAssetsHandler>()
            .AddRequestHandler<ListInformationAssetRevisionsHandler>()
            .AddRequestHandler<RecordDataFlowHandler>()
            .AddRequestHandler<ReviseDataFlowHandler>()
            .AddRequestHandler<GetDataFlowHandler>()
            .AddRequestHandler<ListDataFlowsHandler>()
            .AddRequestHandler<ListDataFlowRevisionsHandler>()
            .AddRequestHandler<RecordLocationHandler>()
            .AddRequestHandler<ReviseLocationHandler>()
            .AddRequestHandler<GetLocationHandler>()
            .AddRequestHandler<ListLocationsHandler>()
            .AddRequestHandler<ListLocationRevisionsHandler>()
            .AddRequestHandler<RecordOperationalProcessHandler>()
            .AddRequestHandler<ReviseOperationalProcessHandler>()
            .AddRequestHandler<GetOperationalProcessHandler>()
            .AddRequestHandler<ListOperationalProcessesHandler>()
            .AddRequestHandler<ListOperationalProcessRevisionsHandler>()
            .AddRequestAuthorizer<TechnologyInventoryAuthorizer>()
            .AddRequestHandler<CreateAdvisorHandoffHandler>()
            .AddRequestHandler<ReviseAdvisorHandoffSelectionHandler>()
            .AddRequestHandler<MarkAdvisorHandoffReadyForReviewHandler>()
            .AddRequestHandler<CloseAdvisorHandoffHandler>()
            .AddRequestHandler<RevokeAdvisorHandoffHandler>()
            .AddRequestHandler<RespondToAdvisorFeedbackHandler>()
            .AddRequestHandler<ResolveAdvisorFeedbackHandler>()
            .AddRequestHandler<RecordAdvisorFeedbackHandler>()
            .AddRequestHandler<GetAdvisorRequestHandler>()
            .AddRequestHandler<GetAdvisorSharedSourceHandler>()
            .AddRequestHandler<PreviewAdvisorHandoffHandler>()
            .AddRequestHandler<ListAdvisorRequestsHandler>()
            .AddRequestHandler<ListAdvisorFeedbackHandler>()
            .AddRequestHandler<CorrectAdvisorFeedbackHandler>()
            .AddRequestHandler<MaterializeAdvisorFeedbackHandler>()
            .AddRequestAuthorizer<AdvisorReceiverAuthorizer>()
            .AddRequestHandler<CreateProgramHandler>()
            .AddRequestHandler<ListCriteriaCatalogEditionsHandler>()
            .AddRequestHandler<GetCriteriaCatalogEditionHandler>()
            .AddRequestHandler<ListCriteriaCatalogEntriesHandler>()
            .AddRequestHandler<ExportCriteriaCatalogEntriesHandler>()
            .AddRequestHandler<GetCriteriaCatalogEntryHandler>()
            .AddRequestHandler<SetCriteriaTextOverlayHandler>()
            .AddRequestHandler<CreateControlDraftHandler>()
            .AddRequestHandler<ReviseControlDraftHandler>()
            .AddRequestHandler<DiscardControlDraftHandler>()
            .AddRequestHandler<GetControlDraftHandler>()
            .AddRequestHandler<GetControlDraftOwnerPersonHandler>()
            .AddRequestHandler<ListControlDraftsHandler>()
            .AddRequestHandler<ListControlDraftRevisionsHandler>()
            .AddRequestHandler<GetControlDraftRevisionHandler>()
            .AddRequestHandler<ReviewControlHandler>()
            .AddRequestHandler<ApproveControlHandler>()
            .AddRequestHandler<GetControlVersionHandler>()
            .AddRequestHandler<GetCurrentControlVersionHandler>()
            .AddRequestHandler<GetEffectiveControlVersionHandler>()
            .AddRequestHandler<ListControlVersionsHandler>()
            .AddRequestHandler<GetControlDecisionHandler>()
            .AddRequestHandler<ListControlDecisionsHandler>()
            .AddRequestHandler<ProposeControlCriterionMappingHandler>()
            .AddRequestHandler<ReviewControlCriterionMappingHandler>()
            .AddRequestHandler<RetireControlCriterionMappingHandler>()
            .AddRequestHandler<GetControlCriterionMappingHandler>()
            .AddRequestHandler<ListControlCriterionMappingsHandler>()
            .AddRequestHandler<ListCriteriaCoverageHandler>()
            .AddRequestHandler<ProposeCriterionNotApplicableHandler>()
            .AddRequestHandler<ReviewCriterionApplicabilityHandler>()
            .AddRequestHandler<WithdrawCriterionNotApplicableHandler>()
            .AddRequestHandler<GetCriterionApplicabilityHandler>()
            .AddRequestHandler<ListCriterionApplicabilityHandler>()
            .AddRequestHandler<RunReadinessAssessmentHandler>()
            .AddRequestHandler<GetReadinessAssessmentHandler>()
            .AddRequestHandler<ListReadinessAssessmentsHandler>()
            .AddRequestHandler<ListReadinessGapsHandler>()
            .AddRequestHandler<PlanReadinessGapHandler>()
            .AddRequestHandler<DecideReadinessHandler>()
            .AddRequestHandler<DecideTypeIEntryHandler>()
            .AddRequestHandler<ListTypeIEntryDecisionsHandler>()
            .AddRequestHandler<ComputeReadinessAssessmentHandler>()
            .AddRequestHandler<AdoptReadinessAssessmentHandler>()
            .AddRequestHandler<AuthorizeReadinessGapPlanReadinessDecisionUseHandler>()
            .AddRequestHandler<CompleteReadinessDecisionHandler>()
            .AddRequestHandler<CompleteTypeIEntryDecisionHandler>()
            .AddRequestHandler<SettleReadinessGapPlanReadinessDecisionUseHandler>()
            .AddRequestHandler<AbortReadinessDecisionHandler>()
            .AddRequestAuthorizer<ReadinessReceiverAuthorizer>()
            .AddRequestHandler<CreateExaminationEngagementHandler>()
            .AddRequestHandler<ReviseExaminationEngagementHandler>()
            .AddRequestHandler<GetExaminationEngagementHandler>()
            .AddRequestHandler<GetTypeIBaselineHandler>()
            .AddRequestHandler<PreviewTypeIBaselineHandler>()
            .AddRequestHandler<FreezeTypeIBaselineHandler>()
            .AddRequestHandler<AmendTypeIBaselineHandler>()
            .AddRequestHandler<ReviewTypeIIOperatingPlanHandler>()
            .AddRequestHandler<ProposeTypeIIOperatingPlanHandler>()
            .AddRequestHandler<ApproveTypeIIOperatingPlanHandler>()
            .AddRequestHandler<StartTypeIIObservationPeriodHandler>()
            .AddRequestHandler<ApproveTypeIIPeriodCloseHandler>()
            .AddRequestHandler<RecordTypeIIPeriodCloseValidationHandler>()
            .AddRequestHandler<FreezeTypeIIPeriodCloseHandler>()
            .AddRequestHandler<AmendTypeIIPeriodCloseHandler>()
            .AddRequestHandler<GetObservationPeriodHandler>()
            .AddRequestHandler<ListObservationPeriodsHandler>()
            .AddRequestHandler<GetObservationPeriodCloseHandler>()
            .AddRequestHandler<PreviewObservationPeriodCloseHandler>()
            .AddRequestHandler<ProposeSystemDescriptionSuccessorHandler>()
            .AddRequestHandler<RecordManagementAssertionHandler>()
            .AddRequestHandler<ApproveManagementAssertionHandler>()
            .AddRequestHandler<RecordManagementRepresentationLetterHandler>()
            .AddRequestHandler<SignManagementRepresentationLetterHandler>()
            .AddRequestHandler<RecordExaminationOutcomeHandler>()
            .AddRequestHandler<RecordManagementExaminationReviewHandler>()
            .AddRequestHandler<GetAuditPackageHandler>()
            .AddRequestHandler<ListAuditPackagesHandler>()
            .AddRequestHandler<CreateAuditPackageHandler>()
            .AddRequestHandler<AmendAuditPackageHandler>()
            .AddRequestHandler<ValidateAuditPackageHandler>()
            .AddRequestHandler<ApproveAuditPackageDisclosureHandler>()
            .AddRequestHandler<ApproveAuditPackageValidationOverrideHandler>()
            .AddRequestHandler<RecordAuditPackageBuildResultHandler>()
            .AddRequestHandler<StartAuditPackageBuildHandler>()
            .AddRequestHandler<MaterializeRecordedAuditPackageDeliveryHandler>()
            .AddRequestHandler<RequestAuditPackageBuildHandler>()
            .AddRequestHandler<RecordAuditPackageDeliveryHandler>()
            .AddRequestHandler<RecordClientExportBuildResultHandler>()
            .AddRequestHandler<RecordClientExportDeliveryHandler>()
            .AddRequestAuthorizer<AuditPackageReceiverAuthorizer>()
            .AddRequestAuthorizer<RecordClientExportBuildResultAuthorizer>()
            .AddRequestHandler<DefineAuditPopulationHandler>()
            .AddRequestHandler<RecordAuditPopulationFactsHandler>()
            .AddRequestHandler<AmendAuditPopulationHandler>()
            .AddRequestHandler<ClassifyAuditPopulationRowHandler>()
            .AddRequestHandler<FreezeAuditPopulationHandler>()
            .AddRequestHandler<ReviseAuditPopulationDefinitionHandler>()
            .AddRequestHandler<GenerateAuditPopulationHandler>()
            .AddRequestHandler<GenerateAuditPopulationExportHandler>()
            .AddRequestHandler<ReconcileAuditPopulationHandler>()
            .AddRequestHandler<GetAuditorRequestHandler>()
            .AddRequestHandler<ListAuditorRequestsHandler>()
            .AddRequestHandler<ListRequestResponsesHandler>()
            .AddRequestHandler<GetSampleSelectionHandler>()
            .AddRequestHandler<RecordAuditorRequestHandler>()
            .AddRequestHandler<ReviseAuditorRequestHandler>()
            .AddRequestHandler<AssignAuditorRequestHandler>()
            .AddRequestHandler<RecordAuditorRequestCommentHandler>()
            .AddRequestHandler<CreateAuditResponseDraftHandler>()
            .AddRequestHandler<ReviseAuditResponseDraftHandler>()
            .AddRequestHandler<SubmitAuditResponseHandler>()
            .AddRequestHandler<ReviewAuditResponseHandler>()
            .AddRequestHandler<DeliverAuditResponseHandler>()
            .AddRequestHandler<RecordAuditorResponseDispositionHandler>()
            .AddRequestHandler<ResolveAuditorRequestHandler>()
            .AddRequestHandler<ReopenAuditorRequestHandler>()
            .AddRequestHandler<RecordSampleSelectionHandler>()
            .AddRequestHandler<AmendSampleSelectionHandler>()
            .AddRequestHandler<MaterializeAuditorResponseHandler>()
            .AddRequestHandler<MaterializeAuditorCommentHandler>()
            .AddRequestHandler<MaterializeAuditorSampleSelectionHandler>()
            .AddRequestHandler<AuthorizeAuditorResponseRequestResolutionUseHandler>()
            .AddRequestHandler<CompleteAuditorRequestResolutionHandler>()
            .AddRequestHandler<SettleAuditorResponseRequestResolutionUseHandler>()
            .AddRequestHandler<AbortAuditorRequestResolutionHandler>()
            .AddRequestAuthorizer<AuditorRequestReceiverAuthorizer>()
            .AddRequestHandler<RecordSignificantChangeHandler>()
            .AddRequestHandler<ReviseSignificantChangeHandler>()
            .AddRequestHandler<AssessSignificantChangeHandler>()
            .AddRequestHandler<GetSignificantChangeHandler>()
            .AddRequestHandler<ListSignificantChangesHandler>()
            .AddRequestHandler<GetComplianceIncidentHandler>()
            .AddRequestHandler<ListComplianceIncidentsHandler>()
            .AddRequestHandler<GetComplianceImpactAssessmentHandler>()
            .AddRequestHandler<LinkImpactReviewWorkHandler>()
            .AddRequestHandler<RecordSignificantChangeSystemDescriptionDecisionHandler>()
            .AddRequestHandler<RecordComplianceIncidentSystemDescriptionDecisionHandler>()
            .AddRequestAuthorizer<ComplianceImpactReactionAuthorizer>()
            .AddRequestHandler<RecordComplianceIncidentHandler>()
            .AddRequestHandler<ReviseComplianceIncidentHandler>()
            .AddRequestHandler<AssessComplianceIncidentHandler>()
            .AddRequestHandler<RecordComplianceIncidentResolutionHandler>()
            .AddRequestHandler<AmendTypeIIObservationPeriodHandler>()
            .AddRequestHandler<CancelTypeIIPeriodAmendmentHandler>()
            .AddRequestHandler<AcquireTypeIIPeriodRangeHandler>()
            .AddRequestHandler<MaterializeControlOccurrenceHandler>()
            .AddRequestHandler<ApplyTypeIIPeriodRangeHandler>()
            .AddRequestHandler<SettleTypeIIPeriodRangeHandler>()
            .AddRequestHandler<RejectTypeIIPeriodOpeningHandler>()
            .AddRequestHandler<RejectTypeIIPeriodAmendmentHandler>()
            .AddRequestHandler<GenerateDueControlOccurrencesHandler>()
            .AddRequestHandler<CreateSystemDescriptionDraftHandler>()
            .AddRequestHandler<ReviseSystemDescriptionDraftHandler>()
            .AddRequestHandler<BindSystemDescriptionSourcesHandler>()
            .AddRequestHandler<ReviewSystemDescriptionHandler>()
            .AddRequestHandler<ApproveSystemDescriptionHandler>()
            .AddRequestHandler<RecordSystemDescriptionNoChangeDecisionHandler>()
            .AddRequestHandler<BindSystemDescriptionToExaminationHandler>()
            .AddRequestHandler<GenerateSystemDescriptionExportHandler>()
            .AddRequestHandler<GetSystemDescriptionHandler>()
            .AddRequestHandler<ListSystemDescriptionVersionsHandler>()
            .AddRequestHandler<PreviewSystemDescriptionImpactHandler>()
            .AddRequestHandler<AnnotateReadinessGapHandler>()
            .AddRequestHandler<ListReadinessAnnotationsHandler>()
            .AddRequestAuthorizer<ReadinessAnnotationCompartmentAuthorizer>()
            .AddRequestHandler<ProposeControlOperatingPlanHandler>()
            .AddRequestHandler<PreviewControlOperatingPlanHandler>()
            .AddRequestHandler<ApproveControlOperatingPlanHandler>()
            .AddRequestHandler<GetControlOperatingPlanHandler>()
            .AddRequestHandler<ListControlOperatingBlockersHandler>()
            .AddRequestHandler<ListMyControlWorkHandler>()
            .AddRequestHandler<OpenControlOccurrenceHandler>()
            .AddRequestHandler<AttestControlOccurrenceHandler>()
            .AddRequestHandler<CorrectControlAttestationHandler>()
            .AddRequestHandler<ReviewControlOccurrenceHandler>()
            .AddRequestHandler<GetControlOccurrenceHandler>()
            .AddRequestHandler<ListControlOccurrencesHandler>()
            .AddRequestHandler<RaiseFindingHandler>()
            .AddRequestHandler<RaiseOccurrenceFindingHandler>()
            .AddRequestHandler<ReviseFindingHandler>()
            .AddRequestHandler<AddCorrectiveActionHandler>()
            .AddRequestHandler<CompleteCorrectiveActionHandler>()
            .AddRequestHandler<LinkFindingAcceptanceHandler>()
            .AddRequestHandler<CloseFindingHandler>()
            .AddRequestHandler<ReopenFindingHandler>()
            .AddRequestHandler<GetFindingHandler>()
            .AddRequestHandler<ListFindingsHandler>()
            .AddRequestHandler<MaterializeCorrectiveActionHandler>()
            .AddRequestHandler<AuthorizeCorrectiveActionFindingClosureUseHandler>()
            .AddRequestHandler<CompleteFindingClosureHandler>()
            .AddRequestHandler<SettleCorrectiveActionFindingClosureUseHandler>()
            .AddRequestHandler<AbortFindingClosureHandler>()
            .AddRequestAuthorizer<FindingReceiverAuthorizer>()
            .AddRequestHandler<Bdgrz.Compliance.Features.Evidence.OpenEvidenceRequestHandler>()
            .AddRequestHandler<Bdgrz.Compliance.Features.Evidence.FulfilEvidenceRequestHandler>()
            .AddRequestHandler<Bdgrz.Compliance.Features.Evidence.CancelEvidenceRequestHandler>()
            .AddRequestHandler<CaptureEvidenceArtifactHandler>()
            .AddRequestAuthorizer<EvidenceArtifactDownloadAccess>()
            .AddRequestHandler<IssueArtifactDownloadHandler>()
            .AddRequestHandler<LinkEvidenceSupportHandler>()
            .AddRequestAuthorizer<EvidenceArtifactInspectionAuthorizer>()
            .AddRequestHandler<RecordEvidenceArtifactInspectionHandler>()
            .AddRequestHandler<ReleaseEvidenceArtifactQuarantineHandler>()
            .AddRequestHandler<GetEvidenceRedactionHandler>()
            .AddRequestAuthorizer<EvidenceRedactionWaiverScopeAuthorizer>()
            .AddRequestHandler<ApproveEvidenceRedactionHandler>()
            .AddRequestHandler<PrepareEvidenceRedactionHandler>()
            .AddRequestAuthorizer<EvidenceRedactionAccess>()
            .AddRequestHandler<GetEvidenceArtifactMetadataHandler>()
            .AddRequestAuthorizer<EvidenceArtifactMetadataAuthorizer>()
            .AddRequestHandler<Bdgrz.Compliance.Features.Evidence.GetEvidenceRequestHandler>()
            .AddRequestHandler<Bdgrz.Compliance.Features.Evidence.ListEvidenceRequestsHandler>()
            .AddRequestHandler<ListWorkHandler>()
            .AddRequestHandler<GetWorkItemHandler>()
            .AddRequestHandler<ClaimWorkItemHandler>()
            .AddRequestHandler<AssignWorkItemHandler>()
            .AddRequestHandler<DelegateWorkItemHandler>()
            .AddRequestHandler<EscalateWorkItemHandler>()
            .AddRequestHandler<ListWorkRemindersHandler>()
            .AddRequestHandler<GetWorkDigestHandler>()
            .AddRequestHandler<GetWorkDigestDispatchStatusHandler>()
            .AddRequestHandler<AuthorizeWorkDigestUnknownRetryHandler>()
            .AddRequestHandler<SetWorkDigestPreferenceHandler>()
            .AddRequestHandler<GetWorkDigestPreferenceHandler>()
            .AddRequestHandler<ScheduleWeeklyWorkDigestHandler>()
            .AddRequestAuthorizer<ScheduleWeeklyWorkDigestAuthorizer>()
            .AddRequestHandler<DeliverWeeklyWorkDigestHandler>()
            .AddRequestAuthorizer<DeliverWeeklyWorkDigestAuthorizer>()
            .AddRequestHandler<RecordWorkDigestDeliveryHandler>()
            .AddRequestAuthorizer<RecordWorkDigestDeliveryAuthorizer>()
            .AddRequestAuthorizer<OperationsReactionAuthorizer>()
            .AddRequestHandler<AcceptAccessPopulationHandler>()
            .AddRequestHandler<ApproveAccessExpectationHandler>()
            .AddRequestHandler<ClassifyAccessPrincipalHandler>()
            .AddRequestHandler<CompleteAccessReviewCampaignHandler>()
            .AddRequestHandler<GetAccessPopulationHandler>()
            .AddRequestHandler<GetAccessReviewCampaignHandler>()
            .AddRequestHandler<GetAccessReviewCoverageHandler>()
            .AddRequestHandler<GetAccessVarianceHandler>()
            .AddRequestHandler<LaunchAccessReviewCampaignHandler>()
            .AddRequestHandler<ListAccessExpectationsHandler>()
            .AddRequestHandler<ListAccessPopulationsHandler>()
            .AddRequestHandler<ListAccessPrincipalsHandler>()
            .AddRequestHandler<ListAccessReviewCampaignsHandler>()
            .AddRequestHandler<OpenAccessPopulationHandler>()
            .AddRequestHandler<PreviewAccessPopulationHandler>()
            .AddRequestHandler<PreviewBulkAccessDecisionHandler>()
            .AddRequestHandler<ProposeAccessExpectationHandler>()
            .AddRequestHandler<RecordAccessDecisionHandler>()
            .AddRequestHandler<ExemptAccessExpectationHandler>()
            .AddRequestHandler<ExemptMissingAccessPopulationHandler>()
            .AddRequestHandler<RecordAccessPopulationFactsHandler>()
            .AddRequestHandler<RecordAccessRemediationChangeHandler>()
            .AddRequestHandler<ReassignAccessReviewResponsibilityHandler>()
            .AddRequestHandler<ExemptAccessRemediationHandler>()
            .AddRequestHandler<MaterializeAccessReviewItemHandler>()
            .AddRequestHandler<AuthorizeAccessReviewItemCampaignCompletionUseHandler>()
            .AddRequestHandler<SettleAccessReviewItemCampaignCompletionUseHandler>()
            .AddRequestHandler<CompleteAccessReviewCampaignClosureHandler>()
            .AddRequestHandler<AbortAccessReviewCampaignCompletionHandler>()
            .AddRequestHandler<RecordBulkAccessDecisionHandler>()
            .AddRequestHandler<MaterializeAccessReviewDecisionBatchHandler>()
            .AddRequestHandler<ReserveAccessReviewItemDecisionHandler>()
            .AddRequestHandler<ApplyAccessReviewItemDecisionHandler>()
            .AddRequestHandler<PublishAccessReviewDecisionBatchHandler>()
            .AddRequestHandler<SettleAccessReviewItemDecisionHandler>()
            .AddRequestHandler<FenceAccessReviewItemDecisionHandler>()
            .AddRequestHandler<CompleteAccessReviewDecisionBatchCancellationHandler>()
            .AddRequestHandler<CancelAccessReviewDecisionBatchHandler>()
            .AddRequestHandler<VerifyAccessRemediationHandler>()
            .AddRequestAuthorizer<AccessReviewAuthorizer>()
            .AddRequestHandler<DefineControlEvaluationPlanHandler>()
            .AddRequestHandler<GetControlEvaluationPlanVersionHandler>()
            .AddRequestHandler<ListControlEvaluationPlanVersionsHandler>()
            .AddRequestHandler<StartControlEvaluationHandler>()
            .AddRequestHandler<RecordControlEvaluationStepHandler>()
            .AddRequestHandler<DisposeControlEvaluationDeviationHandler>()
            .AddRequestHandler<SubmitControlEvaluationHandler>()
            .AddRequestHandler<ReviewControlEvaluationHandler>()
            .AddRequestHandler<GetControlEvaluationHandler>()
            .AddRequestHandler<ListControlEvaluationsHandler>()
            .AddRequestHandler<RaiseEvaluationDeviationFindingHandler>()
            .AddRequestHandler<ProposeControlSuccessorHandler>()
            .AddRequestHandler<ProposeControlRetirementHandler>()
            .AddRequestHandler<PreviewControlImpactHandler>()
            .AddRequestHandler<RetireControlHandler>()
            .AddRequestHandler<WithdrawControlProposalHandler>()
            .AddRequestHandler<DesignateControlOwnerPersonHandler>()
            .AddRequestHandler<CreateCommitmentDraftHandler>()
            .AddRequestHandler<ReviseCommitmentDraftHandler>()
            .AddRequestHandler<GetCommitmentDraftHandler>()
            .AddRequestHandler<ListCommitmentDraftsHandler>()
            .AddRequestHandler<ListCommitmentDraftRevisionsHandler>()
            .AddRequestHandler<GetCommitmentDraftRevisionHandler>()
            .AddRequestHandler<ReviewCommitmentDraftHandler>()
            .AddRequestHandler<ApproveCommitmentDraftHandler>()
            .AddRequestHandler<RetireCommitmentHandler>()
            .AddRequestHandler<PreviewCommitmentImpactHandler>()
            .AddRequestHandler<GetCommitmentVersionHandler>()
            .AddRequestHandler<GetEffectiveCommitmentVersionHandler>()
            .AddRequestHandler<ListCommitmentVersionsHandler>()
            .AddRequestHandler<ListCommitmentDecisionsHandler>()
            .AddRequestHandler<CreatePolicyDraftHandler>()
            .AddRequestHandler<RevisePolicyDraftHandler>()
            .AddRequestHandler<ProposePolicySuccessorHandler>()
            .AddRequestHandler<DiscardPolicyDraftHandler>()
            .AddRequestHandler<ReviewPolicyDraftHandler>()
            .AddRequestHandler<ApprovePolicyHandler>()
            .AddRequestHandler<ConfirmPolicyReviewHandler>()
            .AddRequestHandler<ProposePolicyRetirementHandler>()
            .AddRequestHandler<ApprovePolicyRetirementHandler>()
            .AddRequestHandler<GetPolicyHandler>()
            .AddRequestHandler<ListPoliciesHandler>()
            .AddRequestHandler<GetPolicyVersionHandler>()
            .AddRequestHandler<GetEffectivePolicyVersionHandler>()
            .AddRequestHandler<ListPolicyVersionsHandler>()
            .AddRequestHandler<ListPolicyDecisionsHandler>()
            .AddRequestHandler<PreviewPolicyImpactHandler>()
            .AddRequestHandler<DefineTrainingRequirementHandler>()
            .AddRequestHandler<ReviseTrainingRequirementHandler>()
            .AddRequestHandler<GetTrainingRequirementHandler>()
            .AddRequestHandler<ListTrainingRequirementsHandler>()
            .AddRequestHandler<LaunchPolicyCampaignHandler>()
            .AddRequestHandler<LaunchTrainingCampaignHandler>()
            .AddRequestHandler<ReconcileCampaignAudienceHandler>()
            .AddRequestHandler<AcknowledgePolicyHandler>()
            .AddRequestHandler<RecordTrainingCompletionHandler>()
            .AddRequestHandler<ApproveCampaignWaiverHandler>()
            .AddRequestHandler<CloseCampaignHandler>()
            .AddRequestHandler<GetCampaignHandler>()
            .AddRequestHandler<ListCampaignsHandler>()
            .AddRequestHandler<ListCampaignParticipantsHandler>()
            .AddRequestHandler<ListCampaignAmendmentsHandler>()
            .AddRequestHandler<ApplyPolicyParticipantResultHandler>()
            .AddRequestHandler<AuthorizePolicyCampaignParticipantCampaignClosureUseHandler>()
            .AddRequestHandler<SettlePolicyCampaignParticipantCampaignClosureUseHandler>()
            .AddRequestHandler<CompletePolicyCampaignClosureHandler>()
            .AddRequestHandler<AbortPolicyCampaignClosureHandler>()
            .AddRequestHandler<CreateRiskDraftHandler>()
            .AddRequestHandler<ReviseRiskDraftHandler>()
            .AddRequestHandler<GetRiskDraftHandler>()
            .AddRequestHandler<ListRiskDraftsHandler>()
            .AddRequestHandler<ListRiskDraftRevisionsHandler>()
            .AddRequestHandler<GetRiskDraftRevisionHandler>()
            .AddRequestHandler<PublishRiskMethodVersionHandler>()
            .AddRequestHandler<GetRiskMethodHandler>()
            .AddRequestHandler<GetRiskMethodVersionHandler>()
            .AddRequestHandler<RecordRiskAssessmentHandler>()
            .AddRequestHandler<ChooseRiskTreatmentHandler>()
            .AddRequestHandler<AcceptRiskHandler>()
            .AddRequestHandler<GetRiskEvaluationHandler>()
            .AddRequestHandler<ListRiskEvaluationHistoryHandler>()
            .AddRequestHandler<AssignRiskOwnerHandler>()
            .AddRequestHandler<ProposeRiskControlTreatmentHandler>()
            .AddRequestHandler<ReviewRiskControlTreatmentHandler>()
            .AddRequestHandler<RetireRiskControlTreatmentHandler>()
            .AddRequestHandler<GetRiskGovernanceHandler>()
            .AddRequestHandler<AddRiskTreatmentActionHandler>()
            .AddRequestHandler<ReviseRiskTreatmentActionHandler>()
            .AddRequestHandler<CancelRiskTreatmentActionHandler>()
            .AddRequestHandler<SubmitRiskTreatmentActionCompletionHandler>()
            .AddRequestHandler<ReviewRiskTreatmentActionCompletionHandler>()
            .AddRequestHandler<RaiseRiskReassessmentTriggersHandler>()
            .AddRequestAuthorizer<RiskReassessmentReactionAuthorizer>()
            .AddRequestHandler<CompleteRiskTreatmentActionReviewHandler>()
            .AddRequestHandler<AbortRiskTreatmentActionReviewHandler>()
            .AddRequestAuthorizer<RiskActionReceiverAuthorizer>()
            .AddRequestHandler<Bdgrz.Compliance.Features.Evidence.AuthorizeEvidenceRequestActionCompletionUseHandler>()
            .AddRequestHandler<Bdgrz.Compliance.Features.Evidence.SettleEvidenceRequestActionCompletionUseHandler>()
            .AddRequestAuthorizer<Bdgrz.Compliance.Features.Evidence.EvidenceRequestReceiverAuthorizer>()
            .AddRequestHandler<GetMethodologyTemplateVersionHandler>()
            .AddRequestHandler<ListMethodologyTemplatesHandler>()
            .AddRequestHandler<CreateMethodologyTemplateHandler>()
            .AddRequestHandler<ReviseMethodologyTemplateDraftHandler>()
            .AddRequestHandler<ReviewMethodologyTemplateHandler>()
            .AddRequestHandler<ApproveMethodologyTemplateVersionHandler>()
            .AddRequestHandler<RetireMethodologyTemplateHandler>()
            .AddRequestHandler<GetTemplateApplicationHandler>()
            .AddRequestHandler<ApplyMethodologyTemplateHandler>()
            .AddRequestHandler<ProposeTemplateSuccessorAdoptionHandler>()
            .AddRequestHandler<RecordTemplateSuccessorDispositionHandler>()
            .AddRequestHandler<ProposeDeidentifiedTemplateContributionHandler>()
            .AddRequestHandler<ReviewTemplateContributionHandler>()
            .AddRequestHandler<AcceptTemplateContributionHandler>()
            .AddRequestHandler<ReviseProgramHandler>()
            .AddRequestHandler<ProposeProgramRolloverHandler>()
            .AddRequestHandler<ApproveProgramRolloverHandler>()
            .AddRequestHandler<SelectProgramCriteriaEditionHandler>()
            .AddRequestHandler<GetProgramHandler>()
            .AddRequestHandler<ListProgramsHandler>()
            .AddRequestHandler<ListProgramRevisionsHandler>()
            .AddRequestHandler<GetProgramRevisionHandler>()
            .AddRequestHandler<GetProgramSetupWorkHandler>()
            .AddRequestHandler<CreateClientServiceHandler>()
            .AddRequestHandler<ReviseClientServiceHandler>()
            .AddRequestHandler<RetireClientServiceHandler>()
            .AddRequestHandler<GetClientServiceHandler>()
            .AddRequestHandler<ListClientServicesHandler>()
            .AddRequestHandler<ListProgramClientServicesHandler>()
            .AddRequestHandler<ListClientServiceRevisionsHandler>()
            .AddRequestHandler<GetClientServiceRevisionHandler>()
            .AddRequestHandler<CreateBoundaryHandler>()
            .AddRequestHandler<ReviseBoundaryDraftHandler>()
            .AddRequestHandler<DiscardBoundaryDraftHandler>()
            .AddRequestHandler<GetBoundaryHandler>()
            .AddRequestHandler<ListProgramBoundariesHandler>()
            .AddRequestHandler<GetBoundaryVersionHandler>()
            .AddRequestHandler<ListBoundaryVersionsHandler>()
            .AddRequestHandler<GetEffectiveBoundaryVersionHandler>()
            .AddRequestHandler<GetBoundaryDecisionHandler>()
            .AddRequestHandler<ListBoundaryDecisionsHandler>()
            .AddRequestHandler<PreviewBoundaryImpactHandler>()
            .AddRequestHandler<ReviewBoundaryHandler>()
            .AddRequestHandler<ApproveBoundaryHandler>()
            .AddRequestHandler<RecordSeparationOfDutiesWaiverHandler>()
            .AddRequestHandler<ApproveSeparationOfDutiesWaiverHandler>()
            .AddRequestHandler<GetSeparationOfDutiesWaiverHandler>()
            .AddRequestHandler<AssignResponsibilityHandler>()
            .AddRequestHandler<RevokeResponsibilityHandler>()
            .AddRequestHandler<ListResponsibilitiesHandler>()
            .AddRequestHandler<ListMemberResponsibilitiesHandler>()
            .AddRequestHandler<PreviewResponsibilityConflictsHandler>()
            .AddRequestHandler<ProposeBoundarySuccessorHandler>()
            .AddRequestHandler<FreezeProgramScopeSnapshotHandler>()
            .AddRequestHandler<AmendProgramScopeSnapshotHandler>()
            .AddRequestHandler<GetSnapshotHandler>()
            .AddRequestHandler<VerifyProgramScopeSnapshotHandler>()
            .AddRequestHandler<RegenerateProgramScopeSnapshotManifestHandler>()
            .AddRequestHandler<ListProgramSnapshotsHandler>()
            .AddRequestHandler<FreezeWorkforceRosterSnapshotHandler>()
            .AddRequestHandler<AmendWorkforceRosterSnapshotHandler>()
            .AddRequestHandler<GetWorkforceRosterSnapshotHandler>()
            .AddRequestHandler<GetWorkforceRosterSnapshotAsOfHandler>()
            .AddRequestHandler<ListWorkforceRosterSnapshotsHandler>()
            .AddRequestHandler<RegenerateWorkforceRosterSnapshotManifestHandler>()
            .AddRequestAuthorizer<ProgramManagementAuthorizer>()
            .AddRequestAuthorizer<ClientManagementMutationAuthorizer>()
            .AddRequestAuthorizer<RestrictedComplianceRecordMutationAuthorizer>()
            .AddRequestAuthorizer<ClientRbacMutationAuthorizer>()
            .AddRequestAuthorizer<SeparationOfDutiesWaiverAuthorizer>()
            .AddRequestHandler<RegisterTenantHandler>()
            .AddRequestAuthorizer<RegisterTenantAuthorizer>()
            .AddRequestHandler<SeedPlatformOperatorRosterHandler>()
            .AddRequestAuthorizer<SeedPlatformOperatorRosterAuthorizer>()
            .AddRequestHandler<GrantPlatformOperatorHandler>()
            .AddRequestHandler<RevokePlatformOperatorHandler>()
            .AddRequestHandler<ListPlatformOperatorsHandler>()
            .AddRequestHandler<SuspendTenantHandler>()
            .AddRequestHandler<ReactivateTenantHandler>()
            .AddRequestHandler<OffboardTenantHandler>()
            .AddRequestHandler<InviteTenantMemberHandler>()
            .AddRequestHandler<InviteOrganizationMemberHandler>()
            .AddRequestHandler<InviteGuestConsultantHandler>()
            .AddRequestHandler<ListTenantInvitationsHandler>()
            .AddRequestHandler<GetMemberAccessHandler>()
            .AddRequestHandler<ProvisionGuestProgramAccessHandler>()
            .AddRequestAuthorizer<GuestInvitationReactionAuthorizer>()
            .AddRequestHandler<AcceptTenantInvitationHandler>()
            .AddRequestHandler<SendTenantInvitationHandler>()
            .AddRequestHandler<RecordTenantInvitationDeliverySentHandler>()
            .AddRequestHandler<RecordTenantInvitationDeliveryFailedHandler>()
            .AddRequestAuthorizer<TenantInvitationDeliveryReceiverAuthorizer>()
            .AddRequestHandler<GetTenantHandler>()
            .AddRequestAuthorizer<GetTenantAuthorizer>()
            .AddRequestHandler<GetTenantProvisioningStatusHandler>()
            .AddRequestAuthorizer<GetTenantProvisioningStatusAuthorizer>()
            .AddRequestHandler<ListTenantsHandler>()
            .AddRequestHandler<ListTenantMembersHandler>()
            .AddRequestHandler<ChangeTenantSlugHandler>()
            .AddRequestAuthorizer<PlatformOperatorOrTenantAdminAuthorizer>()
            .AddRequestHandler<ResolveMyTenantSlugHandler>()
            .AddRequestAuthorizer<ResolveMyTenantSlugAuthorizer>()
            .AddRequestAuthorizer<AcceptTenantInvitationAuthorizer>()
            .AddRequestHandler<ActivateTenantHandler>()
            .AddRequestAuthorizer<ActivateTenantAuthorizer>()
            .AddRequestAuthorizer<PlatformOperatorAuthorizer>()
            .AddRequestAuthorizer<ListTenantMembersAuthorizer>()
            .AddRequestHandler<ListMyTenantsHandler>()
            .AddRequestAuthorizer<ListMyTenantsAuthorizer>()
            .AddRequestGuard<RegisterTenantSlugAvailabilityGuard>()
            .AddRequestHandler<RegisterTenantSlugHandler>()
            .AddRequestHandler<ConfirmTenantSlugHandler>()
            .AddRequestHandler<RejectTenantSlugHandler>()
            .AddRequestHandler<RequestTenantSlugSurrenderHandler>()
            .AddRequestHandler<SurrenderTenantSlugHandler>()
            .AddRequestHandler<ConfirmTenantSlugSurrenderHandler>()
            .AddRequestHandler<RejectTenantSlugSurrenderHandler>()
            .AddRequestAuthorizer<TenantLifecycleReactionAuthorizer>()
            .AddReactor<TenantRegistrationReactor>("TenantRegistration", WorkloadScope.Global)
            .AddReactor<TenantInvitationReactor>("TenantInvitation", WorkloadScope.PerTenant, options =>
            {
                // Activation can wait for membership and permission projectors on another host.
                // Keep replay gaps short after the local transient retry window expires. A long
                // outage for one tenant must never fault the worker host (#430); permanent
                // failures are bounded by TenantActivationPolicy instead.
                options.MaximumFailureDelay = TimeSpan.FromSeconds(2);
                options.FailureAttemptLimit = TenantActivationPolicy.WorkloadFailureAttemptLimit;
            })
            .AddReactor<TenantInvitationDeliveryProcessReactor>("TenantInvitationDeliveryProcessV1",
                WorkloadScope.PerTenant, options => options.FailureAttemptLimit = int.MaxValue)
            .AddReactor<EvidenceArtifactInspectionEffectsReactor>(
                EvidenceArtifactInspectionEffectsReactor.WorkloadName, WorkloadScope.PerTenant)
            .AddReactor<EmailReservationReactor>("EmailReservation", WorkloadScope.Global)
            .AddReactor<EmailChallengeDeliveryReactor>("EmailChallengeDeliveryV1", WorkloadScope.Global,
                options => options.FailureAttemptLimit = int.MaxValue)
            .AddReactor<IdentityRecoveryAcceptanceReactor>("IdentityRecoveryAcceptanceV1", WorkloadScope.Global,
                options => options.FailureAttemptLimit = int.MaxValue)
            .AddProjector<MethodologyTemplateDirectoryProjector>(FitzMethodologyTemplateDirectory.ProjectorName, WorkloadScope.Global)
            .AddProjector<ObservationPeriodDirectoryProjector>(FitzObservationPeriodDirectory.ProjectorName, WorkloadScope.PerTenant)
            .AddProjector<AuditPackageDirectoryProjector>(FitzAuditPackageDirectory.ProjectorName, WorkloadScope.PerTenant)
            .AddProjector<AuditorRequestDirectoryProjector>(FitzAuditorRequestDirectory.ProjectorName, WorkloadScope.PerTenant)
            .AddProjector<PlatformUserDirectoryProjector>("PlatformUserDirectoryV2", WorkloadScope.Global)
            .AddProjector<IdentityDirectoryProjector>("UserIdentityDirectory", WorkloadScope.Global)
            .AddProjector<EmailAddressDirectoryProjector>("EmailAddressDirectory", WorkloadScope.Global)
            .AddReactor<TenantRbacBootstrapReactor>("TenantRbacBootstrap", WorkloadScope.Global)
            // Creator activation follows its durable team assignment on a tenant workload,
            // so a stalled activation cannot hold the global RBAC bootstrap cursor.
            .AddReactor<TenantSelfServiceActivationReactor>("TenantSelfServiceActivationV1",
                WorkloadScope.PerTenant, options =>
                {
                    options.MaximumFailureDelay = TimeSpan.FromSeconds(2);
                    options.FailureAttemptLimit = TenantActivationPolicy.WorkloadFailureAttemptLimit;
                })
            .AddReactor<FirmStaffRegistrationReactor>(FirmStaffRegistrationReactor.WorkloadName, WorkloadScope.Global,
                options => { options.FailureAttemptLimit = int.MaxValue; options.MaximumFailureDelay = TimeSpan.FromSeconds(2); })
            .AddReactor<ProfessionalDutyChangeReactor>(ProfessionalDutyChangeReactor.WorkloadName, WorkloadScope.Global,
                options => { options.FailureAttemptLimit = int.MaxValue; options.MaximumFailureDelay = TimeSpan.FromSeconds(2); })
            .AddReactor<ClientIndependencePreviewReactor>(ClientIndependencePreviewReactor.WorkloadName,
                WorkloadScope.PerTenant,
                options => { options.FailureAttemptLimit = int.MaxValue; options.MaximumFailureDelay = TimeSpan.FromSeconds(2); })
            .AddReactor<EngagementPersonalDecisionReactor>(EngagementPersonalDecisionReactor.WorkloadName,
                WorkloadScope.PerTenant,
                options => { options.FailureAttemptLimit = int.MaxValue; options.MaximumFailureDelay = TimeSpan.FromSeconds(2); })
            .AddReactor<EngagementAcceptanceReactor>(EngagementAcceptanceReactor.WorkloadName,
                WorkloadScope.PerTenant,
                options => { options.FailureAttemptLimit = int.MaxValue; options.MaximumFailureDelay = TimeSpan.FromSeconds(2); })
            .AddReactor<TypeIIRangeReactor>(TypeIIRangeReactor.WorkloadName, WorkloadScope.PerTenant,
                options => { options.FailureAttemptLimit = int.MaxValue; options.MaximumFailureDelay = TimeSpan.FromSeconds(2); })
            .AddReactor<TypeIIRangeReleaseReactor>(TypeIIRangeReleaseReactor.WorkloadName, WorkloadScope.PerTenant,
                options => { options.FailureAttemptLimit = int.MaxValue; options.MaximumFailureDelay = TimeSpan.FromSeconds(2); })
            .AddReactor<ControlOccurrenceFanOutReactor>(ControlOccurrenceFanOutReactor.WorkloadName, WorkloadScope.PerTenant,
                options => { options.FailureAttemptLimit = int.MaxValue; options.MaximumFailureDelay = TimeSpan.FromSeconds(2); })
            .AddReactor<ReadinessRunReactor>(ReadinessRunReactor.WorkloadName, WorkloadScope.PerTenant,
                options => { options.FailureAttemptLimit = int.MaxValue; options.MaximumFailureDelay = TimeSpan.FromSeconds(2); })
            .AddReactor<ReadinessDecisionReactor>(ReadinessDecisionReactor.WorkloadName, WorkloadScope.PerTenant,
                options => { options.FailureAttemptLimit = int.MaxValue; options.MaximumFailureDelay = TimeSpan.FromSeconds(2); })
            .AddReactor<AccessReviewLaunchFanOutReactor>(AccessReviewLaunchFanOutReactor.WorkloadName, WorkloadScope.PerTenant,
                options => { options.FailureAttemptLimit = int.MaxValue; options.MaximumFailureDelay = TimeSpan.FromSeconds(2); })
            .AddReactor<AccessReviewCompletionReactor>(AccessReviewCompletionReactor.WorkloadName, WorkloadScope.PerTenant,
                options => { options.FailureAttemptLimit = int.MaxValue; options.MaximumFailureDelay = TimeSpan.FromSeconds(2); })
            .AddReactor<AccessReviewCompletionSettlementReactor>(AccessReviewCompletionSettlementReactor.WorkloadName, WorkloadScope.PerTenant,
                options => { options.FailureAttemptLimit = int.MaxValue; options.MaximumFailureDelay = TimeSpan.FromSeconds(2); })
            .AddReactor<AuditPackageBuildProcessReactor>(AuditPackageBuildProcessReactor.WorkloadName, WorkloadScope.PerTenant,
                options => { options.FailureAttemptLimit = int.MaxValue; options.MaximumFailureDelay = TimeSpan.FromSeconds(2); })
            .AddReactor<AuditPackageDeliveryAdmissionReactor>(AuditPackageDeliveryAdmissionReactor.WorkloadName, WorkloadScope.PerTenant,
                options => { options.FailureAttemptLimit = int.MaxValue; options.MaximumFailureDelay = TimeSpan.FromSeconds(2); })
            .AddReactor<FindingClosureReactor>(FindingClosureReactor.WorkloadName, WorkloadScope.PerTenant,
                options => { options.FailureAttemptLimit = int.MaxValue; options.MaximumFailureDelay = TimeSpan.FromSeconds(2); })
            .AddReactor<AuditorRequestResolutionReactor>(AuditorRequestResolutionReactor.WorkloadName, WorkloadScope.PerTenant,
                options => { options.FailureAttemptLimit = int.MaxValue; options.MaximumFailureDelay = TimeSpan.FromSeconds(2); })
            .AddReactor<AuditorResponseMaterializationReactor>(AuditorResponseMaterializationReactor.WorkloadName, WorkloadScope.PerTenant,
                options => { options.FailureAttemptLimit = int.MaxValue; options.MaximumFailureDelay = TimeSpan.FromSeconds(2); })
            .AddReactor<AuditorCommentMaterializationReactor>(AuditorCommentMaterializationReactor.WorkloadName, WorkloadScope.PerTenant,
                options => { options.FailureAttemptLimit = int.MaxValue; options.MaximumFailureDelay = TimeSpan.FromSeconds(2); })
            .AddReactor<AuditorSampleSelectionMaterializationReactor>(AuditorSampleSelectionMaterializationReactor.WorkloadName, WorkloadScope.PerTenant,
                options => { options.FailureAttemptLimit = int.MaxValue; options.MaximumFailureDelay = TimeSpan.FromSeconds(2); })
            .AddReactor<CorrectiveActionMaterializationReactor>(CorrectiveActionMaterializationReactor.WorkloadName, WorkloadScope.PerTenant,
                options => { options.FailureAttemptLimit = int.MaxValue; options.MaximumFailureDelay = TimeSpan.FromSeconds(2); })
            .AddReactor<PolicyParticipantResultReactor>(PolicyParticipantResultReactor.WorkloadName, WorkloadScope.PerTenant,
                options => { options.FailureAttemptLimit = int.MaxValue; options.MaximumFailureDelay = TimeSpan.FromSeconds(2); })
            .AddReactor<PolicyCampaignClosureReactor>(PolicyCampaignClosureReactor.WorkloadName, WorkloadScope.PerTenant,
                options => { options.FailureAttemptLimit = int.MaxValue; options.MaximumFailureDelay = TimeSpan.FromSeconds(2); })
            .AddReactor<AccessReviewBulkAdmissionReactor>(AccessReviewBulkAdmissionReactor.WorkloadName, WorkloadScope.PerTenant,
                options => { options.FailureAttemptLimit = int.MaxValue; options.MaximumFailureDelay = TimeSpan.FromSeconds(2); })
            .AddReactor<AccessReviewBulkCancellationReactor>(AccessReviewBulkCancellationReactor.WorkloadName, WorkloadScope.PerTenant,
                options => { options.FailureAttemptLimit = int.MaxValue; options.MaximumFailureDelay = TimeSpan.FromSeconds(2); })
            .AddReactor<AccessReviewBulkCancellationCompletionReactor>(AccessReviewBulkCancellationCompletionReactor.WorkloadName, WorkloadScope.PerTenant,
                options => { options.FailureAttemptLimit = int.MaxValue; options.MaximumFailureDelay = TimeSpan.FromSeconds(2); })
            .AddReactor<AccessReviewBulkSettlementReactor>(AccessReviewBulkSettlementReactor.WorkloadName, WorkloadScope.PerTenant,
                options => { options.FailureAttemptLimit = int.MaxValue; options.MaximumFailureDelay = TimeSpan.FromSeconds(2); })
            .AddReactor<AdvisorFeedbackMaterializationReactor>(AdvisorFeedbackMaterializationReactor.WorkloadName, WorkloadScope.PerTenant,
                options => { options.FailureAttemptLimit = int.MaxValue; options.MaximumFailureDelay = TimeSpan.FromSeconds(2); })
            .AddReactor<FirmStaffStatusReevaluationReactor>(FirmStaffStatusReevaluationReactor.WorkloadName, WorkloadScope.Global,
                options => { options.FailureAttemptLimit = int.MaxValue; options.MaximumFailureDelay = TimeSpan.FromSeconds(2); })
            .AddReactor<AcceptedStaffDirectoryReconciliationReactor>(AcceptedStaffDirectoryReconciliationReactor.WorkloadName, WorkloadScope.PerTenant,
                options => { options.FailureAttemptLimit = int.MaxValue; options.MaximumFailureDelay = TimeSpan.FromSeconds(2); })
            .AddReactor<ImportSourceAdmissionReactor>(ImportSourceAdmissionReactor.WorkloadName, WorkloadScope.PerTenant,
                options => { options.FailureAttemptLimit = int.MaxValue; options.MaximumFailureDelay = TimeSpan.FromSeconds(2); })
            .AddReactor<ImportCancellationReactor>(ImportCancellationReactor.WorkloadName, WorkloadScope.PerTenant,
                options => { options.FailureAttemptLimit = int.MaxValue; options.MaximumFailureDelay = TimeSpan.FromSeconds(2); })
            .AddReactor<TenantSlugReactor>("TenantSlug", WorkloadScope.Global)
            .AddReactor<RiskMethodReassessmentReactor>(RiskMethodReassessmentReactor.WorkloadName,
                WorkloadScope.PerTenant,
                options => { options.FailureAttemptLimit = int.MaxValue; options.MaximumFailureDelay = TimeSpan.FromSeconds(2); })
            .AddReactor<BoundaryRiskReassessmentReactor>(BoundaryRiskReassessmentReactor.WorkloadName,
                WorkloadScope.PerTenant,
                options => { options.FailureAttemptLimit = int.MaxValue; options.MaximumFailureDelay = TimeSpan.FromSeconds(2); })
            .AddReactor<RiskActionCompletionReviewReactor>(RiskActionCompletionReviewReactor.WorkloadName,
                WorkloadScope.PerTenant,
                options => { options.FailureAttemptLimit = int.MaxValue; options.MaximumFailureDelay = TimeSpan.FromSeconds(2); })
            .AddReactor<TeamCleanupReactor>("TeamCleanup", WorkloadScope.PerTenant)
            .AddReactor<ProviderChangeAcceptedReactor>("ProviderChangeAcceptedV1", WorkloadScope.PerTenant,
                options => options.FailureAttemptLimit = int.MaxValue)
            .AddReactor<LocationChangeAcceptedReactor>("LocationChangeAcceptedV1", WorkloadScope.PerTenant,
                options => options.FailureAttemptLimit = int.MaxValue)
            .AddReactor<OperationalProcessChangeAcceptedReactor>("OperationalProcessChangeAcceptedV1",
                WorkloadScope.PerTenant, options => options.FailureAttemptLimit = int.MaxValue)
            .AddReactor<ProviderReviewReactor>(ProviderReviewReactor.WorkloadName, WorkloadScope.PerTenant,
                options => options.FailureAttemptLimit = int.MaxValue)
            .AddReactor<ProviderRiskAcceptanceLinkReactor>(ProviderRiskAcceptanceLinkReactor.WorkloadName,
                WorkloadScope.PerTenant, options => options.FailureAttemptLimit = int.MaxValue)
            .AddReactor<AdministratorWithdrawalReactor>("AdministratorWithdrawalV1", WorkloadScope.PerTenant,
                options => options.FailureAttemptLimit = int.MaxValue)
            .AddReactor<AdministratorAssignmentWithdrawalReactor>("AdministratorAssignmentWithdrawalV1",
                WorkloadScope.PerTenant, options => options.FailureAttemptLimit = int.MaxValue)
            .AddReactor<AdministratorRestorationReactor>("AdministratorRestorationV1", WorkloadScope.PerTenant,
                options => options.FailureAttemptLimit = int.MaxValue)
            .AddReactor<AdministratorAssignmentRestorationReactor>("AdministratorAssignmentRestorationV1",
                WorkloadScope.PerTenant, options => options.FailureAttemptLimit = int.MaxValue)
            .AddReactor<AdministratorNeutralMemberMutationReactor>("AdministratorNeutralMemberMutationV1",
                WorkloadScope.PerTenant, options => options.FailureAttemptLimit = int.MaxValue)
            .AddReactor<AdministratorNeutralAssignmentMutationReactor>("AdministratorNeutralAssignmentMutationV1",
                WorkloadScope.PerTenant, options => options.FailureAttemptLimit = int.MaxValue)
            .AddReactor<MemberRetirementReactor>("MemberRetirementV1", WorkloadScope.PerTenant,
                options => options.FailureAttemptLimit = int.MaxValue)
            .AddReactor<RetiredTeamAssignmentReactor>("RetiredTeamAssignmentV1", WorkloadScope.PerTenant,
                options => options.FailureAttemptLimit = int.MaxValue)
            .AddReactor<RetiredMemberGrantReactor>("RetiredMemberGrantV1", WorkloadScope.PerTenant,
                options => options.FailureAttemptLimit = int.MaxValue)
            .AddReactor<RoleCleanupReactor>("RoleCleanup", WorkloadScope.PerTenant)
            .AddReactor<ControlOccurrenceFindingReactor>(
                ControlOccurrenceFindingReactor.WorkloadName, WorkloadScope.PerTenant)
            .AddReactor<LinkImpactReviewWorkReactor>(
                LinkImpactReviewWorkReactor.WorkloadName, WorkloadScope.PerTenant)
            .AddReactor<SystemDescriptionDecisionReactor>(
                SystemDescriptionDecisionReactor.WorkloadName, WorkloadScope.PerTenant)
            .AddReactor<ControlEvaluationDeviationFindingReactor>(
                ControlEvaluationDeviationFindingReactor.WorkloadName, WorkloadScope.PerTenant)
            .AddProjector<ActualStaffEngagementLocatorProjector>(ActualStaffEngagementLocatorProjector.WorkloadName,
                WorkloadScope.PerTenant)
            .AddProjector<PermissionProjector>("PermissionProjection", WorkloadScope.PerTenant)
            .AddProjector<TeamDirectoryProjector>("TeamDirectory", WorkloadScope.PerTenant)
            .AddProjector<TeamMemberDirectoryProjector>("TeamMemberDirectory", WorkloadScope.PerTenant)
            .AddProjector<RoleDirectoryProjector>("RoleDirectory", WorkloadScope.PerTenant)
            .AddProjector<RolePermissionDirectoryProjector>("RolePermissionDirectory", WorkloadScope.PerTenant)
            .AddProjector<RoleTeamDirectoryProjector>("RoleTeamDirectory", WorkloadScope.PerTenant)
            .AddProjector<TeamRoleDirectoryProjector>("TeamRoleDirectory", WorkloadScope.PerTenant)
            .AddProjector<FirmStaffDirectoryProjector>(FitzFirmStaffDirectory.ProjectorName, WorkloadScope.Global)
            .AddProjector<FirmProfessionalDutyDirectoryProjector>(FitzFirmProfessionalDutyDirectory.ProjectorName,
                WorkloadScope.Global)
            .AddProjector<TenantDirectoryProjector>("TenantDirectory", WorkloadScope.Global)
            .AddProjector<TenantMembershipProjector>("TenantMembership", WorkloadScope.PerTenant)
            .AddProjector<TenantInvitationDirectoryProjector>("TenantInvitationDirectory",
                WorkloadScope.PerTenant)
            .AddProjector<ProgramDirectoryProjector>("ProgramDirectory", WorkloadScope.PerTenant)
            .AddProjector<ExaminationEngagementDirectoryProjector>(
                FitzExaminationEngagementDirectory.ProjectorName, WorkloadScope.PerTenant)
            .AddProjector<SignificantChangeDirectoryProjector>(
                SignificantChangeDirectoryProjector.ProjectorName, WorkloadScope.PerTenant)
            .AddProjector<ComplianceIncidentDirectoryProjector>(
                ComplianceIncidentDirectoryProjector.ProjectorName, WorkloadScope.PerTenant)
            .AddProjector<AdvisorHandoffDirectoryProjector>(
                FitzAdvisorHandoffDirectory.ProjectorName, WorkloadScope.PerTenant)
            .AddProjector<AdvisorFeedbackWorkItemProjector>(
                FitzAdvisorFeedbackWorkItemDirectory.ProjectorName, WorkloadScope.PerTenant)
            .AddProjector<ControlDraftDirectoryV2Projector>(FitzControlDraftDirectoryV2.ProjectorName,
                WorkloadScope.PerTenant)
            .AddProjector<ControlDraftHistoryDirectoryV1Projector>(
                FitzControlDraftHistoryDirectoryV1.ProjectorName, WorkloadScope.PerTenant)
            .AddProjector<CommitmentDraftDirectoryProjector>(FitzCommitmentDraftDirectory.ProjectorName,
                WorkloadScope.PerTenant)
            .AddProjector<PolicyDirectoryProjector>(FitzPolicyDirectory.ProjectorName,
                WorkloadScope.PerTenant)
            .AddProjector<PolicyDecisionWorkItemProjector>(
                FitzPolicyDecisionWorkItemDirectory.ProjectorName, WorkloadScope.PerTenant)
            .AddProjector<PolicyCampaignWorkItemProjector>(
                FitzPolicyCampaignWorkItemDirectory.ProjectorName, WorkloadScope.PerTenant)
            .AddProjector<CampaignDirectoryProjector>(FitzCampaignDirectory.ProjectorName,
                WorkloadScope.PerTenant)
            .AddProjector<TrainingRequirementDirectoryProjector>(FitzTrainingRequirementDirectory.ProjectorName,
                WorkloadScope.PerTenant)
            .AddProjector<CommitmentDraftHistoryDirectoryProjectorV1>(
                FitzCommitmentDraftHistoryDirectoryV1.ProjectorName, WorkloadScope.PerTenant)
            .AddProjector<RiskDraftDirectoryProjector>(FitzRiskDraftDirectory.ProjectorName, WorkloadScope.PerTenant)
            .AddProjector<PersonDirectoryProjector>("PersonDirectoryV2", WorkloadScope.PerTenant)
            .AddProjector<TechnologyInventoryDirectoryProjector>(
                FitzTechnologyInventoryDirectory.ProjectorName, WorkloadScope.PerTenant)
            .AddProjector<InventoryRegisterProjector>(
                FitzInventoryRegisterDirectory.ProjectorName, WorkloadScope.PerTenant)
            .AddProjector<WorkRelationshipDirectoryProjector>("WorkRelationshipDirectoryV1",
                WorkloadScope.PerTenant)
            .AddProjector<WorkforceObservationProjector>("WorkforceObservationsV1",
                WorkloadScope.PerTenant)
            .AddProjector<WorkforceObservationResolutionProjector>(
                "WorkforceObservationResolutionsV1", WorkloadScope.PerTenant)
            .AddProjector<WorkforceSourceProjector>("WorkforceSourcesV1", WorkloadScope.PerTenant)
            .AddProjector<ServiceIdentityDirectoryProjector>("ServiceIdentityDirectoryV1",
                WorkloadScope.PerTenant)
            .AddProjector<RiskDraftHistoryProjectorV1>(FitzRiskDraftHistoryDirectoryV1.ProjectorName,
                WorkloadScope.PerTenant)
            .AddProjector<RiskEvaluationDirectoryProjectorV1>(
                FitzRiskEvaluationDirectory.ProjectorName, WorkloadScope.PerTenant)
            .AddProjector<ControlMappingDirectoryV1Projector>(
                FitzControlMappingDirectoryV1.ProjectorName, WorkloadScope.PerTenant)
            .AddProjector<CriterionApplicabilityDirectoryV1Projector>(
                FitzCriterionApplicabilityDirectoryV1.ProjectorName, WorkloadScope.PerTenant)
            .AddProjector<ApplicationDirectoryProjector>("ApplicationDirectoryV2", WorkloadScope.PerTenant)
            .AddProjector<ApplicationImportProjector>("ApplicationImportDirectoryV1", WorkloadScope.PerTenant)
            .AddProjector<AccessPopulationDirectoryProjector>(
                AccessReviewDirectorySchema.PopulationProjector, WorkloadScope.PerTenant)
            .AddProjector<AccessReviewCampaignDirectoryProjector>(
                AccessReviewDirectorySchema.CampaignProjector, WorkloadScope.PerTenant)
            .AddProjector<AccessReviewCampaignImpactProjector>(
                AccessReviewCampaignImpactSchema.Projector, WorkloadScope.PerTenant)
            .AddProjector<AccessReviewCampaignWorkItemProjector>(
                FitzAccessReviewCampaignWorkItemDirectory.ProjectorName, WorkloadScope.PerTenant)
            .AddProjector<AccessReviewScopeDirectoryProjector>(
                AccessReviewScopeStreams.ProjectorName, WorkloadScope.PerTenant)
            .AddProjector<ApplicationBoundaryReferenceProjector>(
                "ApplicationBoundaryReferencesV3", WorkloadScope.PerTenant)
            .AddProjector<ApplicationRelationshipProjector>(
                "ApplicationRelationshipsV1", WorkloadScope.PerTenant)
            .AddProjector<ApplicationIncomingRelationshipProjector>(
                ApplicationIncomingRelationshipProjector.ProjectorName, WorkloadScope.PerTenant)
            .AddProjector<ApplicationControlDraftReferenceProjector>(
                "ApplicationControlDraftReferencesV1", WorkloadScope.PerTenant)
            .AddProjector<ApplicationControlVersionReferenceProjector>(
                FitzApplicationControlVersionReferenceDirectory.ProjectorName,
                WorkloadScope.PerTenant)
            .AddProjector<ApplicationPolicyReferenceProjector>(
                FitzApplicationPolicyReferenceDirectory.ProjectorName, WorkloadScope.PerTenant)
            .AddProjector<ApplicationEvidenceSourceReferenceProjector>(
                FitzApplicationEvidenceSourceReferenceDirectory.ProjectorName,
                WorkloadScope.PerTenant)
            .AddProjector<ProviderProjector>(FitzProviderDirectory.ProjectorName, WorkloadScope.PerTenant)
            .AddProjector<AssuranceProjector>(FitzAssuranceDirectory.ProjectorName, WorkloadScope.PerTenant)
            .AddProjector<ClientServiceDirectoryProjector>("ClientServiceDirectory", WorkloadScope.PerTenant)
            .AddProjector<BoundaryDirectoryProjector>("BoundaryDirectoryV3", WorkloadScope.PerTenant)
            .AddProjector<BoundaryDecisionWorkItemProjector>(
                FitzBoundaryDecisionWorkItemDirectory.ProjectorName, WorkloadScope.PerTenant)
            .AddProjector<CommitmentDecisionWorkItemProjector>(
                FitzCommitmentDecisionWorkItemDirectory.ProjectorName, WorkloadScope.PerTenant)
            .AddProjector<ControlDecisionWorkItemProjector>(
                FitzControlDecisionWorkItemDirectory.ProjectorName, WorkloadScope.PerTenant)
            .AddProjector<EvidenceWorkItemProjector>(FitzEvidenceWorkItemDirectory.ProjectorName,
                WorkloadScope.PerTenant)
            .AddProjector<FindingClosureWorkItemProjector>(
                FitzFindingClosureWorkItemDirectory.ProjectorName, WorkloadScope.PerTenant)
            .AddProjector<CorrectiveActionWorkItemProjector>(
                FitzCorrectiveActionWorkItemDirectory.ProjectorName, WorkloadScope.PerTenant)
            .AddProjector<ControlEvaluationWorkItemProjector>(
                FitzControlEvaluationWorkItemDirectory.ProjectorName, WorkloadScope.PerTenant)
            .AddProjector<ControlOperatingPlanWorkItemProjector>(
                FitzControlOperatingPlanWorkItemDirectory.ProjectorName, WorkloadScope.PerTenant)
            .AddProjector<ControlOccurrenceWorkItemProjector>(
                FitzControlOccurrenceWorkItemDirectory.ProjectorName, WorkloadScope.PerTenant)
            .AddProjector<ControlCriterionMappingWorkItemProjector>(
                FitzControlMappingWorkItemDirectory.ProjectorName, WorkloadScope.PerTenant)
            .AddProjector<CriterionApplicabilityWorkItemProjector>(
                FitzCriterionApplicabilityWorkItemDirectory.ProjectorName, WorkloadScope.PerTenant)
            .AddProjector<ResponsibilitySetProjector>("ResponsibilitySetsV1", WorkloadScope.PerTenant)
            .AddProjector<MemberResponsibilityProjector>(FitzMemberResponsibilityIndex.ProjectorName, WorkloadScope.PerTenant)
            .AddProjector<AccessGrantProjector>("AccessGrantsV1", WorkloadScope.PerTenant)
            .AddProjector<SnapshotDirectoryProjector>("SnapshotDirectory", WorkloadScope.PerTenant)
            .AddProjector<PopulationSnapshotDirectoryProjector>("PopulationSnapshotDirectoryV2",
                WorkloadScope.PerTenant)
            .AddProjector<CriteriaTextOverlayDirectoryProjector>(
                FitzCriteriaTextOverlayDirectory.ProjectorName, WorkloadScope.PerTenant)
            .AddFitz(
                configuration.GetSection("Fitz"),
                fitz => fitz.UseKvCheckpoints("kv://bdgrz/reactors/checkpoints",
                    Cntryl.Fitz.KvDurability.Sync))
            .RequireAuthorization(options => options.AllowAnonymous<StartIdentityRecovery>());

        services.AddPersistenceSafety();
        services.AddHostedService<PlatformOperatorRosterBootstrap>();
        return portia;
    }
}
