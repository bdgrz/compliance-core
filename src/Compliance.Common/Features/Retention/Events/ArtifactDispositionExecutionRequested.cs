using Bdgrz.Compliance.Features.Retention.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Retention.Events;

/// <summary>Records the ArtifactDispositionExecutionRequested domain fact for one tenant-owned subject revision.</summary>
[Discriminator("bdgrz.retention.artifact-disposition-execution-requested", 1)]
public sealed record ArtifactDispositionExecutionRequested(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    ArtifactDispositionView State,
    DateTimeOffset OccurredAt) : DomainEvent;
