using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPackages;

/// <summary>Creates a Type I package only from sources pinned by its frozen baseline.</summary>
public sealed class CreateAuditPackageHandler(IAggregateExecutor executor,
    AuditPackageSourceBuilder sources, TimeProvider clock) : IRequestHandler<CreateAuditPackage>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<CreateAuditPackage> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (!AuditPackageCommandActor.TryGet(context.Actor, context.Invocation, request.TenantId,
                out var actorMemberId, out var actorDisplay))
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Audit package creation requires a personal HTTP actor."));

        var content = await sources.ResolveAsync(request.TenantId, request.ProgramId,
            request.Content, ct).ConfigureAwait(false);
        if (!content.IsSuccess)
            return Result.Failure(content.Error);

        return await executor.ExecuteAsync(new AuditPackage(request.TenantId, request.ProgramId,
                request.PackageId), aggregate =>
            CommandFailureRequestAdapter.ToOutcome(aggregate.Create(content.Value,
                actorMemberId, actorDisplay, clock.GetUtcNow())), context, ct)
            .ConfigureAwait(false);
    }
}
