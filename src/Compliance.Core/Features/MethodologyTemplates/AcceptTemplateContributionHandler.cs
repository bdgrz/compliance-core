using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.MethodologyTemplates;

public sealed class AcceptTemplateContributionHandler(IAggregateExecutor executor, TimeProvider clock)
    : IRequestHandler<AcceptTemplateContribution>
{
    public ValueTask<Result> HandleAsync(IRequestContext<AcceptTemplateContribution> context,
        CancellationToken ct)
    {
        var request = context.Request;
        var actor = MethodologyTemplateCommandActor.For(context, Uuid.Empty);
        var operation = AuthoredCommandReplay.Create(new MethodologyTemplateContribution(request.TenantId,
                request.ContributionId, request.CommandId), request.CommandId,
            nameof(AcceptTemplateContribution), actor.MemberId, null, request, clock.GetUtcNow(), "platform_operator");

        return executor.ExecuteAsync(new MethodologyTemplateContribution(request.TenantId,
                request.ContributionId, request.CommandId), contribution => CommandFailureRequestAdapter.ToOutcome(
                contribution.Accept(contribution.Revision, request.AcceptanceRationale,
                    actor.MemberId, actor.Display, clock.GetUtcNow(), operation: operation)), context, ct);
    }
}
