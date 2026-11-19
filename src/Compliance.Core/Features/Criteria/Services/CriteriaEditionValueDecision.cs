using Bdgrz.Compliance.Features.Criteria.Models;

namespace Bdgrz.Compliance.Features.Criteria.Services;

sealed record CriteriaEditionValueDecision(bool Accepted, string? Reason, CriteriaEditionValueDecisionEvidence Evidence);
