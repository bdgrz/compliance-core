using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Exports.Aggregates;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.UserIdentities;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Exports.Handlers;

public sealed class RecordClientExportDeliveryHandler(IAggregateExecutor executor,
    TimeProvider clock) : IRequestHandler<RecordClientExportDelivery>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<RecordClientExportDelivery> context,
        CancellationToken ct)
    {
        if (context.Invocation is not HttpInvocation || RequestActor.IsSystem(context.Actor) ||
            !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var userId))
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Client export delivery requires a personal HTTP decision."));

        var request = context.Request;
        var now = clock.GetUtcNow();
        if (request.TenantId == Uuid.Empty || request.ExportId == Uuid.Empty ||
            request.ExpectedRevision <= 0 || request.DeliveredAt == default || request.DeliveredAt > now)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "Client export delivery requires a valid export revision and a recorded delivery time no later than now."));

        var actorMemberId = RbacIds.Member(request.TenantId, userId);
        var actorDisplay = UserIdentityClaims.BdgrzDisplay(context.Actor, userId);
        return await executor.ExecuteAsync(new ClientExport(request.TenantId, request.ExportId), export =>
                CommandFailureRequestAdapter.ToOutcome(export.Deliver(request.ExpectedRevision,
                    request.Recipient, request.DeliveryReference, actorMemberId, actorDisplay,
                    request.DeliveredAt)), context, ct)
            .ConfigureAwait(false);
    }
}
