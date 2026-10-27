using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.Applications.Models;

public sealed record ApplicationRelationshipRegistration(Uuid RelationshipId,
    long Revision, string Status);
