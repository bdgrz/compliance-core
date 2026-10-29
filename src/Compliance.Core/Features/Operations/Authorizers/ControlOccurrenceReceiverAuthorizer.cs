using Cntryl.Portia;
using Bdgrz.Compliance.Features.Operations.Contracts;
using Bdgrz.Compliance.Features.Programs.Readers;
namespace Bdgrz.Compliance.Features.Operations.Authorizers;

/// <summary>Only the trusted process worker may dispatch control occurrence receivers.</summary>
sealed class ControlOccurrenceReceiverAuthorizer : IRequestAuthorizer<IControlOccurrenceReceiver>
{
    public ValueTask<Result> AuthorizeAsync(IRequestContext<IControlOccurrenceReceiver> context, CancellationToken ct) =>
        ValueTask.FromResult(RequestActor.IsSystem(context.Actor)
            ? Result.Success
            : Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Only the internal control occurrence fan-out may dispatch occurrence receivers.")));
}
