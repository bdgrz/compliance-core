using System.Text.Json.Serialization;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.Boundaries.Contracts;
using Bdgrz.Compliance.Features.Boundaries.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.Boundaries.Events;

[Discriminator("bdgrz.boundary.draft.revised", 2)]
public sealed record BoundaryDraftRevised(Uuid TenantId, Uuid BoundaryId, Uuid DraftVersionId, long Revision, ImmutableContentRef Content, Uuid AuthorMemberId, string AuthorDisplay, DateTimeOffset ChangedAt) : DomainEvent, IBoundaryFact
{
    public OperationProvenance Operation { get; init; } = default!;
    public ImmutableContentRef? FirstResponse { get; init; }
    public ImmutableContentRef? OperationResult { get; init; }
    public BoundaryAuthorityHeader Header { get; init; } = default!;
    public ImmutableContentRef Change { get; init; } = default!;
    public ImmutableContentRef Authority { get; init; } = default!;
    public long NativeSequence { get; init; }
    public int ChangeCount { get; init; }
}
