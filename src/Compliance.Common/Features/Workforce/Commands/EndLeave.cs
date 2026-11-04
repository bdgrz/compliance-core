using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Workforce.Contracts;

namespace Bdgrz.Compliance.Features.Workforce.Commands;

[Discriminator("bdgrz.workforce.work-relationship.leave.end", 1)]
public sealed record EndLeave(Uuid TenantId, Uuid RelationshipId, long ExpectedRevision,
    DateOnly ReturnDate)
    : IRequest, IWorkforceRequest, IClientManagementMutationRequest, ICallable;
