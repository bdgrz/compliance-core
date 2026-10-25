using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs;

/// <summary>A native program acceptance, with a separate business change and native sequence.</summary>
public interface IProgramFact : INativeOperationFact
{
    Uuid TenantId { get; }
    Uuid ProgramId { get; }
    long Revision { get; }
    ulong NativeSequence { get; }
    int ChangeCount { get; }
    ImmutableContentRef Content { get; }
    ImmutableContentRef? OperationResult { get; }
}
