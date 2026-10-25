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
sealed class PolicyParticipantAdmission(NativeOperationExecutor operations, IImmutableContentStore content, CampaignSource campaigns)
{
    public async ValueTask<Result<OperationAcceptedResponse>> AdmitAsync<TRequest>(
        IRequestContext<TRequest> context, Uuid tenantId, Uuid programId, Uuid campaignId,
        ParticipantAdmissionPlan plan, TRequest request,
        Func<CampaignState, ValueTask<CommandFailure?>> check, CancellationToken ct)
        where TRequest : IRequest<OperationAcceptedResponse>
    {
        if (plan.CommandId == Uuid.Empty || plan.PersonId == Uuid.Empty || plan.ExpectedRevision < 0)
            return Failure(RequestErrorKind.Validation,
                "A participant result requires a command id, a person and a non-negative expected campaign revision.");
        var stream = new PolicyDistributionCampaign(tenantId, campaignId).Stream;
        var participant = PolicyCampaignParticipant.StreamOf(tenantId, campaignId, plan.PersonId);
        var now = plan.AcceptedAt;
        var provenance = PolicyCampaignContent.Provenance(plan.CommandId, plan.CommandKind, stream,
            participant, plan.Recorder, plan.ExpectedRevision, AuthoredIntentDigest.Compute(request), now);
        var binding = AcceptedOperationBinding.Of(provenance);

        ImmutableContentRef? intent = null;
        PolicyCampaignPreparedCut? preparedCut = null;
        var outcome = await operations.ExecuteAsync(new NativeOperation<PolicyDistributionCampaign>(binding,
            () => new PolicyDistributionCampaign(tenantId, campaignId, plan.CommandId),
            owner => owner.Acceptance, async (owner, token) =>
            {
                preparedCut = PolicyCampaignPreparedCut.Capture(owner);
                if (!owner.IsLaunched || owner.ProgramId != programId)
                    return Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.NotFound, "The campaign was not found."));
                var loaded = await campaigns.StateOfAsync(owner, token).ConfigureAwait(false);
                if (!loaded.IsSuccess)
                    return Result<FirstResponseDescriptor>.Failure(loaded.Error!);
                var state = loaded.Value;
                var campaign = state.Campaign;
                if (await check(state).ConfigureAwait(false) is { } failure)
                    return Result<FirstResponseDescriptor>.Failure(failure.ToRequestError());
                var member = state.Roster.FindActive(plan.PersonId)!;
                var performer = plan.Performer ?? (plan.ChildKind == AdmittedChildKind.PolicyAcknowledgement
                    ? new ActorReference("workforce_person", plan.PersonId.ToString(), state.DisplayNameOf(plan.PersonId) ?? "Workforce person")
                    : null);
                var authored = await PolicyCampaignContent.WriteAsync(content, tenantId, plan.AuthoredSchema, request, token)
                    .ConfigureAwait(false);
                var proofs = await PolicyCampaignContent.WriteManifestAsync(content, tenantId,
                    PolicyCampaignContent.AdmissionProofSchema,
                    [new PolicyParticipantAdmissionProof(NativeFactReference.Of(campaign.Stream, campaign.Launch!),
                        member.IncludingFact, performer, plan.RecordedOnBehalf)],
                    SelectionOrder.CanonicalKey, token).ConfigureAwait(false);
                var header = PolicyCampaignContent.HeaderOf(provenance, campaign.Stream, participant, now);
                intent = await PolicyCampaignContent.WriteAsync(content, tenantId, ChildAdmissionIntent.Schema,
                    new ChildAdmissionIntent(header, campaign.Id, (ulong)campaign.Generation, plan.ChildKind,
                        plan.CommandId, authored, proofs, (ulong)(plan.ExpectedRevision + 1)), token).ConfigureAwait(false);
                var response = await PolicyCampaignContent.AcceptedResponseAsync(content, tenantId, plan.CommandId, now, token)
                    .ConfigureAwait(false);
                var prepared = owner.PrepareOpenAdmission(plan.ExpectedRevision);
                return prepared.IsSuccess ? Result<FirstResponseDescriptor>.Success(response)
                    : Result<FirstResponseDescriptor>.Failure(prepared.Error!);
            },
            (PolicyDistributionCampaign owner, ImmutableContentRef frozen) =>
            {
                var guarded = owner.PrepareOpenAdmission(plan.ExpectedRevision);
                if (!guarded.IsSuccess)
                    return guarded;
                return preparedCut is null || !preparedCut.Matches(owner)
                    ? PolicyCampaignPreparedCut.Changed()
                    : owner.AdmitResult(provenance, intent!, frozen);
            }), context, ct).ConfigureAwait(false);
        return await PolicyCampaignContent.AcknowledgmentAsync(content, tenantId, outcome, ct).ConfigureAwait(false);
    }

    static Result<OperationAcceptedResponse> Failure(RequestErrorKind kind, string message) =>
        Result<OperationAcceptedResponse>.Failure(new RequestError(kind, message));
}

/// <summary>What a participant result freezes about itself and its actor; the request carries the authored fields.</summary>
sealed record ParticipantAdmissionPlan(Uuid CommandId, Uuid PersonId, long ExpectedRevision, string CommandKind,
    AdmittedChildKind ChildKind, string AuthoredSchema, ActorReference Recorder, ActorReference? Performer,
    bool RecordedOnBehalf, DateTimeOffset AcceptedAt);
