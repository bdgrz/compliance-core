using Cntryl.Portia;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Queries;
using Bdgrz.Compliance.Features.Remediation.Readers;
namespace Bdgrz.Compliance.Features.Remediation.Handlers;

public sealed class GetWaiverHandler(WaiverReader reader) : IRequestHandler<GetWaiver, WaiverView>
{
    public async ValueTask<Result<WaiverView>> HandleAsync(IRequestContext<GetWaiver> context, CancellationToken ct)
    {
        var view = await reader.ReadAsync(context.Request.TenantId, context.Request.WaiverId, ct).ConfigureAwait(false);
        return view is null
            ? Result<WaiverView>.Failure(new RequestError(RequestErrorKind.NotFound, "The waiver was not found."))
            : Result<WaiverView>.Success(view);
    }
}
