using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Workforce.Contracts;

namespace Bdgrz.Compliance.Features.Workforce.Commands;

[Discriminator("bdgrz.workforce.work-relationship.leave.start", 1)]
public sealed record StartLeave(Uuid TenantId, Uuid RelationshipId, long ExpectedRevision,
    DateOnly FromDate)
    : IRequest, IWorkforceRequest, IClientManagementMutationRequest, ICallable;
