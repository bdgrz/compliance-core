using Bdgrz.Compliance.Features.NativeOperations;

namespace Bdgrz.Compliance.Features.Applications;

/// <summary>One provisional row effect a target group appends: its provenance, frozen row and resulting revision.</summary>
public sealed record ApplicationImportEffectInput(OperationProvenance Operation, ApplicationImportPlannedRow Row,
    long ResultingRevision);
