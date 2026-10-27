using Bdgrz.Compliance.Features.Evidence;
using Bdgrz.Compliance.Features.Evidence.Commands;
using Bdgrz.Compliance.Features.Evidence.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evidence.Handlers;

sealed class ApproveEvidenceRedactionHandler(EvidenceRedactionOperations operations)
    : IRequestHandler<ApproveEvidenceRedaction, EvidenceRedactionView>
{
    public ValueTask<Result<EvidenceRedactionView>> HandleAsync(IRequestContext<ApproveEvidenceRedaction> context, CancellationToken ct) =>
        operations.ApproveAsync(context, ct);
}
