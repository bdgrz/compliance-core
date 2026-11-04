using Cntryl.Portia;
using Bdgrz.Compliance.Features.MethodologyTemplates.Aggregates;
using Bdgrz.Compliance.Features.MethodologyTemplates.Commands;
using Bdgrz.Compliance.Features.MethodologyTemplates.Services;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Handlers;

/// <summary>Records the operator's reason for discarding a draft that has not been approved.</summary>
public sealed class DiscardMethodologyTemplateDraftHandler(IAggregateExecutor executor)
    : IRequestHandler<DiscardMethodologyTemplateDraft>
{
    public ValueTask<Result> HandleAsync(IRequestContext<DiscardMethodologyTemplateDraft> context, CancellationToken ct)
    {
        var request = context.Request;
        if (!MethodologyTemplateDraftRequestActor.TryGet(context, request, out var actor))
            return ValueTask.FromResult(MethodologyTemplateDraftRequestActor.Forbidden());
        if (!MethodologyTemplateDraftRequestActor.ValidTarget(request))
            return ValueTask.FromResult(MethodologyTemplateDraftRequestActor.Invalid());
        return executor.ExecuteAsync(new MethodologyTemplateDraft(request.TemplateId, request.DraftId),
            owner => AggregateOutcome.CommitOnSuccess(owner.Discard(actor, request.Reason)), context, ct);
    }
}
