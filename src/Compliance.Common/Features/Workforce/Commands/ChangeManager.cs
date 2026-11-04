using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Workforce.Contracts;

namespace Bdgrz.Compliance.Features.Workforce.Commands;

[Discriminator("bdgrz.workforce.work-relationship.manager.change", 1)]
public sealed record ChangeManager(Uuid TenantId, Uuid RelationshipId, long ExpectedRevision,
    Uuid? ManagerPersonId)
    : IRequest, IWorkforceRequest, IClientManagementMutationRequest, ICallable;
