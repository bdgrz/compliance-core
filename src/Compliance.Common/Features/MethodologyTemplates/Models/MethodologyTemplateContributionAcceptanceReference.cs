using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Models;

/// <summary>Platform-safe provenance for accepted content, excluding the contributing tenant and source record.</summary>
public sealed record MethodologyTemplateContributionAcceptanceReference(
    Uuid AcceptanceId,
    string ContentSha256,
    Uuid ReviewedByMemberId,
    string ReviewedByDisplay,
    DateTimeOffset ReviewedAt,
    Uuid AcceptedByMemberId,
    string AcceptedByDisplay,
    string AcceptanceRationale,
    DateTimeOffset AcceptedAt);
