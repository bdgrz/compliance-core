using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications.Models;

public sealed record ApplicationIncomingRelationshipView(Uuid SourceApplicationId,
    Uuid RelationshipId, string RelationshipType, Uuid ActorMemberId, string ActorDisplay,
    DateTimeOffset ChangedAt);
