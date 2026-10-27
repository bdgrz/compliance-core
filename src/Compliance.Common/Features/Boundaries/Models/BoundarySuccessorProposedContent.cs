using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Boundaries;

public sealed record BoundarySuccessorProposedContent(Uuid TenantId, Uuid BoundaryId,
    Uuid DraftVersionId, Uuid PredecessorVersionId, BoundaryContent Content,
    Uuid AuthorMemberId, string AuthorDisplay, DateTimeOffset ChangedAt) : IBoundaryMutationContent;
