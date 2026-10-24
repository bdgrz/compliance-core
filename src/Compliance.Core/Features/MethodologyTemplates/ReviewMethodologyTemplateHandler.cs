using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.MethodologyTemplates;

public sealed class ReviewMethodologyTemplateHandler(MethodologyTemplateOperations operations) : IRequestHandler<ReviewMethodologyTemplate>
{
    public ValueTask<Result> HandleAsync(IRequestContext<ReviewMethodologyTemplate> context, CancellationToken ct)
    {
        var request = context.Request;
        return operations.ExecuteAsync(context, request.TemplateId, request.CommandId, nameof(ReviewMethodologyTemplate),
            null, (owner, member) => owner.PrepareReview(request.Version, member),
            (rules, member, display, at) => rules.ReviewDraft(rules.Revision, request.Outcome, request.Comments, member, display, at), ct);
    }
}
