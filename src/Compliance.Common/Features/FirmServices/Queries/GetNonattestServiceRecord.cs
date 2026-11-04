using Cntryl.Portia;
using Bdgrz.Compliance.Features.FirmServices.Models;
namespace Bdgrz.Compliance.Features.FirmServices.Queries;

/// <summary>Reads the current state without discarding voided history.</summary>
[Discriminator("bdgrz.firm-services.nonattest.get", 1)]
public sealed record GetNonattestServiceRecord(Uuid TenantId, Uuid RecordId)
    : IRequest<NonattestServiceRecordView>, ICallable;
