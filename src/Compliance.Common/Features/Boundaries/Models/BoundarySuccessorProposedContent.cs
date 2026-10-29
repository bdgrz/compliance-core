using Cntryl.Portia;
using Bdgrz.Compliance.Features.Boundaries.Contracts;
namespace Bdgrz.Compliance.Features.Boundaries.Models;

public sealed record BoundarySuccessorProposedContent(Uuid TenantId, Uuid BoundaryId,
    Uuid DraftVersionId, Uuid PredecessorVersionId, BoundaryContent Content,
    Uuid AuthorMemberId, string AuthorDisplay, DateTimeOffset ChangedAt) : IBoundaryMutationContent;
