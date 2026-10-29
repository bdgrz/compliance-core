using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AccessReviews.Models;
using Bdgrz.Compliance.Features.AccessReviews.Rules;
using Bdgrz.Compliance.Features.AccessReviews.Services;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.Applications.Services;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Models;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.AccessControl.Handlers;
using Bdgrz.Compliance.Features.AccessReviews.Aggregates;
using Bdgrz.Compliance.Features.AccessReviews.Commands;
using Bdgrz.Compliance.Features.AccessReviews.Readers;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Handlers;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.Tenants.Authorizers;
namespace Bdgrz.Compliance.Features.AccessReviews.Handlers;

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
        try
        {
            return await HandleCoreAsync(context, ct).ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException error)
        {
            return Result<AccessRemediationVerificationView>.Failure(new RequestError(RequestErrorKind.Conflict,
                $"The exact access item source cannot be verified: {error.Message}", error is ImmutableContentUnavailableException));
        }
    }

    async ValueTask<Result<AccessRemediationVerificationView>> HandleCoreAsync(
        IRequestContext<VerifyAccessRemediation> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.CommandId == Uuid.Empty || request.ExpectedRevision < 0)
            return Failure(RequestErrorKind.Validation,
                "A verification requires a command id and a non-negative expected revision.");
        var actor = AccessReviewActor.From(context);
        var read = await campaigns.ReadItemIdentityAsync(request.TenantId, request.CampaignId, request.ItemId,
            request.CommandId, ct).ConfigureAwait(false);
        if (read.Frozen is not { } item)
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
        AccessReviewPreparedCut? preparedCut = null;
        ImmutableContentRef? verification = null;
        ImmutableContentRef? result = null;
        var accepted = await operations.ExecuteAsync(new NativeOperation<AccessReviewItem>(binding,
            () => new AccessReviewItem(request.TenantId, request.CampaignId, request.ItemId, request.CommandId),
            owner => owner.Acceptance,
            async (owner, prepareCt) =>
            {
                var prepared = owner.PrepareChange(request.ExpectedRevision);
                if (!prepared.IsSuccess)
                    return Result<FirstResponseDescriptor>.Failure(prepared.Error!);
                var state = await campaigns.ReadStateAsync(request.TenantId, owner, item,
                    read.Campaign.Launched?.RemediationOwnerMemberId, prepareCt).ConfigureAwait(false);
                if (state.CurrentDecision is not { } decision || !AccessReviewVocabulary.RequiresRemediation(decision.Decision))
                    return AccessReviewOutcome.Failure<FirstResponseDescriptor>(RequestErrorKind.Conflict, "Only an item decided modify or revoke requires remediation.");
                var loaded = await AcceptedAccessPopulation.LoadAsync(reader, content, request.TenantId, request.PopulationId, prepareCt)
                    .ConfigureAwait(false);
                if (!loaded.IsSuccess)
                    return Result<FirstResponseDescriptor>.Failure(loaded.Error);
                var later = loaded.Value;
                if (later.Header.SystemInstanceId != item.SystemInstanceId || later.Population.Id == item.PopulationId)
                    return AccessReviewOutcome.Failure<FirstResponseDescriptor>(RequestErrorKind.Validation,
                        "Verification requires a later accepted population of the same system instance.");
                if (later.Header.ObservedAt <= decision.DecidedAt)
                    return AccessReviewOutcome.Failure<FirstResponseDescriptor>(RequestErrorKind.Conflict,
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
                    return AccessReviewOutcome.Failure<FirstResponseDescriptor>(RequestErrorKind.Conflict, "The later population still shows the access unchanged.");
                var view = new AccessRemediationVerificationView(later.Population.Id, later.Snapshot.Id,
                    later.CalculationId, later.Header.ObservedAt, outcome, actor.Reference, now);
                if (AccessReviewRules.CheckVerification(state, view) is { } failure)
                    return Result<FirstResponseDescriptor>.Failure(failure);
                verification = await AccessReviewContent.WriteAsync(content, request.TenantId,
                    AccessReviewContent.VerificationSchema, view, prepareCt).ConfigureAwait(false);
                (response, result) = await AccessReviewContent.FreezeOkAsync(content, request.TenantId,
                    AccessReviewContent.VerificationViewSchema, view, prepareCt).ConfigureAwait(false);
                preparedCut = AccessReviewPreparedCut.Capture(owner);
                return Result<FirstResponseDescriptor>.Success(response!);
            },
            (AccessReviewItem owner, ImmutableContentRef frozenResponse) =>
            {
                if (preparedCut?.Matches(owner) != true)
                    return AccessReviewPreparedCut.Changed();
                return owner.Verify(provenance,
                verification!, frozenResponse, result!);
            }), context, ct).ConfigureAwait(false);
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
