using Bdgrz.Compliance.Features.Retention.Commands;
using System.Security.Claims;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Retention.Authorizers;

sealed class RecordArtifactDispositionResultAuthorizer
    : IRequestAuthorizer<RecordArtifactDispositionResult>
{
    internal const string ProcessId = "reactor:ArtifactDispositionExecutionV1";

    internal static bool IsNamedWorker(IRequestContext context)
    {
        if (!RequestActor.IsSystem(context.Actor) || context.Invocation is not DirectInvocation)
            return false;
        var subjects = context.Actor.FindAll(ClaimTypes.NameIdentifier).ToArray();
        return subjects.Length == 1 && subjects[0].Issuer == "bdgrz.system" &&
            subjects[0].Value == ProcessId;
    }

    public ValueTask<Result> AuthorizeAsync(
        IRequestContext<RecordArtifactDispositionResult> context, CancellationToken ct) =>
        ValueTask.FromResult(IsNamedWorker(context)
            ? Result.Success
            : Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Only the named artifact disposition worker may record storage results.")));
}
