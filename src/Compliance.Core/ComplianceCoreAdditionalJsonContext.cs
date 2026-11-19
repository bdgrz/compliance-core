using System.Text.Json;
using System.Text.Json.Serialization;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Projections;
using Bdgrz.Compliance.Features.AccessControl.Queries;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AccessReviews.Commands;
using Bdgrz.Compliance.Features.AccessReviews.Events;
using Bdgrz.Compliance.Features.AccessReviews.Models;
using Bdgrz.Compliance.Features.AccessReviews.Queries;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Commands;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Events;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Models;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Projections;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Queries;
using Bdgrz.Compliance.Features.Applications.Commands;
using Bdgrz.Compliance.Features.Applications.Events;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.Applications.Queries;
using Bdgrz.Compliance.Features.Applications.Rules;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.AttestRecords.Events;
using Bdgrz.Compliance.Features.AttestRecords.Models;
using Bdgrz.Compliance.Features.AttestRecords.Queries;
using Bdgrz.Compliance.Features.AuditorRequests.Commands;
using Bdgrz.Compliance.Features.AuditorRequests.Events;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Queries;
using Bdgrz.Compliance.Features.AuditPackages.Commands;
using Bdgrz.Compliance.Features.AuditPackages.Events;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Queries;
using Bdgrz.Compliance.Features.AuditPopulations.Commands;
using Bdgrz.Compliance.Features.AuditPopulations.Events;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditPopulations.Queries;
using Bdgrz.Compliance.Features.Boundaries.Commands;
using Bdgrz.Compliance.Features.Boundaries.Contracts;
using Bdgrz.Compliance.Features.Boundaries.Events;
using Bdgrz.Compliance.Features.Boundaries.Models;
using Bdgrz.Compliance.Features.Boundaries.Queries;
using Bdgrz.Compliance.Features.Changes.Commands;
using Bdgrz.Compliance.Features.Changes.Events;
using Bdgrz.Compliance.Features.Changes.Models;
using Bdgrz.Compliance.Features.Changes.Queries;
using Bdgrz.Compliance.Features.Commitments.Commands;
using Bdgrz.Compliance.Features.Commitments.Contracts;
using Bdgrz.Compliance.Features.Commitments.Events;
using Bdgrz.Compliance.Features.Commitments.Models;
using Bdgrz.Compliance.Features.Commitments.Queries;
using Bdgrz.Compliance.Features.Content.Models;
using Bdgrz.Compliance.Features.ControlMappings.Commands;
using Bdgrz.Compliance.Features.ControlMappings.Events;
using Bdgrz.Compliance.Features.ControlMappings.Models;
using Bdgrz.Compliance.Features.ControlMappings.Queries;
using Bdgrz.Compliance.Features.Controls.Commands;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Controls.Events;
using Bdgrz.Compliance.Features.Controls.Models;
using Bdgrz.Compliance.Features.Controls.Queries;
using Bdgrz.Compliance.Features.Criteria.Commands;
using Bdgrz.Compliance.Features.Criteria.Events;
using Bdgrz.Compliance.Features.Criteria.Models;
using Bdgrz.Compliance.Features.Criteria.Queries;
using Bdgrz.Compliance.Features.Evaluations.Commands;
using Bdgrz.Compliance.Features.Evaluations.Events;
using Bdgrz.Compliance.Features.Evaluations.Models;
using Bdgrz.Compliance.Features.Evaluations.Queries;
using Bdgrz.Compliance.Features.Evidence.Commands;
using Bdgrz.Compliance.Features.Evidence.Events;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Queries;
using Bdgrz.Compliance.Features.Examinations.Commands;
using Bdgrz.Compliance.Features.Examinations.Events;
using Bdgrz.Compliance.Features.Examinations.History;
using Bdgrz.Compliance.Features.Examinations.Models;
using Bdgrz.Compliance.Features.Examinations.Queries;
using Bdgrz.Compliance.Features.Examinations.Rules;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.Exports.Events;
using Bdgrz.Compliance.Features.Exports.Models;
using Bdgrz.Compliance.Features.FirmPortfolio.Models;
using Bdgrz.Compliance.Features.FirmPortfolio.Queries;
using Bdgrz.Compliance.Features.Incidents.Commands;
using Bdgrz.Compliance.Features.Incidents.Events;
using Bdgrz.Compliance.Features.Incidents.Models;
using Bdgrz.Compliance.Features.Incidents.Queries;
using Bdgrz.Compliance.Features.MethodologyTemplates.Commands;
using Bdgrz.Compliance.Features.MethodologyTemplates.Events;
using Bdgrz.Compliance.Features.MethodologyTemplates.Models;
using Bdgrz.Compliance.Features.MethodologyTemplates.Queries;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Commands;
using Bdgrz.Compliance.Features.Operations.Events;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Queries;
using Bdgrz.Compliance.Features.Policies.Commands;
using Bdgrz.Compliance.Features.Policies.Contracts;
using Bdgrz.Compliance.Features.Policies.Events;
using Bdgrz.Compliance.Features.Policies.Models;
using Bdgrz.Compliance.Features.Policies.Queries;
using Bdgrz.Compliance.Features.Policies.Rules;
using Bdgrz.Compliance.Features.PolicyDistribution.Commands;
using Bdgrz.Compliance.Features.PolicyDistribution.Events;
using Bdgrz.Compliance.Features.PolicyDistribution.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Queries;
using Bdgrz.Compliance.Features.PolicyDistribution.Rules;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Commands;
using Bdgrz.Compliance.Features.Programs.Events;
using Bdgrz.Compliance.Features.Programs.Models;
using Bdgrz.Compliance.Features.Programs.Queries;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Commands;
using Bdgrz.Compliance.Features.Providers.Events;
using Bdgrz.Compliance.Features.Providers.Models;
using Bdgrz.Compliance.Features.Providers.Queries;
using Bdgrz.Compliance.Features.Readiness.Commands;
using Bdgrz.Compliance.Features.Readiness.Events;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Projections;
using Bdgrz.Compliance.Features.Readiness.Queries;
using Bdgrz.Compliance.Features.Remediation.Commands;
using Bdgrz.Compliance.Features.Remediation.Events;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Queries;
using Bdgrz.Compliance.Features.Responsibilities.Commands;
using Bdgrz.Compliance.Features.Responsibilities.Events;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.Responsibilities.Queries;
using Bdgrz.Compliance.Features.Retention.Commands;
using Bdgrz.Compliance.Features.Retention.Events;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Retention.Queries;
using Bdgrz.Compliance.Features.Risks.Commands;
using Bdgrz.Compliance.Features.Risks.Events;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.Risks.Queries;
using Bdgrz.Compliance.Features.Snapshots.Commands;
using Bdgrz.Compliance.Features.Snapshots.Events;
using Bdgrz.Compliance.Features.Snapshots.Models;
using Bdgrz.Compliance.Features.Snapshots.Queries;
using Bdgrz.Compliance.Features.SourceFacts.Projections;
using Bdgrz.Compliance.Features.SourceUse.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.SystemDescription.Commands;
using Bdgrz.Compliance.Features.SystemDescription.Events;
using Bdgrz.Compliance.Features.SystemDescription.Models;
using Bdgrz.Compliance.Features.SystemDescription.Processes;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.SystemDescription.Queries;
using Bdgrz.Compliance.Features.TechnologyInventory.Commands;
using Bdgrz.Compliance.Features.TechnologyInventory.Events;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Queries;
using Bdgrz.Compliance.Features.Tenants.Commands;
using Bdgrz.Compliance.Features.Tenants.Events;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Queries;
using Bdgrz.Compliance.Features.UserIdentities.Commands;
using Bdgrz.Compliance.Features.UserIdentities.Events;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Queries;
using Bdgrz.Compliance.Features.Work.Commands;
using Bdgrz.Compliance.Features.Work.Events;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.Work.Projections;
using Bdgrz.Compliance.Features.Work.Queries;
using Bdgrz.Compliance.Features.Work.Rules;
using Bdgrz.Compliance.Features.Workforce.Commands;
using Bdgrz.Compliance.Features.Workforce.Events;
using Bdgrz.Compliance.Features.Workforce.Models;
using Bdgrz.Compliance.Features.Workforce.Projections;
using Bdgrz.Compliance.Features.Workforce.Queries;
using Cntryl.Portia;
namespace Bdgrz.Compliance;

[PortiaJsonContext]
[JsonSourceGenerationOptions(
    JsonSerializerDefaults.Web,
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Evidence.Events.EvidenceSupportWithdrawn))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Evidence.Commands.WithdrawEvidenceSupport))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Evidence.Queries.GetEvidenceSupport))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Evidence.Queries.ListEvidenceSupports))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Evidence.Models.EvidenceSupportStatusView))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Evidence.Models.EvidenceSupportWithdrawalView))]
[JsonSerializable(typeof(Page<Bdgrz.Compliance.Features.Evidence.Models.EvidenceSupportStatusView>))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Readiness.Commands.RunDailyReadinessAssessments))]
[JsonSerializable(typeof(FindingClosureProposed))]
[JsonSerializable(typeof(ProposeFindingClosure))]
[JsonSerializable(typeof(ReviewFindingClosure))]
[JsonSerializable(typeof(WithdrawFindingClosure))]
[JsonSerializable(typeof(ReviseFindingAnalysis))]
[JsonSerializable(typeof(FindingClosureReturned))]
[JsonSerializable(typeof(FindingClosureWithdrawn))]
[JsonSerializable(typeof(FindingAnalysisRevised))]
[JsonSerializable(typeof(GetPlatformOperatorState))]
[JsonSerializable(typeof(PlatformOperatorStateView))]
[JsonSerializable(typeof(DesignatePlatformPrimary))]
[JsonSerializable(typeof(ReleasePlatformPrimary))]
[JsonSerializable(typeof(Page<Bdgrz.Compliance.Features.Tenants.Models.PlatformOperatorDirectoryEntry>))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Tenants.Projections.PlatformOperatorDirectoryRow))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.AccessControl.Events.PlatformOperatorBootstrapCompleted))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.AccessControl.Events.PlatformOperatorAccessGranted))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.AccessControl.Events.PlatformOperatorBootstrapAccessGranted))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.AccessControl.Processes.ReconcilePlatformPrimarySignal))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.AccessControl.Events.PlatformOperatorAccessRevoked))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.AccessControl.Events.PlatformOperatorBootstrapGrantAndMarkAccepted))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.AccessControl.Events.PlatformOperatorPrimaryMarkAccepted))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.AccessControl.Events.PlatformOperatorPrimaryMarkFenced))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.AccessControl.Events.PlatformOperatorPrimaryUnmarked))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.AccessControl.Events.PrimaryPlatformOperatorSeatReserved))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.AccessControl.Events.PrimaryPlatformOperatorAppointed))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.AccessControl.Events.PrimaryPlatformOperatorSeatRefused))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.AccessControl.Events.PrimaryPlatformOperatorSeatReleased))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.AccessControl.Events.PrimaryPlatformOperatorSeatVacated))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.AccessControl.Events.AccessPolicyActivated))]
[JsonSerializable(typeof(DesignatePrimaryOrgAdmin))]
[JsonSerializable(typeof(ReleasePrimaryOrgAdmin))]
[JsonSerializable(typeof(MarkMemberPrimary))]
[JsonSerializable(typeof(UnmarkMemberPrimary))]
[JsonSerializable(typeof(RecordPrimaryMarkOutcome))]
[JsonSerializable(typeof(GetPrimaryOrgAdmins))]
[JsonSerializable(typeof(PrimaryOrgAdminsView))]
[JsonSerializable(typeof(PrimaryOrgAdminSeat))]
[JsonSerializable(typeof(PrimaryOrgAdminDesignationRequested))]
[JsonSerializable(typeof(PrimaryOrgAdminDesignated))]
[JsonSerializable(typeof(PrimaryOrgAdminDesignationRefused))]
[JsonSerializable(typeof(PrimaryOrgAdminReleased))]
[JsonSerializable(typeof(MemberMarkedPrimary))]
[JsonSerializable(typeof(MemberPrimaryMarkRefused))]
[JsonSerializable(typeof(MemberPrimaryMarkRemoved))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.AccessControl.Models.AccessAdmissionEvidence))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.AccessControl.Models.AccessAdmissionSourceVersion))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Criteria.Commands.AbandonCriteriaEditionDraft))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Criteria.Commands.BeginCriteriaEditionRename))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Criteria.Commands.BeginCriteriaPublication))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Criteria.Commands.CancelCriteriaEditionDraftRequest))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Criteria.Commands.CancelCriteriaEditionRename))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Criteria.Commands.ChooseInitialEditionLabel))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Criteria.Commands.CorrectCriterionSummary))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Criteria.Commands.DraftCriteriaEdition))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Criteria.Commands.SetCriteriaEditionField))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Criteria.Commands.SetCriteriaEditionDisclosure))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Criteria.Commands.ProposeCriterionChange))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Criteria.Events.CriteriaEditionDraftAbandoned))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Criteria.Events.CriteriaEditionDraftActivated))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Criteria.Events.CriteriaEditionDraftRenamed))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Criteria.Events.CriteriaEditionDraftRequestCancelled))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Criteria.Events.CriteriaEditionDraftRequested))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Criteria.Events.CriteriaEditionDrafted))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Criteria.Events.CriteriaEditionOperationAttemptFenced))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Criteria.Events.CriteriaEditionPublished))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Criteria.Events.CriteriaEditionValueOperationClaimed))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Criteria.Events.CriteriaEditionValueProposed))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Criteria.Events.CriteriaEditionValueAccepted))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Criteria.Events.CriteriaEditionValueRefused))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Criteria.Events.CriteriaEditionRenameCancelled))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Criteria.Events.CriteriaEditionRenameCompleted))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Criteria.Events.CriteriaEditionRenameRefused))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Criteria.Events.CriteriaEditionRenameRequested))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Criteria.Events.CriteriaInitialLabelRefused))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Criteria.Events.CriteriaInitialLabelRequested))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Criteria.Events.CriteriaPublicationAbortCleanupCompleted))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Criteria.Events.CriteriaPublicationAborted))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Criteria.Events.CriteriaPublicationStarted))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Criteria.Events.CriteriaSeedDisclosureRecorded))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Criteria.Events.CriteriaSeedImportCompleted))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Criteria.Events.CriteriaSeedMetadataRecorded))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Criteria.Events.CriterionChangeProposed))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Criteria.Events.CriterionEntryOperationClaimed))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Criteria.Events.CriterionEntryOperationSeedClaimed))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Criteria.Events.CriterionSeedProposed))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Criteria.Events.CriterionChangeRefused))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Criteria.Events.CriterionChanged))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Criteria.Events.CriterionOperationAttemptFenced))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Criteria.Events.CriterionSummaryCorrected))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Criteria.Events.CriterionSummaryUnchanged))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Criteria.Events.CriterionSummaryRefused))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Criteria.Events.CriterionSummaryAttemptFenced))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Criteria.Events.CriterionWitnessPinFenced))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Criteria.Events.CriterionWitnessPinned))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Criteria.Events.CriterionWitnessRefused))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Criteria.Events.EditionLabelConfirmed))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Criteria.Events.EditionLabelReleased))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Criteria.Events.EditionLabelReserved))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Criteria.Events.EditionLabelReservationFenced))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Criteria.Events.EditionLabelReservationRefused))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Criteria.Models.CriteriaEditionDraftView))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Criteria.Models.CriteriaEditionValueIntent))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Criteria.Models.CriteriaEditionValueDecisionEvidence))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Criteria.Models.CriteriaEntryDecisionEvidence))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Criteria.Models.CriteriaEntryAuthorityResult))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Criteria.Models.CriteriaEntryAuthorityStatus))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Criteria.Models.CriteriaEntryProof))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Criteria.Models.CriteriaOperationReceipt))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Criteria.Models.CriterionChangeIntent))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Criteria.Models.CriterionSummaryIntent))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Criteria.Models.RuntimeCriteriaDisclosure))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Criteria.Models.RuntimeCriteriaDisclosurePage))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Criteria.Models.RuntimeCriteriaEdition))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Criteria.Models.RuntimeCriteriaEditionsPage))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Criteria.Models.RuntimeCriteriaEntry))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Criteria.Models.RuntimeCriteriaPage))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Criteria.Queries.GetCriteriaEditionDraft))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Criteria.Queries.GetCriteriaOperation))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Criteria.Queries.GetCriteriaEditionValueOperation))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Criteria.Queries.GetRuntimeCriteriaEdition))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Criteria.Queries.GetRuntimeCriteriaEntry))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Criteria.Queries.ListRuntimeCriteriaEditions))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Criteria.Queries.ListRuntimeCriteriaEntries))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Criteria.Queries.ListRuntimeCriteriaDisclosures))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Criteria.Commands.ApplyCriteriaLabelConfirmation))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Criteria.Commands.ApplyCriteriaEditionLabelRefusal))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Criteria.Commands.DecideCriteriaEditionLabelRefusal))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Criteria.Commands.ApplyCriteriaLabelReservation))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Criteria.Commands.CompleteCriteriaPublicationAbort))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Criteria.Commands.CompleteCriteriaPublication))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Criteria.Commands.DecideCriteriaWitnessRefusal))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Criteria.Commands.AbortCriteriaPublication))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Criteria.Commands.ConfirmCriteriaEditionLabel))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Criteria.Commands.FenceCriteriaPublicationWitness))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Criteria.Commands.FenceCriteriaEditionLabelRequest))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Criteria.Commands.PinCriteriaPublicationWitness))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Criteria.Commands.ReleaseCriteriaEditionLabel))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Criteria.Commands.ReserveCriteriaEditionLabel))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Criteria.Processes.ReconcileCriteriaLabelSignal))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Criteria.Processes.ReconcileCriteriaValueSignal))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Criteria.Processes.ReconcileCriteriaPublicationSignal))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Criteria.Processes.ReconcileCriteriaEntrySignal))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Programs.Events.ProgramCreationAttemptFenced))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Programs.Models.CriteriaPublicationProof))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Criteria.Models.RuntimeCriteriaDisclosureIndexRow))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Criteria.Models.RuntimeCriteriaEditionIndexRow))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Criteria.Models.RuntimeCriteriaEntryIndexRow))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Criteria.Models.RuntimeCriteriaPendingIndex))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Criteria.Models.RuntimeCriteriaProjectionClock))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Criteria.Models.RuntimeCriteriaEditionFactIndexRow))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Criteria.Models.RuntimeCriteriaValueClaimIndexRow))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Criteria.Models.RuntimeCriteriaValuePendingIndexRow))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Criteria.Models.RuntimeCriteriaValueUnresolvedIndexRow))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Commitments.Governance.Commands.ConfirmCommitmentControlLink))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Commitments.Governance.Commands.DiscardCommitmentDraft))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Commitments.Governance.Commands.DraftCommitment))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Commitments.Governance.Commands.LinkCommitmentToControl))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Commitments.Governance.Commands.ProposeCommitmentChange))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Commitments.Governance.Commands.RetireGovernanceCommitment))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Commitments.Governance.Commands.ReviewGovernanceCommitmentDraft))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Commitments.Governance.Commands.ReviseGovernanceCommitmentDraft))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Commitments.Governance.Commands.UnlinkCommitmentFromControl))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Commitments.Governance.Events.CommitmentChangeProposed))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Commitments.Governance.Events.CommitmentChangesRequested))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Commitments.Governance.Events.CommitmentControlLinkConfirmed))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Commitments.Governance.Events.CommitmentControlLinked))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Commitments.Governance.Events.CommitmentControlUnlinked))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Commitments.Governance.Events.CommitmentDraftDiscarded))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Commitments.Governance.Events.CommitmentDrafted))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Commitments.Governance.Events.CommitmentVersionApproved))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Commitments.Governance.Events.GovernanceCommitmentDraftRevised))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Commitments.Governance.Events.GovernanceCommitmentRetired))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Commitments.Governance.Models.ApprovedCommitment))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Commitments.Governance.Models.CommitmentLinkStatus))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Commitments.Governance.Models.CommitmentSourceCitation))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Commitments.Governance.Models.CommitmentStatus))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Commitments.Governance.Models.CommitmentTerms))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Commitments.Governance.Queries.GetCommitment))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Commitments.Governance.Queries.GetCommitmentControlLink))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.FirmServices.Commands.DesignatePartner))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.FirmServices.Commands.DraftIndependenceRuleSet))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.FirmServices.Commands.RatifyIndependenceRuleSet))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.FirmServices.Commands.RecordFirmNonattestService))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.FirmServices.Commands.ReviseIndependenceRuleSetDraft))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.FirmServices.Commands.ReviseNonattestService))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.FirmServices.Commands.RevokePartnerDesignation))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.FirmServices.Commands.VoidNonattestService))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.FirmServices.Events.FirmNonattestServiceRecorded))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.FirmServices.Events.IndependenceRuleSetDraftRevised))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.FirmServices.Events.IndependenceRuleSetDrafted))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.FirmServices.Events.IndependenceRuleSetRatified))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.FirmServices.Events.NonattestServiceRevised))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.FirmServices.Events.NonattestServiceVoided))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.FirmServices.Events.PartnerDesignated))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.FirmServices.Events.PartnerDesignationRevoked))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.FirmServices.Models.FirmServiceReceipt))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.FirmServices.Models.IndependenceAssessmentView))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.FirmServices.Models.IndependenceRuleSetTerms))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.FirmServices.Models.IndependenceRuleSetView))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.FirmServices.Models.NonattestServiceClassification))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.FirmServices.Models.NonattestServiceRecordView))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.FirmServices.Models.NonattestServiceTerms))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.FirmServices.Models.NonattestServiceVersion))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.FirmServices.Models.PartnerDesignationView))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.FirmServices.Queries.GetActiveIndependenceRuleSet))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.FirmServices.Queries.GetNonattestServiceRecord))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.FirmServices.Queries.GetNonattestServiceRecordRevision))]
sealed partial class ComplianceCoreAdditionalJsonContext : JsonSerializerContext;
