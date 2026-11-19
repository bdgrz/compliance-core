using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Boundaries.Contracts;
using Bdgrz.Compliance.Features.Boundaries.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;

namespace Bdgrz.Compliance.Features.Boundaries.Events;

[Discriminator("bdgrz.boundary.version_approved", 1)]
public sealed record BoundaryVersionApproved(Uuid TenantId, Uuid ProgramId, Uuid DraftVersionId, long Revision, Uuid DecisionId, Uuid ActorMemberId, string ActorDisplay, DateOnly EffectiveFrom, DateTimeOffset DecidedAt, Uuid? SeparationOfDutiesWaiverId = null) : DomainEvent, IBoundaryFact
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
