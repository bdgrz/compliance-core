namespace Bdgrz.Compliance.Features.Criteria.Services;

/// <summary>A seed's durable readiness and the individual step awaiting recovery.</summary>
sealed record CriteriaSeedBootstrapResult(CriteriaSeedWriteStatus Status, string Step);
