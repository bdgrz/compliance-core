using Cntryl.Portia;
using Bdgrz.Compliance.Features.MethodologyTemplates.Contracts;
using Bdgrz.Compliance.Features.MethodologyTemplates.Services;
using Bdgrz.Compliance.Features.Tenants.Contracts;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Authorizers;

/// <summary>Authorizes the named methodology permissions through the platform super-administrator roster.</summary>
public sealed class MethodologyTemplateDraftCommandAuthorizer(IPlatformOperatorAccess operators)
    : IRequestAuthorizer<IMethodologyTemplateDraftCommand>
{
    public async ValueTask<Result> AuthorizeAsync(IRequestContext<IMethodologyTemplateDraftCommand> context, CancellationToken ct)
    {
        if (!MethodologyTemplateDraftRequestActor.TryGet(context, context.Request, out var userId))
            return MethodologyTemplateDraftRequestActor.Forbidden();
        if (context.Request.Permission is not (IMethodologyTemplateDraftCommand.AuthorPermission or
            IMethodologyTemplateDraftCommand.ApprovePermission))
            return MethodologyTemplateDraftRequestActor.Forbidden();
        return await operators.IsOperatorAsync(userId, ct).ConfigureAwait(false)
            ? Result.Success : Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Only a current platform operator may perform methodology authoring or approval."));
    }
}
