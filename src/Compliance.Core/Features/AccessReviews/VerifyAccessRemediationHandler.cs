using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>
///     Verifies remediation from a later accepted population of the same system instance: a revoked item must be
///     absent, and a modified item must be absent or reach the entitlement differently. A ticket or provider change
///     alone is never verification (M0-D07). The verification is the root native fact of the item.
/// </summary>
public sealed class VerifyAccessRemediationHandler(NativeOperationExecutor operations, AccessReviewReader campaigns,
    IAggregateReader reader, IImmutableContentStore content, TimeProvider clock,
    RestrictedApplicationVisibility visibility)
    : IRequestHandler<VerifyAccessRemediation, AccessRemediationVerificationView>
{
    const string CommandKind = nameof(VerifyAccessRemediation);

    public async ValueTask<Result<AccessRemediationVerificationView>> HandleAsync(
        IRequestContext<VerifyAccessRemediation> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.CommandId == Uuid.Empty || request.ExpectedRevision < 0)
            return Failure(RequestErrorKind.Validation,
                "A verification requires a command id and a non-negative expected revision.");
        var actor = AccessReviewActor.From(context);
        var read = await campaigns.ReadItemAsync(request.TenantId, request.CampaignId, request.ItemId,
            request.CommandId, ct).ConfigureAwait(false);
        if (read.State is not { } state || read.Frozen is not { } item)
            return Failure(RequestErrorKind.NotFound, "The review item was not found.");
        if (!await visibility.CanReadSystemInstanceAsync(request.TenantId, actor.UserId, item.SystemInstanceId, ct)
                .ConfigureAwait(false) ||
            !await RestrictedAccessReviewVisibility.CanReadPopulationAsync(reader, visibility, request.TenantId,
                actor.UserId, request.PopulationId, ct).ConfigureAwait(false))
            return Failure(RequestErrorKind.NotFound, "The review item was not found.");

        var now = clock.GetUtcNow();
        var provenance = AccessReviewItemWrites.Provenance(read, request.CommandId, CommandKind, actor.Reference,
            request.ExpectedRevision, AuthoredIntentDigest.Compute(request), now);
        var binding = AcceptedOperationBinding.Of(provenance);
        FirstResponseDescriptor? response = null;
        ImmutableContentRef? verification = null;
        ImmutableContentRef? result = null;
        if (read.Item.Acceptance.Decide(binding) == NativeAcceptanceDecision.None)
        {
            if (read.Item.PrepareChange(request.ExpectedRevision) is { IsSuccess: false } blocked)
                return Result<AccessRemediationVerificationView>.Failure(blocked.Error!);
            if (state.CurrentDecision is not { } decision || !AccessReviewVocabulary.RequiresRemediation(decision.Decision))
                return Failure(RequestErrorKind.Conflict, "Only an item decided modify or revoke requires remediation.");
            var loaded = await AcceptedAccessPopulation.LoadAsync(reader, content, request.TenantId, request.PopulationId, ct, withClassifications: false)
                .ConfigureAwait(false);
            if (!loaded.IsSuccess)
                return Result<AccessRemediationVerificationView>.Failure(loaded.Error);
            var later = loaded.Value;
            if (later.Header.SystemInstanceId != item.SystemInstanceId || later.Population.Id == item.PopulationId)
                return Failure(RequestErrorKind.Validation,
                    "Verification requires a later accepted population of the same system instance.");
            if (later.Header.ObservedAt <= decision.DecidedAt)
                return Failure(RequestErrorKind.Conflict,
                    "Verification requires a population observed after the decision.");
            var row = later.EffectiveAccess.FirstOrDefault(access =>
                access.ProviderSubjectId == item.ProviderSubjectId &&
                access.ProviderEntitlementId == item.ProviderEntitlementId);
            string outcome;
            if (row is null)
                outcome = "removed";
            else if (decision.Decision == AccessReviewVocabulary.Modify && !SameAccess(row, item))
                outcome = "changed";
            else
                return Failure(RequestErrorKind.Conflict, "The later population still shows the access unchanged.");
            var view = new AccessRemediationVerificationView(later.Population.Id, later.Snapshot.Id,
                later.CalculationId, later.Header.ObservedAt, outcome, actor.Reference, now);
            if (AccessReviewRules.CheckVerification(state, view) is { } failure)
                return Result<AccessRemediationVerificationView>.Failure(failure);
            verification = await AccessReviewContent.WriteAsync(content, request.TenantId,
                AccessReviewContent.VerificationSchema, view, ct).ConfigureAwait(false);
            (response, result) = await AccessReviewContent.FreezeOkAsync(content, request.TenantId,
                AccessReviewContent.VerificationViewSchema, view, ct).ConfigureAwait(false);
        }

        var observed = state.Revision;
        var accepted = await operations.ExecuteAsync(new NativeOperation<AccessReviewItem>(binding,
            () => new AccessReviewItem(request.TenantId, request.CampaignId, request.ItemId, request.CommandId),
            owner => owner.Acceptance,
            owner => AccessReviewItemWrites.Prepare(owner, request.ExpectedRevision, observed, response),
            (AccessReviewItem owner, ImmutableContentRef frozenResponse) => owner.Verify(provenance,
                verification!, frozenResponse, result!)), context, ct).ConfigureAwait(false);
        return await AccessReviewContent.ResponseAsync<AccessRemediationVerificationView>(content, request.TenantId,
            accepted, ct).ConfigureAwait(false);
    }

    static bool SameAccess(EffectiveAccessView row, AccessReviewItemView item) =>
        row.ExpiresAt == item.ExpiresAt && row.Paths.Count == item.Paths.Count &&
        row.Paths.Zip(item.Paths).All(static pair => pair.First.ExpiresAt == pair.Second.ExpiresAt &&
                                                     pair.First.Hops.SequenceEqual(pair.Second.Hops));

    static Result<AccessRemediationVerificationView> Failure(RequestErrorKind kind, string message) =>
        AccessReviewOutcome.Failure<AccessRemediationVerificationView>(kind, message);
}
