using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Features.Applications.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications.Commands;

[Discriminator("bdgrz.application.relationship.remove", 1)]
public sealed record RemoveApplicationRelationship(Uuid TenantId, Uuid ApplicationId,
    Uuid RelationshipId, long ExpectedRelationshipRevision)
    : IRequest<ApplicationRelationshipChange>, IApplicationInventoryWriteRequest, ICallable;
