using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Policies.Rules;
using Bdgrz.Compliance.Features.PolicyDistribution.Rules;
using Bdgrz.Compliance.Features.PolicyDistribution.Services;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Commands;
namespace Bdgrz.Compliance.Features.PolicyDistribution.Handlers;

/// <summary>
///     Admits a reviewed completion on the campaign at the expected campaign revision; the participant applies it
///     afterwards.
/// </summary>
sealed class RecordTrainingCompletionHandler(CampaignSource campaigns, TimeProvider clock,
    PolicyParticipantAdmission admission) : IRequestHandler<RecordTrainingCompletion, OperationAcceptedResponse>
{
    public ValueTask<Result<OperationAcceptedResponse>> HandleAsync(
        IRequestContext<RecordTrainingCompletion> context, CancellationToken ct) =>
        PolicyCampaignFailures.ClassifyAsync(() => HandleCoreAsync(context, ct), ct);

    async ValueTask<Result<OperationAcceptedResponse>> HandleCoreAsync(
        IRequestContext<RecordTrainingCompletion> context, CancellationToken ct)
    {
        var request = context.Request;
        var source = await campaigns.ReadOwnerAsync(request.TenantId, request.ProgramId, request.CampaignId,
            request.CommandId, ct).ConfigureAwait(false);
        if (!source.IsSuccess)
            return Result<OperationAcceptedResponse>.Failure(source.Error);
        var actor = PolicyActor.From(context, request.TenantId);
        var now = clock.GetUtcNow();
        var plan = new ParticipantAdmissionPlan(request.CommandId, request.PersonId, request.ExpectedCampaignRevision,
            nameof(RecordTrainingCompletion), AdmittedChildKind.TrainingCompletion,
            PolicyCampaignContent.CompletionCommandSchema, actor.Reference, null, false, now);
        return await admission.AdmitAsync(context, request.TenantId, request.ProgramId, request.CampaignId, plan, request,
            current => ValueTask.FromResult(CampaignRules.CheckCompletion(current, request, now)), ct)
            .ConfigureAwait(false);
    }
}
