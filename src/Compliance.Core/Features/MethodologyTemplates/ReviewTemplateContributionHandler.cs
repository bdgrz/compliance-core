using Bdgrz.Compliance.Features.NativeOperations;
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
        var operation = AuthoredCommandReplay.Create(new MethodologyTemplateContribution(request.TenantId,
                request.ContributionId, request.CommandId), request.CommandId,
            nameof(ReviewTemplateContribution), actor.MemberId, null, request, clock.GetUtcNow(), "platform_operator");

        return executor.ExecuteAsync(new MethodologyTemplateContribution(request.TenantId,
                request.ContributionId, request.CommandId), contribution => CommandFailureRequestAdapter.ToOutcome(
                contribution.Review(contribution.Revision, request.Outcome, request.ReviewNotes,
                    actor.MemberId, actor.Display, clock.GetUtcNow(), operation: operation)), context, ct);
    }
}
