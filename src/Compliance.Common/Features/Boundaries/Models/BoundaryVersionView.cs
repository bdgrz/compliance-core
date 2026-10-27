using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.Boundaries.Models;

public sealed record BoundaryVersionView(Uuid TenantId, Uuid BoundaryId, Uuid ProgramId,
    Uuid VersionId, long Revision, BoundaryContent Content, string Status,
    DateOnly? EffectiveFrom, Uuid AuthorMemberId, string AuthorDisplay,
    DateTimeOffset ChangedAt);
