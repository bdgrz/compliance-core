using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Retention.Commands;
using Bdgrz.Compliance.Features.Retention.Models;
namespace Bdgrz.Compliance.Features.Evidence.Events;

/// <summary>Records an attributed decision that a malware quarantine was a false positive.</summary>
[Discriminator("bdgrz.evidence.artifact.quarantine_released", 1)]
public sealed record EvidenceArtifactQuarantineReleased(Uuid TenantId, Uuid ArtifactId,
    long Revision, Uuid RequestId, string RescanState, DateTimeOffset RescannedAt,
    string Rationale, ActorReference DecidedBy, DateTimeOffset DecidedAt) : DomainEvent;
