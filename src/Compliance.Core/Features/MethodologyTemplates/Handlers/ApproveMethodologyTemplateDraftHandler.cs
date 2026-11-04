using Cntryl.Portia;
using Bdgrz.Compliance.Features.MethodologyTemplates.Commands;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Handlers;

public sealed class ApproveMethodologyTemplateDraftHandler : IRequestHandler<ApproveMethodologyTemplateDraft>
{
    public ApproveMethodologyTemplateDraftHandler(IAggregateExecutor executor)
    {
        ArgumentNullException.ThrowIfNull(executor);
    }

    public ValueTask<Result> HandleAsync(IRequestContext<ApproveMethodologyTemplateDraft> context, CancellationToken ct) =>
        ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.NotFound,
            $"Draft {context.Request.DraftId} command is not implemented.")));
}
