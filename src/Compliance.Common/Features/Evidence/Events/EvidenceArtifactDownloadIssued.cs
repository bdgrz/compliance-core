using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evidence.Events;

/// <summary>Records the EvidenceArtifactDownloadIssued domain fact for one tenant-owned subject revision.</summary>
[Discriminator("bdgrz.evidence.evidence-artifact-download-issued", 1)]
public sealed record EvidenceArtifactDownloadIssued(
    Uuid TenantId,
    Uuid ArtifactId,
    long Revision,
    Uuid RequestId,
    string ContentSha256,
    long ContentLength,
    string Purpose,
    ActorReference IssuedBy,
    DateTimeOffset IssuedAt) : DomainEvent;
