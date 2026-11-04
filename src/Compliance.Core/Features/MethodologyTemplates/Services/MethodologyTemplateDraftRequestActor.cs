using Cntryl.Portia;
using Bdgrz.Compliance.Features.MethodologyTemplates.Contracts;
using Bdgrz.Compliance.Features.UserIdentities.Services;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Services;

/// <summary>Derives the personal platform actor and enforces the personal-decision transport boundary.</summary>
static class MethodologyTemplateDraftRequestActor
{
    public static bool TryGet(IRequestContext context, IMethodologyTemplateDraftCommand request, out Uuid userId)
    {
        userId = Uuid.Empty;
        return !RequestActor.IsSystem(context.Actor) && (!request.RequiresHttp || context.Invocation is HttpInvocation) &&
            UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out userId);
    }

    public static bool ValidTarget(IMethodologyTemplateDraftCommand request) => request.CommandId != Uuid.Empty &&
        request.TemplateId != Uuid.Empty && request.DraftId != Uuid.Empty;

    public static Result Forbidden() => Result.Failure(new RequestError(RequestErrorKind.Forbidden,
        "Methodology draft actions require a personal platform operator; review and approval require HTTP."));
    public static Result Invalid() => Result.Failure(new RequestError(RequestErrorKind.Validation,
        "A methodology draft command requires its exact command, template and draft identities."));
}
