using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

static class ApplicationIncomingRelationshipSchema
{
    public static readonly KvDirectoryIndex<ApplicationRelationshipView> ByTargetApplication =
        new("by_active_target_application", 1, static relationship =>
            relationship.Status is "active" or "proposed" or "approved"
                ? [relationship.TargetApplicationId.ToString(),
                    relationship.SourceApplicationId.ToString(),
                    relationship.RelationshipId.ToString()]
                : []);

    public static readonly KvDirectory<ApplicationRelationshipView, Uuid> Relationships = new(
        "active_application_incoming_relationships",
        ComplianceCoreJsonContext.Default.ApplicationRelationshipView,
        static relationship => relationship.RelationshipId,
        static id => [id.ToString()], [ByTargetApplication]);
}
