using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;

namespace Bdgrz.Compliance.Features.Tenants;

/// <summary>A compact receipt on the single authoritative platform roster stream.</summary>
public interface IPlatformOperatorFact : INativeOperationFact
{
    ulong Revision { get; }
    int ActiveCount { get; }
    ImmutableContentRef Authority { get; }
    ImmutableContentRef Change { get; }
    ImmutableContentRef? OperationResult { get; }
}
