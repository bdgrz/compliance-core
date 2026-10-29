using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
namespace Bdgrz.Compliance.Features.AccessControl.Queries;

[Discriminator("bdgrz.sod-waiver.get", 1)]
public sealed record GetSeparationOfDutiesWaiver(Uuid TenantId, Uuid WaiverId)
    : IRequest<SeparationOfDutiesWaiverView>, ISeparationOfDutiesWaiverAdminRequest, ICallable;
