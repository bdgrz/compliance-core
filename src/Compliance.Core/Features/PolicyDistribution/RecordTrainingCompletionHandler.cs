using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Policies;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.PolicyDistribution;

/// <summary>
///     Admits a reviewed completion on the campaign at the expected campaign revision; the participant applies it
///     afterwards.
/// </summary>
sealed class RecordTrainingCompletionHandler(CampaignSource campaigns, TimeProvider clock,
    PolicyParticipantAdmission admission) : IRequestHandler<RecordTrainingCompletion, OperationAcceptedResponse>
{
    public async ValueTask<Result<OperationAcceptedResponse>> HandleAsync(
        IRequestContext<RecordTrainingCompletion> context, CancellationToken ct)
    {
        var request = context.Request;
        var source = await campaigns.ReadAsync(request.TenantId, request.ProgramId, request.CampaignId, ct,
            request.CommandId).ConfigureAwait(false);
        if (!source.IsSuccess)
            return Result<OperationAcceptedResponse>.Failure(source.Error);
        var actor = PolicyActor.From(context, request.TenantId);
        var now = clock.GetUtcNow();
        var plan = new ParticipantAdmissionPlan(request.CommandId, request.PersonId, request.ExpectedCampaignRevision,
            nameof(RecordTrainingCompletion), AdmittedChildKind.TrainingCompletion,
            PolicyCampaignContent.CompletionCommandSchema, actor.Reference, null, false, now);
        return await admission.AdmitAsync(context, source.Value, plan, request,
            current => ValueTask.FromResult(CampaignRules.CheckCompletion(current, request, now)), ct)
            .ConfigureAwait(false);
    }
}
