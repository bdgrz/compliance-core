using Cntryl.Portia;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Retention.Commands;
namespace Bdgrz.Compliance.Features.Evidence.Queries;

[Discriminator("bdgrz.evidence.artifact.metadata.get", 1)]
public sealed record GetEvidenceArtifactMetadata(Uuid TenantId, Uuid ArtifactId)
    : IRequest<EvidenceArtifactMetadataView>, ICallable;
