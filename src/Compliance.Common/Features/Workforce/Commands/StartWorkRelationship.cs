using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Workforce.Contracts;

namespace Bdgrz.Compliance.Features.Workforce.Commands;

[Discriminator("bdgrz.workforce.work-relationship.start", 1)]
public sealed record StartWorkRelationship(Uuid TenantId, Uuid RelationshipId, long ExpectedRevision,
    DateOnly StartDate)
    : IRequest, IWorkforceRequest, IClientManagementMutationRequest, ICallable;
