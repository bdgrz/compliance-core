using Bdgrz.Compliance.Features.Retention.Models;
using Cntryl.Portia;

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
