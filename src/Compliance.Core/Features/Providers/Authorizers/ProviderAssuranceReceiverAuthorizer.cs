using Cntryl.Portia;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Contracts;
namespace Bdgrz.Compliance.Features.Providers.Authorizers;

/// <summary>Only the trusted process worker may dispatch provider assurance receivers.</summary>
sealed class ProviderAssuranceReceiverAuthorizer : IRequestAuthorizer<IProviderAssuranceReceiver>
{
    public ValueTask<Result> AuthorizeAsync(IRequestContext<IProviderAssuranceReceiver> context, CancellationToken ct) =>
        ValueTask.FromResult(RequestActor.IsSystem(context.Actor)
            ? Result.Success
            : Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Only the internal provider assurance processes may dispatch their receivers.")));
}
