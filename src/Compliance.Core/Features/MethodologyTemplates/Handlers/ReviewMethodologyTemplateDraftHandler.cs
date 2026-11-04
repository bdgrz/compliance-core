using Cntryl.Portia;
using Bdgrz.Compliance.Features.MethodologyTemplates.Aggregates;
using Bdgrz.Compliance.Features.MethodologyTemplates.Commands;
using Bdgrz.Compliance.Features.MethodologyTemplates.Services;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Handlers;

/// <summary>Records an HTTP personal review using the authenticated operator identity.</summary>
public sealed class ReviewMethodologyTemplateDraftHandler(IAggregateExecutor executor)
    : IRequestHandler<ReviewMethodologyTemplateDraft>
{
    public ValueTask<Result> HandleAsync(IRequestContext<ReviewMethodologyTemplateDraft> context, CancellationToken ct)
    {
        var request = context.Request;
        if (!MethodologyTemplateDraftRequestActor.TryGet(context, request, out var reviewer))
            return ValueTask.FromResult(MethodologyTemplateDraftRequestActor.Forbidden());
        if (!MethodologyTemplateDraftRequestActor.ValidTarget(request))
            return ValueTask.FromResult(MethodologyTemplateDraftRequestActor.Invalid());
        return executor.ExecuteAsync(new MethodologyTemplateDraft(request.TemplateId, request.DraftId),
            owner => AggregateOutcome.CommitOnSuccess(owner.Review(request.Revision, reviewer, request.Accept,
                request.Comments)), context, ct);
    }
}
