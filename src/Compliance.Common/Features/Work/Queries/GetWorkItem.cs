using Cntryl.Portia;
using Bdgrz.Compliance.Features.Programs.Contracts;
using Bdgrz.Compliance.Features.Work.Commands;
using Bdgrz.Compliance.Features.Work.Models;
namespace Bdgrz.Compliance.Features.Work.Queries;

/// <summary>Reads one visible work item with its attributed assignment and escalation history.</summary>
[Discriminator("bdgrz.work.get", 1)]
public sealed record GetWorkItem(Uuid TenantId, Uuid ProgramId, Uuid WorkItemId)
    : IRequest<WorkItemDetailView>, IProgramReadRequest, ICallable;
