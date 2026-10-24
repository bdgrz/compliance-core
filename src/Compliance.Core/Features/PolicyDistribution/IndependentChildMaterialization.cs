using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.SourceFacts;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Features.PolicyDistribution;

/// <summary>
///     The unordered process that materializes admitted children, exactly as the design catalog defines it: one root
///     fact kind and one receiver per admitted child kind, with no ordering between receivers; a parent closure drains
///     them before it acquires its own uses. This owner ships the policy participant variant: the factory builds
///     <see cref="ApplyPolicyParticipantResult" /> for a <see cref="PolicyParticipantResultAdmitted" /> root. The root
///     kinds and receivers of the other owners are part of the catalog definition and are built by the slice that
///     ships each of them; a record can only start from a root whose reactor exists, so none is dispatched before.
/// </summary>
static class IndependentChildMaterialization
{
    public const string ProcessName = "IndependentChildMaterialization";
    public const string Materialize = "materialize";

    /// <summary>The generation a materialization is bound to is the admitted parent generation, frozen in the intent.</summary>
    public static ProcessDefinition Definition() => new(ProcessName,
        [
            "AdvisorFeedbackAdmitted", "AuditorResponseAdmitted", "AuditorCommentAdmitted", "CorrectiveActionAdmitted",
            "AuditorSampleSelectionAdmitted", "ProviderCoverageGapRiskAcceptanceAdmitted",
            nameof(PolicyParticipantResultAdmitted),
        ], false,
        [
            new ProcessStageDefinition(Materialize,
                [
                    "MaterializeAdvisorFeedback", "MaterializeAuditorResponse", "MaterializeAuditorComment",
                    "MaterializeCorrectiveAction", "MaterializeAuditorSampleSelection",
                    "MaterializeProviderRiskAcceptanceLink", nameof(ApplyPolicyParticipantResult),
                ], "Single receiver per admitted child; no cross-receiver ordering. Parent closure drains these before its own acquisition stage."),
        ], [], Receiver);

    /// <summary>The command id the process driver derives for the receiver of one admitted child.</summary>
    public static Uuid ReceiverCommandId(Uuid rootSourceEventId, string target, string episode) =>
        Kernel.EffectIdentity.CommandId(rootSourceEventId, $"{ProcessName}:{nameof(ApplyPolicyParticipantResult)}",
            target, episode);

    static async ValueTask<IRequest<ReceiverOutcome>?> Receiver(ProcessStep step, IServiceProvider services,
        CancellationToken ct)
    {
        // Only the receiver of this owner is dispatched here; the other owners' factories join it with their slices.
        if (step.Receiver != nameof(ApplyPolicyParticipantResult))
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
