using Cntryl.Portia;
using Bdgrz.Compliance.Features.Responsibilities.Models;
namespace Bdgrz.Compliance.Features.Work.Models;

public sealed record CommitmentDecisionWorkState(Uuid TenantId, Uuid ProgramId, Uuid DraftId,
    string Identifier, long SourceRevision, long? LatestEffectiveRevision,
    Uuid? AcceptedReviewDecisionId, Uuid? AcceptedReviewerMemberId,
    Uuid[] PendingAuthorMemberIds, DateTimeOffset DraftChangedAt,
    ResponsibilityAssignmentView[] Assignments);
