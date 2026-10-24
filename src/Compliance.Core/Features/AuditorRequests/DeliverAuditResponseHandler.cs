using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests;

/// <summary>
///     Records the intentional delivery of the exact response version. When the request required an independent review
///     (frozen in the admitted draft), the version must be approved; a delivery receipt is immutable.
/// </summary>
sealed class DeliverAuditResponseHandler(NativeOperationExecutor operations, IAggregateReader reader,
    IImmutableContentStore content, TimeProvider clock) : IRequestHandler<DeliverAuditResponse>
{
    const string CommandKind = "DeliverAuditResponse";

    public async ValueTask<Result> HandleAsync(IRequestContext<DeliverAuditResponse> context, CancellationToken ct)
    {
        var request = context.Request;
        if (!AuditorRequestCommandActor.TryGet(context.Actor, context.Invocation, request.TenantId,
                out var actorMemberId, out var actorDisplay))
            return AuditorCommands.Forbidden("Delivering an audit response requires a personal HTTP actor.");
        if (!AuditorResponseVersionNumber.TryGet(request.Version, out var versionNumber))
            return AuditorCommands.Validation("The audit response version is invalid.");
        if (request.CommandId == Uuid.Empty || request.RequestId == Uuid.Empty || request.ResponseId == Uuid.Empty ||
            request.ExpectedRevision < 0)
            return AuditorCommands.Validation(
                "A delivery requires a command id, a response and a non-negative expected revision.");
        var now = clock.GetUtcNow();
        var snapshot = await reader.HydrateAsync(new AuditorResponse(request.TenantId, request.ProgramId,
            request.RequestId, request.ResponseId, request.CommandId), ct).ConfigureAwait(false);
        var provenance = AuditorContent.Provenance(request.CommandId, CommandKind, snapshot.Stream,
            AuditorCommands.Actor(actorMemberId, actorDisplay), (ulong)request.ExpectedRevision,
            AuditorCommands.Digest(request), now);
        var binding = AcceptedOperationBinding.Of(provenance);

        ImmutableContentRef? delivery = null;
        FirstResponseDescriptor? response = null;
        ImmutableContentRef? result = null;
        var requiresReview = true;
        if (snapshot.Acceptance.Decide(binding) == NativeAcceptanceDecision.None)
        {
            var policy = await AuditorCommands.RequiresReviewAsync(content, request.TenantId, snapshot, ct)
                .ConfigureAwait(false);
            if (!policy.IsSuccess)
                return Result.Failure(policy.Error!);
            requiresReview = policy.Value;
            var prepared = snapshot.PrepareDeliver(request.ExpectedRevision, request.Version, requiresReview);
            if (!prepared.IsSuccess)
                return prepared;
            if (AuditorRequestRules.Delivery(request.Recipient, request.DeliveryReference) is not { } receipt)
                return AuditorCommands.Validation("Intentional response delivery requires a recipient and delivery reference.");
            delivery = await AuditorContent.WriteAsync(content, request.TenantId, AuditorContent.DeliverySchema,
                new AuditorResponseDeliveryView(versionNumber, receipt.Recipient, receipt.Reference, actorMemberId,
                    actorDisplay, now), ct).ConfigureAwait(false);
            (response, result) = await AuditorContent.UnitAsync(content, request.TenantId, ct).ConfigureAwait(false);
        }

        var outcome = await operations.ExecuteAsync(new NativeOperation<AuditorResponse>(binding,
            () => new AuditorResponse(request.TenantId, request.ProgramId, request.RequestId, request.ResponseId,
                request.CommandId),
            auditorResponse => auditorResponse.Acceptance,
            auditorResponse => AuditorContent.Prepared(
                auditorResponse.PrepareDeliver(request.ExpectedRevision, request.Version, requiresReview), response),
            (AuditorResponse auditorResponse, ImmutableContentRef frozen) => auditorResponse.Deliver(provenance,
                request.Version, requiresReview, delivery!, actorMemberId, now, frozen, result!)), context, ct)
            .ConfigureAwait(false);
        return AuditorContent.UnitOutcome(outcome);
    }
}
