using System.Globalization;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AuditPackages.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Programs.Readers;
namespace Bdgrz.Compliance.Features.AuditPackages.Authorizers;

/// <summary>Only internal process principals may dispatch effects; every handler also verifies its exact source.</summary>
sealed class AuditPackageReceiverAuthorizer : IRequestAuthorizer<IAuditPackageReceiver>
{
    public ValueTask<Result> AuthorizeAsync(IRequestContext<IAuditPackageReceiver> context, CancellationToken ct) =>
        ValueTask.FromResult(RequestActor.IsSystem(context.Actor) ? Result.Success : Result.Failure(new RequestError(
            RequestErrorKind.Forbidden, "Only the internal package process may dispatch package effects.")));
}
