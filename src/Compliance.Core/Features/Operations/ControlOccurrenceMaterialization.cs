using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>A materialization that precedes a proof on an occurrence that is not materialized yet.</summary>
public sealed record ControlOccurrenceMaterialization(OperationProvenance Operation, ControlOccurrenceTerms Terms,
    ImmutableContentRef CreationIntent);
