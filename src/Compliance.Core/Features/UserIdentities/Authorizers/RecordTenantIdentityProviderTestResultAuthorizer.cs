using System.Security.Claims;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

sealed class RecordTenantIdentityProviderTestResultAuthorizer
    : IRequestAuthorizer<RecordTenantIdentityProviderTestResult>
{
    internal const string ProcessId = "reactor:TenantIdentityProviderTestV1";

    internal static bool IsNamedWorker(IRequestContext context)
    {
        if (!RequestActor.IsSystem(context.Actor) || context.Invocation is not DirectInvocation)
            return false;
        var subjects = context.Actor.FindAll(ClaimTypes.NameIdentifier).ToArray();
        return subjects.Length == 1 && subjects[0].Issuer == "bdgrz.system" &&
            subjects[0].Value == ProcessId;
    }

    public ValueTask<Result> AuthorizeAsync(
        IRequestContext<RecordTenantIdentityProviderTestResult> context, CancellationToken ct) =>
        ValueTask.FromResult(IsNamedWorker(context)
            ? Result.Success
            : Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Only the named identity-provider test worker may record provider results.")));
}
