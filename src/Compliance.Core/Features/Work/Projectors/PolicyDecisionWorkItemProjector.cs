using Cntryl.Portia;
using Bdgrz.Compliance.Features.Work.Readers;
using Bdgrz.Compliance.Features.Policies.Events;
using Bdgrz.Compliance.Features.Work.Projections;
namespace Bdgrz.Compliance.Features.Work.Projectors;

public sealed partial class PolicyDecisionWorkItemProjector(
    IPolicyDecisionWorkItemProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant("policies"),
        FitzPolicyDecisionWorkItemDirectory.ProjectorName),
      IProjectorHandler<PolicyDraftCreated>, IProjectorHandler<PolicyDraftRevised>,
      IProjectorHandler<PolicyReviewed>, IProjectorHandler<PolicyApproved>,
      IProjectorHandler<PolicyPeriodicReviewConfirmed>,
      IProjectorHandler<PolicyRetirementProposed>, IProjectorHandler<PolicyRetired>,
      IProjectorHandler<PolicyDraftDiscarded>
{
    public async ValueTask<Result> HandleAsync(IProjectorContext<PolicyDraftCreated> context,
        CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<PolicyDraftRevised> context,
        CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<PolicyReviewed> context,
        CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<PolicyApproved> context,
        CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<PolicyPeriodicReviewConfirmed> context,
        CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<PolicyRetirementProposed> context,
        CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<PolicyRetired> context,
        CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<PolicyDraftDiscarded> context,
        CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }
}
