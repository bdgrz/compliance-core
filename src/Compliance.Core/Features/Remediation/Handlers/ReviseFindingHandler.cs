using Cntryl.Portia;
using Bdgrz.Compliance.Features.Remediation.Commands;
using Bdgrz.Compliance.Features.Remediation.Models;
namespace Bdgrz.Compliance.Features.Remediation.Handlers;

/// <summary>Refuses the retired generic writer while keeping its historical request contract decodable.</summary>
public sealed class ReviseFindingHandler : IRequestHandler<ReviseFinding, FindingView>
{
    public ValueTask<Result<FindingView>> HandleAsync(IRequestContext<ReviseFinding> context, CancellationToken ct) =>
        ValueTask.FromResult(Result<FindingView>.Failure(new RequestError(RequestErrorKind.Validation,
            "Generic finding revision is retired; use ReviseFindingAnalysis and the named ownership commands.")));
}
