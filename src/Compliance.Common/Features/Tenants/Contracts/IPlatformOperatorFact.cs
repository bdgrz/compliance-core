using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.Tenants.Contracts;

/// <summary>A compact receipt on the single authoritative platform roster stream.</summary>
public interface IPlatformOperatorFact : INativeOperationFact
{
    ulong Revision { get; }
    int ActiveCount { get; }
    ImmutableContentRef Authority { get; }
    ImmutableContentRef Change { get; }
    ImmutableContentRef? OperationResult { get; }
}
