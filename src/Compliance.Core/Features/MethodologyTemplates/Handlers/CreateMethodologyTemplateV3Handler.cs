using Cntryl.Portia;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.MethodologyTemplates.Commands;
using Bdgrz.Compliance.Features.MethodologyTemplates.Readers;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.SourceFacts.Contracts;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Handlers;

public sealed class CreateMethodologyTemplateV3Handler : IRequestHandler<CreateMethodologyTemplateV3>
{
    public CreateMethodologyTemplateV3Handler(NativeOperationExecutor operations, IImmutableContentStore content, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(operations);
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(clock);
    }
    public ValueTask<Result> HandleAsync(IRequestContext<CreateMethodologyTemplateV3> context, CancellationToken ct) =>
        ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.NotFound,
            $"Template {context.Request.TemplateId} creation handler is not implemented.")));
}
