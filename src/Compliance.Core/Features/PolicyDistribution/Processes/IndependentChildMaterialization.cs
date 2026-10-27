using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Examinations.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.PolicyDistribution.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Services;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.PolicyDistribution.Aggregates;
using Bdgrz.Compliance.Features.PolicyDistribution.Commands;
using Bdgrz.Compliance.Features.PolicyDistribution.Events;
using Bdgrz.Compliance.Features.PolicyDistribution.Queries;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.PolicyDistribution.Processes;

/// <summary>
///     The unordered process that materializes admitted children, exactly as the design catalog defines it: one root
///     fact kind and one receiver per admitted child kind, with no ordering between receivers; a parent closure drains
///     them before it acquires its own uses. This owner ships the policy participant variant: the factory builds
///     <see cref="ApplyPolicyParticipantResult" /> for a <see cref="PolicyParticipantResultAdmitted" /> root. The root
///     kinds and receivers of the other owners are part of the catalog definition and are built by the slice that
///     ships each of them; a record can only start from a root whose reactor exists, so none is dispatched before.
/// </summary>
sealed class IndependentChildMaterialization : IChildMaterializationReceiver
{
    public const string ProcessName = "IndependentChildMaterialization";
    public const string Materialize = "materialize";

    /// <summary>The generation a materialization is bound to is the admitted parent generation, frozen in the intent.</summary>
    /// <summary>The command id the process driver derives for the receiver of one admitted child.</summary>
    public static Uuid ReceiverCommandId(Uuid rootSourceEventId, string target, string episode) =>
        Bdgrz.Compliance.Features.Kernel.Services.EffectIdentity.CommandId(rootSourceEventId, $"{ProcessName}:{nameof(ApplyPolicyParticipantResult)}",
            target, episode);

    public string Receiver => nameof(ApplyPolicyParticipantResult);

    public async ValueTask<IRequest<ReceiverOutcome>?> CreateAsync(ProcessStep step, IServiceProvider services,
        CancellationToken ct)
    {
        // Only the receiver of this owner is dispatched here; the other owners' factories join it with their slices.
        if (step.Receiver != nameof(ApplyPolicyParticipantResult) ||
            step.Record.Source.Stream.Area != PolicyDistributionCampaign.Area)
            return null;
        if (step.Target is null)
            return null;
        var record = step.Record;
        var tenantId = PolicyCampaignContent.ParseTenant(record.Key.Realm);
        var store = services.GetRequiredService<IImmutableContentStore>();
        ChildAdmissionIntent intent;
        CampaignParticipantResultKind kind;
        Uuid personId;
        try
        {
            intent = await PolicyCampaignContent.ReadAsync<ChildAdmissionIntent>(store, tenantId, record.Intent, ct)
                .ConfigureAwait(false);
            (kind, personId) = intent.ChildKind switch
            {
                AdmittedChildKind.PolicyAcknowledgement => (CampaignParticipantResultKind.PolicyAcknowledgement,
                    (await PolicyCampaignContent.ReadAsync<AcknowledgePolicy>(store, tenantId,
                        intent.CanonicalAuthoredContent, ct).ConfigureAwait(false)).PersonId),
                AdmittedChildKind.TrainingCompletion => (CampaignParticipantResultKind.TrainingCompletion,
                    (await PolicyCampaignContent.ReadAsync<RecordTrainingCompletion>(store, tenantId,
                        intent.CanonicalAuthoredContent, ct).ConfigureAwait(false)).PersonId),
                AdmittedChildKind.CampaignWaiver => (CampaignParticipantResultKind.Waiver,
                    (await PolicyCampaignContent.ReadAsync<ApproveCampaignWaiver>(store, tenantId,
                        intent.CanonicalAuthoredContent, ct).ConfigureAwait(false)).PersonId),
                _ => throw new InvalidOperationException($"{intent.ChildKind} is not a campaign participant result."),
            };
        }
        catch (ImmutableContentIntegrityException exception)
        {
            throw new SourceFactVerificationException(
                $"The frozen admission cannot be verified yet: {exception.Message}");
        }

        var target = PolicyCampaignParticipant.StreamOf(tenantId, intent.ParentId, personId);
        if (target.Resource != step.Target)
            throw new InvalidOperationException($"{step.Target} is not the participant of the frozen admission.");
        return new ApplyPolicyParticipantResult(step.CommandId, record.Source, target, record.Key.RootOperationId,
            (long)intent.ParentGeneration, null, record.Intent, intent.ParentId, personId, kind);
    }
}
