using Cntryl.Portia;
using Bdgrz.Compliance.Features.MethodologyTemplates.Aggregates;
using Bdgrz.Compliance.Features.MethodologyTemplates.Commands;
using Bdgrz.Compliance.Features.MethodologyTemplates.Services;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Handlers;

/// <summary>Records the independent third person's HTTP approval of the accepted current revision.</summary>
public sealed class ApproveMethodologyTemplateDraftHandler(IAggregateExecutor executor)
    : IRequestHandler<ApproveMethodologyTemplateDraft>
{
    public ValueTask<Result> HandleAsync(IRequestContext<ApproveMethodologyTemplateDraft> context, CancellationToken ct)
    {
        var request = context.Request;
        if (!MethodologyTemplateDraftRequestActor.TryGet(context, request, out var approver))
            return ValueTask.FromResult(MethodologyTemplateDraftRequestActor.Forbidden());
        if (!MethodologyTemplateDraftRequestActor.ValidTarget(request))
            return ValueTask.FromResult(MethodologyTemplateDraftRequestActor.Invalid());
        return executor.ExecuteAsync(new MethodologyTemplateDraft(request.TemplateId, request.DraftId),
            owner => AggregateOutcome.CommitOnSuccess(owner.Approve(request.Revision, approver)), context, ct);
    }
}
