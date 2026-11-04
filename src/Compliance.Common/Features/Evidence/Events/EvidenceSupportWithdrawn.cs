using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evidence.Events;

/// <summary>A reasoned withdrawal, separate from the immutable original support assertion.</summary>
[Discriminator("bdgrz.evidence.evidence-support-withdrawn", 1)]
public sealed record EvidenceSupportWithdrawn(Uuid TenantId, Uuid SubjectId, long Revision,
    Uuid ActorMemberId, DateTimeOffset OccurredAt, Uuid ProgramId, Uuid CommandId,
    long OriginalExpectedRevision, string AuthoredIntentSha256, string Reason, string ActorDisplay)
    : DomainEvent;
