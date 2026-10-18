using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.SystemDescription;

public sealed class BindSystemDescriptionToExaminationHandler(IAggregateExecutor executor,
    TimeProvider clock) : IRequestHandler<BindSystemDescriptionToExamination>
{
    public ValueTask<Result> HandleAsync(IRequestContext<BindSystemDescriptionToExamination> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (!SystemDescriptionCommandActor.TryGet(context.Actor, request.TenantId,
                out var actorMemberId, out var actorDisplay))
            return ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Unauthorized,
                "Binding a system description requires a Bdgrz member identity.")));
        return executor.ExecuteAsync(new SystemDescription(request.TenantId, request.ProgramId,
                request.DescriptionId), aggregate =>
            {
                var version = aggregate.FindVersion(request.Version);
                var snapshotId = version?.Scope.Stage switch
                {
                    "type_i" => version.Scope.TypeIBaselineId ?? Uuid.Empty,
                    "type_ii" => version.Scope.CloseSnapshotId ?? Uuid.Empty,
                    _ => Uuid.Empty,
                };
                var failure = aggregate.BindToExamination(request.Version, request.ExaminationId,
                    request.BindingPurpose, snapshotId, actorMemberId, actorDisplay,
                    clock.GetUtcNow());
                return CommandFailureRequestAdapter.ToOutcome(failure);
            }, context, ct);
    }
}
