using Cntryl.Portia;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.Tenants.Contracts;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
namespace Bdgrz.Compliance.Features.Tenants.Authorizers;

/// <summary>Allows tenant lifecycle commands only from Portia's trusted system actor.</summary>
sealed class TenantLifecycleReactionAuthorizer : IRequestAuthorizer<ITenantLifecycleReactionRequest>
{
    public ValueTask<Result> AuthorizeAsync(
        IRequestContext<ITenantLifecycleReactionRequest> context,
        CancellationToken ct) =>
        ValueTask.FromResult(RequestActor.IsSystem(context.Actor)
            ? Result.Success
            : Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Only Portia's trusted system actor may dispatch this request.")));
}
