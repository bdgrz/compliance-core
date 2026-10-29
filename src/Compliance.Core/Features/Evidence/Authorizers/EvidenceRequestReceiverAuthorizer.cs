using Cntryl.Portia;
using Bdgrz.Compliance.Features.Evidence.Contracts;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
namespace Bdgrz.Compliance.Features.Evidence.Authorizers;

/// <summary>Only the trusted process worker may dispatch the risk action completion review receivers on evidence requests.</summary>
sealed class EvidenceRequestReceiverAuthorizer : IRequestAuthorizer<IEvidenceRequestReceiver>
{
    public ValueTask<Result> AuthorizeAsync(IRequestContext<IEvidenceRequestReceiver> context, CancellationToken ct) =>
        ValueTask.FromResult(RequestActor.IsSystem(context.Actor)
            ? Result.Success
            : Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Only the internal risk action review process may dispatch evidence request receivers.")));
}
