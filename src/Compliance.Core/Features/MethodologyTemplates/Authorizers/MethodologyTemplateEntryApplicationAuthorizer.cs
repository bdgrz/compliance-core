using Cntryl.Portia;
using Bdgrz.Compliance.Features.MethodologyTemplates.Commands;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Authorizers;

/// <summary>Only internal reactions may realize a committed entry; its handler verifies the exact source.</summary>
public sealed class MethodologyTemplateEntryApplicationAuthorizer : IRequestAuthorizer<ApplyMethodologyTemplateEntry>
{
    public ValueTask<Result> AuthorizeAsync(IRequestContext<ApplyMethodologyTemplateEntry> context, CancellationToken ct) =>
        ValueTask.FromResult(RequestActor.IsSystem(context.Actor) ? Result.Success : Result.Failure(new RequestError(
            RequestErrorKind.Forbidden, "Only a committed methodology application reaction may realize an entry.")));
}
