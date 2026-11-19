using Cntryl.Portia;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Queries;
using Bdgrz.Compliance.Features.Evidence.Services;

namespace Bdgrz.Compliance.Features.Evidence.Handlers;

sealed class ListEvidenceSupportsHandler(EvidenceSupportStatusRead read)
    : IRequestHandler<ListEvidenceSupports, Page<EvidenceSupportStatusView>>
{
    public ValueTask<Result<Page<EvidenceSupportStatusView>>> HandleAsync(IRequestContext<ListEvidenceSupports> context,
        CancellationToken ct) => read.ListAsync(context, context.Request, ct);
}
