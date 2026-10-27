using Cntryl.Portia;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Queries;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.AuditorRequests.Models;

/// <summary>
///     The frozen terms of one admitted sample selection: the sealed request generation, the exact population version
///     content, the complete nonempty duplicate-free row manifest and the authored provenance. The materializing
///     receiver and the selection owner copy it and never load the live request or the latest population.
/// </summary>
public sealed record AuditorSampleSelectionIntent(DurableIntentHeader Header, Uuid ProgramId, Uuid RequestId,
    ulong RequestGeneration,
    ulong ParentAdmissionRevision, Uuid SelectionId, Uuid? SupersedesSelectionId, Uuid PopulationId,
    int PopulationVersion, ImmutableContentRef PopulationContent, ImmutableSelectionManifest PopulationRowIds,
    string SelectionMethod, string Source, string ExternalReference, string? AmendmentReason,
    DateTimeOffset RecordedAt)
{
    /// <summary>Schema of the content written to the immutable store.</summary>
    public const string Schema = "auditor-sample-selection-intent.v1";
}
