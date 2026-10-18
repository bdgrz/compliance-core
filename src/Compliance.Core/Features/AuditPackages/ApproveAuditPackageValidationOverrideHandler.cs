using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPackages;

public sealed class ApproveAuditPackageValidationOverrideHandler(IAggregateExecutor executor,
    TimeProvider clock) : IRequestHandler<ApproveAuditPackageValidationOverride>
{
    public ValueTask<Result> HandleAsync(
        IRequestContext<ApproveAuditPackageValidationOverride> context, CancellationToken ct)
    {
        var request = context.Request;
        if (!AuditPackageCommandActor.TryGet(context.Actor, context.Invocation, request.TenantId,
                out var actorMemberId, out var actorDisplay))
            return ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "A package validation override requires a personal HTTP actor.")));
        if (request.PackageVersion is < 1 or > int.MaxValue)
            return ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A validation override requires a valid package version.")));
        if (!Uuid.TryParse(request.ValidationIssue, out var findingId) || findingId == Uuid.Empty)
            return ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A validation override must identify one exact validation finding.")));

        return executor.ExecuteAsync(new AuditPackage(request.TenantId, request.ProgramId,
                request.PackageId), aggregate => CommandFailureRequestAdapter.ToOutcome(
                aggregate.ApproveValidationOverride(aggregate.Revision,
                    (int)request.PackageVersion, findingId, request.Rationale, actorMemberId,
                    actorDisplay, clock.GetUtcNow())), context, ct);
    }
}
