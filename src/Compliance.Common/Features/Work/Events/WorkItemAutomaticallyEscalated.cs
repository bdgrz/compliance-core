using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work.Events;

/// <summary>The scheduler recorded one overdue escalation, retaining the current assignment.</summary>
[Discriminator("bdgrz.work.automatically_escalated", 1)]
public sealed record WorkItemAutomaticallyEscalated(Uuid TenantId, Uuid ProgramId, Uuid WorkItemId,
    long Revision, Uuid CommandId, Uuid SourceId, string SourceKind, DateOnly DueOn,
    Uuid? AssigneeMemberId, DateTimeOffset EscalatedAt) : DomainEvent;
