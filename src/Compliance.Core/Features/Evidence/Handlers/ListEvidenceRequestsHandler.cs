using Bdgrz.Compliance.Features.Evidence.Aggregates;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Queries;
using Bdgrz.Compliance.Features.Evidence.Readers;
using Bdgrz.Compliance.Features.Controls;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evidence.Handlers;

/// <summary>Lists the program's evidence requests in the order opened, composed from their own owner streams.</summary>
public sealed class ListEvidenceRequestsHandler(EvidenceRequestReader requests)
    : IRequestHandler<ListEvidenceRequests, Page<EvidenceRequestView>>
{
    public async ValueTask<Result<Page<EvidenceRequestView>>> HandleAsync(
        IRequestContext<ListEvidenceRequests> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.Status is not (null or EvidenceRequest.Open or EvidenceRequest.Fulfilled or
            EvidenceRequest.Cancelled))
            return Result<Page<EvidenceRequestView>>.Failure(new RequestError(RequestErrorKind.Validation,
                "The status filter must be open, fulfilled, or cancelled."));
        var snapshot = await requests.ReadAsync(request.TenantId, request.ProgramId, ct).ConfigureAwait(false);
        var items = snapshot.ReadAll().Where(item => request.Status is null || item.Status == request.Status)
            .ToArray();
        return ControlActivationSource.Paginate(items, request.Limit, request.Cursor, "evidence requests");
    }
}
