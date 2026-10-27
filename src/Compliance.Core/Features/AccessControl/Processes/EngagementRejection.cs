using System.Globalization;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.Applications.Processes;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Processes;
using Bdgrz.Compliance.Features.PolicyDistribution.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SourceUse.Processes;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Retention.Commands;
using Bdgrz.Compliance.Features.Content.Persistence;
namespace Bdgrz.Compliance.Features.AccessControl.Processes;

/// <summary>
///     The shared flow of the two abort receivers. A pending acceptance or staff assignment is rejected before its
///     irreversible authorization, which fences the authorization against late delivery; after the authorization the
///     operation recovers forward and the rejection is a permanent conflict, never silently applied or dropped. No
///     permanent practice commitment can exist before the authorization, so a rejection removes no wall.
/// </summary>
sealed class EngagementRejection(IAggregateReader reader, IAggregateExecutor executor, IImmutableContentStore content,
    TimeProvider clock)
{
    const int MaximumAttempts = 4;

    /// <summary>Rejects the operation of <paramref name="operationId" /> once.</summary>
    public async ValueTask<Result<ReceiverOutcome>> RejectAsync(Uuid commandId, Uuid operationId,
        EventStreamAddress target, long generation, ImmutableContentRef failureProof,
        EngagementIndependenceOperationKind kind, string commandKind, IExecutionContext context, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(failureProof);
        if (commandId == Uuid.Empty || operationId == Uuid.Empty || generation != EngagementAcceptanceEffects.Generation ||
            target.Area != ServiceEngagementIndependence.Area ||
            !Uuid.TryParse(target.Realm, CultureInfo.InvariantCulture, out var tenantId) ||
            !Uuid.TryParse(target.Resource, CultureInfo.InvariantCulture, out var engagementId))
            return SourceUseReceiverSupport.Permanent("The rejection names another operation, engagement or generation.");
        var owner = await reader.HydrateAsync(new ServiceEngagementIndependence(tenantId, engagementId, commandId, operationId), ct)
            .ConfigureAwait(false);
        if (owner.OperationOf(operationId) is not { } operation)
            return SourceUseReceiverSupport.Transient("The admitted operation is not visible yet.");
        if (operation.Kind != kind || operation.Intent != failureProof)
            return SourceUseReceiverSupport.Permanent("The rejection differs from the frozen operation.");
        EngagementIndependenceIntent intent;
        try
        {
            intent = await content.ReadJsonAsync<EngagementIndependenceIntent>(IndependenceContent.Realm(tenantId),
                failureProof, ct).ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return SourceUseReceiverSupport.Permanent($"The frozen operation cannot be verified: {exception.Message}");
        }

        var provenance = EngagementAcceptanceEffects.Child(commandId, commandKind, operation.Requested.Metadata.EventId,
            intent, owner.Stream, clock.GetUtcNow());
        var binding = AcceptedOperationBinding.Of(provenance);
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var step = await executor.ExecuteAsync(new ServiceEngagementIndependence(tenantId, engagementId,
                    commandId, operationId), fresh =>
                    {
                        switch (fresh.Acceptance.Decide(binding))
                        {
                            case NativeAcceptanceDecision.Replayed:
                                return AggregateOutcome.Discard(Result<Step>.Success(
                                    new Step(NativeFactReference.Of(fresh.Stream, fresh.Acceptance.SelectedFact!), false)));
                            case NativeAcceptanceDecision.Conflict:
                                return AggregateOutcome.Discard(Result<Step>.Failure(AcceptedAppendResult.ConflictError));
                        }

                        if (fresh.ReplayCut.Digest != owner.ReplayCut.Digest)
                            return AggregateOutcome.Discard(Result<Step>.Failure(new RequestError(RequestErrorKind.Conflict,
                                "The engagement native prefix changed during receiver preparation; retry from its exact current cut.", true)));

                        if (fresh.OperationOf(operationId) is { Rejected: { } existing })
                            return AggregateOutcome.Discard(Result<Step>.Success(
                                new Step(NativeFactReference.Of(fresh.Stream, existing), false)));
                        var rejected = fresh.RejectOperation(provenance, kind);
                        return rejected.IsSuccess
                            ? AggregateOutcome.Commit(Result<Step>.Success(new Step(
                                NativeFactReference.Of(fresh.Stream, fresh.Acceptance.SelectedFact!), true)))
                            : AggregateOutcome.Discard(Result<Step>.Failure(rejected.Error!));
                    }, context, ct).ConfigureAwait(false);
                if (!step.IsSuccess)
                    return Result<ReceiverOutcome>.Failure(step.Error!);
                return step.Value!.Applied
                    ? SourceUseReceiverSupport.Applied(step.Value.Fact)
                    : SourceUseReceiverSupport.Replayed(step.Value.Fact);
            }
            catch (EventStreamConcurrencyException) when (attempt < MaximumAttempts)
            {
                // A concurrent delivery or authorization won the append; rehydrate and decide again.
            }
        }
    }

    sealed record Step(NativeFactReference Fact, bool Applied);
}
