using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.SystemDescription;

public sealed class ReviseSystemDescriptionDraftHandler(IAggregateExecutor executor,
    TimeProvider clock) : IRequestHandler<ReviseSystemDescriptionDraft>
{
    public ValueTask<Result> HandleAsync(IRequestContext<ReviseSystemDescriptionDraft> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (!TryGetAuthor(context, request.TenantId, out var authorMemberId,
                out var authorDisplay))
            return ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Revising a system description requires a personal HTTP actor.")));

        return executor.ExecuteAsync(new SystemDescription(request.TenantId, request.ProgramId,
                request.DescriptionId), aggregate =>
            {
                if (aggregate.CurrentVersion is not { } current)
                    return AggregateOutcome.Discard(Result.Failure(new RequestError(
                        RequestErrorKind.NotFound, "The system description was not found.")));
                var content = new SystemDescriptionDraftContent(request.Title, request.Sections,
                    current.Content.Sources, current.Content.PeriodChanges,
                    current.Content.SignificantChangeIds);
                return CommandFailureRequestAdapter.ToOutcome(aggregate.Revise(
                    request.ExpectedRevision, context.RequestId, content, request.ChangeReason,
                    authorMemberId, authorDisplay, clock.GetUtcNow()));
            }, context, ct);
    }

    static bool TryGetAuthor(IRequestContext<ReviseSystemDescriptionDraft> context,
        Uuid tenantId, out Uuid memberId, out string display)
    {
        memberId = Uuid.Empty;
        display = string.Empty;
        return context.Invocation is HttpInvocation && !RequestActor.IsSystem(context.Actor) &&
               SystemDescriptionCommandActor.TryGet(context.Actor, tenantId, out memberId,
                   out display);
    }
}
