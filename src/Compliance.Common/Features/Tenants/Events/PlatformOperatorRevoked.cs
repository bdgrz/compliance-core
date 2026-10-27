using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

[Discriminator("bdgrz.platform.operator.revoked", 1)]
public sealed record PlatformOperatorRevoked(Uuid CommandId, ulong ExpectedRevision,
    Uuid ActorUserId, Uuid SubjectUserId, Uuid AssignmentId, ActorAttribution Actor, DateTimeOffset OccurredAt,
    string Reason, string? ActorDisplay = null) : DomainEvent, IPlatformOperatorFact
{
    public OperationProvenance Operation { get; init; } = default!;
    public ImmutableContentRef? FirstResponse { get; init; }
    public ImmutableContentRef? OperationResult { get; init; }
    public ImmutableContentRef Authority { get; init; } = default!;
    public ImmutableContentRef Change { get; init; } = default!;
    public ulong Revision { get; init; }
    public int ActiveCount { get; init; }
}
