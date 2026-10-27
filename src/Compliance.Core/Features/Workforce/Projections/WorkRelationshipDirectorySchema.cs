using Cntryl.Portia;
using Bdgrz.Compliance.Features.Work.Commands;
using Bdgrz.Compliance.Features.Workforce.Models;
namespace Bdgrz.Compliance.Features.Workforce.Projections;

static class WorkRelationshipDirectorySchema
{
    public static readonly KvDirectoryIndex<WorkRelationshipView> BySourceWorkerId = new(
        "by_source_worker_id", 1,
        static view => [view.SourceWorkerId, view.RelationshipId.ToString()]);

    public static readonly KvDirectory<WorkRelationshipView, Uuid> Relationships = new(
        "work-relationships", ComplianceCoreJsonContext.Default.WorkRelationshipView,
        static view => view.RelationshipId,
        static relationshipId => [relationshipId.ToString()], [BySourceWorkerId]);
}
