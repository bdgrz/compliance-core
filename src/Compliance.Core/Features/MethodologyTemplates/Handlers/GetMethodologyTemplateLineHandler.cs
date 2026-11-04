using Cntryl.Portia;
using Bdgrz.Compliance.Features.MethodologyTemplates.Models;
using Bdgrz.Compliance.Features.MethodologyTemplates.Queries;
using Bdgrz.Compliance.Features.MethodologyTemplates.Readers;
using Bdgrz.Compliance.Features.MethodologyTemplates.Services;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Handlers;

public sealed class GetMethodologyTemplateLineHandler : IRequestHandler<GetMethodologyTemplateLine, MethodologyTemplateLineView>
{
    public GetMethodologyTemplateLineHandler(MethodologyTemplateLineReader templates)
    {
        ArgumentNullException.ThrowIfNull(templates);
    }
    public ValueTask<Result<MethodologyTemplateLineView>> HandleAsync(IRequestContext<GetMethodologyTemplateLine> context, CancellationToken ct) =>
        ValueTask.FromResult(Result<MethodologyTemplateLineView>.Failure(new RequestError(RequestErrorKind.NotFound,
            $"Methodology read {context.Request} has no separate-draft implementation yet.")));
}
