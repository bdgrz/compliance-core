using Cntryl.Portia;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Commands;
namespace Bdgrz.Compliance.Features.Evidence.Handlers;

sealed class ApproveEvidenceRedactionHandler(EvidenceRedactionOperations operations)
    : IRequestHandler<ApproveEvidenceRedaction, EvidenceRedactionView>
{
    public ValueTask<Result<EvidenceRedactionView>> HandleAsync(IRequestContext<ApproveEvidenceRedaction> context, CancellationToken ct) =>
        operations.ApproveAsync(context, ct);
}
