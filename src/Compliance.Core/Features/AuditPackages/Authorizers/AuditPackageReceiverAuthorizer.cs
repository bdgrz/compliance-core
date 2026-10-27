using Bdgrz.Compliance.Features.AuditPackages;
using System.Globalization;
using Bdgrz.Compliance.Features.Artifacts;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPackages.Authorizers;

/// <summary>Only internal process principals may dispatch effects; every handler also verifies its exact source.</summary>
sealed class AuditPackageReceiverAuthorizer : IRequestAuthorizer<IAuditPackageReceiver>
{
    public ValueTask<Result> AuthorizeAsync(IRequestContext<IAuditPackageReceiver> context, CancellationToken ct) =>
        ValueTask.FromResult(RequestActor.IsSystem(context.Actor) ? Result.Success : Result.Failure(new RequestError(
            RequestErrorKind.Forbidden, "Only the internal package process may dispatch package effects.")));
}
