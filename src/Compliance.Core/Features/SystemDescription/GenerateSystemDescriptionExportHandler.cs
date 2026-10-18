using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.SystemDescription;

public sealed class GenerateSystemDescriptionExportHandler(IAggregateExecutor executor,
    TimeProvider clock) : IRequestHandler<GenerateSystemDescriptionExport>
{
    public ValueTask<Result> HandleAsync(IRequestContext<GenerateSystemDescriptionExport> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (!SystemDescriptionCommandActor.TryGet(context.Actor, request.TenantId,
                out var actorMemberId, out var actorDisplay))
            return ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Unauthorized,
                "Generating a system description export requires a Bdgrz member identity.")));
        return executor.ExecuteAsync(new SystemDescription(request.TenantId, request.ProgramId,
                request.DescriptionId), aggregate => CommandFailureRequestAdapter.ToOutcome(
                aggregate.GenerateExport(request.Version, context.RequestId, request.Format,
                    request.ExportKey, actorMemberId, actorDisplay, clock.GetUtcNow())), context, ct);
    }
}
