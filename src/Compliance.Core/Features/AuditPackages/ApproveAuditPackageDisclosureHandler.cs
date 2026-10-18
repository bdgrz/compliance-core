using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPackages;

public sealed class ApproveAuditPackageDisclosureHandler(IAggregateExecutor executor,
    TimeProvider clock) : IRequestHandler<ApproveAuditPackageDisclosure>
{
    public ValueTask<Result> HandleAsync(IRequestContext<ApproveAuditPackageDisclosure> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (!AuditPackageCommandActor.TryGet(context.Actor, context.Invocation, request.TenantId,
                out var actorMemberId, out var actorDisplay))
            return ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Audit package disclosure approval requires a personal HTTP actor.")));
        if (request.PackageVersion is < 1 or > int.MaxValue)
            return ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Validation,
                "Disclosure approval requires a valid package version.")));

        return executor.ExecuteAsync(new AuditPackage(request.TenantId, request.ProgramId,
                request.PackageId), aggregate => CommandFailureRequestAdapter.ToOutcome(
                aggregate.ApproveDisclosure(aggregate.Revision, (int)request.PackageVersion,
                    request.Recipient, request.Purpose, request.Rationale, actorMemberId,
                    actorDisplay, clock.GetUtcNow())), context, ct);
    }
}
