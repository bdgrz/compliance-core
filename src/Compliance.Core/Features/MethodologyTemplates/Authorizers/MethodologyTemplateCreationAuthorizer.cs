using Cntryl.Portia;
using Bdgrz.Compliance.Features.MethodologyTemplates.Commands;
using Bdgrz.Compliance.Features.Tenants.Contracts;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Authorizers;

public sealed class MethodologyTemplateCreationAuthorizer : IRequestAuthorizer<CreateMethodologyTemplateV3>
{
    public MethodologyTemplateCreationAuthorizer(IPlatformOperatorAccess operators) => ArgumentNullException.ThrowIfNull(operators);
    public ValueTask<Result> AuthorizeAsync(IRequestContext<CreateMethodologyTemplateV3> context, CancellationToken ct) =>
        ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Forbidden,
            $"Template {context.Request.TemplateId} requires a personal platform operator.")));
}
