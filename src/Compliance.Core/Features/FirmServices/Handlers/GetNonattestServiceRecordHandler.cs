using Cntryl.Portia;
using Bdgrz.Compliance.Features.FirmServices.Aggregates;
using Bdgrz.Compliance.Features.FirmServices.Models;
using Bdgrz.Compliance.Features.FirmServices.Queries;
using Bdgrz.Compliance.Features.FirmServices.Services;
namespace Bdgrz.Compliance.Features.FirmServices.Handlers;

/// <summary>Returns authoritative current record state, including retained voids.</summary>
sealed class GetNonattestServiceRecordHandler(IAggregateReader reader, FirmServicesAuthority authority)
    : IRequestHandler<GetNonattestServiceRecord, NonattestServiceRecordView>
{
    public async ValueTask<Result<NonattestServiceRecordView>> HandleAsync(
        IRequestContext<GetNonattestServiceRecord> context, CancellationToken ct)
    {
        var actor = await authority.RequirePartnerAsync(context.Actor, "engagement_partner", ct).ConfigureAwait(false);
        if (!actor.IsSuccess)
            return Result<NonattestServiceRecordView>.Failure(actor.Error!);
        var request = context.Request;
        if (request.TenantId == Uuid.Empty || request.RecordId == Uuid.Empty)
            return Result<NonattestServiceRecordView>.Failure(new(RequestErrorKind.Validation, "The owning tenant and record are required."));
        var owner = await reader.HydrateAsync(new NonattestServiceRecord(request.TenantId, request.RecordId), ct)
            .ConfigureAwait(false);
        return owner.View is { } view ? Result<NonattestServiceRecordView>.Success(view)
            : Result<NonattestServiceRecordView>.Failure(new(RequestErrorKind.NotFound, "The service record was not found."));
    }
}
