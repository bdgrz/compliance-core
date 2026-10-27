using Bdgrz.Compliance.Features.SystemDescription;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.SystemDescription.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.SystemDescription.Events;

/// <summary>One selected native fact, compact authority and exact immutable structured business payload.</summary>
[Discriminator("bdgrz.systemdescription.system-description-bound", 2)]
public sealed record SystemDescriptionBound(Uuid TenantId,
    Uuid ProgramId,
    Uuid SystemDescriptionId,
    long AggregateRevision,
    ImmutableContentRef BindingContent,
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
