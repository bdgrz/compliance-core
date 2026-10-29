using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Retention.Commands;
using Bdgrz.Compliance.Features.Retention.Models;
namespace Bdgrz.Compliance.Features.Retention.Events;

/// <summary>Records a changed, attributed handling classification for an exact artifact identity.</summary>
[Discriminator("bdgrz.retention.artifact-handling-policy-revised", 1)]
public sealed record ArtifactHandlingPolicyRevised(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    EvidenceHandlingPolicyView State,
    DateTimeOffset OccurredAt) : DomainEvent;
