using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.PolicyDistribution;

/// <summary>
///     Admits one complete participant result on the campaign's open generation. The campaign owner is the first
///     owner and its logical revision the only expected revision: the result, the source proofs selected from the
///     campaign's own facts and the original actor are frozen into a <see cref="ChildAdmissionIntent" /> before the
///     fixed 202 acknowledgment is frozen on the root fact. The participant applies the result afterwards from the
///     intent alone, so an exact retry replays the original acknowledgment even after the audience changed, the
///     participant applied the result or the campaign closed.
/// </summary>
sealed class PolicyParticipantAdmission(NativeOperationExecutor operations, IImmutableContentStore content)
{
    public async ValueTask<Result<OperationAcceptedResponse>> AdmitAsync<TRequest>(
        IRequestContext<TRequest> context, CampaignState state, ParticipantAdmissionPlan plan, TRequest request,
        Func<CampaignState, ValueTask<CommandFailure?>> check, CancellationToken ct)
        where TRequest : IRequest<OperationAcceptedResponse>
    {
        if (plan.CommandId == Uuid.Empty || plan.PersonId == Uuid.Empty || plan.ExpectedRevision < 0)
            return Failure(RequestErrorKind.Validation,
                "A participant result requires a command id, a person and a non-negative expected campaign revision.");
        var campaign = state.Campaign;
        var tenantId = campaign.TenantId;
        var participant = PolicyCampaignParticipant.StreamOf(tenantId, campaign.Id, plan.PersonId);
        var now = plan.AcceptedAt;
        var provenance = PolicyCampaignContent.Provenance(plan.CommandId, plan.CommandKind, campaign.Stream,
            participant, plan.Recorder, plan.ExpectedRevision, AuthoredIntentDigest.Compute(request), now);
        var binding = AcceptedOperationBinding.Of(provenance);

        // A logical id that was already accepted resolves from the owner's facts before any guard is evaluated.
        ImmutableContentRef? intent = null;
        FirstResponseDescriptor? response = null;
        if (campaign.Acceptance.Decide(binding) == NativeAcceptanceDecision.None)
        {
            if (await check(state).ConfigureAwait(false) is { } failure)
                return Result<OperationAcceptedResponse>.Failure(failure.ToRequestError());
            var member = state.Roster.FindActive(plan.PersonId)!;
            var authored = await PolicyCampaignContent.WriteAsync(content, tenantId, plan.AuthoredSchema, request, ct)
                .ConfigureAwait(false);
            var proofs = await PolicyCampaignContent.WriteManifestAsync(content, tenantId,
                PolicyCampaignContent.AdmissionProofSchema,
                [new PolicyParticipantAdmissionProof(NativeFactReference.Of(campaign.Stream, campaign.Launch!),
                    member.IncludingFact, plan.Performer, plan.RecordedOnBehalf)],
                SelectionOrder.CanonicalKey, ct).ConfigureAwait(false);
            var header = PolicyCampaignContent.HeaderOf(provenance, campaign.Stream, participant, now);
            intent = await PolicyCampaignContent.WriteAsync(content, tenantId, ChildAdmissionIntent.Schema,
                new ChildAdmissionIntent(header, campaign.Id, (ulong)campaign.Generation, plan.ChildKind,
                    plan.CommandId, authored, proofs, (ulong)(plan.ExpectedRevision + 1)), ct).ConfigureAwait(false);
            response = await PolicyCampaignContent.AcceptedResponseAsync(content, tenantId, plan.CommandId, now, ct)
                .ConfigureAwait(false);
        }

        var outcome = await operations.ExecuteAsync(new NativeOperation<PolicyDistributionCampaign>(binding,
            () => new PolicyDistributionCampaign(tenantId, campaign.Id, plan.CommandId),
            owner => owner.Acceptance, owner => Prepare(owner, plan.ExpectedRevision, response),
            (PolicyDistributionCampaign owner, ImmutableContentRef frozen) =>
                owner.AdmitResult(provenance, intent!, frozen)), context, ct).ConfigureAwait(false);
        return await PolicyCampaignContent.AcknowledgmentAsync(content, tenantId, outcome, ct).ConfigureAwait(false);
    }

    static Result<FirstResponseDescriptor> Prepare(PolicyDistributionCampaign owner, long expectedRevision,
        FirstResponseDescriptor? response)
    {
        var prepared = owner.PrepareOpenAdmission(expectedRevision);
        if (!prepared.IsSuccess)
            return Result<FirstResponseDescriptor>.Failure(prepared.Error!);
        return response is null
            ? Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The campaign changed while the result was prepared; reload and retry.", true))
            : Result<FirstResponseDescriptor>.Success(response);
    }

    static Result<OperationAcceptedResponse> Failure(RequestErrorKind kind, string message) =>
        Result<OperationAcceptedResponse>.Failure(new RequestError(kind, message));
}

/// <summary>What a participant result freezes about itself and its actor; the request carries the authored fields.</summary>
sealed record ParticipantAdmissionPlan(Uuid CommandId, Uuid PersonId, long ExpectedRevision, string CommandKind,
    AdmittedChildKind ChildKind, string AuthoredSchema, ActorReference Recorder, ActorReference? Performer,
    bool RecordedOnBehalf, DateTimeOffset AcceptedAt);
