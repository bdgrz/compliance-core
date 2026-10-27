using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AccessReviews.Models;
using Bdgrz.Compliance.Features.AccessReviews.Rules;
using Bdgrz.Compliance.Features.AccessReviews.Services;
using Bdgrz.Compliance.Features.Applications.Services;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessReviews.Aggregates;
using Bdgrz.Compliance.Features.AccessReviews.Commands;
using Bdgrz.Compliance.Features.AccessReviews.Readers;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.Tenants.Authorizers;
namespace Bdgrz.Compliance.Features.AccessReviews.Handlers;

/// <summary>
///     Records a provider-side change as the root native fact of the item; it never counts as platform verification.
///     The frozen first response is the complete change.
/// </summary>
public sealed class RecordAccessRemediationChangeHandler(NativeOperationExecutor operations,
    AccessReviewReader campaigns, IImmutableContentStore content, TimeProvider clock,
    RestrictedApplicationVisibility visibility, AccessReviewQueueEligibility eligibility)
    : IRequestHandler<RecordAccessRemediationChange, AccessRemediationChangeView>
{
    const string CommandKind = nameof(RecordAccessRemediationChange);

    public async ValueTask<Result<AccessRemediationChangeView>> HandleAsync(
        IRequestContext<RecordAccessRemediationChange> context, CancellationToken ct)
    {
        try
        {
            return await HandleCoreAsync(context, ct).ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException error)
        {
            return Result<AccessRemediationChangeView>.Failure(new RequestError(RequestErrorKind.Conflict,
                $"The exact access item source cannot be verified: {error.Message}", error is ImmutableContentUnavailableException));
        }
    }

    async ValueTask<Result<AccessRemediationChangeView>> HandleCoreAsync(
        IRequestContext<RecordAccessRemediationChange> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.CommandId == Uuid.Empty || request.ExpectedRevision < 0)
            return AccessReviewOutcome.Failure<AccessRemediationChangeView>(RequestErrorKind.Validation,
                "A change requires a command id and a non-negative expected revision.");
        var actor = AccessReviewActor.From(context);
        var read = await campaigns.ReadItemIdentityAsync(request.TenantId, request.CampaignId, request.ItemId,
            request.CommandId, ct).ConfigureAwait(false);
        if (read.Frozen is not { } frozen || read.Campaign.Launched is not { } launched ||
            !await visibility.CanReadSystemInstanceAsync(request.TenantId, actor.UserId, frozen.SystemInstanceId, ct)
                .ConfigureAwait(false))
            return AccessReviewOutcome.Failure<AccessRemediationChangeView>(RequestErrorKind.NotFound,
                "The review item was not found.");

        var now = clock.GetUtcNow();
        var provenance = AccessReviewItemWrites.Provenance(read, request.CommandId, CommandKind, actor.Reference,
            request.ExpectedRevision, AuthoredIntentDigest.Compute(request), now);
        var binding = AcceptedOperationBinding.Of(provenance);
        FirstResponseDescriptor? response = null;
        AccessReviewPreparedCut? preparedCut = null;
        ImmutableContentRef? change = null;
        ImmutableContentRef? result = null;
        var outcome = await operations.ExecuteAsync(new NativeOperation<AccessReviewItem>(binding,
            () => new AccessReviewItem(request.TenantId, request.CampaignId, request.ItemId, request.CommandId),
            owner => owner.Acceptance,
            async (owner, prepareCt) =>
            {
                var prepared = owner.PrepareChange(request.ExpectedRevision);
                if (!prepared.IsSuccess)
                    return Result<FirstResponseDescriptor>.Failure(prepared.Error!);
                var state = await campaigns.ReadStateAsync(request.TenantId, owner, frozen,
                    read.Campaign.Launched?.RemediationOwnerMemberId, prepareCt).ConfigureAwait(false);
                if (launched.ProgramId is { } programId &&
                    (state.CurrentRemediationOwnerMemberId != actor.MemberId ||
                     !await eligibility.CanRemediateAsync(request.TenantId, programId, actor.MemberId,
                         frozen.SystemInstanceId, prepareCt).ConfigureAwait(false)))
                    return AccessReviewOutcome.Failure<FirstResponseDescriptor>(RequestErrorKind.NotFound,
                        "The review item was not found among the remediation owner's current queue access.");
                if (AccessReviewRules.CheckChange(state, request.Reference, request.Description, request.ChangedAt, now)
                    is { } failure)
                    return Result<FirstResponseDescriptor>.Failure(failure);
                var view = new AccessRemediationChangeView(request.CommandId, request.Reference.Trim(),
                    request.Description.Trim(), request.ChangedAt, actor.Reference, now);
                change = await AccessReviewContent.WriteAsync(content, request.TenantId, AccessReviewContent.ChangeSchema,
                    view, prepareCt).ConfigureAwait(false);
                (response, result) = await AccessReviewContent.FreezeOkAsync(content, request.TenantId,
                    AccessReviewContent.ChangeViewSchema, view, prepareCt).ConfigureAwait(false);
                preparedCut = AccessReviewPreparedCut.Capture(owner);
                return Result<FirstResponseDescriptor>.Success(response!);
            },
            (AccessReviewItem owner, ImmutableContentRef frozenResponse) =>
            {
                if (preparedCut?.Matches(owner) != true)
                    return AccessReviewPreparedCut.Changed();
                return owner.RecordChange(provenance, change!,
                frozenResponse, result!);
            }), context, ct).ConfigureAwait(false);
        return await AccessReviewContent.ResponseAsync<AccessRemediationChangeView>(content, request.TenantId,
            outcome, ct).ConfigureAwait(false);
    }
}
