using Cntryl.Portia;
using Bdgrz.Compliance.Features.MethodologyTemplates.Commands;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Authorizers;

/// <summary>Restricts line publication and closure to their internal committed draft reactions.</summary>
public sealed class MethodologyTemplateLineReactionAuthorizer : IRequestAuthorizer<PublishMethodologyTemplateVersion>,
    IRequestAuthorizer<CloseMethodologyTemplateDraft>
{
    public ValueTask<Result> AuthorizeAsync(IRequestContext<PublishMethodologyTemplateVersion> context, CancellationToken ct) =>
        ValueTask.FromResult(Authorize(context));
    public ValueTask<Result> AuthorizeAsync(IRequestContext<CloseMethodologyTemplateDraft> context, CancellationToken ct) =>
        ValueTask.FromResult(Authorize(context));
    static Result Authorize(IRequestContext context) => RequestActor.IsSystem(context.Actor) ? Result.Success :
        Result.Failure(new RequestError(RequestErrorKind.Forbidden, "Only an internal draft reaction may publish or close its template line."));
}
