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
    public ValueTask HandleAsync(ControlDraftCreated ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(ControlDraftRevised ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(ControlDraftDiscarded ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);
    public ValueTask HandleAsync(ControlSuccessorProposed ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(ControlProposalWithdrawn ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);
}
