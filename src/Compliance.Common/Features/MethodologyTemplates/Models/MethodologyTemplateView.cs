using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Models;

/// <summary>Platform-owned template chronology; contains no tenant identifiers or client records.</summary>
public sealed record MethodologyTemplateView(
    Uuid TemplateId,
    string Status,
    long Revision,
    IReadOnlyList<MethodologyTemplateVersionView> Versions,
    string? RetirementRationale,
    DateOnly? RetiredEffectiveOn,
    Uuid ActorMemberId,
    string ActorDisplay,
    DateTimeOffset UpdatedAt,
    string ContentSha256);
