using Cntryl.Portia;
using Bdgrz.Compliance.Features.FirmServices.Models;
namespace Bdgrz.Compliance.Features.FirmServices.Queries;

/// <summary>Reads one immutable historical service revision while retaining current authorization.</summary>
[Discriminator("bdgrz.firm-services.nonattest.get-revision", 1)]
public sealed record GetNonattestServiceRecordRevision(Uuid TenantId, Uuid RecordId, long Revision)
    : IRequest<NonattestServiceRecordView>, ICallable;
