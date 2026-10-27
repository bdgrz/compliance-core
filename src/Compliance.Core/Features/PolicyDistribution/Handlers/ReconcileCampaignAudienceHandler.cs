using Bdgrz.Compliance.Features.Versioning;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Policies;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.PolicyDistribution;

/// <summary>
///     Reconciles a campaign against a roster snapshot frozen after the one it last used. The previous snapshot
///     explains whether an addition is a joiner or a mover. The campaign's logical revision is the only expected
///     revision; the command id is the logical operation, so an exact retry replays the original reconciliation.
/// </summary>
sealed class ReconcileCampaignAudienceHandler(NativeOperationExecutor operations, CampaignSource campaigns,
    IAggregateReader reader, IImmutableContentStore content, TimeProvider clock)
    : IRequestHandler<ReconcileCampaignAudience, CampaignReconciliationView>
{
    const string CommandKind = "ReconcileCampaignAudience";

    public ValueTask<Result<CampaignReconciliationView>> HandleAsync(
        IRequestContext<ReconcileCampaignAudience> context, CancellationToken ct) =>
        PolicyCampaignFailures.ClassifyAsync(() => HandleCoreAsync(context, ct), ct);

    async ValueTask<Result<CampaignReconciliationView>> HandleCoreAsync(
        IRequestContext<ReconcileCampaignAudience> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.CommandId == Uuid.Empty || request.RosterSnapshotId == Uuid.Empty || request.ExpectedRevision < 0)
            return Failure(RequestErrorKind.Validation,
                "A reconciliation requires a command id, a roster snapshot and a non-negative expected revision.");
        var actor = PolicyActor.From(context, request.TenantId);
        var now = clock.GetUtcNow();
        var stream = new PolicyDistributionCampaign(request.TenantId, request.CampaignId).Stream;
        var provenance = PolicyCampaignContent.Provenance(request.CommandId, CommandKind, stream,
            stream, actor.Reference, request.ExpectedRevision, AuthoredIntentDigest.Compute(request), now);
        var binding = AcceptedOperationBinding.Of(provenance);

        PolicyCampaignPreparedCut? preparedCut = null;
        ImmutableContentRef? amendments = null;
        ImmutableContentRef? result = null;
        string? rosterSha = null;
        var observedOn = DateOnly.FromDateTime(now.UtcDateTime);
        var outcome = await operations.ExecuteAsync(new NativeOperation<PolicyDistributionCampaign>(binding,
            () => new PolicyDistributionCampaign(request.TenantId, request.CampaignId, request.CommandId, request.RosterSnapshotId),
            owner => owner.Acceptance, async (owner, token) =>
            {
                preparedCut = PolicyCampaignPreparedCut.Capture(owner);
                if (!owner.IsLaunched || owner.ProgramId != request.ProgramId)
                    return Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.NotFound, "The campaign was not found."));
                var source = await campaigns.StateOfAsync(owner, token).ConfigureAwait(false);
                if (!source.IsSuccess)
                    return Result<FirstResponseDescriptor>.Failure(source.Error!);
                var state = source.Value;
                var campaign = state.Campaign;
                if (CampaignRules.CheckOpen(campaign) is { } closed)
                    return Result<FirstResponseDescriptor>.Failure(closed.ToRequestError());
                var previousId = campaign.LatestRosterSnapshotId;
                var previous = await RosterSnapshotSource.ReadAsync(reader, request.TenantId, previousId, token)
                    .ConfigureAwait(false);
                if (!previous.IsSuccess)
                    return Result<FirstResponseDescriptor>.Failure(previous.Error);
                var current = await RosterSnapshotSource.ReadAsync(reader, request.TenantId, request.RosterSnapshotId, token)
                    .ConfigureAwait(false);
                if (!current.IsSuccess)
                    return Result<FirstResponseDescriptor>.Failure(current.Error);
                if (campaign.HasReconciled(request.RosterSnapshotId))
                    return Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Conflict, "The campaign already reconciled that roster snapshot."));
                if (current.Value.FrozenAt <= previous.Value.FrozenAt)
                    return Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Conflict,
                        "Reconciliation requires a roster snapshot frozen after the campaign's latest one."));
                var launch = campaign.Launch!;
                var rule = state.Teams;
                var audience = RosterAudience.Evaluate(current.Value.Roster, launch.AudienceKind, rule);
                var previouslyPresent = RosterAudience.Evaluate(previous.Value.Roster, launch.AudienceKind, rule).Present;
                var list = CampaignRules.Amendments(state.Roster, audience, previouslyPresent, launch.DueOn, observedOn);
                rosterSha = current.Value.Roster.ContentSha256;
                var view = new CampaignReconciliationView(campaign.Id, campaign.Reconciliations + 1,
                    request.RosterSnapshotId, list);
                amendments = await PolicyCampaignContent.WriteAsync(content, request.TenantId,
                    PolicyCampaignContent.AmendmentsSchema, list, token).ConfigureAwait(false);
                var body = await PolicyCampaignContent.WriteAsync(content, request.TenantId,
                    PolicyCampaignContent.ReconciliationSchema, view, token).ConfigureAwait(false);
                result = await PolicyCampaignContent.WriteResultAsync(content, request.TenantId,
                    PolicyCampaignContent.ReconciliationSchema, body, token).ConfigureAwait(false);
                var prepared = owner.PrepareOpenAdmission(request.ExpectedRevision);
                return prepared.IsSuccess
                    ? Result<FirstResponseDescriptor>.Success(PolicyCampaignContent.OkResponse(PolicyCampaignContent.ReconciliationSchema, body))
                    : Result<FirstResponseDescriptor>.Failure(prepared.Error!);
            },
            (PolicyDistributionCampaign owner, ImmutableContentRef frozen) =>
            {
                var guarded = owner.PrepareOpenAdmission(request.ExpectedRevision);
                if (!guarded.IsSuccess)
                    return guarded;
                return preparedCut is null || !preparedCut.Matches(owner)
                    ? PolicyCampaignPreparedCut.Changed()
                    : owner.Reconcile(provenance, request.RosterSnapshotId, rosterSha!, observedOn, amendments!,
                        actor.Reference, now, frozen, result!);
            }),
            context, ct).ConfigureAwait(false);
        return await PolicyCampaignContent.ResponseAsync<CampaignReconciliationView>(content, request.TenantId,
            outcome, ct).ConfigureAwait(false);
    }

    static Result<CampaignReconciliationView> Failure(RequestErrorKind kind, string message) =>
        Result<CampaignReconciliationView>.Failure(new RequestError(kind, message));
}
