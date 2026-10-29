using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.SystemDescription.Models;

/// <summary>One immutable narrative version with its authorship and exact source context.</summary>
public sealed record SystemDescriptionVersionView(Uuid TenantId, Uuid ProgramId,
    Uuid SystemDescriptionId, Uuid SystemDescriptionVersionId, long Version,
    Uuid? ParentVersionId, string Status, SystemDescriptionScope Scope,
    SystemDescriptionDraftContent Content, string ChangeReason,
    Uuid AuthorMemberId, string AuthorDisplay, DateTimeOffset UpdatedAt,
    string ContentSha256);
