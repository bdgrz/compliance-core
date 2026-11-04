using Cntryl.Portia;
using Bdgrz.Compliance.Features.MethodologyTemplates.Commands;
using Bdgrz.Compliance.Features.Content.Contracts;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Handlers;

public sealed class ReviseMethodologyTemplateDraftV3Handler : IRequestHandler<ReviseMethodologyTemplateDraftV3>
{
    public ReviseMethodologyTemplateDraftV3Handler(IAggregateExecutor executor, IImmutableContentStore content)
    {
        ArgumentNullException.ThrowIfNull(executor);
        ArgumentNullException.ThrowIfNull(content);
    }

    public ValueTask<Result> HandleAsync(IRequestContext<ReviseMethodologyTemplateDraftV3> context, CancellationToken ct) =>
        ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.NotFound,
            $"Draft {context.Request.DraftId} command is not implemented.")));
}
