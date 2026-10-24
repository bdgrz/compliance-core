using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Controls;

public sealed class ReviewControlHandler(IAggregateExecutor executor, IAggregateReader reader,
    ControlActivationReleaseGate releaseGate, TimeProvider clock)
    : IRequestHandler<ReviewControl>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<ReviewControl> context,
        CancellationToken ct)
    {
        if (context.Invocation is not HttpInvocation || RequestActor.IsSystem(context.Actor) ||
            !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out _))
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Control decision requires a personal HTTP invocation."));
        var request = context.Request;
        var userId = UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var subject)
            ? subject
            : throw new InvalidOperationException("ProgramManagementAuthorizer must reject this actor.");
        var operation = AuthoredCommandReplay.Create(new ControlDraft(request.TenantId, request.ControlId, request.CommandId), request.CommandId,
            nameof(ReviewControl), RbacIds.Member(request.TenantId, userId), request.ExpectedRevision, request, clock.GetUtcNow());
        var replayOwner = await reader.HydrateAsync(new ControlDraft(request.TenantId, request.ControlId, request.CommandId), ct).ConfigureAwait(false);
        if (AuthoredCommandReplay.Unit(replayOwner.ReplayAcceptance, operation) is { } replay)
            return replay;
        if (!releaseGate.IsEnabled)
            return Result.Failure(ControlActivationReleaseGate.Unavailable);

        SeparationOfDutiesWaiver? waiver = null;
        if (request.SeparationOfDutiesWaiverId is { } waiverId)
            waiver = await reader.HydrateAsync(new SeparationOfDutiesWaiver(request.TenantId,
                waiverId), ct).ConfigureAwait(false);
        return await executor.ExecuteAsync(new ControlDraft(request.TenantId, request.ControlId, request.CommandId),
            control => CommandFailureRequestAdapter.ToOutcome(control.Review(request.ProgramId,
                request.ExpectedRevision, request.CommandId, request.Outcome, request.Rationale,
                RbacIds.Member(request.TenantId, userId),
                UserIdentityClaims.BdgrzDisplay(context.Actor, userId), clock.GetUtcNow(),
                waiver, operation: operation)),
            context, ct).ConfigureAwait(false);
    }
}
