using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evidence;

sealed class PrepareEvidenceRedactionHandler(EvidenceRedactionOperations operations)
    : IRequestHandler<PrepareEvidenceRedaction, EvidenceRedactionView>
{
    public ValueTask<Result<EvidenceRedactionView>> HandleAsync(IRequestContext<PrepareEvidenceRedaction> context, CancellationToken ct) =>
        operations.PrepareAsync(context, ct);
}
