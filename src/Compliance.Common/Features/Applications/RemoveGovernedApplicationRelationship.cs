using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

[Discriminator("bdgrz.application.relationship.remove-governed", 1)]
public sealed record RemoveGovernedApplicationRelationship(Uuid TenantId,
    Uuid SourceApplicationId, Uuid TargetApplicationId, string RelationshipType,
    long ExpectedRelationshipRevision, string Reason)
    : IRequest, IApplicationInventoryWriteRequest, ICallable;
