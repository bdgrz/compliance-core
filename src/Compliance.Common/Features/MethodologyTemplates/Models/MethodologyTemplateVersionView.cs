using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.MethodologyTemplates;

/// <summary>Versioned authored content, review, and independent approval provenance.</summary>
public sealed record MethodologyTemplateVersionView(
    Uuid TemplateId,
    Uuid VersionId,
    long VersionNumber,
    string Status,
    MethodologyTemplateContent Content,
    string ContentSha256,
    Uuid AuthoredByMemberId,
    string AuthoredByDisplay,
    DateTimeOffset AuthoredAt,
    MethodologyTemplateReviewView? Review,
    Uuid? ApprovedByMemberId,
    string? ApprovedByDisplay,
    DateTimeOffset? ApprovedAt,
    string ChangeNotes,
    IReadOnlyList<MethodologyTemplateContributionAcceptanceReference>? AcceptedContributions = null,
    string? ApprovalRationale = null);
