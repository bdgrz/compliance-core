using Cntryl.Portia;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.SystemDescription.Models;
namespace Bdgrz.Compliance.Features.SystemDescription.Contracts;

public interface ISystemDescriptionFact : INativeOperationFact
{
    Uuid TenantId { get; }
    Uuid ProgramId { get; }
    Uuid SystemDescriptionId { get; }
    long AggregateRevision { get; }
    DateTimeOffset OccurredAt { get; }
    SystemDescriptionAuthorityHeader Header { get; }
    ImmutableContentRef CurrentVersionContent { get; }
    ImmutableContentRef CurrentAuthorityContent { get; }
    ImmutableContentRef? SourceSnapshotContent { get; }
    ImmutableContentRef? OperationResult { get; }
    long NativeSequence { get; }
    int ChangeCount { get; }
    Uuid ActorMemberId { get; }
}
