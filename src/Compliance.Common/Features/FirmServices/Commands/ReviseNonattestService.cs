using Cntryl.Portia;
using Bdgrz.Compliance.Features.FirmServices.Models;
namespace Bdgrz.Compliance.Features.FirmServices.Commands;

/// <summary>Records one named nonattest service decision.</summary>
[Discriminator("bdgrz.firm-services.nonattest.revise", 1)]
public sealed record ReviseNonattestService(Uuid CommandId, Uuid TenantId, Uuid RecordId, long ExpectedRevision,
    NonattestServiceTerms Terms, string Reason) : IRequest<NonattestServiceRecordView>, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
