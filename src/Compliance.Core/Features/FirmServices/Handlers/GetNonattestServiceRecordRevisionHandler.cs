using Cntryl.Portia;
using Bdgrz.Compliance.Features.FirmServices.Aggregates;
using Bdgrz.Compliance.Features.FirmServices.Models;
using Bdgrz.Compliance.Features.FirmServices.Queries;
using Bdgrz.Compliance.Features.FirmServices.Services;
namespace Bdgrz.Compliance.Features.FirmServices.Handlers;

/// <summary>Historical reads preserve original terms and always check current partner authority.</summary>
sealed class GetNonattestServiceRecordRevisionHandler(IAggregateReader reader, FirmServicesAuthority authority)
    : IRequestHandler<GetNonattestServiceRecordRevision, NonattestServiceRecordView>
{
    public async ValueTask<Result<NonattestServiceRecordView>> HandleAsync(
        IRequestContext<GetNonattestServiceRecordRevision> context, CancellationToken ct)
    {
        var actor = await authority.RequirePartnerAsync(context.Actor, "engagement_partner", ct).ConfigureAwait(false);
        if (!actor.IsSuccess)
            return Result<NonattestServiceRecordView>.Failure(actor.Error!);
        var request = context.Request;
        if (request.TenantId == Uuid.Empty || request.RecordId == Uuid.Empty || request.Revision <= 0)
            return Result<NonattestServiceRecordView>.Failure(new(RequestErrorKind.Validation, "A tenant, record and positive revision are required."));
        var owner = await reader.HydrateAsync(new NonattestServiceRecord(request.TenantId, request.RecordId,
            selectedRevision: request.Revision), ct).ConfigureAwait(false);
        return owner.SelectedRevisionView is { } view ? Result<NonattestServiceRecordView>.Success(view)
            : Result<NonattestServiceRecordView>.Failure(new(RequestErrorKind.NotFound, "The service revision was not found."));
    }
}
