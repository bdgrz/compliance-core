using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Changes.Models;

/// <summary>A compliance-owned change fact; external provenance is retained only when supplied.</summary>
public sealed record SignificantChangeContent(
    string? SourceSystem,
    string? SourceIdentifier,
    string? SourceUrl,
    string Title,
    string Description,
    DateOnly EffectiveOn,
    DateOnly? EffectiveThrough,
    Uuid OwnerMemberId,
    IReadOnlyList<Uuid> AffectedSystemIds,
    string Severity,
    string HandlingClass,
    IReadOnlyList<Uuid> EvidenceArtifactIds);
