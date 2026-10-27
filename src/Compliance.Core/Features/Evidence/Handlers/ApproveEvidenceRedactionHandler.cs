using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evidence;

sealed class ApproveEvidenceRedactionHandler(EvidenceRedactionOperations operations)
    : IRequestHandler<ApproveEvidenceRedaction, EvidenceRedactionView>
{
    public ValueTask<Result<EvidenceRedactionView>> HandleAsync(IRequestContext<ApproveEvidenceRedaction> context, CancellationToken ct) =>
        operations.ApproveAsync(context, ct);
}
