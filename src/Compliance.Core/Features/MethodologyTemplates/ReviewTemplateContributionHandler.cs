using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.MethodologyTemplates;

public sealed class ReviewTemplateContributionHandler(IAggregateExecutor executor, TimeProvider clock)
    : IRequestHandler<ReviewTemplateContribution>
{
    public ValueTask<Result> HandleAsync(IRequestContext<ReviewTemplateContribution> context,
        CancellationToken ct)
    {
        var request = context.Request;
        var actor = MethodologyTemplateCommandActor.For(context, Uuid.Empty);
        return executor.ExecuteAsync(new MethodologyTemplateContribution(request.TenantId,
                request.ContributionId), contribution => CommandFailureRequestAdapter.ToOutcome(
                contribution.Review(contribution.Revision, request.Outcome, request.ReviewNotes,
                    actor.MemberId, actor.Display, clock.GetUtcNow())), context, ct);
    }
}
