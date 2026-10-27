using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Contracts;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
namespace Bdgrz.Compliance.Features.AuditorRequests.Authorizers;

/// <summary>Only the trusted process worker may dispatch the request resolution and child materialization receivers.</summary>
sealed class AuditorRequestReceiverAuthorizer : IRequestAuthorizer<IAuditorRequestReceiver>
{
    public ValueTask<Result> AuthorizeAsync(IRequestContext<IAuditorRequestReceiver> context, CancellationToken ct) =>
        ValueTask.FromResult(RequestActor.IsSystem(context.Actor)
            ? Result.Success
            : Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Only the internal auditor request processes may dispatch auditor request receivers.")));
}
