using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

[Discriminator("bdgrz.platform.operator-roster.seeded", 1)]
public sealed record PlatformOperatorRosterSeeded(Uuid RosterId, Uuid[] UserIds,
    ActorAttribution Actor, DateTimeOffset OccurredAt) : DomainEvent, IPlatformOperatorFact
{
    public OperationProvenance Operation { get; init; } = default!;
    public ImmutableContentRef? FirstResponse { get; init; }
    public ImmutableContentRef? OperationResult { get; init; }
    public ImmutableContentRef Authority { get; init; } = default!;
    public ImmutableContentRef Change { get; init; } = default!;
    public ulong Revision { get; init; }
    public int ActiveCount { get; init; }
}
