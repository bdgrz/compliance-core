using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Proofs;

namespace Bdgrz.Compliance.Features.Evidence;

/// <summary>Exact observed source locators, not a hold, migration or future disclosure guarantee.</summary>
public sealed record EvidenceRedactionNativeSources(SourceFactProof<EvidenceArtifactRegistered> OriginalRegistration,
    ulong OriginalPhysicalPosition, string OriginalState, string OriginalPrefixSha256, SourceFactProof<EvidenceArtifactRegistered> DerivedRegistration,
    ulong DerivedPhysicalPosition, string DerivedState, string DerivedPrefixSha256, SourceFactProof<EvidenceArtifactInspected>? Inspection,
    SourceFactProof<EvidenceArtifactQuarantineReleased>? Release, SourceFactProof<EvidenceRedactionPrepared>? LegacyPreparation,
    SourceFactProof<EvidenceRedactionPreparedV2>? NativePreparation, SourceFactProof<SeparationOfDutiesWaiverRecorded>? WaiverRecorded,
    SourceFactProof<SeparationOfDutiesWaiverApproved>? WaiverApproved, ulong? WaiverPhysicalPosition, string? WaiverPrefixSha256)
{
    public const string Schema = "evidence-redaction-native-sources.v1";
}
