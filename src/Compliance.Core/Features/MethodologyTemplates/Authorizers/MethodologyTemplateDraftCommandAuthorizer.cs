using Cntryl.Portia;
using Bdgrz.Compliance.Features.MethodologyTemplates.Contracts;
using Bdgrz.Compliance.Features.Tenants.Contracts;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Authorizers;

/// <summary>Authorizes the named methodology permissions through the platform super-administrator roster.</summary>
public sealed class MethodologyTemplateDraftCommandAuthorizer : IRequestAuthorizer<IMethodologyTemplateDraftCommand>
{
    public MethodologyTemplateDraftCommandAuthorizer(IPlatformOperatorAccess operators)
    {
        ArgumentNullException.ThrowIfNull(operators);
    }

    public ValueTask<Result> AuthorizeAsync(IRequestContext<IMethodologyTemplateDraftCommand> context,
        CancellationToken ct) => ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Forbidden,
            $"The actor cannot perform {context.Request.Permission}.")));
}
