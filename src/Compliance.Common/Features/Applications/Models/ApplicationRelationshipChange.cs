using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.Applications.Models;

public sealed record ApplicationRelationshipChange(Uuid RelationshipId,
    long RelationshipRevision, string State);
