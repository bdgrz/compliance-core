using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

public sealed class ReviseRiskDraftHandler(RiskDraftOperations operations) : IRequestHandler<ReviseRiskDraft>
{
    public ValueTask<Result> HandleAsync(IRequestContext<ReviseRiskDraft> context, CancellationToken ct) => operations.ReviseAsync(context, ct);
}
