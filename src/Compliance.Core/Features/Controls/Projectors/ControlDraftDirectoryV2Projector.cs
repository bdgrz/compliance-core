using Cntryl.Portia;
using Bdgrz.Compliance.Features.Controls.Services;
using Bdgrz.Compliance.Features.Controls.Aggregates;
using Bdgrz.Compliance.Features.Controls.Events;
using Bdgrz.Compliance.Features.Controls.Projections;
using Bdgrz.Compliance.Features.Responsibilities.Events;
namespace Bdgrz.Compliance.Features.Controls.Projectors;

public sealed partial class ControlDraftDirectoryV2Projector(IControlDraftDirectoryProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant(ControlDraft.Area), FitzControlDraftDirectoryV2.ProjectorName),
        IProjectorHandler<ControlDraftCreated>, IProjectorHandler<ControlDraftRevised>, IProjectorHandler<ControlDraftDiscarded>, IProjectorHandler<ControlReviewed>, IProjectorHandler<ControlApproved>, IProjectorHandler<ControlSuccessorProposed>, IProjectorHandler<ControlRetirementProposed>, IProjectorHandler<ControlRetired>, IProjectorHandler<ControlProposalWithdrawn>, IProjectorHandler<ControlOwnerPersonDesignated>, IProjectorHandler<ResponsibilityAssigned>, IProjectorHandler<ResponsibilityRevoked>
{
    public async ValueTask<Result> HandleAsync(IProjectorContext<ControlDraftCreated> context, CancellationToken ct)
    {
        var value = context.Trigger;
        await projection.ApplyAsync(value, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<ControlDraftRevised> context, CancellationToken ct)
    {
        var value = context.Trigger;
        await projection.ApplyAsync(value, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<ControlDraftDiscarded> context, CancellationToken ct)
    {
        var value = context.Trigger;
        await projection.ApplyAsync(value, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<ControlReviewed> context, CancellationToken ct)
    {
        var value = context.Trigger;
        await projection.ApplyAsync(value, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<ControlApproved> context, CancellationToken ct)
    {
        var value = context.Trigger;
        await projection.ApplyAsync(value, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<ControlSuccessorProposed> context, CancellationToken ct)
    {
        var value = context.Trigger;
        await projection.ApplyAsync(value, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<ControlRetirementProposed> context, CancellationToken ct)
    {
        var value = context.Trigger;
        await projection.ApplyAsync(value, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<ControlRetired> context, CancellationToken ct)
    {
        var value = context.Trigger;
        await projection.ApplyAsync(value, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<ControlProposalWithdrawn> context, CancellationToken ct)
    {
        var value = context.Trigger;
        await projection.ApplyAsync(value, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<ControlOwnerPersonDesignated> context, CancellationToken ct)
    {
        var value = context.Trigger;
        await projection.ApplyAsync(value, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<ResponsibilityAssigned> context, CancellationToken ct)
    {
        var value = context.Trigger;
        await projection.ApplyAsync(value, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<ResponsibilityRevoked> context, CancellationToken ct)
    {
        var value = context.Trigger;
        await projection.ApplyAsync(value, ct).ConfigureAwait(false);
        return Result.Success;
    }
}
