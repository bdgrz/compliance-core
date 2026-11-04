using Cntryl.Portia;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Applications.Projections;
using Bdgrz.Compliance.Features.Controls.Events;
namespace Bdgrz.Compliance.Features.Applications.Projectors;

/// <summary>Replays current Control draft applicability into a tenant-scoped reverse index.</summary>
public sealed partial class ApplicationControlDraftReferenceProjector(
    IApplicationControlDraftReferenceProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant("controls"),
        "ApplicationControlDraftReferencesV1"),
      IProjectorHandler<ControlDraftCreated>, IProjectorHandler<ControlDraftRevised>,
      IProjectorHandler<ControlDraftDiscarded>, IProjectorHandler<ControlSuccessorProposed>,
      IProjectorHandler<ControlProposalWithdrawn>
{
    public async ValueTask<Result> HandleAsync(IProjectorContext<ControlDraftCreated> context,
        CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<ControlDraftRevised> context,
        CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<ControlDraftDiscarded> context,
        CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<ControlSuccessorProposed> context,
        CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<ControlProposalWithdrawn> context,
        CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }
}
