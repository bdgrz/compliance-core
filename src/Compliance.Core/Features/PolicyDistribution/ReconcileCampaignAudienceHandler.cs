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

    public async ValueTask<Result<CampaignReconciliationView>> HandleAsync(
        IRequestContext<ReconcileCampaignAudience> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.CommandId == Uuid.Empty || request.RosterSnapshotId == Uuid.Empty || request.ExpectedRevision < 0)
            return Failure(RequestErrorKind.Validation,
                "A reconciliation requires a command id, a roster snapshot and a non-negative expected revision.");
        var source = await campaigns.ReadAsync(request.TenantId, request.ProgramId, request.CampaignId, ct,
            request.CommandId).ConfigureAwait(false);
        if (!source.IsSuccess)
            return Result<CampaignReconciliationView>.Failure(source.Error);
        var state = source.Value;
        var campaign = state.Campaign;
        var actor = PolicyActor.From(context, request.TenantId);
        var now = clock.GetUtcNow();
        var provenance = PolicyCampaignContent.Provenance(request.CommandId, CommandKind, campaign.Stream,
            campaign.Stream, actor.Reference, request.ExpectedRevision, AuthoredIntentDigest.Compute(request), now);
        var binding = AcceptedOperationBinding.Of(provenance);

        FirstResponseDescriptor? response = null;
        ImmutableContentRef? amendments = null;
        ImmutableContentRef? result = null;
        string? rosterSha = null;
        var observedOn = DateOnly.FromDateTime(now.UtcDateTime);
        if (campaign.Acceptance.Decide(binding) == NativeAcceptanceDecision.None)
        {
            if (CampaignRules.CheckOpen(campaign) is { } closed)
                return Result<CampaignReconciliationView>.Failure(closed.ToRequestError());
            var previousId = campaign.LatestRosterSnapshotId;
            var previous = await RosterSnapshotSource.ReadAsync(reader, request.TenantId, previousId, ct)
                .ConfigureAwait(false);
            if (!previous.IsSuccess)
                return Result<CampaignReconciliationView>.Failure(previous.Error);
            var current = await RosterSnapshotSource.ReadAsync(reader, request.TenantId, request.RosterSnapshotId, ct)
                .ConfigureAwait(false);
            if (!current.IsSuccess)
                return Result<CampaignReconciliationView>.Failure(current.Error);
            if (campaign.HasReconciled(request.RosterSnapshotId))
                return Failure(RequestErrorKind.Conflict, "The campaign already reconciled that roster snapshot.");
            if (current.Value.FrozenAt <= previous.Value.FrozenAt)
                return Failure(RequestErrorKind.Conflict,
                    "Reconciliation requires a roster snapshot frozen after the campaign's latest one.");
            var launch = campaign.Launch!;
            var rule = state.Teams;
            var audience = RosterAudience.Evaluate(current.Value.Roster, launch.AudienceKind, rule);
            var previouslyPresent = RosterAudience.Evaluate(previous.Value.Roster, launch.AudienceKind, rule).Present;
            var list = CampaignRules.Amendments(state.Roster, audience, previouslyPresent, launch.DueOn, observedOn);
            rosterSha = current.Value.Roster.ContentSha256;
            var view = new CampaignReconciliationView(campaign.Id, campaign.Reconciliations + 1,
                request.RosterSnapshotId, list);
            amendments = await PolicyCampaignContent.WriteAsync(content, request.TenantId,
                PolicyCampaignContent.AmendmentsSchema, list, ct).ConfigureAwait(false);
            var body = await PolicyCampaignContent.WriteAsync(content, request.TenantId,
                PolicyCampaignContent.ReconciliationSchema, view, ct).ConfigureAwait(false);
            result = await PolicyCampaignContent.WriteResultAsync(content, request.TenantId,
                PolicyCampaignContent.ReconciliationSchema, body, ct).ConfigureAwait(false);
            response = PolicyCampaignContent.OkResponse(PolicyCampaignContent.ReconciliationSchema, body);
        }

        var outcome = await operations.ExecuteAsync(new NativeOperation<PolicyDistributionCampaign>(binding,
            () => new PolicyDistributionCampaign(request.TenantId, request.CampaignId, request.CommandId),
            owner => owner.Acceptance, owner => Prepare(owner, request.ExpectedRevision, response),
            (PolicyDistributionCampaign owner, ImmutableContentRef frozen) => owner.Reconcile(provenance,
                request.RosterSnapshotId, rosterSha!, observedOn, amendments!, actor.Reference, now, frozen, result!)),
            context, ct).ConfigureAwait(false);
        return await PolicyCampaignContent.ResponseAsync<CampaignReconciliationView>(content, request.TenantId,
            outcome, ct).ConfigureAwait(false);
    }

    static Result<FirstResponseDescriptor> Prepare(PolicyDistributionCampaign owner, long expectedRevision,
        FirstResponseDescriptor? response)
    {
        var prepared = owner.PrepareOpenAdmission(expectedRevision);
        if (!prepared.IsSuccess)
            return Result<FirstResponseDescriptor>.Failure(prepared.Error!);
        return response is null
            ? Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The campaign changed while the reconciliation was prepared; reload and retry.", true))
            : Result<FirstResponseDescriptor>.Success(response);
    }

    static Result<CampaignReconciliationView> Failure(RequestErrorKind kind, string message) =>
        Result<CampaignReconciliationView>.Failure(new RequestError(kind, message));
}
