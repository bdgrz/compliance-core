using Cntryl.Portia;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.MethodologyTemplates.Commands;
using Bdgrz.Compliance.Features.SourceFacts.Contracts;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Handlers;

public sealed class PublishMethodologyTemplateVersionHandler : IRequestHandler<PublishMethodologyTemplateVersion>
{
    public PublishMethodologyTemplateVersionHandler(IAggregateExecutor executor, IAggregateReader reader,
        ISourceFactResolver sources, IImmutableContentStore content)
    {
        ArgumentNullException.ThrowIfNull(executor);
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentNullException.ThrowIfNull(content);
    }

    public ValueTask<Result> HandleAsync(IRequestContext<PublishMethodologyTemplateVersion> context, CancellationToken ct) =>
        ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.NotFound,
            $"Template {context.Request.TemplateId} line command is not implemented.")));
}
