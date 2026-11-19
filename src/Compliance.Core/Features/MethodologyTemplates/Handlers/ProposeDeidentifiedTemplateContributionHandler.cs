using Cntryl.Portia;
using Bdgrz.Compliance.Features.MethodologyTemplates.Commands;
using Bdgrz.Compliance.Features.MethodologyTemplates.Services;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Handlers;

public sealed class ProposeDeidentifiedTemplateContributionHandler : IRequestHandler<ProposeDeidentifiedTemplateContribution>
{
    public ProposeDeidentifiedTemplateContributionHandler(MethodologyTemplateContributionOperations operations)
    {
        ArgumentNullException.ThrowIfNull(operations);
    }

    public ValueTask<Result> HandleAsync(IRequestContext<ProposeDeidentifiedTemplateContribution> context, CancellationToken ct) =>
        ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Validation,
            "Client contribution commands are retired. Author platform methodology directly without client source content.")));
}
