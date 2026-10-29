using Cntryl.Portia;
using Bdgrz.Compliance.Features.Boundaries.Contracts;
namespace Bdgrz.Compliance.Features.Boundaries.Models;

public sealed record BoundaryDraftCreatedContent(Uuid TenantId, Uuid BoundaryId, Uuid ProgramId,
    Uuid DraftVersionId, BoundaryContent Content, Uuid AuthorMemberId,
    string AuthorDisplay, DateTimeOffset ChangedAt) : IBoundaryMutationContent;
