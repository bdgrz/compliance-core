using System.Text.Json.Serialization;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Boundaries;

[Discriminator("bdgrz.boundary.draft.created", 2)]
public sealed record BoundaryDraftCreated(Uuid TenantId, Uuid BoundaryId, Uuid ProgramId, Uuid DraftVersionId, ImmutableContentRef Content, Uuid AuthorMemberId, string AuthorDisplay, DateTimeOffset ChangedAt) : DomainEvent, IBoundaryFact
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
