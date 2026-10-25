using Bdgrz.Compliance.Features.Proofs;

namespace Bdgrz.Compliance.Features.Incidents;

/// <summary>One exact changed business component and the immutable before/after current-authority commitments.</summary>
public sealed record ComplianceIncidentMutation(string Kind, string? BeforeSha256, string AfterSha256, ImmutableContentRef Payload);
