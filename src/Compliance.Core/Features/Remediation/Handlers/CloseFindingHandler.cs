using Cntryl.Portia;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Remediation.Commands;
namespace Bdgrz.Compliance.Features.Remediation.Handlers;

/// <summary>Refuses new one-step closure decisions; historical facts and accepted process completion remain decodable.</summary>
public sealed class CloseFindingHandler : IRequestHandler<CloseFinding, OperationAcceptedResponse>
{
    public ValueTask<Result<OperationAcceptedResponse>> HandleAsync(IRequestContext<CloseFinding> context, CancellationToken ct) =>
        ValueTask.FromResult(Result<OperationAcceptedResponse>.Failure(new RequestError(RequestErrorKind.Validation,
            "One-step finding closure is retired; use ProposeFindingClosure and ReviewFindingClosure.")));
}
