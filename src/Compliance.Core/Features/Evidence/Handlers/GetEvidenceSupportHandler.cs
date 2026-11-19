using Cntryl.Portia;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Queries;
using Bdgrz.Compliance.Features.Evidence.Services;

namespace Bdgrz.Compliance.Features.Evidence.Handlers;

sealed class GetEvidenceSupportHandler(EvidenceSupportStatusRead read)
    : IRequestHandler<GetEvidenceSupport, EvidenceSupportStatusView>
{
    public ValueTask<Result<EvidenceSupportStatusView>> HandleAsync(IRequestContext<GetEvidenceSupport> context,
        CancellationToken ct) => read.GetAsync(context, context.Request.TenantId, context.Request.ProgramId,
            context.Request.EvidenceSupportId, ct);
}
