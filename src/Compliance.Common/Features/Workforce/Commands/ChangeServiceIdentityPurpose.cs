using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Workforce.Contracts;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce.Commands;

[Discriminator("bdgrz.workforce.service-identity.purpose.change", 1)]
public sealed record ChangeServiceIdentityPurpose(Uuid TenantId, Uuid ServiceIdentityId, long ExpectedRevision, string Purpose)
    : IRequest, IWorkforceRequest, IClientManagementMutationRequest, ICallable;
