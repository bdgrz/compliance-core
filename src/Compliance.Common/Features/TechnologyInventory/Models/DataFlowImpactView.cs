using Cntryl.Portia;
using Bdgrz.Compliance.Features.PolicyDistribution.Queries;
namespace Bdgrz.Compliance.Features.TechnologyInventory.Models;

/// <summary>
/// One carrying flow. <see cref="EncryptionViolation"/> means the recomputed classification
/// requires encryption the flow lacks and it names no exception reference.
/// <see cref="CarriesRetiredAssetOnly"/> means every carried asset would be retired.
/// </summary>
public sealed record DataFlowImpactView(Uuid DataFlowId, long Revision,
    string RecordedClassification, string RecomputedClassification,
    bool ClassificationChanged, bool EncryptedInTransit, bool EncryptedAtRest,
    string? ExceptionReference, bool EncryptionViolation, bool CarriesRetiredAssetOnly);
