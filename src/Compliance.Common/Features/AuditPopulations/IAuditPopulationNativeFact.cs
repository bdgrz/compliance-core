using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;

namespace Bdgrz.Compliance.Features.AuditPopulations;

/// <summary>A selected native local decision with complete authored input and actual predecessor source cut.</summary>
public interface IAuditPopulationNativeFact : IAuditPopulationStateFact, INativeOperationFact
{
    ImmutableContentRef Command { get; }
    ImmutableContentRef SourceProofs { get; }
    ImmutableContentRef? OperationResult { get; }
}
