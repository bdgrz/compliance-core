using Bdgrz.Compliance.Features.Evidence.Projections;
namespace Bdgrz.Compliance.Features.Evidence.Models;

sealed record EvidenceRedactionSourcePair(EvidenceArtifactMetadataSnapshot Original, EvidenceArtifactMetadataSnapshot Derived);
