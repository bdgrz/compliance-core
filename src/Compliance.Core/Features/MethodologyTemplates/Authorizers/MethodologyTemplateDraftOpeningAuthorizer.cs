using Cntryl.Portia;
using Bdgrz.Compliance.Features.MethodologyTemplates.Commands;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Authorizers;

/// <summary>Only the committed line's internal reaction can open a methodology draft.</summary>
public sealed class MethodologyTemplateDraftOpeningAuthorizer : IRequestAuthorizer<OpenMethodologyTemplateDraft>
{
    public ValueTask<Result> AuthorizeAsync(IRequestContext<OpenMethodologyTemplateDraft> context, CancellationToken ct) =>
        ValueTask.FromResult(RequestActor.IsSystem(context.Actor) ? Result.Success : Result.Failure(new RequestError(
            RequestErrorKind.Forbidden, "Only a system line-start reaction may open a methodology draft.")));
}
