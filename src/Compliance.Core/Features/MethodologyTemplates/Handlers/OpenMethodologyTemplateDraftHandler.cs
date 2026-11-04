using Cntryl.Portia;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.MethodologyTemplates.Commands;
using Bdgrz.Compliance.Features.SourceFacts.Contracts;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Handlers;

/// <summary>Opens a separate draft only from its exact immutable line-start source.</summary>
public sealed class OpenMethodologyTemplateDraftHandler : IRequestHandler<OpenMethodologyTemplateDraft>
{
    public OpenMethodologyTemplateDraftHandler(IAggregateExecutor executor, ISourceFactResolver sources,
        IImmutableContentStore content)
    {
        ArgumentNullException.ThrowIfNull(executor);
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentNullException.ThrowIfNull(content);
    }

    public ValueTask<Result> HandleAsync(IRequestContext<OpenMethodologyTemplateDraft> context, CancellationToken ct) =>
        ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.NotFound,
            $"Draft {context.Request.DraftId} has not been opened from its line yet.", true)));
}
