using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Workforce.Contracts;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce.Commands;

[Discriminator("bdgrz.workforce.service-identity.owner.change", 1)]
public sealed record ChangeServiceIdentityOwner(Uuid TenantId, Uuid ServiceIdentityId, long ExpectedRevision, string OwnerKind, Uuid OwnerId)
    : IRequest, IWorkforceRequest, IClientManagementMutationRequest, ICallable;
