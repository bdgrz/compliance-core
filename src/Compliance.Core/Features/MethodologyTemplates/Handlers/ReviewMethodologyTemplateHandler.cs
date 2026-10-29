using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.MethodologyTemplates.Services;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.MethodologyTemplates.Commands;
namespace Bdgrz.Compliance.Features.MethodologyTemplates.Handlers;

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
