using Cntryl.Portia;
using Bdgrz.Compliance.Features.Responsibilities.Models;
namespace Bdgrz.Compliance.Features.Work.Models;

public sealed record BoundaryDecisionWorkState(Uuid TenantId, Uuid ProgramId,
    Uuid BoundaryId, long SourceRevision, Uuid DraftVersionId, long DraftRevision,
    Uuid DraftAuthorMemberId, DateTimeOffset DraftChangedAt, string? LatestReviewOutcome,
    ResponsibilityAssignmentView[] Assignments);
