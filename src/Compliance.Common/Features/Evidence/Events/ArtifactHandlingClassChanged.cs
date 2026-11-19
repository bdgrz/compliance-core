using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;

namespace Bdgrz.Compliance.Features.Evidence.Events;

[Discriminator("bdgrz.evidence.artifact.handling-class-changed", 1)]
public sealed record ArtifactHandlingClassChanged(Uuid TenantId, Uuid ArtifactId, long Revision,
    Uuid CommandId, long OriginalExpectedRevision, string AuthoredIntentSha256, string PreviousClass,
    string HandlingClass, string Rationale, ActorReference ChangedBy, DateTimeOffset ChangedAt) : DomainEvent;
