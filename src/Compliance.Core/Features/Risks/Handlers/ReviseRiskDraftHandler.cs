using Cntryl.Portia;
using Bdgrz.Compliance.Features.Risks.Services;
using Bdgrz.Compliance.Features.Risks.Commands;
namespace Bdgrz.Compliance.Features.Risks.Handlers;

public sealed class ReviseRiskDraftHandler(RiskDraftOperations operations) : IRequestHandler<ReviseRiskDraft>
{
    public ValueTask<Result> HandleAsync(IRequestContext<ReviseRiskDraft> context, CancellationToken ct) => operations.ReviseAsync(context, ct);
}
