using Cntryl.Portia;
using Bdgrz.Compliance.Features.MethodologyTemplates.Commands;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Authorizers;

/// <summary>Entry application outcomes are recorded only by internal reactions.</summary>
public sealed class TemplateVersionApplicationAuthorizer : IRequestAuthorizer<OpenTemplateVersionApplication>,
    IRequestAuthorizer<RecordTemplateEntryOutcome>
{
    public ValueTask<Result> AuthorizeAsync(IRequestContext<OpenTemplateVersionApplication> context,
        CancellationToken ct) => Authorize(context);

    public ValueTask<Result> AuthorizeAsync(IRequestContext<RecordTemplateEntryOutcome> context,
        CancellationToken ct) => Authorize(context);

    static ValueTask<Result> Authorize(IExecutionContext context) => ValueTask.FromResult(
        RequestActor.IsSystem(context.Actor) ? Result.Success : Result.Failure(new RequestError(
            RequestErrorKind.Forbidden, "Only system reactions may record template entry application outcomes.")));
}
