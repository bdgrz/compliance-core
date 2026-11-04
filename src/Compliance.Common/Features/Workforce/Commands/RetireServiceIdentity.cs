using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Workforce.Contracts;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce.Commands;

[Discriminator("bdgrz.workforce.service-identity.retire", 1)]
public sealed record RetireServiceIdentity(Uuid TenantId, Uuid ServiceIdentityId, long ExpectedRevision, string? Reason = null)
    : IRequest, IWorkforceRequest, IClientManagementMutationRequest, ICallable;
