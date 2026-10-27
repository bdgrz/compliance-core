using Bdgrz.Compliance.Features.SystemDescription;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.SystemDescription.Commands;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.SystemDescription.Handlers;

public sealed class ReviewSystemDescriptionHandler(SystemDescriptionOperations operations) : IRequestHandler<ReviewSystemDescription>
{
    public ValueTask<Result> HandleAsync(IRequestContext<ReviewSystemDescription> context, CancellationToken ct)
    {
        var request = context.Request;
        if (!SystemDescriptionCommandActor.TryGet(context.Actor, request.TenantId, out var member, out var display))
            return ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Unauthorized, "A description decision requires a Bdgrz member identity.")));
        return operations.ExecuteAsync(context, request.TenantId, request.ProgramId, request.DescriptionId, request.CommandId,
            nameof(ReviewSystemDescription), request.ExpectedRevision, (snapshot, operation, token) =>
            {
                return ValueTask.FromResult(SystemDescriptionOperations.Outcome(snapshot.Rules.Review(request.Version, member, display, request.Outcome, request.Comments, operation.AcceptedAt)));
            }, (rules, operation) => new SystemDescriptionReviewedBody(request.TenantId, request.ProgramId, request.DescriptionId, rules.Revision, rules.Reviews.First(item => item.Version == request.Version && item.ReviewerMemberId == member), operation.AcceptedAt), ct);
    }
}
