using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Work.Commands;
namespace Bdgrz.Compliance.Features.Work.Events;

/// <summary>A work item was escalated to program managers for visibility.</summary>
[Discriminator("bdgrz.work.escalated", 1)]
public sealed record WorkItemEscalated(Uuid TenantId, Uuid ProgramId, Uuid WorkItemId,
    long Revision, string Reason, ActorReference EscalatedBy, DateTimeOffset EscalatedAt)
    : DomainEvent;
