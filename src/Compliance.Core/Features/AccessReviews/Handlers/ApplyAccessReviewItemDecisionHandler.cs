using System.Globalization;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AccessReviews.Processes;
using Bdgrz.Compliance.Features.AccessReviews.Rules;
using Bdgrz.Compliance.Features.AccessReviews.Services;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.Applications.Processes;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Processes;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SourceUse.Processes;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.SourceUse.Services;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessReviews.Aggregates;
using Bdgrz.Compliance.Features.AccessReviews.Commands;
using Bdgrz.Compliance.Features.AccessReviews.Models;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Handlers;
using Bdgrz.Compliance.Features.Content.Persistence;
namespace Bdgrz.Compliance.Features.AccessReviews.Handlers;

/// <summary>
///     Applies the batch's shared decision to one held item as a provisional fact. The decision is built only from the
///     frozen intent (the reviewer, the shared rationale, the admission time and the preview token), so a replay or a
///     resumed process appends the identical decision. It stays hidden until the batch publishes. An item fenced
///     against a canceled batch, or whose hold is gone, refuses it; a hold that is not visible yet is transient.
/// </summary>
sealed class ApplyAccessReviewItemDecisionHandler(IAggregateReader reader, IAggregateExecutor executor,
    IImmutableContentStore content, AccessReviewBulkVerifier verifier, TimeProvider clock)
    : IRequestHandler<ApplyAccessReviewItemDecision, ReceiverOutcome>
{
    const int MaximumAttempts = 4;

    public async ValueTask<Result<ReceiverOutcome>> HandleAsync(IRequestContext<ApplyAccessReviewItemDecision> context,
        CancellationToken ct)
    {
        var command = context.Request;
        if (command.CommandId == Uuid.Empty || command.OperationId == Uuid.Empty ||
            command.Generation != AccessReviewEffects.Generation || command.CampaignId == Uuid.Empty ||
            command.ItemId == Uuid.Empty || command.DecisionBatchId != command.OperationId ||
            !Uuid.TryParse(command.Target.Realm, CultureInfo.InvariantCulture, out var tenant) ||
            command.Target != AccessReviewEffects.ItemStream(tenant, command.CampaignId, command.ItemId))
            return SourceUseReceiverSupport.Permanent("The apply names another item stream, operation or generation.");
        var bulk = await verifier.ReadNativeAsync(tenant, command.CampaignId, command.Source, command.Intent,
            command.OperationId, ct).ConfigureAwait(false);
        if (bulk.Error is not null)
            return Result<ReceiverOutcome>.Failure(bulk.Error);
        var provenance = SourceUseReceiverSupport.Child(command.CommandId, nameof(ApplyAccessReviewItemDecision),
            command.Source.EventId, AccessReviewIntentBindings.HeaderOf(bulk.Admitted!.Operation), command.Target,
            clock.GetUtcNow());
        var binding = AcceptedOperationBinding.Of(provenance);
        var preparation = await NativeReceiverPreparation.PrepareAsync<AccessReviewItem, PreparedDecisions>(reader,
            () => Open(tenant, command), owner => Decide(owner, binding, command, provenance),
            (_, token) => PrepareAsync(tenant, command, bulk, token), ct).ConfigureAwait(false);
        if (!preparation.IsSuccess)
            return Result<ReceiverOutcome>.Failure(preparation.Error!);
        if (preparation.Value!.Outcome is { } known)
            return known;
        var decisions = preparation.Value.Value!.Content;
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var current = await reader.HydrateAsync(Open(tenant, command), ct).ConfigureAwait(false);
                if (Decide(current, binding, command, provenance) is { } early)
                    return early;
                var step = await executor.ExecuteAsync(Open(tenant, command), item =>
                    {
                        if (Decide(item, binding, command, provenance) is { } decided)
                            return AggregateOutcome.Discard(Result<Decided>.Success(new Decided(decided)));
                        var raised = item.ApplyBulkDecision(provenance, decisions, command.Intent);
                        return raised.IsSuccess
                            ? AggregateOutcome.Commit(Result<Decided>.Success(new Decided(
                                SourceUseReceiverSupport.Applied(BulkReceiverSupport.Fact(item.Stream,
                                    item.Acceptance.SelectedFact!)))))
                            : AggregateOutcome.Discard(Result<Decided>.Failure(raised.Error!));
                    }, context, ct).ConfigureAwait(false);
                return step.IsSuccess ? step.Value!.Outcome : Result<ReceiverOutcome>.Failure(step.Error!);
            }
            catch (EventStreamConcurrencyException) when (attempt < MaximumAttempts)
            {
                // The item changed concurrently; rehydrate and decide again, including against a fence.
            }
        }
    }

    async ValueTask<Result<PreparedDecisions>> PrepareAsync(Uuid tenant, ApplyAccessReviewItemDecision command,
        AccessReviewBulk native, CancellationToken ct)
    {
        var bulk = await verifier.PrepareAsync(tenant, native, ct).ConfigureAwait(false);
        if (bulk.Error is not null)
            return Result<PreparedDecisions>.Failure(bulk.Error);
        var (campaign, intent) = (bulk.Campaign!, bulk.Intent!);
        try
        {
            var selected = await verifier.ItemOfAsync(command.Target.Realm, intent, command.Target.Resource, ct)
                .ConfigureAwait(false);
            if (selected is null || selected.ItemId != command.ItemId || campaign.Launched is not { } launched)
                return Result<PreparedDecisions>.Failure(new RequestError(RequestErrorKind.Conflict,
                    "The apply differs from the frozen bulk decision selection.", false));
            var view = new AccessDecisionView(Uuid.CreateVersion5(command.OperationId, $"bulk-decision:{command.ItemId}"),
                command.ItemId, intent.Decision, intent.Rationale, selected.ReviewerMemberId, intent.Header.OriginalActor,
                intent.Header.AcceptedAt, intent.PreviewToken, intent.Header.AcceptedAt > launched.Deadline, null);
            var decisions = await content.WriteJsonAsync<IReadOnlyList<AccessDecisionView>>(command.Target.Realm,
                Uuid.CreateVersion5(command.CommandId, "bulk-decisions"), AccessReviewContent.DecisionsSchema, [view], ct)
                .ConfigureAwait(false);
            return Result<PreparedDecisions>.Success(new PreparedDecisions(decisions));
        }
        catch (ImmutableContentIntegrityException error)
        {
            ct.ThrowIfCancellationRequested();
            return Result<PreparedDecisions>.Failure(new RequestError(RequestErrorKind.Conflict,
                error.Message, error is ImmutableContentUnavailableException));
        }
    }

    static AccessReviewItem Open(Uuid tenant, ApplyAccessReviewItemDecision command)
    {
        var owner = new AccessReviewItem(tenant, command.CampaignId, command.ItemId, command.CommandId, command.DecisionBatchId);
        return owner.ForSourceUses(owner.BulkKey(command.OperationId));
    }

    static Result<ReceiverOutcome>? Decide(AccessReviewItem item, AcceptedOperationBinding binding,
        ApplyAccessReviewItemDecision command, OperationProvenance provenance)
    {
        var batchId = command.DecisionBatchId;
        if (item.ProvisionalFor(batchId) is { } applied &&
            (applied.Intent != command.Intent || applied.Operation.RootOperationId != command.OperationId ||
                applied.Operation.CausationEventId != command.Source.EventId || applied.Operation.FirstOwner != provenance.FirstOwner ||
                AcceptedOperationBinding.Of(applied.Operation with { LogicalId = provenance.LogicalId }) != binding))
            return SourceUseReceiverSupport.Permanent("The applied decision has another exact native source binding.");
        if (BulkReceiverSupport.Decide(item.Acceptance, item.Stream, binding) is { } accepted)
            return accepted;
        if (item.ProvisionalFor(batchId) is { } existing)
            return SourceUseReceiverSupport.Replayed(BulkReceiverSupport.Fact(item.Stream, existing));
        if (item.FenceFor(batchId) is { } fence)
            return SourceUseReceiverSupport.Superseded("The item was fenced against the canceled bulk decision.",
                BulkReceiverSupport.Fact(item.Stream, fence));
        if (item.Opened is null || item.Uses.Find(item.BulkKey(batchId)) is not { IsActive: true })
            return item.Uses.Find(item.BulkKey(batchId)) is { IsSettled: true } settled
                ? SourceUseReceiverSupport.Superseded("The bulk decision use of the item already settled.",
                    BulkReceiverSupport.Fact(item.Stream, settled.SettleFact!))
                : SourceUseReceiverSupport.Transient("The hold of the item is not visible yet.");
        return null;
    }

    sealed record PreparedDecisions(ImmutableContentRef Content);

    sealed record Decided(Result<ReceiverOutcome> Outcome);
}
