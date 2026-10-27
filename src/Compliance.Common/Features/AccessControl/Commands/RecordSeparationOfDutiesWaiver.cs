using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Commands;

[Discriminator("bdgrz.sod-waiver.record", 1)]
public sealed record RecordSeparationOfDutiesWaiver(
    Uuid TenantId,
    SeparationOfDutiesWaiverScope Scope,
    Uuid BeneficiaryUserId,
    string Rationale,
    DateTimeOffset ExpiresAt)
    : IRequest<SeparationOfDutiesWaiverView>, ISeparationOfDutiesWaiverAdminRequest, IClientManagementMutationRequest, ICallable;
