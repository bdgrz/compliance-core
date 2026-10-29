using Cntryl.Portia;
using Bdgrz.Compliance.Features.Changes.Models;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.Changes.Contracts;

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
