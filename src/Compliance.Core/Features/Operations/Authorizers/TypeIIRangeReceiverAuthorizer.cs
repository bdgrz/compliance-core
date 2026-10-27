using Cntryl.Portia;
using Bdgrz.Compliance.Features.Operations.Contracts;
using Bdgrz.Compliance.Features.Programs.Readers;
namespace Bdgrz.Compliance.Features.Operations.Authorizers;

/// <summary>Only the trusted process worker may dispatch Type II range receivers.</summary>
sealed class TypeIIRangeReceiverAuthorizer : IRequestAuthorizer<ITypeIIRangeReceiver>
{
    public ValueTask<Result> AuthorizeAsync(IRequestContext<ITypeIIRangeReceiver> context, CancellationToken ct) =>
        ValueTask.FromResult(RequestActor.IsSystem(context.Actor)
            ? Result.Success
            : Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Only the internal Type II range process may dispatch range receivers.")));
}
