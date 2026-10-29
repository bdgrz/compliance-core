using Cntryl.Portia;
using Bdgrz.Compliance.Features.Workforce.Contracts;
using Bdgrz.Compliance.Features.Work.Commands;
using Bdgrz.Compliance.Features.Workforce.Models;
namespace Bdgrz.Compliance.Features.Workforce.Queries;

[Discriminator("bdgrz.workforce.work-relationship.get", 1)]
public sealed record GetWorkRelationship(Uuid TenantId, Uuid RelationshipId,
    long? MinimumRevision = null) : IRequest<WorkRelationshipView>, IWorkforceRequest, ICallable;
