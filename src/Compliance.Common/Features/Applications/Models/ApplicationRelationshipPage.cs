using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications.Models;

public sealed record ApplicationRelationshipPage(long RelationshipRevision,
    IReadOnlyList<ApplicationRelationshipView> Items, string? NextCursor);
