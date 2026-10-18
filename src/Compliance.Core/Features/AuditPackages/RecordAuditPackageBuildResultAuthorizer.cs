using System.Security.Claims;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPackages;

sealed class RecordAuditPackageBuildResultAuthorizer
    : IRequestAuthorizer<RecordAuditPackageBuildResult>
{
    internal const string ProcessId = "reactor:AuditPackageBuildExecutionV1";

    internal static bool IsNamedWorker(IRequestContext context)
    {
        if (!RequestActor.IsSystem(context.Actor) || context.Invocation is not DirectInvocation)
            return false;
        var subjects = context.Actor.FindAll(ClaimTypes.NameIdentifier).ToArray();
        return subjects.Length == 1 && subjects[0].Issuer == "bdgrz.system" &&
            subjects[0].Value == ProcessId;
    }

    public ValueTask<Result> AuthorizeAsync(
        IRequestContext<RecordAuditPackageBuildResult> context, CancellationToken ct) =>
        ValueTask.FromResult(IsNamedWorker(context)
            ? Result.Success
            : Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Only the named audit package build worker may record build results.")));
}
