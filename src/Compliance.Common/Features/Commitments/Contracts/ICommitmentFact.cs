using Cntryl.Portia;
using Bdgrz.Compliance.Features.Commitments.Models;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.Commitments.Contracts;

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
