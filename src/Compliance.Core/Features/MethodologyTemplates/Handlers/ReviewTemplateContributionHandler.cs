using Cntryl.Portia;
using Bdgrz.Compliance.Features.MethodologyTemplates.Commands;
using Bdgrz.Compliance.Features.MethodologyTemplates.Services;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Handlers;

public sealed class ReviewTemplateContributionHandler : IRequestHandler<ReviewTemplateContribution>
{
    public ReviewTemplateContributionHandler(MethodologyTemplateContributionOperations operations)
    {
        ArgumentNullException.ThrowIfNull(operations);
    }

    public ValueTask<Result> HandleAsync(IRequestContext<ReviewTemplateContribution> context, CancellationToken ct) =>
        ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Validation,
            "Client contribution commands are retired. Author platform methodology directly without client source content.")));
}
