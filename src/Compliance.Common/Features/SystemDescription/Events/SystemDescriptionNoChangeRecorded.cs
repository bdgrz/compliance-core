using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.SystemDescription.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.SystemDescription.Models;
namespace Bdgrz.Compliance.Features.SystemDescription.Events;

/// <summary>One selected native fact, compact authority and exact immutable structured business payload.</summary>
[Discriminator("bdgrz.systemdescription.system-description-no-change-recorded", 2)]
public sealed record SystemDescriptionNoChangeRecorded(Uuid TenantId,
    Uuid ProgramId,
    Uuid SystemDescriptionId,
    long AggregateRevision,
    ImmutableContentRef DecisionContent,
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
