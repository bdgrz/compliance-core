using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Commitments;

public interface ICommitmentFact : INativeOperationFact
{
    Uuid TenantId { get; }
    CommitmentAuthorityHeader Header { get; }
    ImmutableContentRef Change { get; }
    ImmutableContentRef Authority { get; }
    ImmutableContentRef? OperationResult { get; }
    long NativeSequence { get; }
    int ChangeCount { get; }
}
