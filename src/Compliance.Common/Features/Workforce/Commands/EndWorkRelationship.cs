using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Workforce.Contracts;

namespace Bdgrz.Compliance.Features.Workforce.Commands;

[Discriminator("bdgrz.workforce.work-relationship.end", 1)]
public sealed record EndWorkRelationship(Uuid TenantId, Uuid RelationshipId, long ExpectedRevision,
    DateOnly EndDate, string? Reason = null)
    : IRequest, IWorkforceRequest, IClientManagementMutationRequest, ICallable;
