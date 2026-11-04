using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Models;

/// <summary>The actual separate-draft line state at one authoritative read cut.</summary>
public sealed record MethodologyTemplateLineView(Uuid TemplateId, string Name, string Purpose, string Status,
    long Revision, Uuid? OpenDraftId, long PublishedVersion, Uuid? PublishedVersionId, Uuid CreatedBy,
    DateTimeOffset CreatedAt, Uuid? RetiredBy, string? RetirementReason, DateTimeOffset? RetiredAt,
    DateTimeOffset UpdatedAt);
