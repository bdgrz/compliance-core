using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

public sealed class ReviseRiskDraftHandler(IAggregateExecutor executor,
    TimeProvider clock) : IRequestHandler<ReviseRiskDraft>
{
    public ValueTask<Result> HandleAsync(IRequestContext<ReviseRiskDraft> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (request.CommandId == Uuid.Empty)
            return ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A risk revision requires its typed command identity.")));
        var userId = UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var subject)
            ? subject
            : throw new InvalidOperationException("ProgramManagementAuthorizer must reject this actor.");
        return executor.ExecuteAsync(new RiskDraft(request.TenantId, request.RiskId, request.CommandId),
            risk => CommandFailureRequestAdapter.ToOutcome(risk.Revise(request,
                RbacIds.Member(request.TenantId, userId),
                UserIdentityClaims.BdgrzDisplay(context.Actor, userId), clock.GetUtcNow())),
            context, ct);
    }
}
