using Bdgrz.Compliance.Features.Criteria.Events;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Services;

/// <summary>Exact claim-owned refusal and its committed edition request.</summary>
sealed record CriteriaLabelRefusalEvidence(EditionLabelReservationRefused Refusal, DomainEvent Request);
