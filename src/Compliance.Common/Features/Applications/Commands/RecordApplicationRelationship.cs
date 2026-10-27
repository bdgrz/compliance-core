using Cntryl.Portia;
using Bdgrz.Compliance.Features.Applications.Contracts;
using Bdgrz.Compliance.Features.Applications.Models;
namespace Bdgrz.Compliance.Features.Applications.Commands;

[Discriminator("bdgrz.application.relationship.record", 1)]
public sealed record RecordApplicationRelationship(Uuid TenantId, Uuid SourceApplicationId,
    Uuid TargetApplicationId, string RelationshipType)
    : IRequest<ApplicationRelationshipRegistration>, IApplicationInventoryWriteRequest, ICallable;
