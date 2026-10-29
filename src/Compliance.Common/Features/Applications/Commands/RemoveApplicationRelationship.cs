using Cntryl.Portia;
using Bdgrz.Compliance.Features.Applications.Contracts;
using Bdgrz.Compliance.Features.Applications.Models;
namespace Bdgrz.Compliance.Features.Applications.Commands;

[Discriminator("bdgrz.application.relationship.remove", 1)]
public sealed record RemoveApplicationRelationship(Uuid TenantId, Uuid ApplicationId,
    Uuid RelationshipId, long ExpectedRelationshipRevision)
    : IRequest<ApplicationRelationshipChange>, IApplicationInventoryWriteRequest, ICallable;
