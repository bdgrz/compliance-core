using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Workforce.Contracts;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce.Commands;

[Discriminator("bdgrz.workforce.service-identity.enable", 1)]
public sealed record EnableServiceIdentity(Uuid TenantId, Uuid ServiceIdentityId, long ExpectedRevision)
    : IRequest, IWorkforceRequest, IClientManagementMutationRequest, ICallable;
