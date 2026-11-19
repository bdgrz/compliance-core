using Cntryl.Portia;
using Bdgrz.Compliance.Features.FirmServices.Models;
namespace Bdgrz.Compliance.Features.FirmServices.Commands;

/// <summary>Records an operator decision about one partner duty.</summary>
[Discriminator("bdgrz.firm-services.partner.designate", 1)]
public sealed record DesignatePartner(Uuid CommandId, Uuid UserId, string Duty, long ExpectedRevision, string Reason)
    : IRequest<PartnerDesignationView>, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
