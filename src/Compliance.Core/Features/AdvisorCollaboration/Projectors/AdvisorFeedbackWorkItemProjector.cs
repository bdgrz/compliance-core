using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Readers;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Aggregates;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Events;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Projections;
using Bdgrz.Compliance.Features.Work.Commands;
namespace Bdgrz.Compliance.Features.AdvisorCollaboration.Projectors;

/// <summary>Projects each feedback thread's facts into the accountable work item read model.</summary>
public sealed partial class AdvisorFeedbackWorkItemProjector(
    IAdvisorFeedbackWorkItemProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant(AdvisorFeedbackThread.Area),
        FitzAdvisorFeedbackWorkItemDirectory.ProjectorName),
      IProjectorHandler<AdvisorFeedbackRecorded>,
      IProjectorHandler<AdvisorFeedbackCorrected>,
      IProjectorHandler<AdvisorFeedbackResponded>,
      IProjectorHandler<AdvisorFeedbackResolved>
{
    public async ValueTask<Result> HandleAsync(IProjectorContext<AdvisorFeedbackRecorded> context,
        CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<AdvisorFeedbackCorrected> context,
        CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<AdvisorFeedbackResponded> context,
        CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<AdvisorFeedbackResolved> context,
        CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }
}
