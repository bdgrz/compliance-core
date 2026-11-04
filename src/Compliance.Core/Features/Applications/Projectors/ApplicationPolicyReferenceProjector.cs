using Cntryl.Portia;
using Bdgrz.Compliance.Features.Applications.Readers;
using Bdgrz.Compliance.Features.Policies.Events;
namespace Bdgrz.Compliance.Features.Applications.Projectors;

public sealed partial class ApplicationPolicyReferenceProjector(
    IApplicationPolicyReferenceProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant("policies"),
        FitzApplicationPolicyReferenceDirectory.ProjectorName),
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

    public ValueTask<Result> HandleAsync(IProjectorContext<PolicyPeriodicReviewConfirmed> context,
        CancellationToken ct) =>
        ValueTask.FromResult(Result.Success);

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
