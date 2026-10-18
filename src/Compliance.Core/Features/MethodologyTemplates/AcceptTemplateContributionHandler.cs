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
        return executor.ExecuteAsync(new MethodologyTemplateContribution(request.TenantId,
                request.ContributionId), contribution => CommandFailureRequestAdapter.ToOutcome(
                contribution.Accept(contribution.Revision, request.AcceptanceRationale,
                    actor.MemberId, actor.Display, clock.GetUtcNow())), context, ct);
    }
}
