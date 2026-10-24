using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Changes;

public interface ISignificantChangeFact : INativeOperationFact
{
    Uuid TenantId { get; }
    Uuid SubjectId { get; }
    long Revision { get; }
    Uuid? ActorMemberId { get; }
    DateTimeOffset OccurredAt { get; }
    SignificantChangeAuthorityHeader Header { get; }
    ImmutableContentRef CurrentContent { get; }
    ImmutableContentRef Change { get; }
    ImmutableContentRef OperationResult { get; }
    long NativeSequence { get; }
    int ChangeCount { get; }
}
