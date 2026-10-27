using Bdgrz.Compliance.Features.Workforce;
using Bdgrz.Compliance.Features.Workforce.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce.Queries;

[Discriminator("bdgrz.workforce.work-relationship.get", 1)]
public sealed record GetWorkRelationship(Uuid TenantId, Uuid RelationshipId,
    long? MinimumRevision = null) : IRequest<WorkRelationshipView>, IWorkforceRequest, ICallable;
