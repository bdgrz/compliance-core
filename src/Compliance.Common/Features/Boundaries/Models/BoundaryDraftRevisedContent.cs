using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Boundaries;

public sealed record BoundaryDraftRevisedContent(Uuid TenantId, Uuid BoundaryId,
    Uuid DraftVersionId, long Revision, BoundaryContent Content, Uuid AuthorMemberId,
    string AuthorDisplay, DateTimeOffset ChangedAt) : IBoundaryMutationContent;
