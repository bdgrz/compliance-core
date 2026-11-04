using Cntryl.Portia;
using Bdgrz.Compliance.Features.MethodologyTemplates.Commands;
using Bdgrz.Compliance.Features.Tenants.Contracts;
using Bdgrz.Compliance.Features.UserIdentities.Services;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Authorizers;

/// <summary>Checks methodology approval against the current platform super-administrator roster.</summary>
public sealed class MethodologyTemplateRetirementAuthorizer(IPlatformOperatorAccess operators)
    : IRequestAuthorizer<RetireMethodologyTemplateV3>
{
    public async ValueTask<Result> AuthorizeAsync(IRequestContext<RetireMethodologyTemplateV3> context, CancellationToken ct)
    {
        if (RequestActor.IsSystem(context.Actor) || !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var user))
            return Denied();
        return await operators.IsOperatorAsync(user, ct).ConfigureAwait(false) ? Result.Success : Denied();
    }

    static Result Denied() => Result.Failure(new RequestError(RequestErrorKind.Forbidden,
        "Retiring methodology requires a personal current platform operator with methodology.approve."));
}
