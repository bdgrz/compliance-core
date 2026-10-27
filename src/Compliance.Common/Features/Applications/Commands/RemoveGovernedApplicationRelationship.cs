using Cntryl.Portia;
using Bdgrz.Compliance.Features.Applications.Contracts;
namespace Bdgrz.Compliance.Features.Applications.Commands;

[Discriminator("bdgrz.application.relationship.remove-governed", 1)]
public sealed record RemoveGovernedApplicationRelationship(Uuid TenantId,
    Uuid SourceApplicationId, Uuid TargetApplicationId, string RelationshipType,
    long ExpectedRelationshipRevision, string Reason)
    : IRequest, IApplicationInventoryWriteRequest, ICallable;
