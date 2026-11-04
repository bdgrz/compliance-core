using Cntryl.Portia;
using Bdgrz.Compliance.Features.FirmServices.Models;
namespace Bdgrz.Compliance.Features.FirmServices.Commands;

/// <summary>Records one named nonattest service decision.</summary>
[Discriminator("bdgrz.firm-services.nonattest.record", 1)]
public sealed record RecordNonattestService(Uuid CommandId, Uuid TenantId, Uuid RecordId, long ExpectedRevision,
    NonattestServiceTerms Terms) : IRequest<NonattestServiceRecordView>, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
