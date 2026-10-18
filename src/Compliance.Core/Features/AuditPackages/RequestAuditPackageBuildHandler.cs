using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPackages;

public sealed class RequestAuditPackageBuildHandler(IAggregateExecutor executor,
    TimeProvider clock) : IRequestHandler<RequestAuditPackageBuild>
{
    public ValueTask<Result> HandleAsync(IRequestContext<RequestAuditPackageBuild> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (!AuditPackageCommandActor.TryGet(context.Actor, context.Invocation, request.TenantId,
                out var actorMemberId, out var actorDisplay))
            return ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "An audit package build request requires a personal HTTP actor.")));

        return executor.ExecuteAsync(new AuditPackage(request.TenantId, request.ProgramId,
                request.PackageId), aggregate =>
            {
                var current = aggregate.Current;
                if (current is null)
                    return AggregateOutcome.Discard(Result.Failure(new RequestError(
                        RequestErrorKind.NotFound,
                        "The audit package was not found in this program.")));
                if (current.ExaminationId != request.ExaminationId ||
                    !string.Equals(current.PackageKind, request.PackageKind, StringComparison.Ordinal))
                    return AggregateOutcome.Discard(Result.Failure(new RequestError(
                        RequestErrorKind.Conflict,
                        "The build request must identify the package's exact examination and kind.")));

                return CommandFailureRequestAdapter.ToOutcome(aggregate.RequestBuild(
                    aggregate.Revision, request.BuildKey, actorMemberId, actorDisplay,
                    clock.GetUtcNow()));
            }, context, ct);
    }
}
