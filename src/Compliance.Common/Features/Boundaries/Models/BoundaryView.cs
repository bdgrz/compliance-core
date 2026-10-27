using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Boundaries.Models;

public sealed record BoundaryView(Uuid TenantId, Uuid BoundaryId, Uuid ProgramId,
    BoundaryVersionView? Draft, BoundaryVersionView? LatestApprovedVersion,
    BoundaryDecisionView? LatestDecision, long Revision = 0);
