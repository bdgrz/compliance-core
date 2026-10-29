using Cntryl.Portia;
using Bdgrz.Compliance.Features.Boundaries.Contracts;
namespace Bdgrz.Compliance.Features.Boundaries.Models;

public sealed record BoundaryDraftRevisedContent(Uuid TenantId, Uuid BoundaryId,
    Uuid DraftVersionId, long Revision, BoundaryContent Content, Uuid AuthorMemberId,
    string AuthorDisplay, DateTimeOffset ChangedAt) : IBoundaryMutationContent;
