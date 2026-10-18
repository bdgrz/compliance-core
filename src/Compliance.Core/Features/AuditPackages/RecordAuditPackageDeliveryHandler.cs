using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPackages;

public sealed class RecordAuditPackageDeliveryHandler(IAggregateExecutor executor)
    : IRequestHandler<RecordAuditPackageDelivery>
{
    public ValueTask<Result> HandleAsync(IRequestContext<RecordAuditPackageDelivery> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (!AuditPackageCommandActor.TryGet(context.Actor, context.Invocation, request.TenantId,
                out var actorMemberId, out var actorDisplay))
            return ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Audit package delivery requires a personal HTTP actor.")));
        if (request.PackageVersion is < 1 or > int.MaxValue)
            return ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Validation,
                "Delivery requires a valid package version.")));

        return executor.ExecuteAsync(new AuditPackage(request.TenantId, request.ProgramId,
                request.PackageId), aggregate => CommandFailureRequestAdapter.ToOutcome(
                aggregate.RecordDelivery(aggregate.Revision, (int)request.PackageVersion,
                    request.Recipient, request.DeliveryReference, actorMemberId, actorDisplay,
                    request.DeliveredAt)), context, ct);
    }
}
