using Cntryl.Portia;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.MethodologyTemplates.Commands;
using Bdgrz.Compliance.Features.MethodologyTemplates.Readers;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.SourceFacts.Contracts;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Handlers;

public sealed class RefuseMethodologyTemplateCreationHandler : IRequestHandler<RefuseMethodologyTemplateCreation>
{
    public RefuseMethodologyTemplateCreationHandler(IAggregateExecutor executor, ISourceFactResolver sources, IImmutableContentStore content, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(executor);
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(clock);
    }
    public ValueTask<Result> HandleAsync(IRequestContext<RefuseMethodologyTemplateCreation> context, CancellationToken ct) =>
        ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.NotFound,
            $"Template {context.Request.TemplateId} creation handler is not implemented.")));
}
