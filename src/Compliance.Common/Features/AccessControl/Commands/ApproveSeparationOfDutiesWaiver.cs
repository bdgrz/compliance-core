using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
namespace Bdgrz.Compliance.Features.AccessControl.Commands;

[Discriminator("bdgrz.sod-waiver.approve", 1)]
public sealed record ApproveSeparationOfDutiesWaiver(Uuid TenantId, Uuid WaiverId)
    : IRequest<SeparationOfDutiesWaiverView>, ISeparationOfDutiesWaiverAdminRequest, IClientManagementMutationRequest, ICallable;
