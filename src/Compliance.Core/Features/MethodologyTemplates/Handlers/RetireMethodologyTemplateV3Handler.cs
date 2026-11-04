using Cntryl.Portia;
using Bdgrz.Compliance.Features.MethodologyTemplates.Aggregates;
using Bdgrz.Compliance.Features.MethodologyTemplates.Commands;
using Bdgrz.Compliance.Features.UserIdentities.Services;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Handlers;

/// <summary>Invokes the line's idle-only terminal transition; no publication or legacy draft is required.</summary>
public sealed class RetireMethodologyTemplateV3Handler(IAggregateExecutor executor)
    : IRequestHandler<RetireMethodologyTemplateV3>
{
    public ValueTask<Result> HandleAsync(IRequestContext<RetireMethodologyTemplateV3> context, CancellationToken ct)
    {
        var request = context.Request;
        if (RequestActor.IsSystem(context.Actor) || !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var user))
            return ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Retiring methodology requires a personal platform operator.")));
        if (request.CommandId == Uuid.Empty || request.TemplateId == Uuid.Empty || string.IsNullOrWhiteSpace(request.Reason) ||
            request.Reason.Length > 2000)
            return ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A retirement requires its exact command and template identities and a reason of at most 2000 characters.")));
        return executor.ExecuteAsync(new MethodologyTemplate(request.TemplateId),
            owner => AggregateOutcome.CommitOnSuccess(owner.RetireLine(user, request.Reason)), context, ct);
    }
}
