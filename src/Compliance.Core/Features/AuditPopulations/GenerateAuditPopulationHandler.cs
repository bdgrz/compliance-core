using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPopulations;

public sealed class GenerateAuditPopulationHandler(AuditPopulationMutations mutations,
    TimeProvider clock) : IRequestHandler<GenerateAuditPopulation>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<GenerateAuditPopulation> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (!AuditPopulationCommandActor.TryGet(context.Actor, context.Invocation, request.TenantId,
                out var actorMemberId, out var actorDisplay))
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Starting audit population collection requires a personal HTTP actor."));

        var appended = await mutations.AppendUnqualifiedAsync(context, request.TenantId, request.ProgramId,
            request.DefinitionId, rules =>
            {
                if (rules.Current is not { } current)
                    return CommandFailure.MissingRecord("The audit population was not found.");
                if (current.CloseSnapshot.CloseSnapshotId != request.PeriodCloseId)
                    return CommandFailure.StateConflict("Collection must target the exact period-close snapshot pinned by the population.");
                return rules.Generate(rules.Revision, request.GenerationKey, actorMemberId, actorDisplay, clock.GetUtcNow());
            }, ct).ConfigureAwait(false);
        return appended.IsSuccess ? Result.Success : Result.Failure(appended.Error!);
    }
}
