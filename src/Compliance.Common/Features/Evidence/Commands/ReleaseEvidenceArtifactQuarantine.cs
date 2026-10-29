using Cntryl.Portia;
using Bdgrz.Compliance.Features.Retention.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Retention.Commands;
using Bdgrz.Compliance.Features.Retention.Models;
namespace Bdgrz.Compliance.Features.Evidence.Commands;

/// <summary>Releases a malware-quarantined artifact after a clean rescan and an attributed admin decision.</summary>
[Discriminator("bdgrz.evidence.artifact.quarantine.release", 1)]
public sealed record ReleaseEvidenceArtifactQuarantine(Uuid TenantId, Uuid ArtifactId,
    long ExpectedRevision, string Rationale) : IRequest, IArtifactRetentionAdminRequest,
    IClientManagementMutationRequest, ICallable;
