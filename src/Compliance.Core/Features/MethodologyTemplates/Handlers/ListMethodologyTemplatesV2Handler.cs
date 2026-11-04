using Cntryl.Portia;
using Bdgrz.Compliance.Features.MethodologyTemplates.Models;
using Bdgrz.Compliance.Features.MethodologyTemplates.Queries;
using Bdgrz.Compliance.Features.MethodologyTemplates.Readers;
using Bdgrz.Compliance.Features.MethodologyTemplates.Services;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Handlers;

public sealed class ListMethodologyTemplatesV2Handler : IRequestHandler<ListMethodologyTemplatesV2, Page<MethodologyTemplateSummaryView>>
{
    public ListMethodologyTemplatesV2Handler(IMethodologyTemplateDirectoryReader directory, MethodologyTemplateDirectoryReadConsistency consistency, MethodologyTemplateLineReader templates)
    {
        ArgumentNullException.ThrowIfNull(directory);
        ArgumentNullException.ThrowIfNull(consistency);
        ArgumentNullException.ThrowIfNull(templates);
    }
    public ValueTask<Result<Page<MethodologyTemplateSummaryView>>> HandleAsync(IRequestContext<ListMethodologyTemplatesV2> context, CancellationToken ct) =>
        ValueTask.FromResult(Result<Page<MethodologyTemplateSummaryView>>.Failure(new RequestError(RequestErrorKind.NotFound,
            $"Methodology read {context.Request} has no separate-draft implementation yet.")));
}
