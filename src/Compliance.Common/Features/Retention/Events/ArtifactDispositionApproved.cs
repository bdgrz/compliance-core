using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Retention.Commands;
using Bdgrz.Compliance.Features.Retention.Models;
namespace Bdgrz.Compliance.Features.Retention.Events;

/// <summary>Records the ArtifactDispositionApproved domain fact for one tenant-owned subject revision.</summary>
[Discriminator("bdgrz.retention.artifact-disposition-approved", 1)]
public sealed record ArtifactDispositionApproved(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    ArtifactDispositionView State,
    DateTimeOffset OccurredAt) : DomainEvent;
