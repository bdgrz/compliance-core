using Cntryl.Portia;
using Bdgrz.Compliance.Features.MethodologyTemplates.Commands;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Authorizers;

public sealed class MethodologyTemplateCreationReactionAuthorizer : IRequestAuthorizer<CreateMethodologyTemplateLine>,
    IRequestAuthorizer<RefuseMethodologyTemplateCreation>
{
    public ValueTask<Result> AuthorizeAsync(IRequestContext<CreateMethodologyTemplateLine> context, CancellationToken ct) =>
        ValueTask.FromResult(Check(context));
    public ValueTask<Result> AuthorizeAsync(IRequestContext<RefuseMethodologyTemplateCreation> context, CancellationToken ct) =>
        ValueTask.FromResult(Check(context));
    static Result Check(IRequestContext context) => RequestActor.IsSystem(context.Actor) ? Result.Success :
        Result.Failure(new RequestError(RequestErrorKind.Forbidden, "Only an admitted name reaction may create or refuse its template line."));
}
