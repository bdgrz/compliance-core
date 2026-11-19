using Cntryl.Portia;
using Bdgrz.Compliance.Features.MethodologyTemplates.Commands;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Authorizers;

/// <summary>Canonical application child opening and parent completion are internal source-verified reactions.</summary>
public sealed class TemplateApplicationV4ReactionAuthorizer : IRequestAuthorizer<OpenTemplateVersionApplicationV4>,
    IRequestAuthorizer<RecordTemplateApplicationCompleted>
{
    public ValueTask<Result> AuthorizeAsync(IRequestContext<OpenTemplateVersionApplicationV4> context,
        CancellationToken ct) => Authorize(context);

    public ValueTask<Result> AuthorizeAsync(IRequestContext<RecordTemplateApplicationCompleted> context,
        CancellationToken ct) => Authorize(context);

    static ValueTask<Result> Authorize(IExecutionContext context) => ValueTask.FromResult(
        RequestActor.IsSystem(context.Actor) ? Result.Success : Result.Failure(new RequestError(
            RequestErrorKind.Forbidden, "Only system reactions may coordinate template applications.")));
}
