using Cntryl.Portia;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Commands;
namespace Bdgrz.Compliance.Features.Evidence.Handlers;

sealed class PrepareEvidenceRedactionHandler(EvidenceRedactionOperations operations)
    : IRequestHandler<PrepareEvidenceRedaction, EvidenceRedactionView>
{
    public ValueTask<Result<EvidenceRedactionView>> HandleAsync(IRequestContext<PrepareEvidenceRedaction> context, CancellationToken ct) =>
        operations.PrepareAsync(context, ct);
}
