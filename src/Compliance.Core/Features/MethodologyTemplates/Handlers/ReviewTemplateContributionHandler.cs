using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.MethodologyTemplates.Services;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.MethodologyTemplates.Commands;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
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
