using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Workforce.Contracts;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce.Commands;

[Discriminator("bdgrz.workforce.service-identity.disable", 1)]
public sealed record DisableServiceIdentity(Uuid TenantId, Uuid ServiceIdentityId, long ExpectedRevision, string? Reason = null)
    : IRequest, IWorkforceRequest, IClientManagementMutationRequest, ICallable;
