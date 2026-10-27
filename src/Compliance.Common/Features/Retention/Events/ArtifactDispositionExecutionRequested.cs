using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Retention.Commands;
using Bdgrz.Compliance.Features.Retention.Models;
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
