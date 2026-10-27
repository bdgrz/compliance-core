using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.SystemDescription;

/// <summary>One selected native fact, compact authority and exact immutable structured business payload.</summary>
[Discriminator("bdgrz.systemdescription.system-description-reviewed", 2)]
public sealed record SystemDescriptionReviewed(Uuid TenantId,
    Uuid ProgramId,
    Uuid SystemDescriptionId,
    long AggregateRevision,
    ImmutableContentRef ReviewContent,
    DateTimeOffset OccurredAt,
    OperationProvenance Operation,
    ImmutableContentRef? FirstResponse,
    ImmutableContentRef? OperationResult,
    SystemDescriptionAuthorityHeader Header,
    ImmutableContentRef CurrentVersionContent,
    long NativeSequence,
    int ChangeCount,
    Uuid ActorMemberId) : DomainEvent, ISystemDescriptionFact
{
    public ImmutableContentRef CurrentAuthorityContent { get; init; } = null!;
    public ImmutableContentRef? SourceSnapshotContent { get; init; }
}
