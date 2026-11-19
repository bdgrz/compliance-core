using Bdgrz.Compliance.Features.Criteria.Models;

namespace Bdgrz.Compliance.Features.Criteria.Services;

sealed record CriteriaEntryDecision(bool Accepted, string? Reason, CriteriaEntryDecisionEvidence Evidence);
