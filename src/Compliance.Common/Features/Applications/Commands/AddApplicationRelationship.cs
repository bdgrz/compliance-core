using Cntryl.Portia;
using Bdgrz.Compliance.Features.Applications.Contracts;
using Bdgrz.Compliance.Features.Applications.Models;
namespace Bdgrz.Compliance.Features.Applications.Commands;

[Discriminator("bdgrz.application.relationship.add", 1)]
public sealed record AddApplicationRelationship(Uuid TenantId, Uuid ApplicationId,
    Uuid RelationshipId, long ExpectedRelationshipRevision, Uuid TargetApplicationId,
    string RelationshipType) : IRequest<ApplicationRelationshipChange>,
    IApplicationInventoryWriteRequest, ICallable;
