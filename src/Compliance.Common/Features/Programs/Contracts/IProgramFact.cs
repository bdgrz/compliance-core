using Cntryl.Portia;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.Programs.Contracts;

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
