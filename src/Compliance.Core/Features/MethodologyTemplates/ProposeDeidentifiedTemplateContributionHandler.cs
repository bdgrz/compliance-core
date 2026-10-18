using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.MethodologyTemplates;

public sealed class ProposeDeidentifiedTemplateContributionHandler(IAggregateExecutor executor,
    TimeProvider clock) : IRequestHandler<ProposeDeidentifiedTemplateContribution>
{
    public ValueTask<Result> HandleAsync(IRequestContext<ProposeDeidentifiedTemplateContribution> context,
        CancellationToken ct)
    {
        var request = context.Request;
        var actor = MethodologyTemplateCommandActor.For(context, request.TenantId);
        return executor.ExecuteAsync(new MethodologyTemplateContribution(request.TenantId,
                request.ContributionId), contribution => CommandFailureRequestAdapter.ToOutcome(
                contribution.Propose(request.SourceRecordId, request.SourceVersion, request.ProposedContent,
                    request.DeidentificationRationale, actor.MemberId, actor.Display, clock.GetUtcNow())),
            context, ct);
    }
}
