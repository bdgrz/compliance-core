using Cntryl.Portia;
using Bdgrz.Compliance.Features.MethodologyTemplates.Commands;
using Bdgrz.Compliance.Features.Tenants.Contracts;
using Bdgrz.Compliance.Features.UserIdentities.Services;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Authorizers;

/// <summary>Original platform authoring uses the current super-administrator roster and a personal server-derived actor.</summary>
public sealed class MethodologyTemplateCreationAuthorizer(IPlatformOperatorAccess operators) : IRequestAuthorizer<CreateMethodologyTemplateV3>
{
    public async ValueTask<Result> AuthorizeAsync(IRequestContext<CreateMethodologyTemplateV3> context, CancellationToken ct)
    {
        if (RequestActor.IsSystem(context.Actor) || !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var user))
            return Denied();
        return await operators.IsOperatorAsync(user, ct).ConfigureAwait(false) ? Result.Success : Denied();
    }
    static Result Denied() => Result.Failure(new RequestError(RequestErrorKind.Forbidden,
        "Only a personal current platform operator may author a methodology template."));
}
