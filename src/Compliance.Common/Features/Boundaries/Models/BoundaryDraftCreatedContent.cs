using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Boundaries;

public sealed record BoundaryDraftCreatedContent(Uuid TenantId, Uuid BoundaryId, Uuid ProgramId,
    Uuid DraftVersionId, BoundaryContent Content, Uuid AuthorMemberId,
    string AuthorDisplay, DateTimeOffset ChangedAt) : IBoundaryMutationContent;
