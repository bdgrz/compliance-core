using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.Work.Commands;
namespace Bdgrz.Compliance.Features.Work.Projections;

/// <summary>The actor's visible, ordered work as of one read, and whether they manage the program.</summary>
sealed record WorkQueueSnapshot(DateOnly Today, bool ActorManages,
    IReadOnlyList<WorkQueueEntry> Entries)
{
    public WorkQueueEntry? Find(Uuid workItemId) =>
        Entries.FirstOrDefault(entry => entry.Item.WorkItemId == workItemId);

    public static RequestError NotFound() =>
        new(RequestErrorKind.NotFound, "The work item was not found.");
}
