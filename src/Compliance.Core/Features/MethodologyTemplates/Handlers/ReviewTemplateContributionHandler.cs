using Bdgrz.Compliance.Features.MethodologyTemplates;
using Bdgrz.Compliance.Features.MethodologyTemplates.Commands;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Handlers;

public sealed class ReviewTemplateContributionHandler(MethodologyTemplateContributionOperations operations)
    : IRequestHandler<ReviewTemplateContribution>
{
    public ValueTask<Result> HandleAsync(IRequestContext<ReviewTemplateContribution> context, CancellationToken ct)
    {
        var request = context.Request;
        return operations.ExecuteAsync(context, request.TenantId, request.ContributionId, request.CommandId,
            nameof(ReviewTemplateContribution), true, (owner, user) => owner.PrepareReview(user),
            (rules, member, display, at) => rules.Review(rules.Revision, request.Outcome, request.ReviewNotes, member, display, at),
            (owner, operation, member, user, change, response, result) =>
                owner.Review(operation, member, user, request.Outcome.Trim().ToLowerInvariant(), change, response, result), ct);
    }
}
